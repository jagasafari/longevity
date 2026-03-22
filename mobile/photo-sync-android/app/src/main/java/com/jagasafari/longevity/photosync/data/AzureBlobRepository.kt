package com.jagasafari.longevity.photosync.data

import android.util.Log
import com.jagasafari.longevity.photosync.domain.model.UploadConfig
import com.jagasafari.longevity.photosync.domain.model.UploadResult
import com.jagasafari.longevity.photosync.domain.repository.BlobRepository
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody
import okio.BufferedSink
import okio.source
import org.xmlpull.v1.XmlPullParser
import org.xmlpull.v1.XmlPullParserFactory
import java.io.InputStream
import java.io.StringReader
import java.net.URLEncoder

class AzureBlobRepository(
    private val client: OkHttpClient = OkHttpClient(),
    private val baseUrlOverride: String? = null
) : BlobRepository {

    companion object {
        private const val TAG = "AzureBlobRepo"
    }

    private fun baseUrl(config: UploadConfig): String =
        baseUrlOverride ?: config.saUrl

    override fun listAllBlobs(config: UploadConfig): Set<String> {
        val allBlobs = mutableSetOf<String>()
        var marker: String? = null

        do {
            val (blobs, nextMarker) = fetchBlobListPage(config, marker)
            allBlobs.addAll(blobs)
            marker = nextMarker
        } while (marker != null)

        return allBlobs
    }

    private fun fetchBlobListPage(config: UploadConfig, marker: String? = null): Pair<List<String>, String?> {
        // Construct the list URL. SAS token already starts with '?'
        var url = "${baseUrl(config)}${config.normalizedSasToken}&comp=list&restype=container"
        if (marker != null) {
            url += "&marker=$marker"
        }

        val request = Request.Builder()
            .url(url)
            .get()
            .build()

        return try {
            client.newCall(request).execute().use { response ->
                if (!response.isSuccessful) {
                    val code = response.code
                    val bodyStr = response.body?.string() ?: ""
                    Log.e(TAG, "List blobs failed: HTTP $code - $bodyStr")
                    return Pair(emptyList(), null)
                }

                val xmlBody = response.body?.string() ?: return Pair(emptyList(), null)
                parseBlobListXml(xmlBody)
            }
        } catch (e: Exception) {
            Log.e(TAG, "Error listing blobs", e)
            Pair(emptyList(), null)
        }
    }

    private fun parseBlobListXml(xml: String): Pair<List<String>, String?> {
        val blobNames = mutableListOf<String>()
        var nextMarker: String? = null

        try {
            val factory = XmlPullParserFactory.newInstance()
            val parser = factory.newPullParser()
            parser.setInput(StringReader(xml))

            var eventType = parser.eventType
            var currentTag = ""
            var currentBlobName = ""

            while (eventType != XmlPullParser.END_DOCUMENT) {
                when (eventType) {
                    XmlPullParser.START_TAG -> {
                        currentTag = parser.name
                    }
                    XmlPullParser.TEXT -> {
                        val text = parser.text.trim()
                        if (text.isNotEmpty()) {
                            if (currentTag == "Name") {
                                currentBlobName = text
                            } else if (currentTag == "NextMarker") {
                                nextMarker = text
                            }
                        }
                    }
                    XmlPullParser.END_TAG -> {
                        if (parser.name == "Name" && currentBlobName.isNotEmpty()) {
                            blobNames.add(currentBlobName)
                            currentBlobName = ""
                        }
                        currentTag = ""
                    }
                }
                eventType = parser.next()
            }
        } catch (e: Exception) {
            Log.e(TAG, "Failed to parse XML", e)
        }

        return Pair(blobNames, if (nextMarker.isNullOrEmpty()) null else nextMarker)
    }

    override fun upload(
        config: UploadConfig,
        filename: String,
        contentType: String,
        inputStream: InputStream,
        contentLength: Long
    ): UploadResult {
        return try {
            val originalExt = filename.substringAfterLast('.', "")
            val finalName = if (originalExt.equals("heic", ignoreCase = true)) {
                filename.substringBeforeLast('.') + ".jpg"
            } else {
                filename
            }

            // Construct the upload URL. SAS token already starts with '?'
            val encoded = URLEncoder.encode(finalName, "UTF-8")
                .replace("+", "%20")
            val url = "${baseUrl(config)}/$encoded${config.normalizedSasToken}"
            
            val requestBody = object : RequestBody() {
                override fun contentType() = "application/octet-stream".toMediaType()
                override fun contentLength() = contentLength
                
                override fun writeTo(sink: BufferedSink) {
                    inputStream.source().use { source ->
                        sink.writeAll(source)
                    }
                }
            }

            val request = Request.Builder()
                .url(url)
                .put(requestBody)
                .addHeader("x-ms-blob-type", "BlockBlob")
                .addHeader("Content-Type", contentType)
                .build()

            client.newCall(request).execute().use { response ->
                if (response.isSuccessful) {
                    UploadResult.Success
                } else {
                    val code = response.code
                    val errorBody = response.body?.string()
                    Log.e(TAG, "Upload failed HTTP $code: $errorBody")
                    if (code in 500..599 || code == 429) {
                        UploadResult.Retry("HTTP $code")
                    } else {
                        UploadResult.Failure("HTTP $code")
                    }
                }
            }
        } catch (e: Exception) {
            Log.e(TAG, "Exception during upload", e)
            UploadResult.Retry(e.message ?: "Network error")
        }
    }
}
