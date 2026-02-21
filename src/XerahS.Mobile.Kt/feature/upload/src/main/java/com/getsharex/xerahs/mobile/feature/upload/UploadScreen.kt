package com.getsharex.xerahs.mobile.feature.upload

import androidx.compose.foundation.layout.Arrangement
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
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.getsharex.xerahs.mobile.core.data.UploadQueueWorker
import com.getsharex.xerahs.mobile.core.domain.UploadResultItem

@Composable
fun UploadScreen(
    worker: UploadQueueWorker,
    onOpenHistory: () -> Unit,
    onOpenSettings: () -> Unit,
    onPickFiles: (() -> Unit)? = null,
    onCopyToClipboard: (String) -> Unit = {},
    initialPaths: Array<String>? = null,
    viewModel: UploadViewModel = androidx.lifecycle.viewmodel.compose.viewModel(
        factory = object : androidx.lifecycle.ViewModelProvider.Factory {
            @Suppress("UNCHECKED_CAST")
            override fun <T : androidx.lifecycle.ViewModel> create(modelClass: Class<T>): T =
                UploadViewModel(worker) as T
        }
    )
) {
    if (!initialPaths.isNullOrEmpty()) {
        androidx.compose.runtime.LaunchedEffect(initialPaths) {
            viewModel.processFiles(initialPaths)
        }
    }
    val statusText by viewModel.statusText.collectAsState()
    val isUploading by viewModel.isUploading.collectAsState()
    val results by viewModel.results.collectAsState()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(20.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                text = "XerahS",
                style = MaterialTheme.typography.titleLarge
            )
            Row {
                OutlinedButton(onClick = onOpenHistory) { Text("History") }
                Spacer(modifier = Modifier.padding(4.dp))
                OutlinedButton(onClick = onOpenSettings) { Text("Settings") }
            }
        }
        Spacer(modifier = Modifier.height(16.dp))
        Column(
            modifier = Modifier
                .weight(1f)
                .verticalScroll(rememberScrollState()),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(
                text = "Share & Upload",
                style = MaterialTheme.typography.titleMedium
            )
            Spacer(modifier = Modifier.height(8.dp))
            Text(
                text = statusText,
                style = MaterialTheme.typography.bodyMedium
            )
            if (onPickFiles != null) {
                Spacer(modifier = Modifier.height(12.dp))
                Button(onClick = onPickFiles) { Text("Choose Photo or File") }
            }
            if (isUploading) {
                Spacer(modifier = Modifier.height(16.dp))
                CircularProgressIndicator()
            }
            Spacer(modifier = Modifier.height(16.dp))
            results.forEach { item ->
                ResultCard(
                    item = item,
                    onCopyUrl = if (item.hasUrl && item.url != null) ({ onCopyToClipboard(item.url) }) else null,
                    onCopyError = if (!item.success && item.error != null) ({ onCopyToClipboard(item.error) }) else null
                )
                Spacer(modifier = Modifier.height(8.dp))
            }
        }
    }
}

@Composable
private fun ResultCard(
    item: UploadResultItem,
    onCopyUrl: ((String) -> Unit)? = null,
    onCopyError: ((String) -> Unit)? = null
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors()
    ) {
        Column(modifier = Modifier.padding(12.dp)) {
            Text(
                text = item.fileName,
                style = MaterialTheme.typography.titleSmall
            )
            if (item.hasUrl && item.url != null) {
                Text(
                    text = item.url,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.primary
                )
                if (onCopyUrl != null) {
                    OutlinedButton(onClick = { onCopyUrl(item.url) }) { Text("Copy URL") }
                }
            }
            if (!item.success && item.error != null) {
                Text(
                    text = item.error,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.error
                )
                if (onCopyError != null) {
                    OutlinedButton(onClick = { onCopyError(item.error) }) { Text("Copy Error") }
                }
            }
        }
    }
}
