package com.jagasafari.longevity.photosync.domain.model

import android.net.Uri

data class LocalPhoto(
    val id: Long,
    val filename: String,
    val uri: Uri,
    val folder: String
)
