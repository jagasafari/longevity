package com.jagasafari.longevity.photosync

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.ContentUris
import android.content.Intent
import android.database.ContentObserver
import android.net.Uri
import android.os.Handler
import android.os.IBinder
import android.os.Looper
import android.provider.MediaStore
import android.util.Log
import androidx.core.app.NotificationCompat
import androidx.work.BackoffPolicy
import androidx.work.Constraints
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.workDataOf
import java.util.concurrent.TimeUnit

class MediaObserverService : Service() {

    private lateinit var observer: ContentObserver
    private var lastHandledId: Long = -1

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        Log.d(TAG, "Service onCreate")
        createNotificationChannel()
        startForeground(NOTIFICATION_ID, buildNotification())

        observer = object : ContentObserver(Handler(Looper.getMainLooper())) {
            override fun onChange(selfChange: Boolean) {
                Log.d(TAG, "onChange(selfChange=$selfChange) — no URI")
                handleChange(null)
            }

            override fun onChange(selfChange: Boolean, uri: Uri?) {
                Log.d(TAG, "onChange(selfChange=$selfChange, uri=$uri)")
                handleChange(uri)
            }

            override fun onChange(selfChange: Boolean, uris: Collection<Uri>, flags: Int) {
                Log.d(TAG, "onChange(selfChange=$selfChange, uris=${uris.size}, flags=$flags)")
                for (u in uris) handleChange(u)
            }
        }
        contentResolver.registerContentObserver(
            MediaStore.Images.Media.EXTERNAL_CONTENT_URI,
            true,
            observer
        )
        Log.d(TAG, "Observer registered")
    }

    private fun handleChange(uri: Uri?) {
        val resolved = resolveLatestCameraPhoto()
        Log.d(TAG, "Resolved URI: $resolved")
        if (resolved != null) {
            enqueueUpload(resolved)
        } else {
            Log.d(TAG, "No new camera photo found")
        }
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        Log.d(TAG, "onStartCommand")
        return START_STICKY
    }

    override fun onDestroy() {
        Log.d(TAG, "Service onDestroy")
        contentResolver.unregisterContentObserver(observer)
        super.onDestroy()
    }

    private fun enqueueUpload(uri: Uri) {
        val constraints = Constraints.Builder()
            .setRequiredNetworkType(NetworkType.CONNECTED)
            .build()

        val work = OneTimeWorkRequestBuilder<UploadWorker>()
            .setInputData(workDataOf("uri" to uri.toString()))
            .setConstraints(constraints)
            .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, 30, TimeUnit.SECONDS)
            .build()

        val uniqueName = "upload:${uri.lastPathSegment ?: uri}"
        Log.d(TAG, "Enqueuing work: $uniqueName")
        WorkManager.getInstance(this).enqueueUniqueWork(
            uniqueName,
            ExistingWorkPolicy.KEEP,
            work
        )
    }

    private fun resolveLatestCameraPhoto(): Uri? {
        val projection = arrayOf(
            MediaStore.Images.Media._ID,
            MediaStore.Images.Media.DISPLAY_NAME,
            MediaStore.Images.Media.RELATIVE_PATH
        )
        val selection = "${MediaStore.Images.Media.RELATIVE_PATH} LIKE ?"
        val selectionArgs = arrayOf("DCIM/Camera%")
        val sortOrder = "${MediaStore.Images.Media.DATE_ADDED} DESC"

        return contentResolver.query(
            MediaStore.Images.Media.EXTERNAL_CONTENT_URI,
            projection,
            selection,
            selectionArgs,
            sortOrder
        )?.use { cursor ->
            if (!cursor.moveToFirst()) {
                Log.d(TAG, "No images found in DCIM/Camera")
                return@use null
            }
            val id = cursor.getLong(cursor.getColumnIndexOrThrow(MediaStore.Images.Media._ID))
            val name = cursor.getString(cursor.getColumnIndexOrThrow(MediaStore.Images.Media.DISPLAY_NAME))
            Log.d(TAG, "Latest camera photo: id=$id name=$name")
            if (id == lastHandledId) {
                Log.d(TAG, "Already handled id=$id, skipping")
                return@use null
            }
            lastHandledId = id
            ContentUris.withAppendedId(MediaStore.Images.Media.EXTERNAL_CONTENT_URI, id)
        }
    }

    private fun createNotificationChannel() {
        val channel = NotificationChannel(
            CHANNEL_ID,
            "Photo Sync",
            NotificationManager.IMPORTANCE_LOW
        ).apply { description = "Watching for new photos to upload" }
        getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
    }

    private fun buildNotification(): Notification =
        NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle("Photo Sync active")
            .setContentText("Watching DCIM/Camera for new photos")
            .setSmallIcon(android.R.drawable.ic_menu_upload)
            .setOngoing(true)
            .build()

    companion object {
        private const val TAG = "MediaObserverService"
        private const val CHANNEL_ID = "photo_sync_channel"
        private const val NOTIFICATION_ID = 1
    }
}
