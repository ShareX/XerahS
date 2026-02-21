package com.getsharex.xerahs.mobile.feature.history

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.getsharex.xerahs.mobile.core.data.HistoryRepository
import com.getsharex.xerahs.mobile.core.domain.HistoryEntry
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

private const val MAX_ITEMS = 100

class HistoryViewModel(
    private val historyRepository: HistoryRepository
) : ViewModel() {

    private val _entries = MutableStateFlow<List<HistoryEntry>>(emptyList())
    val entries: StateFlow<List<HistoryEntry>> = _entries.asStateFlow()

    private val _searchQuery = MutableStateFlow("")
    val searchQuery: StateFlow<String> = _searchQuery.asStateFlow()

    val filteredEntries: StateFlow<List<HistoryEntry>> = combine(_entries, _searchQuery) { list, q ->
        if (q.isBlank()) list else list.filter {
            it.fileName.contains(q, ignoreCase = true) ||
                it.url.contains(q, ignoreCase = true) ||
                it.host.contains(q, ignoreCase = true)
        }
    }.stateIn(viewModelScope, SharingStarted.Eagerly, emptyList())

    init {
        refresh()
    }

    fun refresh() {
        viewModelScope.launch {
            _entries.value = historyRepository.getRecentEntries(MAX_ITEMS)
        }
    }

    fun setSearchQuery(query: String) {
        _searchQuery.value = query
    }

    fun clearAll(): Int {
        val count = historyRepository.clearEntries()
        refresh()
        return count
    }

    fun deleteEntry(id: Long): Boolean {
        val ok = historyRepository.deleteEntry(id)
        if (ok) refresh()
        return ok
    }
}
