package com.getsharex.xerahs.mobile

import android.content.Intent
import android.net.Uri
import android.provider.OpenableColumns
import java.io.File
import java.util.UUID

object ShareIntentHandler {

    fun handleIntent(activity: MainActivity, intent: Intent?): Array<String>? {
        if (intent == null) return null
        val action = intent.action
        if (action != Intent.ACTION_SEND && action != Intent.ACTION_SEND_MULTIPLE) return null
        if (intent.type.isNullOrEmpty()) return null

        val app = activity.application as? XerahSApplication ?: return null
        val cacheDir = app.cacheDir ?: return null
        val localPaths = mutableListOf<String>()

        when (action) {
            Intent.ACTION_SEND -> {
                @Suppress("DEPRECATION")
                val uri = intent.getParcelableExtra<Uri>(Intent.EXTRA_STREAM)
                if (uri != null) {
                    copyUriToCache(activity, uri, cacheDir)?.let { localPaths.add(it) }
                }
            }
            Intent.ACTION_SEND_MULTIPLE -> {
                val uris = intent.getParcelableArrayListExtra<Uri>(Intent.EXTRA_STREAM)
                uris?.forEach { uri ->
                    copyUriToCache(activity, uri, cacheDir)?.let { localPaths.add(it) }
                }
            }
        }

        return if (localPaths.isEmpty()) null else localPaths.toTypedArray()
    }

    private fun copyUriToCache(activity: MainActivity, uri: Uri, cacheDir: File): String? {
        return try {
            val fileName = getFileNameFromUri(activity, uri) ?: "share_${UUID.randomUUID().toString().take(8)}"
            val cachePath = File(cacheDir, fileName)
            activity.contentResolver.openInputStream(uri)?.use { input ->
                cachePath.outputStream().use { output ->
                    input.copyTo(output)
                }
            }
            cachePath.absolutePath
        } catch (e: Exception) {
            null
        }
    }

    @Suppress("DEPRECATION")
    private fun getFileNameFromUri(activity: MainActivity, uri: Uri): String? {
        if (uri.scheme != "content") return uri.lastPathSegment?.substringAfterLast('/')
        activity.contentResolver.query(uri, null, null, null, null)?.use { cursor ->
            if (cursor.moveToFirst()) {
                val nameIndex = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME)
                if (nameIndex >= 0) return cursor.getString(nameIndex)
            }
        }
        return uri.lastPathSegment?.substringAfterLast('/')
    }
}
