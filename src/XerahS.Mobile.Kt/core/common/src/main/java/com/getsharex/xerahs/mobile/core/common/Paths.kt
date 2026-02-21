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
