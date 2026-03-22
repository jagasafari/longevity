package com.jagasafari.longevity.photosync.data

import android.content.Context
import com.jagasafari.longevity.photosync.SecurePrefs
import com.jagasafari.longevity.photosync.domain.model.UploadConfig
import com.jagasafari.longevity.photosync.domain.repository.ConfigRepository

class SecurePrefsConfigRepository(private val context: Context) : ConfigRepository {
    override fun getConfig(): UploadConfig? {
        val prefs = SecurePrefs.get(context)
        val rawToken = prefs.getString("sas_token", null) ?: return null
        
        if (rawToken.isBlank()) return null

        val storageAccount = prefs.getString("storage_account", "longevityphotos")
            ?.trim().orEmpty().ifBlank { "longevityphotos" }
        val container = prefs.getString("container", "photos")
            ?.trim().orEmpty().ifBlank { "photos" }

        return UploadConfig(storageAccount, container, rawToken)
    }
}
