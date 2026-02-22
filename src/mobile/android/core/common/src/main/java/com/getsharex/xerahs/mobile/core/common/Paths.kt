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

package com.getsharex.xerahs.mobile.core.common

import java.io.File

/**
 * Paths for settings, history DB, and cache. Call [init] from Application with app dirs, then [ensureDirectoriesExist].
 * Matches C# PathsManager: PersonalFolder = base, Settings = PersonalFolder/Settings, History = PersonalFolder/History/History.db.
 */
object Paths {
    private const val SETTINGS_FOLDER_NAME = "Settings"
    private const val HISTORY_FOLDER_NAME = "History"
    private const val HISTORY_FILE_NAME = "History.db"

    var settingsFolder: File? = null
        private set
    var historyFolder: File? = null
        private set
    var historyFilePath: String? = null
        private set
    var cacheDir: File? = null
        private set

    /**
     * Call from Application.onCreate. personalFolder = context.filesDir, cacheDirPath = context.cacheDir.
     */
    fun init(personalFolder: File, cacheDirPath: File) {
        settingsFolder = File(personalFolder, SETTINGS_FOLDER_NAME)
        historyFolder = File(personalFolder, HISTORY_FOLDER_NAME)
        historyFilePath = File(historyFolder!!, HISTORY_FILE_NAME).absolutePath
        cacheDir = cacheDirPath
    }

    fun ensureDirectoriesExist() {
        settingsFolder?.mkdirs()
        historyFolder?.mkdirs()
        cacheDir?.mkdirs()
    }
}
