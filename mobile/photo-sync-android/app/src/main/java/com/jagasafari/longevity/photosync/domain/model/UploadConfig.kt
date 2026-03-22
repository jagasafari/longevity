package com.jagasafari.longevity.photosync.domain.model

data class UploadConfig(
    val storageAccount: String,
    val container: String,
    val sasToken: String
)
