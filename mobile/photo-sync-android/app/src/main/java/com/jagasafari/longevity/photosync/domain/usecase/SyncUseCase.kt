package com.jagasafari.longevity.photosync.domain.usecase

import com.jagasafari.longevity.photosync.domain.model.LocalPhoto
import com.jagasafari.longevity.photosync.domain.model.UploadConfig
import com.jagasafari.longevity.photosync.domain.repository.BlobRepository
import com.jagasafari.longevity.photosync.domain.repository.PhotoRepository

class SyncUseCase(
    private val photoRepository: PhotoRepository,
    private val blobRepository: BlobRepository
) {

    fun executeCatchUp(config: UploadConfig): List<LocalPhoto> {
        val remoteBlobs = blobRepository.listAllBlobs(config)
        
        val cutoffSeconds = (System.currentTimeMillis() / 1000) - 48 * 60 * 60
        val cameraPhotos = photoRepository.getPhotos("DCIM/Camera%", cutoffSeconds)
        val uploadsPhotos = photoRepository.getPhotos("DCIM/Uploads%", null)

        val allLocal = (cameraPhotos + uploadsPhotos).distinctBy { it.id }

        return allLocal.filter { local ->
            !remoteBlobs.contains(local.name) && !remoteBlobs.contains(
                if (local.name.endsWith(".heic", ignoreCase = true)) {
                    local.name.substringBeforeLast('.') + ".jpg"
                } else {
                    local.name
                }
            )
        }
    }

    fun getUnseenPhotos(lastHandledId: Long): Pair<List<LocalPhoto>, Long> {
        return photoRepository.getUnseenPhotos(lastHandledId)
    }
}
