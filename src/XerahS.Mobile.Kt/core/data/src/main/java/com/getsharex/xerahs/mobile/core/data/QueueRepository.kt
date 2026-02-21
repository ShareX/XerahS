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
