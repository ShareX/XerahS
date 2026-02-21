package com.getsharex.xerahs.mobile.core.data

import com.getsharex.xerahs.mobile.core.common.Paths
import com.getsharex.xerahs.mobile.core.domain.ApplicationConfig
import com.getsharex.xerahs.mobile.core.domain.CustomUploaderEntry
import com.getsharex.xerahs.mobile.core.domain.S3Config
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import java.io.File

private const val CONFIG_FILE_NAME = "ApplicationConfig.json"

/**
 * Load/save [ApplicationConfig] as JSON in settings folder. Thread-safe via synchronized on file.
 */
class SettingsRepository(
    private val gson: Gson = Gson()
) {
    private val configFile: File?
        get() = Paths.settingsFolder?.let { File(it, CONFIG_FILE_NAME) }

    fun load(): ApplicationConfig {
        val file = configFile ?: return ApplicationConfig()
        if (!file.exists()) return ApplicationConfig()
        return try {
            file.readText().let { gson.fromJson(it, ApplicationConfig::class.java) } ?: ApplicationConfig()
        } catch (e: Exception) {
            ApplicationConfig()
        }
    }

    fun save(config: ApplicationConfig) {
        val file = configFile ?: return
        Paths.settingsFolder?.mkdirs()
        file.writeText(gson.toJson(config))
    }

    fun loadS3Config(): S3Config = load().s3Config
    fun saveS3Config(config: S3Config) {
        val c = load()
        save(c.copy(s3Config = config))
    }

    fun loadCustomUploaders(): List<CustomUploaderEntry> = load().customUploaders
    fun saveCustomUploaders(list: List<CustomUploaderEntry>) {
        val c = load()
        save(c.copy(customUploaders = list))
    }

    fun getDefaultDestinationInstanceId(): String? = load().defaultDestinationInstanceId
    fun setDefaultDestinationInstanceId(id: String?) {
        val c = load()
        save(c.copy(defaultDestinationInstanceId = id))
    }
}
