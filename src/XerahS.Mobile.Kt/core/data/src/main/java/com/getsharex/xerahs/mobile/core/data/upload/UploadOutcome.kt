package com.getsharex.xerahs.mobile.core.data.upload

sealed class UploadOutcome {
    data class Success(val url: String) : UploadOutcome()
    data class Failure(val error: String) : UploadOutcome()
}
