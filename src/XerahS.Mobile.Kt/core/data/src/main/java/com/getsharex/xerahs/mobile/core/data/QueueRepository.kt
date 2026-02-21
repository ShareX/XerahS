package com.getsharex.xerahs.mobile.core.data

import com.getsharex.xerahs.mobile.core.common.Paths
import com.getsharex.xerahs.mobile.core.domain.UploadQueueItem
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import java.io.File
import java.time.Instant

private const val QUEUE_FILE_NAME = "MobileUploadQueue.json"

/**
 * Persistent upload queue as JSON (matches C# UploadQueueService snapshot format).
 * Thread-safe via synchronized on this.
 */
class QueueRepository(
    private val gson: Gson = Gson()
) {
    private val queueFile: File?
        get() = Paths.settingsFolder?.let { File(it, QUEUE_FILE_NAME) }

    private val type = object : TypeToken<List<UploadQueueItem>>() {}.type

    @Synchronized
    fun enqueue(filePaths: List<String>): Int {
        val items = loadSnapshot().toMutableList()
        val now = Instant.now().toString()
        var added = 0
        for (path in filePaths) {
            if (path.isBlank()) continue
            items.add(UploadQueueItem(filePath = path, enqueuedUtc = now))
            added++
        }
        if (added > 0) saveSnapshot(items)
        return added
    }

    @Synchronized
    fun peek(): UploadQueueItem? = loadSnapshot().firstOrNull()

    @Synchronized
    fun dequeue(): UploadQueueItem? {
        val items = loadSnapshot().toMutableList()
        val head = items.removeFirstOrNull() ?: return null
        saveSnapshot(items)
        return head
    }

    @Synchronized
    fun snapshot(): List<UploadQueueItem> = loadSnapshot()

    @Synchronized
    fun pendingCount(): Int = loadSnapshot().size

    private fun loadSnapshot(): List<UploadQueueItem> {
        val file = queueFile ?: return emptyList()
        if (!file.exists()) return emptyList()
        return try {
            gson.fromJson<List<UploadQueueItem>>(file.readText(), type) ?: emptyList()
        } catch (e: Exception) {
            emptyList()
        }
    }

    private fun saveSnapshot(items: List<UploadQueueItem>) {
        val file = queueFile ?: return
        Paths.settingsFolder?.mkdirs()
        if (items.isEmpty()) {
            if (file.exists()) file.delete()
            return
        }
        file.writeText(gson.toJson(items))
    }
}
