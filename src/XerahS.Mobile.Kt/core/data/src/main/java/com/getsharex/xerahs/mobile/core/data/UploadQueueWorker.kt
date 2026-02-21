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

package com.getsharex.xerahs.mobile.core.data

import com.getsharex.xerahs.mobile.core.data.upload.CustomUploader
import com.getsharex.xerahs.mobile.core.data.upload.S3Uploader
import com.getsharex.xerahs.mobile.core.data.upload.UploadOutcome
import com.getsharex.xerahs.mobile.core.domain.UploadResultItem
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.io.File

/**
 * Processes the persistent upload queue: dequeue one item, resolve destination (S3 or custom),
 * upload, append to history, emit result. Runs on IO dispatcher.
 */
class UploadQueueWorker(
    private val settingsRepository: SettingsRepository,
    private val queueRepository: QueueRepository,
    private val historyRepository: HistoryRepository,
    private val s3Uploader: S3Uploader = S3Uploader(),
    private val customUploader: CustomUploader = CustomUploader()
) {
    private val scope = CoroutineScope(Dispatchers.IO + Job())
    private val _state = MutableStateFlow(UploadQueueState(processing = false, pendingCount = 0))
    val state: StateFlow<UploadQueueState> = _state.asStateFlow()

    private val _itemCompleted = MutableStateFlow<UploadResultItem?>(null)
    val itemCompleted: StateFlow<UploadResultItem?> = _itemCompleted.asStateFlow()

    private var processingJob: Job? = null

    fun startProcessing() {
        if (processingJob?.isActive == true) return
        processingJob = scope.launch {
            while (isActive) {
                updateState()
                val item = queueRepository.dequeue() ?: break
                val fileName = File(item.filePath).name
                val result = uploadOne(item.filePath)
                val resultUrl = result.url
                if (result.success && resultUrl != null) {
                    historyRepository.insertEntry(
                        fileName = fileName,
                        filePath = item.filePath,
                        type = "File",
                        host = "upload",
                        url = resultUrl
                    )
                }
                _itemCompleted.value = result
                _itemCompleted.value = null
            }
            updateState()
        }
    }

    fun updateState() {
        _state.value = UploadQueueState(
            processing = queueRepository.pendingCount() > 0,
            pendingCount = queueRepository.pendingCount()
        )
    }

    fun enqueueFiles(filePaths: List<String>): Int {
        val valid = filePaths.filter { path -> File(path).exists() }
        val added = queueRepository.enqueue(valid)
        if (added > 0) {
            updateState()
            startProcessing()
        }
        return added
    }

    private fun uploadOne(filePath: String): UploadResultItem {
        val fileName = File(filePath).name
        if (!File(filePath).exists()) return UploadResultItem(fileName, false, null, "File not found")
        val config = settingsRepository.load()
        val destId = config.defaultDestinationInstanceId

        when {
            config.s3Config.isConfigured && (destId == null || destId == "amazons3" || destId.startsWith("amazons3")) -> {
                val outcome = s3Uploader.uploadFile(filePath, config.s3Config)
                return when (outcome) {
                    is UploadOutcome.Success -> UploadResultItem(fileName, true, outcome.url, null)
                    is UploadOutcome.Failure -> UploadResultItem(fileName, false, null, outcome.error)
                }
            }
            config.customUploaders.isNotEmpty() -> {
                val entry = config.customUploaders.find { it.id == destId } ?: config.customUploaders.first()
                val outcome = customUploader.uploadFile(filePath, entry)
                return when (outcome) {
                    is UploadOutcome.Success -> UploadResultItem(fileName, true, outcome.url, null)
                    is UploadOutcome.Failure -> UploadResultItem(fileName, false, null, outcome.error)
                }
            }
            else -> return UploadResultItem(fileName, false, null, "No upload destination configured. Configure S3 or a custom uploader in Settings.")
        }
    }
}

data class UploadQueueState(val processing: Boolean, val pendingCount: Int)
