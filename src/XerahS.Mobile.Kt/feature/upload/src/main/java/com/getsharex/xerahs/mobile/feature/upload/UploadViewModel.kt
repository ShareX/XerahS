/*
 * XerahS - The Avalonia UI implementation of ShareX
 * Copyright (c) 2007-2026 ShareX Team
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 *
 * Optionally you can also view the license at <http://www.gnu.org/licenses/>.
 */

package com.getsharex.xerahs.mobile.feature.upload

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.getsharex.xerahs.mobile.core.data.UploadQueueWorker
import com.getsharex.xerahs.mobile.core.domain.UploadResultItem
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.launchIn
import kotlinx.coroutines.flow.onEach
import kotlinx.coroutines.launch

class UploadViewModel(
    private val worker: UploadQueueWorker
) : ViewModel() {

    private val _statusText = MutableStateFlow("Share files to XerahS to upload them.")
    val statusText: StateFlow<String> = _statusText.asStateFlow()

    private val _isUploading = MutableStateFlow(false)
    val isUploading: StateFlow<Boolean> = _isUploading.asStateFlow()

    private val _results = MutableStateFlow<List<UploadResultItem>>(emptyList())
    val results: StateFlow<List<UploadResultItem>> = _results.asStateFlow()

    init {
        worker.state.onEach { state ->
            _isUploading.value = state.processing
            _statusText.value = when {
                state.processing -> "Uploading ${state.pendingCount} file(s)..."
                state.pendingCount > 0 -> "Queued ${state.pendingCount} file(s)."
                else -> if (_results.value.isEmpty()) "Share files to XerahS to upload them." else "Done."
            }
        }.launchIn(viewModelScope)
        worker.itemCompleted.onEach { item ->
            item?.let { result ->
                _results.value = _results.value + result
            }
        }.launchIn(viewModelScope)
        viewModelScope.launch {
            worker.updateState()
        }
    }

    fun processFiles(paths: Array<String>) {
        if (paths.isEmpty()) {
            _statusText.value = "No files received."
            return
        }
        val added = worker.enqueueFiles(paths.toList())
        if (added == 0) {
            _statusText.value = "No valid files to upload."
            return
        }
        _statusText.value = "Queued $added file(s) for upload."
    }

    fun addResult(fileName: String, success: Boolean, url: String?, error: String?) {
        _results.value = _results.value + UploadResultItem(fileName, success, url, error)
    }

    fun clearResults() {
        _results.value = emptyList()
    }
}
