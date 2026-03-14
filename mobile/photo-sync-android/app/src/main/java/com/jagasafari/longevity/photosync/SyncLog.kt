package com.jagasafari.longevity.photosync

import androidx.lifecycle.LiveData
import androidx.lifecycle.MutableLiveData

enum class SyncStatus { UPLOADED, FAILED }

data class SyncEntry(val filename: String, val status: SyncStatus)

object SyncLog {
    private val _entries = MutableLiveData<List<SyncEntry>>(emptyList())
    val entries: LiveData<List<SyncEntry>> = _entries

    fun add(filename: String, status: SyncStatus) {
        val current = _entries.value.orEmpty()
        _entries.postValue(listOf(SyncEntry(filename, status)) + current)
    }
}
