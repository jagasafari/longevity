package com.jagasafari.longevity.photosync.domain.model

sealed class UploadResult {
    data object Success : UploadResult()
    data class Retry(val reason: String) : UploadResult()
    data class Failure(val reason: String) : UploadResult()
}
