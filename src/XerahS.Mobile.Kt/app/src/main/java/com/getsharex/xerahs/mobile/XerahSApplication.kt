package com.getsharex.xerahs.mobile

import android.app.Application
import com.getsharex.xerahs.mobile.core.common.Paths
import com.getsharex.xerahs.mobile.core.data.HistoryRepository
import com.getsharex.xerahs.mobile.core.data.QueueRepository
import com.getsharex.xerahs.mobile.core.data.SettingsRepository
import com.getsharex.xerahs.mobile.core.data.UploadQueueWorker

class XerahSApplication : Application() {
    override fun onCreate() {
        super.onCreate()
        Paths.init(filesDir, cacheDir)
        Paths.ensureDirectoriesExist()
    }

    val settingsRepository: SettingsRepository by lazy { SettingsRepository() }
    val historyRepository: HistoryRepository by lazy { HistoryRepository() }
    val queueRepository: QueueRepository by lazy { QueueRepository() }
    val uploadQueueWorker: UploadQueueWorker by lazy {
        UploadQueueWorker(settingsRepository, queueRepository, historyRepository)
    }

    /** Paths from share intent (or other) to process when Upload screen is ready. Cleared after consumed. */
    val pendingSharedPaths: MutableList<Array<String>> = mutableListOf()

    /** Set by NavGraph so MainActivity can navigate to Upload when share intent arrives while app is running. */
    var navController: androidx.navigation.NavController? = null
}
