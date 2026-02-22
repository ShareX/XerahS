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
