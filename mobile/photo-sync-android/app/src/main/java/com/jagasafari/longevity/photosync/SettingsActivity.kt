package com.jagasafari.longevity.photosync

import android.os.Bundle
import android.widget.Button
import android.widget.EditText
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity

class SettingsActivity : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_settings)

        val prefs = SecurePrefs.get(this)
        val sasTokenInput = findViewById<EditText>(R.id.sas_token_input)
        val storageAccountInput = findViewById<EditText>(R.id.storage_account_input)
        val containerInput = findViewById<EditText>(R.id.container_input)
        val saveButton = findViewById<Button>(R.id.save_button)

        sasTokenInput.setText(prefs.getString("sas_token", ""))
        storageAccountInput.setText(prefs.getString("storage_account", "longevityphotos"))
        containerInput.setText(prefs.getString("container", "photos"))

        saveButton.setOnClickListener {
            val sasToken = sasTokenInput.text.toString().trim()
            if (sasToken.isEmpty()) {
                Toast.makeText(this, "SAS token is required", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }
            prefs.edit()
                .putString("sas_token", sasToken)
                .putString("storage_account", storageAccountInput.text.toString().trim())
                .putString("container", containerInput.text.toString().trim())
                .apply()
            Toast.makeText(this, "Settings saved", Toast.LENGTH_SHORT).show()
            finish()
        }
    }
}
