package com.getsharex.xerahs.mobile.core.domain

data class UploadResultItem(
    val fileName: String,
    val success: Boolean,
    val url: String?,
    val error: String?
) {
    val hasUrl: Boolean get() = !url.isNullOrBlank()
}
