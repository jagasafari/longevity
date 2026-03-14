package com.jagasafari.longevity.photosync

import android.content.Context
import android.util.Log
import androidx.work.ListenableWorker
import androidx.work.WorkerFactory
import androidx.work.WorkerParameters

class PhotoSyncWorkerFactory : WorkerFactory() {
    override fun createWorker(
        appContext: Context,
        workerClassName: String,
        workerParameters: WorkerParameters
    ): ListenableWorker? {
        Log.d(TAG, "createWorker class=$workerClassName")
        return when (workerClassName) {
            UploadWorker::class.java.name -> UploadWorker(appContext, workerParameters)
            else -> null
        }
    }

    companion object {
        private const val TAG = "PhotoSyncWorkerFactory"
    }
}
