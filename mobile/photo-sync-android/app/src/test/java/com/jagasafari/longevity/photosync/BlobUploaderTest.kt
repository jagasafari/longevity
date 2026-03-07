package com.jagasafari.longevity.photosync

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.io.InputStream
import java.io.OutputStream
import java.net.HttpURLConnection
import java.net.URL

class BlobUploaderTest {

    private lateinit var mockLogger: MockLogger
    private lateinit var uploader: BlobUploader

    @Before
    fun setUp() {
        mockLogger = MockLogger()
    }

    // --- validateConfig tests ---

    @Test
    fun `validateConfig returns null for valid config`() {
        uploader = BlobUploader(mockLogger)
        val config = UploadConfig("account", "container", "sv=2026&sig=abc")
        assertNull(uploader.validateConfig(config))
    }

    @Test
    fun `validateConfig returns error for blank sas token`() {
        uploader = BlobUploader(mockLogger)
        val config = UploadConfig("account", "container", "   ")
        assertEquals("SAS token is blank", uploader.validateConfig(config))
    }

    @Test
    fun `validateConfig returns error for sas token with only question mark`() {
        uploader = BlobUploader(mockLogger)
        val config = UploadConfig("account", "container", "?")
        assertEquals("SAS token is blank", uploader.validateConfig(config))
    }

    @Test
    fun `validateConfig returns error for blank storage account`() {
        uploader = BlobUploader(mockLogger)
        val config = UploadConfig("", "container", "sv=2026&sig=abc")
        assertEquals("Storage account is blank", uploader.validateConfig(config))
    }

    @Test
    fun `validateConfig returns error for blank container`() {
        uploader = BlobUploader(mockLogger)
        val config = UploadConfig("account", "", "sv=2026&sig=abc")
        assertEquals("Container is blank", uploader.validateConfig(config))
    }

    // --- buildUrl tests ---

    @Test
    fun `buildUrl produces correct format`() {
        uploader = BlobUploader(mockLogger)
        val config = UploadConfig("myaccount", "mycontainer", "sv=2026&sig=abc")
        val url = uploader.buildUrl(config, "photo.jpg")
        assertEquals(
            "https://myaccount.blob.core.windows.net/mycontainer/photo.jpg?sv=2026&sig=abc",
            url
        )
    }

    @Test
    fun `buildUrl strips leading question mark from token`() {
        uploader = BlobUploader(mockLogger)
        val config = UploadConfig("acc", "cont", "?sv=2026&sig=abc")
        val url = uploader.buildUrl(config, "test.jpg")
        assertTrue(url.contains("?sv=2026"))
        assertFalse(url.contains("??"))
    }

    @Test
    fun `buildUrl encodes spaces in filename`() {
        uploader = BlobUploader(mockLogger)
        val config = UploadConfig("acc", "cont", "sv=2026&sig=abc")
        val url = uploader.buildUrl(config, "my photo.jpg")
        assertTrue(url.contains("my%20photo.jpg"))
    }

    @Test
    fun `buildUrl encodes special characters`() {
        uploader = BlobUploader(mockLogger)
        val config = UploadConfig("acc", "cont", "sv=2026&sig=abc")
        val url = uploader.buildUrl(config, "photo (1).jpg")
        assertTrue(url.contains("photo%20%281%29.jpg"))
    }

    // --- shouldRetry tests ---

    @Test
    fun `shouldRetry returns true for 5xx codes`() {
        uploader = BlobUploader(mockLogger)
        assertTrue(uploader.shouldRetry(500))
        assertTrue(uploader.shouldRetry(502))
        assertTrue(uploader.shouldRetry(503))
        assertTrue(uploader.shouldRetry(504))
    }

    @Test
    fun `shouldRetry returns true for 408 and 429`() {
        uploader = BlobUploader(mockLogger)
        assertTrue(uploader.shouldRetry(408))
        assertTrue(uploader.shouldRetry(429))
    }

    @Test
    fun `shouldRetry returns false for 4xx client errors`() {
        uploader = BlobUploader(mockLogger)
        assertFalse(uploader.shouldRetry(400))
        assertFalse(uploader.shouldRetry(401))
        assertFalse(uploader.shouldRetry(403))
        assertFalse(uploader.shouldRetry(404))
    }

    @Test
    fun `shouldRetry returns false for 201 success`() {
        uploader = BlobUploader(mockLogger)
        assertFalse(uploader.shouldRetry(201))
    }

    // --- upload tests with mock connection ---

    @Test
    fun `upload returns Failure and logs error for invalid config`() {
        uploader = BlobUploader(mockLogger)
        val config = UploadConfig("account", "container", "")
        val result = uploader.upload(config, "test.jpg", "image/jpeg", ByteArrayInputStream(byteArrayOf()))

        assertTrue(result is UploadResult.Failure)
        assertEquals("SAS token is blank", (result as UploadResult.Failure).reason)
        assertTrue(mockLogger.hasError("BlobUploader", "SAS token is blank"))
    }

    @Test
    fun `upload returns Success and logs on 201`() {
        val mockConnection = MockHttpURLConnection(201)
        uploader = BlobUploader(mockLogger) { mockConnection }

        val config = UploadConfig("acc", "cont", "sv=2026&sig=abc")
        val result = uploader.upload(config, "photo.jpg", "image/jpeg", ByteArrayInputStream("data".toByteArray()))

        assertTrue(result is UploadResult.Success)
        assertTrue(mockLogger.hasDebug("BlobUploader", "Upload success filename=photo.jpg"))
    }

    @Test
    fun `upload returns Retry and logs on 503`() {
        val mockConnection = MockHttpURLConnection(503)
        uploader = BlobUploader(mockLogger) { mockConnection }

        val config = UploadConfig("acc", "cont", "sv=2026&sig=abc")
        val result = uploader.upload(config, "photo.jpg", "image/jpeg", ByteArrayInputStream("data".toByteArray()))

        assertTrue(result is UploadResult.Retry)
        assertEquals("HTTP 503", (result as UploadResult.Retry).reason)
        assertTrue(mockLogger.hasError("BlobUploader", "Upload failed code=503"))
    }

    @Test
    fun `upload returns Failure and logs on 403`() {
        val mockConnection = MockHttpURLConnection(403)
        uploader = BlobUploader(mockLogger) { mockConnection }

        val config = UploadConfig("acc", "cont", "sv=2026&sig=abc")
        val result = uploader.upload(config, "photo.jpg", "image/jpeg", ByteArrayInputStream("data".toByteArray()))

        assertTrue(result is UploadResult.Failure)
        assertEquals("HTTP 403", (result as UploadResult.Failure).reason)
        assertTrue(mockLogger.hasError("BlobUploader", "Upload failed code=403"))
    }

    @Test
    fun `upload returns Retry on connection exception`() {
        uploader = BlobUploader(mockLogger) { throw java.net.ConnectException("Connection refused") }

        val config = UploadConfig("acc", "cont", "sv=2026&sig=abc")
        val result = uploader.upload(config, "photo.jpg", "image/jpeg", ByteArrayInputStream("data".toByteArray()))

        assertTrue(result is UploadResult.Retry)
        assertTrue(mockLogger.hasError("BlobUploader", "Upload exception"))
    }

    @Test
    fun `upload logs starting message with filename`() {
        val mockConnection = MockHttpURLConnection(201)
        uploader = BlobUploader(mockLogger) { mockConnection }

        val config = UploadConfig("acc", "cont", "sv=2026&sig=abc")
        uploader.upload(config, "myimage.jpg", "image/jpeg", ByteArrayInputStream("data".toByteArray()))

        assertTrue(mockLogger.hasDebug("BlobUploader", "Uploading filename=myimage.jpg"))
    }

    // --- Helper classes ---

    class MockLogger : Logger {
        private val debugLogs = mutableListOf<Pair<String, String>>()
        private val errorLogs = mutableListOf<Triple<String, String, Throwable?>>()

        override fun d(tag: String, message: String) {
            debugLogs.add(tag to message)
        }

        override fun e(tag: String, message: String, throwable: Throwable?) {
            errorLogs.add(Triple(tag, message, throwable))
        }

        fun hasDebug(tag: String, messageContains: String): Boolean =
            debugLogs.any { it.first == tag && it.second.contains(messageContains) }

        fun hasError(tag: String, messageContains: String): Boolean =
            errorLogs.any { it.first == tag && it.second.contains(messageContains) }

        fun allDebugMessages(): List<String> = debugLogs.map { "${it.first}: ${it.second}" }
        fun allErrorMessages(): List<String> = errorLogs.map { "${it.first}: ${it.second}" }
    }

    class MockHttpURLConnection(private val mockResponseCode: Int) : HttpURLConnection(URL("https://example.com")) {
        private val outputBuffer = ByteArrayOutputStream()

        override fun getResponseCode(): Int = mockResponseCode
        override fun getOutputStream(): OutputStream = outputBuffer
        override fun getInputStream(): InputStream = ByteArrayInputStream(byteArrayOf())
        override fun getErrorStream(): InputStream? = if (responseCode >= 400) ByteArrayInputStream("error".toByteArray()) else null
        override fun connect() {}
        override fun disconnect() {}
        override fun usingProxy(): Boolean = false
    }
}
