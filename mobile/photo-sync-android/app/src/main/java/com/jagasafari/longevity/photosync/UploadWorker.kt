package com.jagasafari.longevity.photosync

import android.content.Context
import android.net.Uri
import android.provider.MediaStore
import android.provider.OpenableColumns
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.jagasafari.longevity.photosync.data.AzureBlobRepository
import com.jagasafari.longevity.photosync.data.SecurePrefsConfigRepository
import com.jagasafari.longevity.photosync.domain.model.UploadResult
import com.jagasafari.longevity.photosync.domain.repository.BlobRepository
import com.jagasafari.longevity.photosync.domain.repository.ConfigRepository

class UploadWorker @JvmOverloads constructor(
    ctx: Context,
    params: WorkerParameters,
    private val blobRepository: BlobRepository = AzureBlobRepository(),
    private val configRepository: ConfigRepository = SecurePrefsConfigRepository(ctx),
    private val logger: Logger = AndroidLogger
) : CoroutineWorker(ctx, params) {

    override suspend fun doWork(): Result {
        return try {
            val uriString = inputData.getString("uri") ?: run {
                logger.e(TAG, "Missing uri inputData")
                return Result.failure()
            }
            val uri = Uri.parse(uriString)
            logger.d(TAG, "Worker started for uri=$uri runAttemptCount=$runAttemptCount")

            val config = configRepository.getConfig() ?: run {
                logger.e(TAG, "Missing configuration (no SAS token)")
                return Result.failure()
            }

            val inputFileName = inputData.getString("fileName")
            val filename = inputFileName ?: resolveFilename(uri) ?: "photo_${System.currentTimeMillis()}.jpg"
            val contentType = applicationContext.contentResolver.getType(uri) ?: "image/jpeg"
            val size = resolveSize(uri)

            val input = applicationContext.contentResolver.openInputStream(uri)
            if (input == null) {
                logger.e(TAG, "Failed to open input stream for uri=$uri")
                return Result.retry()
            }

            input.use { stream ->
                when (val result = blobRepository.upload(config, filename, contentType, stream, size)) {
                    is UploadResult.Success -> {
                        UploadLogStore.addLog("Uploaded: $filename")
                        Result.success()
                    }
                    is UploadResult.Retry -> {
                        UploadLogStore.addLog("Retry: $filename (${result.reason})")
                        Result.retry()
                    }
                    is UploadResult.Failure -> {
                        UploadLogStore.addLog("Failed: $filename (${result.reason})")
                        Result.failure()
                    }
                }
            }
        } catch (ex: SecurityException) {
            logger.e(TAG, "SecurityException in worker", ex)
            Result.failure()
        } catch (ex: Exception) {
            logger.e(TAG, "Unexpected worker exception", ex)
            Result.retry()
        }
    }

    private fun resolveFilename(uri: Uri): String? {
        val projection = arrayOf(MediaStore.Images.Media.DISPLAY_NAME)
        return applicationContext.contentResolver.query(uri, projection, null, null, null)?.use { cursor ->
            if (cursor.moveToFirst()) cursor.getString(0) else null
        }
    }

    private fun resolveSize(uri: Uri): Long {
        val fromCursor = applicationContext.contentResolver.query(uri, null, null, null, null)?.use { cursor ->
            val sizeIndex = cursor.getColumnIndex(OpenableColumns.SIZE)
            if (sizeIndex >= 0 && cursor.moveToFirst() && !cursor.isNull(sizeIndex)) {
                cursor.getLong(sizeIndex)
            } else {
                0L
            }
        } ?: 0L

        if (fromCursor > 0) return fromCursor

        return try {
            applicationContext.contentResolver.openFileDescriptor(uri, "r")?.use { it.statSize } ?: 0L
        } catch (_: Exception) {
            0L
        }
    }

    companion object {
        private const val TAG = "UploadWorker"
    }
}
