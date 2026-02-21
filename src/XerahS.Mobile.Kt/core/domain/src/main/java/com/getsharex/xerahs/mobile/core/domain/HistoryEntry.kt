package com.getsharex.xerahs.mobile.core.domain

/**
 * One upload history entry. Matches C# HistoryItem / SQLite History table for compatibility.
 */
data class HistoryEntry(
    val id: Long,
    val fileName: String,
    val filePath: String,
    val dateTime: String,  // ISO or same format as C#
    val type: String,
    val host: String,
    val url: String,
    val thumbnailUrl: String = "",
    val deletionUrl: String = "",
    val shortenedUrl: String = "",
    val tags: Map<String, String?> = emptyMap()
)
