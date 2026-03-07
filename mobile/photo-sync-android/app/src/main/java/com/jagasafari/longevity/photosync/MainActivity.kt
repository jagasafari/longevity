package com.jagasafari.longevity.photosync

import android.Manifest
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
        ContextCompat.startForegroundService(this, Intent(this, MediaObserverService::class.java))
        statusText.text = "Sync running"
    }

    private fun stopSyncService() {
        stopService(Intent(this, MediaObserverService::class.java))
        statusText.text = "Sync stopped"
    }

    private fun updateStatus() {
        val prefs = SecurePrefs.get(this)
        val hasSas = !prefs.getString("sas_token", null).isNullOrBlank()
        statusText.text = if (hasSas) "Ready — press Start" else "Set SAS token in Settings"
    }
}
