package com.jagasafari.longevity.photosync

import java.io.InputStream
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLEncoder

data class UploadConfig(
    val storageAccount: String,
    val container: String,
    val sasToken: String
)

sealed class UploadResult {
    data object Success : UploadResult()
    data class Retry(val reason: String) : UploadResult()
    data class Failure(val reason: String) : UploadResult()
}

class BlobUploader(
    private val logger: Logger = AndroidLogger,
    private val connectionFactory: (URL) -> HttpURLConnection = { it.openConnection() as HttpURLConnection }
) {
    companion object {
        private const val TAG = "BlobUploader"
    }

    fun validateConfig(config: UploadConfig): String? {
        val cleaned = config.sasToken.trim().removePrefix("?")
        return when {
            cleaned.isBlank() -> "SAS token is blank"
            config.storageAccount.isBlank() -> "Storage account is blank"
            config.container.isBlank() -> "Container is blank"
            else -> null
        }
    }

    fun buildUrl(config: UploadConfig, filename: String): String {
        val cleanedToken = config.sasToken.trim().removePrefix("?")
        val encodedFilename = URLEncoder.encode(filename, "UTF-8").replace("+", "%20")
        return "https://${config.storageAccount}.blob.core.windows.net/${config.container}/$encodedFilename?$cleanedToken"
    }

    fun shouldRetry(httpCode: Int): Boolean =
        httpCode >= 500 || httpCode == 408 || httpCode == 429

    fun upload(
        config: UploadConfig,
        filename: String,
        contentType: String,
        inputStream: InputStream
    ): UploadResult {
        val validationError = validateConfig(config)
        if (validationError != null) {
            logger.e(TAG, validationError)
            return UploadResult.Failure(validationError)
        }

        val url = buildUrl(config, filename)
        logger.d(TAG, "Uploading filename=$filename to $url")

        return try {
            val connection = connectionFactory(URL(url))
            try {
                connection.requestMethod = "PUT"
                connection.connectTimeout = 15000
                connection.readTimeout = 30000
                connection.setRequestProperty("x-ms-blob-type", "BlockBlob")
                connection.setRequestProperty("Content-Type", contentType)
                connection.doOutput = true
                connection.outputStream.use { output -> inputStream.copyTo(output) }

                val code = connection.responseCode
                when {
                    code == 201 -> {
                        logger.d(TAG, "Upload success filename=$filename")
                        UploadResult.Success
                    }
                    shouldRetry(code) -> {
                        val errorBody = connection.errorStream?.bufferedReader()?.use { it.readText() }
                        logger.e(TAG, "Upload failed code=$code (retriable) body=${errorBody.orEmpty()}")
                        UploadResult.Retry("HTTP $code")
                    }
                    else -> {
                        val errorBody = connection.errorStream?.bufferedReader()?.use { it.readText() }
                        logger.e(TAG, "Upload failed code=$code body=${errorBody.orEmpty()}")
                        UploadResult.Failure("HTTP $code")
                    }
                }
            } finally {
                connection.disconnect()
            }
        } catch (ex: Exception) {
            logger.e(TAG, "Upload exception", ex)
            UploadResult.Retry(ex.message ?: "Unknown error")
        }
    }
}
