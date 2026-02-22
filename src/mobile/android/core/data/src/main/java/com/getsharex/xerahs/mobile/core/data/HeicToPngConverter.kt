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

import android.graphics.Bitmap
import android.graphics.ImageDecoder
import android.os.Build
import com.getsharex.xerahs.mobile.core.common.Paths
import java.io.File

private val HEIC_EXTENSIONS = setOf("heic", "heif")

/**
 * If [convertEnabled] is true and [filePath] is a HEIC/HEIF image, decodes it and writes a PNG to cache,
 * returning the path to the PNG. Otherwise returns [filePath].
 * On API < 28, HEIC decoding is not supported; returns [filePath] unchanged.
 */
fun convertHeicToPngIfNeeded(filePath: String, convertEnabled: Boolean): String {
    if (!convertEnabled) return filePath
    val file = File(filePath)
    if (!file.exists()) return filePath
    val ext = file.extension.lowercase()
    if (ext !in HEIC_EXTENSIONS) return filePath
    if (Build.VERSION.SDK_INT < Build.VERSION_CODES.P) return filePath // ImageDecoder added in API 28
    val cacheDir = Paths.cacheDir ?: return filePath
    return try {
        val source = ImageDecoder.createSource(file)
        val bitmap = ImageDecoder.decodeBitmap(source)
        val outFile = File(cacheDir, file.nameWithoutExtension + "_converted.png")
        outFile.outputStream().use { out ->
            bitmap.compress(Bitmap.CompressFormat.PNG, 100, out)
        }
        outFile.absolutePath
    } catch (e: Exception) {
        filePath
    }
}
