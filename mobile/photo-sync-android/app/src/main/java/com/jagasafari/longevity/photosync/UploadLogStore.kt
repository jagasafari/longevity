package com.jagasafari.longevity.photosync

import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

object UploadLogStore {
    private val _logs = mutableListOf<String>()
    
    private val _updates = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val updates: SharedFlow<Unit> = _updates

    val logs: List<String>
        get() = synchronized(_logs) { _logs.toList() }

    fun addLog(msg: String) {
        val time = SimpleDateFormat("HH:mm:ss", Locale.US).format(Date())
        val entry = "[$time] $msg"
        synchronized(_logs) {
            _logs.add(0, entry)
            if (_logs.size > 200) _logs.removeLast()
        }
        _updates.tryEmit(Unit)
    }
}
