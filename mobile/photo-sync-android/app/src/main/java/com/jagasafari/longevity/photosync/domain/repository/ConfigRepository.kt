package com.jagasafari.longevity.photosync.domain.repository

import com.jagasafari.longevity.photosync.domain.model.UploadConfig

interface ConfigRepository {
    fun getConfig(): UploadConfig?
}
