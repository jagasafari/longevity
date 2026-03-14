package com.jagasafari.longevity.photosync

import android.Manifest
import android.app.ActivityManager
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Bundle
import android.widget.Button
import android.widget.TextView
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat

class MainActivity : AppCompatActivity() {

    private lateinit var statusText: TextView

    private val permissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { grants ->
        if (grants.values.all { it }) {
            startSyncService()
        } else {
            statusText.text = "Permissions denied — cannot watch photos"
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        statusText = findViewById(R.id.status_text)
        val startButton = findViewById<Button>(R.id.start_button)
        val stopButton = findViewById<Button>(R.id.stop_button)
        val settingsButton = findViewById<Button>(R.id.settings_button)

        startButton.setOnClickListener { requestPermissionsAndStart() }
        stopButton.setOnClickListener { stopSyncService() }
        settingsButton.setOnClickListener {
            startActivity(Intent(this, SettingsActivity::class.java))
        }

        updateStatus()
    }

    override fun onResume() {
        super.onResume()
        updateStatus()
    }

    private fun requestPermissionsAndStart() {
        val needed = mutableListOf<String>()
        if (checkSelfPermission(Manifest.permission.READ_MEDIA_IMAGES) != PackageManager.PERMISSION_GRANTED)
            needed.add(Manifest.permission.READ_MEDIA_IMAGES)
        if (checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED)
            needed.add(Manifest.permission.POST_NOTIFICATIONS)

        if (needed.isEmpty()) {
            startSyncService()
        } else {
            permissionLauncher.launch(needed.toTypedArray())
        }
    }

    private fun startSyncService() {
        val prefs = SecurePrefs.get(this)
        if (prefs.getString("sas_token", null).isNullOrBlank()) {
            statusText.text = "Configure SAS token in Settings first"
            return
        }
        prefs.edit().putBoolean(SecurePrefs.KEY_SYNC_ENABLED, true).apply()
        ContextCompat.startForegroundService(this, Intent(this, MediaObserverService::class.java))
        updateStatus()
    }

    private fun stopSyncService() {
        stopService(Intent(this, MediaObserverService::class.java))
        SecurePrefs.get(this).edit().putBoolean(SecurePrefs.KEY_SYNC_ENABLED, false).apply()
        updateStatus()
    }

    private fun updateStatus() {
        val prefs = SecurePrefs.get(this)
        val hasSas = !prefs.getString("sas_token", null).isNullOrBlank()
        val isRunning = isServiceRunning(MediaObserverService::class.java)
        
        statusText.text = when {
            !hasSas -> "Set SAS token in Settings"
            isRunning -> "Sync running"
            else -> "Ready — press Start"
        }
    }

    private fun isServiceRunning(serviceClass: Class<*>): Boolean {
        val manager = getSystemService(Context.ACTIVITY_SERVICE) as ActivityManager
        for (service in manager.getRunningServices(Int.MAX_VALUE)) {
            if (serviceClass.name == service.service.className) {
                return true
            }
        }
        return false
    }
}
