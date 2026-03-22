package com.jagasafari.longevity.photosync

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
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
import com.jagasafari.longevity.photosync.data.AzureBlobRepository
import com.jagasafari.longevity.photosync.data.MediaStorePhotoRepository
import com.jagasafari.longevity.photosync.data.SecurePrefsConfigRepository
import com.jagasafari.longevity.photosync.domain.model.LocalPhoto
import com.jagasafari.longevity.photosync.domain.usecase.SyncUseCase
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

class MediaObserverService : Service() {

    private lateinit var observer: ContentObserver
    private var lastHandledId: Long = -1
    private val serviceScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val changeMutex = Mutex()

    // Manual Dependency Injection
    private val configRepository by lazy { SecurePrefsConfigRepository(this) }
    private val syncUseCase by lazy {
        SyncUseCase(
            photoRepository = MediaStorePhotoRepository(this),
            blobRepository = AzureBlobRepository()
        )
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        Log.d(TAG, "Service onCreate")
        createNotificationChannel()
        startForeground(NOTIFICATION_ID, buildNotification())

        observer = object : ContentObserver(Handler(Looper.getMainLooper())) {
            override fun onChange(selfChange: Boolean) {
                handleChange()
            }

            override fun onChange(selfChange: Boolean, uri: Uri?) {
                handleChange()
            }

            override fun onChange(selfChange: Boolean, uris: Collection<Uri>, flags: Int) {
                handleChange()
            }
        }
        contentResolver.registerContentObserver(
            MediaStore.Images.Media.EXTERNAL_CONTENT_URI,
            true,
            observer
        )
        Log.d(TAG, "Observer registered")
        serviceScope.launch { runCatchUpSync() }
    }

    private fun handleChange() {
        serviceScope.launch {
            changeMutex.withLock {
                val (unseenPhotos, newWatermark) = syncUseCase.getUnseenPhotos(lastHandledId)
                if (newWatermark > lastHandledId) {
                    lastHandledId = newWatermark
                    Log.d(TAG, "Resolved ${unseenPhotos.size} unseen photos. New watermark: $lastHandledId")
                    unseenPhotos.forEach { enqueueUpload(it) }
                } else {
                    Log.d(TAG, "No new target photos found (watermark unchanged)")
                }
            }
        }
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        Log.d(TAG, "onStartCommand")
        return START_STICKY
    }

    override fun onDestroy() {
        Log.d(TAG, "Service onDestroy")
        serviceScope.cancel()
        contentResolver.unregisterContentObserver(observer)
        super.onDestroy()
    }

    private fun enqueueUpload(localPhoto: LocalPhoto) {
        val constraints = Constraints.Builder()
            .setRequiredNetworkType(NetworkType.CONNECTED)
            .build()

        val work = OneTimeWorkRequestBuilder<UploadWorker>()
            .setInputData(workDataOf(
                "uri" to localPhoto.uri.toString(),
                "fileName" to localPhoto.filename // Provide file name to worker
            ))
            .setConstraints(constraints)
            .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, 30, TimeUnit.SECONDS)
            .build()

        val uniqueName = "upload:${localPhoto.id}"
        Log.d(TAG, "Enqueuing work: $uniqueName")
        WorkManager.getInstance(this).enqueueUniqueWork(
            uniqueName,
            ExistingWorkPolicy.KEEP,
            work
        )
    }

    private suspend fun runCatchUpSync() {
        val config = configRepository.getConfig() ?: run {
            Log.d(TAG, "Catch-up: no SAS token configured, skipping")
            return
        }

        UploadLogStore.addLog("Fetching blob list...")
        
        try {
            val missing = syncUseCase.executeCatchUp(config)
            
            Log.d(TAG, "Catch-up: enqueueing ${missing.size} missing photos")
            UploadLogStore.addLog("Uploading ${missing.size} missing photos")
            
            missing.forEach { enqueueUpload(it) }
        } catch (e: Exception) {
            Log.e(TAG, "Failed fetch or diff during catch-up", e)
            UploadLogStore.addLog("Catch-up failed: ${e.message}")
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
            .setContentText("Watching DCIM/Camera & DCIM/Uploads")
            .setSmallIcon(android.R.drawable.ic_menu_upload)
            .setOngoing(true)
            .build()

    companion object {
        private const val TAG = "MediaObserverService"
        private const val CHANNEL_ID = "photo_sync_channel"
        private const val NOTIFICATION_ID = 1
    }
}
