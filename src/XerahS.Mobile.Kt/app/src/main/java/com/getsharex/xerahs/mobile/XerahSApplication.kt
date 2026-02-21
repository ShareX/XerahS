package com.getsharex.xerahs.mobile

import android.app.Application
import com.getsharex.xerahs.mobile.core.common.Paths

class XerahSApplication : Application() {
    override fun onCreate() {
        super.onCreate()
        Paths.init(filesDir, cacheDir)
        Paths.ensureDirectoriesExist()
    }
}
