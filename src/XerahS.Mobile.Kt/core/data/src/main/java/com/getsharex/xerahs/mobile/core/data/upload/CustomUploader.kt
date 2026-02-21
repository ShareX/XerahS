package com.getsharex.xerahs.mobile.core.data.upload

import com.getsharex.xerahs.mobile.core.domain.CustomUploaderEntry
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MultipartBody
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.asRequestBody
import java.io.File
import java.util.concurrent.TimeUnit

/**
 * Upload a file via custom .sxcu-style config: POST to RequestURL with multipart or body.
 */
class CustomUploader(
    private val client: OkHttpClient = OkHttpClient.Builder()
        .connectTimeout(30, TimeUnit.SECONDS)
        .writeTimeout(60, TimeUnit.SECONDS)
        .readTimeout(60, TimeUnit.SECONDS)
        .build()
) {

    fun uploadFile(filePath: String, entry: CustomUploaderEntry): UploadOutcome {
        if (entry.requestUrl.isBlank()) return UploadOutcome.Failure("Request URL is empty")
        val file = File(filePath)
        if (!file.exists()) return UploadOutcome.Failure("File not found")
        return try {
            val request = buildRequest(entry, file)
            val response = client.newCall(request).execute()
            if (!response.isSuccessful) {
                return UploadOutcome.Failure("HTTP ${response.code}: ${response.body?.string()?.take(200) ?: ""}")
            }
            val bodyStr = response.body?.string() ?: ""
            val url = extractUrl(bodyStr, entry.urlExpression)
                ?: bodyStr.trim().take(500)
            if (url.isNotBlank()) UploadOutcome.Success(url) else UploadOutcome.Failure("No URL in response")
        } catch (e: Exception) {
            UploadOutcome.Failure(e.message ?: "Upload failed")
        }
    }

    private fun buildRequest(entry: CustomUploaderEntry, file: File): Request {
        val formName = entry.fileFormName.ifBlank { "file" }
        val multipart = MultipartBody.Builder()
            .setType(MultipartBody.FORM)
            .addFormDataPart(formName, file.name, file.asRequestBody(null))
        if (entry.body.isNotBlank()) multipart.addFormDataPart("body", entry.body)
        val body = multipart.build()
        val builder = Request.Builder().url(entry.requestUrl).post(body)
        entry.headers.forEach { (k, v) -> if (v != null) builder.addHeader(k, v) }
        return builder.build()
    }

    private fun extractUrl(responseBody: String, expression: String): String? {
        if (expression.isBlank()) return null
        return try {
            val regex = expression.toRegex()
            regex.find(responseBody)?.groupValues?.getOrNull(1) ?: regex.find(responseBody)?.value
        } catch (e: Exception) {
            null
        }
    }
}
