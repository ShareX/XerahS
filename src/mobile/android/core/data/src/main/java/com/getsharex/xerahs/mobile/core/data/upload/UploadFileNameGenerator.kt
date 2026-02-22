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

package com.getsharex.xerahs.mobile.core.data.upload

import java.io.File
import java.util.Calendar
import kotlin.random.Random

/**
 * Generates upload file names using the same default pattern as desktop: %y%mo%dT%h%mi_%ra{10}
 * (year, zero-padded month/day/hour/minute, literal T, underscore, 10 random alphanumeric).
 * Matches [TaskSettings.NameFormatPattern] default in src/desktop.
 */
object UploadFileNameGenerator {

    private const val ALPHANUMERIC = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"

    /**
     * Returns a file name for upload: pattern base + original extension from file path.
     */
    @JvmStatic
    fun uploadFileName(filePath: String): String {
        val ext = File(filePath).extension
        val base = defaultPatternBase()
        return if (ext.isBlank()) base else "$base.$ext"
    }

    /**
     * Pattern base only: yyyyMMddTHHmm_ + 10 random alphanumeric (no extension).
     */
    private fun defaultPatternBase(): String {
        val cal = Calendar.getInstance()
        val y = cal.get(Calendar.YEAR)
        val mo = cal.get(Calendar.MONTH) + 1
        val d = cal.get(Calendar.DAY_OF_MONTH)
        val h = cal.get(Calendar.HOUR_OF_DAY)
        val mi = cal.get(Calendar.MINUTE)
        val timePart = "%04d%02d%02dT%02d%02d".format(y, mo, d, h, mi)
        val randomPart = (0..9).map { ALPHANUMERIC[Random.nextInt(ALPHANUMERIC.length)] }.joinToString("")
        return "${timePart}_$randomPart"
    }
}
