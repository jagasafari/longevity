package com.jagasafari.longevity.photosync

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Color
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.Button
import android.widget.TextView
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.recyclerview.widget.LinearLayoutManager
import androidx.recyclerview.widget.RecyclerView

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

        val adapter = SyncLogAdapter()
        val list = findViewById<RecyclerView>(R.id.sync_log_list)
        list.layoutManager = LinearLayoutManager(this)
        list.adapter = adapter
        SyncLog.entries.observe(this) { entries -> adapter.submit(entries) }

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
        statusText.text = "Starting sync..."
    }

    private fun stopSyncService() {
        stopService(Intent(this, MediaObserverService::class.java))
        SecurePrefs.get(this).edit().putBoolean(SecurePrefs.KEY_SYNC_SERVICE_RUNNING, false).apply()
        updateStatus()
    }

    private fun updateStatus() {
        val prefs = SecurePrefs.get(this)
        val hasSas = !prefs.getString("sas_token", null).isNullOrBlank()
        val isRunning = prefs.getBoolean(SecurePrefs.KEY_SYNC_SERVICE_RUNNING, false)
        statusText.text = when {
            !hasSas -> "Set SAS token in Settings"
            isRunning -> "Sync running"
            else -> "Ready — press Start"
        }
    }
}

private class SyncLogAdapter : RecyclerView.Adapter<SyncLogAdapter.VH>() {

    private var items: List<SyncEntry> = emptyList()

    fun submit(newItems: List<SyncEntry>) {
        items = newItems
        notifyDataSetChanged()
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): VH {
        val view = LayoutInflater.from(parent.context)
            .inflate(R.layout.item_sync_entry, parent, false)
        return VH(view)
    }

    override fun onBindViewHolder(holder: VH, position: Int) = holder.bind(items[position])

    override fun getItemCount() = items.size

    class VH(view: View) : RecyclerView.ViewHolder(view) {
        private val status: TextView = view.findViewById(R.id.entry_status)
        private val filename: TextView = view.findViewById(R.id.entry_filename)

        fun bind(entry: SyncEntry) {
            filename.text = entry.filename
            when (entry.status) {
                SyncStatus.UPLOADED -> {
                    status.text = "✓ uploaded"
                    status.setTextColor(Color.parseColor("#2E7D32"))
                }
                SyncStatus.FAILED -> {
                    status.text = "✗ failed"
                    status.setTextColor(Color.parseColor("#C62828"))
                }
            }
        }
    }
}
