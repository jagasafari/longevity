package com.jagasafari.longevity.photosync.data

import android.content.ContentUris
import android.content.Context
import android.provider.MediaStore
import android.util.Log
import com.jagasafari.longevity.photosync.domain.model.LocalPhoto
import com.jagasafari.longevity.photosync.domain.repository.PhotoRepository

class MediaStorePhotoRepository(private val context: Context) : PhotoRepository {
    
    companion object {
        private const val TAG = "MediaStoreRepo"
    }

    override fun getPhotos(folderPrefix: String, cutoffSeconds: Long?): List<LocalPhoto> {
        val projection = arrayOf(
            MediaStore.Images.Media._ID,
            MediaStore.Images.Media.DISPLAY_NAME,
            MediaStore.Images.Media.RELATIVE_PATH
        )
        val selection = if (cutoffSeconds != null) {
            "${MediaStore.Images.Media.RELATIVE_PATH} LIKE ? AND ${MediaStore.Images.Media.DATE_ADDED} >= ?"
        } else {
            "${MediaStore.Images.Media.RELATIVE_PATH} LIKE ?"
        }
        val args = if (cutoffSeconds != null) {
            arrayOf(folderPrefix, cutoffSeconds.toString())
        } else {
            arrayOf(folderPrefix)
        }

        val results = mutableListOf<LocalPhoto>()
        context.contentResolver.query(
            MediaStore.Images.Media.EXTERNAL_CONTENT_URI,
            projection,
            selection,
            args,
            "${MediaStore.Images.Media.DATE_ADDED} DESC"
        )?.use { cursor ->
            val idCol = cursor.getColumnIndexOrThrow(MediaStore.Images.Media._ID)
            val nameCol = cursor.getColumnIndexOrThrow(MediaStore.Images.Media.DISPLAY_NAME)
            val pathCol = cursor.getColumnIndexOrThrow(MediaStore.Images.Media.RELATIVE_PATH)
            
            while (cursor.moveToNext()) {
                val id = cursor.getLong(idCol)
                val name = cursor.getString(nameCol)
                val path = cursor.getString(pathCol)
                val uri = ContentUris.withAppendedId(MediaStore.Images.Media.EXTERNAL_CONTENT_URI, id)
                results.add(LocalPhoto(id, name, uri, path))
            }
        }
        return results
    }

    override fun getUnseenPhotos(lastHandledId: Long): Pair<List<LocalPhoto>, Long> {
        val projection = arrayOf(
            MediaStore.Images.Media._ID,
            MediaStore.Images.Media.DISPLAY_NAME,
            MediaStore.Images.Media.RELATIVE_PATH
        )
        val selection = "${MediaStore.Images.Media.RELATIVE_PATH} LIKE ? OR ${MediaStore.Images.Media.RELATIVE_PATH} LIKE ?"
        val selectionArgs = arrayOf("DCIM/Camera%", "DCIM/Uploads%")
        val sortOrder = "${MediaStore.Images.Media._ID} DESC"

        val unseen = mutableListOf<LocalPhoto>()
        var highestSeenId = lastHandledId

        context.contentResolver.query(
            MediaStore.Images.Media.EXTERNAL_CONTENT_URI,
            projection,
            selection,
            selectionArgs,
            sortOrder
        )?.use { cursor ->
            val idCol = cursor.getColumnIndexOrThrow(MediaStore.Images.Media._ID)
            val nameCol = cursor.getColumnIndexOrThrow(MediaStore.Images.Media.DISPLAY_NAME)
            val pathCol = cursor.getColumnIndexOrThrow(MediaStore.Images.Media.RELATIVE_PATH)

            while (cursor.moveToNext()) {
                val id = cursor.getLong(idCol)
                if (lastHandledId == -1L) {
                    val name = cursor.getString(nameCol)
                    val path = cursor.getString(pathCol)
                    val uri = ContentUris.withAppendedId(MediaStore.Images.Media.EXTERNAL_CONTENT_URI, id)
                    unseen.add(LocalPhoto(id, name, uri, path))
                    highestSeenId = id
                    break
                }

                if (id <= lastHandledId) break

                val name = cursor.getString(nameCol)
                val path = cursor.getString(pathCol)
                val uri = ContentUris.withAppendedId(MediaStore.Images.Media.EXTERNAL_CONTENT_URI, id)
                unseen.add(LocalPhoto(id, name, uri, path))
                highestSeenId = maxOf(highestSeenId, id)
            }
        }

        val newWatermark = if (highestSeenId > lastHandledId) highestSeenId else lastHandledId
        return Pair(unseen, newWatermark)
    }
}
