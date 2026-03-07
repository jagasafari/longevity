package com.jagasafari.longevity.photosync

import android.content.Context
import android.net.Uri
import android.provider.MediaStore
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters

class UploadWorker(
    ctx: Context,
    params: WorkerParameters,
    private val uploader: BlobUploader = BlobUploader(),
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

            val prefs = SecurePrefs.get(applicationContext)
            val rawToken = prefs.getString("sas_token", null) ?: run {
                logger.e(TAG, "Missing sas_token in settings")
                return Result.failure()
            }

            val storageAccount = prefs.getString("storage_account", "longevityphotos")
                ?.trim()
                .orEmpty()
                .ifBlank { "longevityphotos" }
            val container = prefs.getString("container", "photos")
                ?.trim()
                .orEmpty()
                .ifBlank { "photos" }

            val config = UploadConfig(storageAccount, container, rawToken)
            val filename = resolveFilename(uri) ?: "photo_${System.currentTimeMillis()}.jpg"
            val contentType = applicationContext.contentResolver.getType(uri) ?: "image/jpeg"

            val input = applicationContext.contentResolver.openInputStream(uri)
            if (input == null) {
                logger.e(TAG, "Failed to open input stream for uri=$uri")
                return Result.retry()
            }

            input.use { stream ->
                when (val result = uploader.upload(config, filename, contentType, stream)) {
                    is UploadResult.Success -> Result.success()
                    is UploadResult.Retry -> Result.retry()
                    is UploadResult.Failure -> Result.failure()
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

    companion object {
        private const val TAG = "UploadWorker"
    }
}
