package com.jagasafari.longevity.photosync.domain.model

import org.junit.Assert.assertEquals
import org.junit.Test

class UploadConfigTest {

    @Test
    fun `saUrl builds correct Azure blob URL`() {
        val config = UploadConfig("myaccount", "mycontainer", "sv=2026&sig=abc")
        assertEquals(
            "https://myaccount.blob.core.windows.net/mycontainer",
            config.saUrl
        )
    }

    @Test
    fun `normalizedSasToken prepends question mark when missing`() {
        val config = UploadConfig("acc", "cont", "sv=2026&sig=abc")
        assertEquals("?sv=2026&sig=abc", config.normalizedSasToken)
    }

    @Test
    fun `normalizedSasToken keeps existing question mark`() {
        val config = UploadConfig("acc", "cont", "?sv=2026&sig=abc")
        assertEquals("?sv=2026&sig=abc", config.normalizedSasToken)
    }
}
