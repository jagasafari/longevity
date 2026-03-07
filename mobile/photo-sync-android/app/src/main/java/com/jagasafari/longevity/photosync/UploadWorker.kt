package com.jagasafari.longevity.photosync

import android.content.Context
import android.util.Log
import android.net.Uri
import android.provider.MediaStore
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import java.net.HttpURLConnection
import java.net.URL

class UploadWorker(ctx: Context, params: WorkerParameters) : CoroutineWorker(ctx, params) {

    override suspend fun doWork(): Result {
        return try {
            val uriString = inputData.getString("uri") ?: run {
                Log.e(TAG, "Missing uri inputData")
                return Result.failure()
            }
            val uri = Uri.parse(uriString)
            Log.d(TAG, "Worker started for uri=$uri runAttemptCount=$runAttemptCount")

            val prefs = SecurePrefs.get(applicationContext)
            val rawToken = prefs.getString("sas_token", null) ?: run {
                Log.e(TAG, "Missing sas_token in settings")
                return Result.failure()
            }
            val sasToken = rawToken.trim().removePrefix("?")
            if (sasToken.isBlank()) {
                Log.e(TAG, "SAS token is blank")
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
            val filename = resolveFilename(uri) ?: "photo_${System.currentTimeMillis()}.jpg"
            val encodedFilename = Uri.encode(filename)
            val url = "https://$storageAccount.blob.core.windows.net/$container/$encodedFilename?$sasToken"
            val contentType = applicationContext.contentResolver.getType(uri) ?: "image/jpeg"

            val input = applicationContext.contentResolver.openInputStream(uri)
            if (input == null) {
                Log.e(TAG, "Failed to open input stream for uri=$uri")
                return Result.retry()
            }

            input.use { stream ->
                val connection = URL(url).openConnection() as HttpURLConnection
                try {
                    connection.requestMethod = "PUT"
                    connection.connectTimeout = 15000
                    connection.readTimeout = 30000
                    connection.setRequestProperty("x-ms-blob-type", "BlockBlob")
                    connection.setRequestProperty("Content-Type", contentType)
                    connection.doOutput = true
                    connection.outputStream.use { output -> stream.copyTo(output) }

                    val code = connection.responseCode
                    if (code == 201) {
                        Log.d(TAG, "Upload success filename=$filename")
                        Result.success()
                    } else {
                        val errorBody = connection.errorStream?.bufferedReader()?.use { it.readText() }
                        Log.e(TAG, "Upload failed code=$code uri=$uri body=${errorBody ?: ""}")
                        if (code >= 500 || code == 408 || code == 429) Result.retry() else Result.failure()
                    }
                } finally {
                    connection.disconnect()
                }
            }
        } catch (ex: SecurityException) {
            Log.e(TAG, "SecurityException in worker", ex)
            Result.failure()
        } catch (ex: Exception) {
            Log.e(TAG, "Unexpected worker exception", ex)
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
