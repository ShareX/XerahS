package com.getsharex.xerahs.mobile.core.domain

/**
 * One item in the persistent upload queue. Matches C# UploadQueueItem for JSON compatibility.
 */
data class UploadQueueItem(
    val filePath: String,
    val enqueuedUtc: String  // ISO 8601
)
