package com.jagasafari.longevity.photosync.domain.repository

import com.jagasafari.longevity.photosync.domain.model.UploadConfig
import com.jagasafari.longevity.photosync.domain.model.UploadResult
import java.io.InputStream

interface BlobRepository {
    fun listAllBlobs(config: UploadConfig): Set<String>
    fun upload(config: UploadConfig, filename: String, contentType: String, inputStream: InputStream): UploadResult
}
