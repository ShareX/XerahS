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

package com.getsharex.xerahs.mobile.feature.settings

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.getsharex.xerahs.mobile.core.data.SettingsRepository
import com.getsharex.xerahs.mobile.core.domain.S3Config
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

data class S3RegionOption(val displayName: String, val regionId: String)

class S3ConfigViewModel(
    private val settingsRepository: SettingsRepository
) : ViewModel() {

    companion object {
        val REGIONS: List<S3RegionOption> = listOf(
            S3RegionOption("US East (N. Virginia)", "us-east-1"),
            S3RegionOption("US East (Ohio)", "us-east-2"),
            S3RegionOption("US West (N. California)", "us-west-1"),
            S3RegionOption("US West (Oregon)", "us-west-2"),
            S3RegionOption("Asia Pacific (Mumbai)", "ap-south-1"),
            S3RegionOption("Asia Pacific (Singapore)", "ap-southeast-1"),
            S3RegionOption("Asia Pacific (Sydney)", "ap-southeast-2"),
            S3RegionOption("Asia Pacific (Tokyo)", "ap-northeast-1"),
            S3RegionOption("Europe (Frankfurt)", "eu-central-1"),
            S3RegionOption("Europe (Ireland)", "eu-west-1"),
            S3RegionOption("Europe (London)", "eu-west-2"),
            S3RegionOption("Canada (Central)", "ca-central-1")
        )
    }

    private val _accessKeyId = MutableStateFlow("")
    val accessKeyId: StateFlow<String> = _accessKeyId.asStateFlow()
    private val _secretAccessKey = MutableStateFlow("")
    val secretAccessKey: StateFlow<String> = _secretAccessKey.asStateFlow()
    private val _bucketName = MutableStateFlow("")
    val bucketName: StateFlow<String> = _bucketName.asStateFlow()
    private val _regionIndex = MutableStateFlow(0)
    val regionIndex: StateFlow<Int> = _regionIndex.asStateFlow()
    private val _customEndpoint = MutableStateFlow("")
    val customEndpoint: StateFlow<String> = _customEndpoint.asStateFlow()
    private val _useCustomDomain = MutableStateFlow(false)
    val useCustomDomain: StateFlow<Boolean> = _useCustomDomain.asStateFlow()
    private val _customDomain = MutableStateFlow("")
    val customDomain: StateFlow<String> = _customDomain.asStateFlow()
    private val _signedPayload = MutableStateFlow(true)
    val signedPayload: StateFlow<Boolean> = _signedPayload.asStateFlow()
    private val _setPublicAcl = MutableStateFlow(false)
    val setPublicAcl: StateFlow<Boolean> = _setPublicAcl.asStateFlow()
    private val _saveSuccess = MutableStateFlow(false)
    val saveSuccess: StateFlow<Boolean> = _saveSuccess.asStateFlow()
    private val _validationError = MutableStateFlow<String?>(null)
    val validationError: StateFlow<String?> = _validationError.asStateFlow()

    init {
        viewModelScope.launch {
            val config = settingsRepository.loadS3Config()
            _accessKeyId.value = config.accessKeyId
            _secretAccessKey.value = config.secretAccessKey
            _bucketName.value = config.bucketName
            _customEndpoint.value = config.customEndpoint
            _useCustomDomain.value = config.useCustomDomain
            _customDomain.value = config.customDomain
            _signedPayload.value = config.signedPayload
            _setPublicAcl.value = config.setPublicAcl
            val idx = REGIONS.indexOfFirst { it.regionId == config.region }
            _regionIndex.value = if (idx >= 0) idx else 0
        }
    }

    fun setAccessKeyId(value: String) { _accessKeyId.value = value }
    fun setSecretAccessKey(value: String) { _secretAccessKey.value = value }
    fun setBucketName(value: String) { _bucketName.value = value }
    fun setRegionIndex(index: Int) { _regionIndex.value = index.coerceIn(0, REGIONS.lastIndex) }
    fun setCustomEndpoint(value: String) { _customEndpoint.value = value }
    fun setUseCustomDomain(value: Boolean) { _useCustomDomain.value = value }
    fun setCustomDomain(value: String) { _customDomain.value = value }
    fun setSignedPayload(value: Boolean) { _signedPayload.value = value }
    fun setSetPublicAcl(value: Boolean) { _setPublicAcl.value = value }

    fun save(): Boolean {
        val accessKey = _accessKeyId.value.trim()
        val secret = _secretAccessKey.value.trim()
        val bucket = _bucketName.value.trim()
        val region = REGIONS.getOrNull(_regionIndex.value)?.regionId ?: ""
        when {
            accessKey.isBlank() -> { _validationError.value = "Access Key is required"; return false }
            secret.isBlank() -> { _validationError.value = "Secret Key is required"; return false }
            bucket.isBlank() -> { _validationError.value = "Bucket name is required"; return false }
            region.isBlank() -> { _validationError.value = "Region is required"; return false }
        }
        _validationError.value = null
        val config = S3Config(
            accessKeyId = accessKey,
            secretAccessKey = secret,
            bucketName = bucket,
            region = region,
            customEndpoint = _customEndpoint.value.trim(),
            useCustomDomain = _useCustomDomain.value,
            customDomain = _customDomain.value.trim(),
            signedPayload = _signedPayload.value,
            setPublicAcl = _setPublicAcl.value
        )
        settingsRepository.saveS3Config(config)
        _saveSuccess.value = true
        return true
    }

    fun clearValidationError() { _validationError.value = null }
    fun clearSaveSuccess() { _saveSuccess.value = false }
}
