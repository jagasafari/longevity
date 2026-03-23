package com.jagasafari.longevity.photosync.domain.repository

import com.jagasafari.longevity.photosync.domain.model.LocalPhoto

interface PhotoRepository {
    fun getPhotos(folderPrefix: String, cutoffSeconds: Long? = null): List<LocalPhoto>
    fun getUnseenPhotos(lastHandledId: Long): Pair<List<LocalPhoto>, Long>
}
