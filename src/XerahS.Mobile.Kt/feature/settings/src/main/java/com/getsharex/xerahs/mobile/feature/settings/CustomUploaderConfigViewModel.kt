package com.getsharex.xerahs.mobile.feature.settings

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.getsharex.xerahs.mobile.core.data.SettingsRepository
import com.getsharex.xerahs.mobile.core.domain.CustomUploaderEntry
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import java.util.UUID

class CustomUploaderConfigViewModel(
    private val settingsRepository: SettingsRepository
) : ViewModel() {

    private val _uploaders = MutableStateFlow<List<CustomUploaderEntry>>(emptyList())
    val uploaders: StateFlow<List<CustomUploaderEntry>> = _uploaders.asStateFlow()

    private val _editingEntry = MutableStateFlow<CustomUploaderEntry?>(null)
    val editingEntry: StateFlow<CustomUploaderEntry?> = _editingEntry.asStateFlow()

    init {
        viewModelScope.launch {
            _uploaders.value = settingsRepository.loadCustomUploaders()
        }
    }

    fun refresh() {
        viewModelScope.launch {
            _uploaders.value = settingsRepository.loadCustomUploaders()
        }
    }

    fun addNew() {
        _editingEntry.value = CustomUploaderEntry(
            id = "custom_${UUID.randomUUID().toString().take(8)}",
            name = "New Uploader",
            requestUrl = "",
            fileFormName = "file"
        )
    }

    fun edit(entry: CustomUploaderEntry) {
        _editingEntry.value = entry.copy()
    }

    fun saveEdit(entry: CustomUploaderEntry) {
        val list = _uploaders.value.toMutableList()
        val index = list.indexOfFirst { it.id == entry.id }
        if (index >= 0) {
            list[index] = entry
        } else {
            list.add(entry)
        }
        settingsRepository.saveCustomUploaders(list)
        _uploaders.value = list
        _editingEntry.value = null
    }

    fun cancelEdit() {
        _editingEntry.value = null
    }

    fun delete(entry: CustomUploaderEntry) {
        val list = _uploaders.value.filter { it.id != entry.id }
        settingsRepository.saveCustomUploaders(list)
        _uploaders.value = list
    }
}
