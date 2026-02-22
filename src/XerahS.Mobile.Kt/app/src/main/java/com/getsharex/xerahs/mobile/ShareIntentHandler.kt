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

package com.getsharex.xerahs.mobile

import android.content.Intent
import android.net.Uri
import android.provider.OpenableColumns
import android.util.Log
import java.io.File
import java.util.UUID

private const val TAG = "ShareIntentHandler"

object ShareIntentHandler {

    fun handleIntent(activity: MainActivity, intent: Intent?): Array<String>? {
        if (intent == null) return null
        val action = intent.action
        if (action != Intent.ACTION_SEND && action != Intent.ACTION_SEND_MULTIPLE) return null
        // Allow type to be null; many share senders (e.g. Photos) set data in ClipData only
        val type = intent.type
        if (type.isNullOrEmpty() && intent.clipData == null) return null

        val app = activity.application as? XerahSApplication ?: return null
        val cacheDir = app.cacheDir ?: return null
        val localPaths = mutableListOf<String>()

        when (action) {
            Intent.ACTION_SEND -> {
                @Suppress("DEPRECATION")
                var uri: Uri? = intent.getParcelableExtra(Intent.EXTRA_STREAM)
                if (uri == null && intent.clipData != null && intent.clipData!!.itemCount > 0) {
                    uri = intent.clipData!!.getItemAt(0).uri
                }
                if (uri != null) {
                    copyUriToCache(activity, uri, cacheDir)?.let { localPaths.add(it) }
                        ?: Log.w(TAG, "copyUriToCache failed for $uri")
                } else {
                    Log.w(TAG, "ACTION_SEND: no URI in EXTRA_STREAM or clipData")
                }
            }
            Intent.ACTION_SEND_MULTIPLE -> {
                @Suppress("DEPRECATION")
                var uris = intent.getParcelableArrayListExtra<Uri>(Intent.EXTRA_STREAM)
                if (uris.isNullOrEmpty() && intent.clipData != null) {
                    uris = ArrayList((0 until intent.clipData!!.itemCount).map { intent.clipData!!.getItemAt(it).uri })
                }
                uris?.forEach { uri ->
                    copyUriToCache(activity, uri, cacheDir)?.let { localPaths.add(it) }
                        ?: Log.w(TAG, "copyUriToCache failed for $uri")
                }
                if (uris.isNullOrEmpty()) Log.w(TAG, "ACTION_SEND_MULTIPLE: no URIs in EXTRA_STREAM or clipData")
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
