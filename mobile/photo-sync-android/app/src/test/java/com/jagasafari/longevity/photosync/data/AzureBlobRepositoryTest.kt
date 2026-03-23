package com.jagasafari.longevity.photosync.data

import com.jagasafari.longevity.photosync.domain.model.UploadConfig
import com.jagasafari.longevity.photosync.domain.model.UploadResult
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Ignore
import org.junit.Test
import java.io.ByteArrayInputStream

class AzureBlobRepositoryTest {

    private lateinit var server: MockWebServer
    private lateinit var repository: AzureBlobRepository
    private lateinit var config: UploadConfig

    @Before
    fun setUp() {
        server = MockWebServer()
        server.start()
        val baseUrl = server.url("/container").toString().trimEnd('/')
        repository = AzureBlobRepository(baseUrlOverride = baseUrl)
        config = UploadConfig("unused", "unused", "sv=2026&sig=abc")
    }

    @After
    fun tearDown() {
        server.shutdown()
    }

    @Ignore("XmlPullParserFactory requires Android runtime")
    @Test
    fun `listAllBlobs returns blob names from XML response`() {
        val xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <EnumerationResults>
              <Blobs>
                <Blob><Name>photo1.jpg</Name></Blob>
                <Blob><Name>photo2.jpg</Name></Blob>
              </Blobs>
              <NextMarker/>
            </EnumerationResults>
        """.trimIndent()
        server.enqueue(MockResponse().setBody(xml).setResponseCode(200))

        val result = repository.listAllBlobs(config)

        assertEquals(setOf("photo1.jpg", "photo2.jpg"), result)
    }

    @Ignore("XmlPullParserFactory requires Android runtime")
    @Test
    fun `listAllBlobs returns empty set on HTTP error`() {
        server.enqueue(MockResponse().setResponseCode(500).setBody("error"))

        val result = repository.listAllBlobs(config)

        assertTrue(result.isEmpty())
    }

    @Ignore("XmlPullParserFactory requires Android runtime")
    @Test
    fun `listAllBlobs paginates using NextMarker`() {
        val page1 = """
            <?xml version="1.0" encoding="utf-8"?>
            <EnumerationResults>
              <Blobs><Blob><Name>a.jpg</Name></Blob></Blobs>
              <NextMarker>marker1</NextMarker>
            </EnumerationResults>
        """.trimIndent()
        val page2 = """
            <?xml version="1.0" encoding="utf-8"?>
            <EnumerationResults>
              <Blobs><Blob><Name>b.jpg</Name></Blob></Blobs>
              <NextMarker/>
            </EnumerationResults>
        """.trimIndent()
        server.enqueue(MockResponse().setBody(page1).setResponseCode(200))
        server.enqueue(MockResponse().setBody(page2).setResponseCode(200))

        val result = repository.listAllBlobs(config)

        assertEquals(setOf("a.jpg", "b.jpg"), result)
        assertEquals(2, server.requestCount)
    }

    @Test
    fun `upload returns Success on 201`() {
        server.enqueue(MockResponse().setResponseCode(201))

        val result = repository.upload(
            config, "photo.jpg", "image/jpeg",
            ByteArrayInputStream("data".toByteArray()), 4L
        )

        assertTrue(result is UploadResult.Success)
        val request = server.takeRequest()
        assertEquals("PUT", request.method)
        assertTrue(request.path!!.contains("photo.jpg"))
        assertEquals("BlockBlob", request.getHeader("x-ms-blob-type"))
    }

    @Test
    fun `upload returns Retry on 503`() {
        server.enqueue(MockResponse().setResponseCode(503).setBody("busy"))

        val result = repository.upload(
            config, "photo.jpg", "image/jpeg",
            ByteArrayInputStream("data".toByteArray()), 4L
        )

        assertTrue(result is UploadResult.Retry)
        assertEquals("HTTP 503", (result as UploadResult.Retry).reason)
    }

    @Test
    fun `upload returns Retry on 429`() {
        server.enqueue(MockResponse().setResponseCode(429))

        val result = repository.upload(
            config, "photo.jpg", "image/jpeg",
            ByteArrayInputStream("data".toByteArray()), 4L
        )

        assertTrue(result is UploadResult.Retry)
        assertEquals("HTTP 429", (result as UploadResult.Retry).reason)
    }

    @Test
    fun `upload returns Failure on 403`() {
        server.enqueue(MockResponse().setResponseCode(403).setBody("forbidden"))

        val result = repository.upload(
            config, "photo.jpg", "image/jpeg",
            ByteArrayInputStream("data".toByteArray()), 4L
        )

        assertTrue(result is UploadResult.Failure)
        assertEquals("HTTP 403", (result as UploadResult.Failure).reason)
    }

    @Test
    fun `upload renames heic to jpg`() {
        server.enqueue(MockResponse().setResponseCode(201))

        repository.upload(
            config, "IMG_001.heic", "image/jpeg",
            ByteArrayInputStream("data".toByteArray()), 4L
        )

        val request = server.takeRequest()
        assertTrue(request.path!!.contains("IMG_001.jpg"))
    }

    @Test
    fun `upload sends correct content length`() {
        server.enqueue(MockResponse().setResponseCode(201))
        val body = "hello world"

        repository.upload(
            config, "test.jpg", "image/jpeg",
            ByteArrayInputStream(body.toByteArray()), body.length.toLong()
        )

        val request = server.takeRequest()
        assertEquals(body.length.toLong(), request.bodySize)
    }

    @Test
    fun `upload encodes spaces in filename`() {
        server.enqueue(MockResponse().setResponseCode(201))

        repository.upload(
            config, "my photo (1).jpg", "image/jpeg",
            ByteArrayInputStream("data".toByteArray()), 4L
        )

        val request = server.takeRequest()
        assertTrue(request.path!!.contains("my%20photo%20%281%29.jpg"))
    }
}
