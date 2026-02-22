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

import com.getsharex.xerahs.mobile.core.common.Paths
import com.getsharex.xerahs.mobile.core.domain.ApplicationConfig
import com.getsharex.xerahs.mobile.core.domain.CustomUploaderEntry
import com.getsharex.xerahs.mobile.core.domain.S3Config
import com.google.gson.Gson
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

    fun getConvertHeicToPng(): Boolean = load().convertHeicToPng
    fun setConvertHeicToPng(value: Boolean) {
        val c = load()
        save(c.copy(convertHeicToPng = value))
    }
}
