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

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Checkbox
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.ui.Alignment
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardOptions
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.getsharex.xerahs.mobile.core.data.SettingsRepository
import androidx.lifecycle.viewmodel.compose.viewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun S3ConfigScreen(
    settingsRepository: SettingsRepository?,
    onBack: () -> Unit
) {
    if (settingsRepository == null) {
        Button(onClick = onBack) { Text("Back") }
        return
    }
    val viewModel: S3ConfigViewModel = viewModel(
        factory = object : androidx.lifecycle.ViewModelProvider.Factory {
            @Suppress("UNCHECKED_CAST")
            override fun <T : androidx.lifecycle.ViewModel> create(modelClass: Class<T>): T =
                S3ConfigViewModel(settingsRepository) as T
        }
    )
    val accessKey by viewModel.accessKeyId.collectAsState()
    val secretKey by viewModel.secretAccessKey.collectAsState()
    val bucket by viewModel.bucketName.collectAsState()
    val regionIndex by viewModel.regionIndex.collectAsState()
    val customEndpoint by viewModel.customEndpoint.collectAsState()
    val useCustomDomain by viewModel.useCustomDomain.collectAsState()
    val customDomain by viewModel.customDomain.collectAsState()
    val signedPayload by viewModel.signedPayload.collectAsState()
    val setPublicAcl by viewModel.setPublicAcl.collectAsState()
    val validationError by viewModel.validationError.collectAsState()

    var regionExpanded by remember { mutableStateOf(false) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
            .verticalScroll(rememberScrollState())
    ) {
        Button(onClick = onBack) { Text("Back") }
        Spacer(modifier = Modifier.height(16.dp))
        Text(
            text = "Amazon S3",
            style = MaterialTheme.typography.titleLarge
        )
        if (validationError != null) {
            Text(
                text = validationError!!,
                color = MaterialTheme.colorScheme.error,
                style = MaterialTheme.typography.bodySmall
            )
            Spacer(modifier = Modifier.height(8.dp))
        }
        Spacer(modifier = Modifier.height(8.dp))
        OutlinedTextField(
            value = accessKey,
            onValueChange = { viewModel.setAccessKeyId(it); viewModel.clearValidationError() },
            modifier = Modifier.fillMaxWidth(),
            label = { Text("Access Key ID") },
            singleLine = true
        )
        Spacer(modifier = Modifier.height(8.dp))
        OutlinedTextField(
            value = secretKey,
            onValueChange = { viewModel.setSecretAccessKey(it); viewModel.clearValidationError() },
            modifier = Modifier.fillMaxWidth(),
            label = { Text("Secret Access Key") },
            singleLine = true
        )
        Spacer(modifier = Modifier.height(8.dp))
        OutlinedTextField(
            value = bucket,
            onValueChange = { viewModel.setBucketName(it); viewModel.clearValidationError() },
            modifier = Modifier.fillMaxWidth(),
            label = { Text("Bucket Name") },
            singleLine = true,
            keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.None)
        )
        Spacer(modifier = Modifier.height(8.dp))
        ExposedDropdownMenuBox(
            expanded = regionExpanded,
            onExpandedChange = { regionExpanded = it }
        ) {
            OutlinedTextField(
                value = S3ConfigViewModel.REGIONS.getOrNull(regionIndex)?.displayName ?: "",
                onValueChange = {},
                readOnly = true,
                modifier = Modifier
                    .fillMaxWidth()
                    .menuAnchor(),
                label = { Text("Region") },
                trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = regionExpanded) }
            )
            DropdownMenu(
                expanded = regionExpanded,
                onDismissRequest = { regionExpanded = false }
            ) {
                S3ConfigViewModel.REGIONS.forEachIndexed { index, option ->
                    DropdownMenuItem(
                        text = { Text(option.displayName) },
                        onClick = {
                            viewModel.setRegionIndex(index)
                            regionExpanded = false
                        }
                    )
                }
            }
        }
        Spacer(modifier = Modifier.height(8.dp))
        OutlinedTextField(
            value = customEndpoint,
            onValueChange = { viewModel.setCustomEndpoint(it) },
            modifier = Modifier.fillMaxWidth(),
            label = { Text("Custom Endpoint (optional)") },
            supportingText = { Text("Override S3 API endpoint for MinIO or other S3-compatible storage. Leave blank for AWS.") },
            singleLine = true
        )
        Spacer(modifier = Modifier.height(12.dp))
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Checkbox(
                checked = useCustomDomain,
                onCheckedChange = { viewModel.setUseCustomDomain(it) }
            )
            Text("Use Custom Domain (CDN)")
        }
        if (useCustomDomain) {
            Spacer(modifier = Modifier.height(4.dp))
            OutlinedTextField(
                value = customDomain,
                onValueChange = { viewModel.setCustomDomain(it) },
                modifier = Modifier.fillMaxWidth(),
                label = { Text("Custom domain URL") },
                placeholder = { Text("https://cdn.example.com") },
                singleLine = true,
                keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.None)
            )
        }
        Spacer(modifier = Modifier.height(8.dp))
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Checkbox(
                checked = signedPayload,
                onCheckedChange = { viewModel.setSignedPayload(it) }
            )
            Text("Signed payload")
        }
        Text(
            text = "Recommended: sign request body (avoids 403 when bucket blocks public ACLs)",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Spacer(modifier = Modifier.height(4.dp))
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Checkbox(
                checked = setPublicAcl,
                onCheckedChange = { viewModel.setSetPublicAcl(it) }
            )
            Text("Make uploads public (public-read ACL)")
        }
        Spacer(modifier = Modifier.height(24.dp))
        Button(
            onClick = {
                if (viewModel.save()) onBack()
            }
        ) {
            Text("Save")
        }
    }
}
