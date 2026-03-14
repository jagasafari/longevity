package com.jagasafari.longevity.photosync

import android.app.Application
import android.util.Log
import androidx.work.Configuration
import androidx.work.WorkManager

class PhotoSyncApp : Application(), Configuration.Provider {
    override fun onCreate() {
        super.onCreate()
        Log.d(TAG, "PhotoSyncApp onCreate - initializing WorkManager with custom factory")
        WorkManager.initialize(this, workManagerConfiguration)
    }

    override val workManagerConfiguration: Configuration
        get() = Configuration.Builder()
            .setWorkerFactory(PhotoSyncWorkerFactory())
            .build()

    companion object {
        private const val TAG = "PhotoSyncApp"
    }
}
