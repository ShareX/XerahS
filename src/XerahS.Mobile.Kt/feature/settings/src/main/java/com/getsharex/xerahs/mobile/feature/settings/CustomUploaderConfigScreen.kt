package com.getsharex.xerahs.mobile.feature.settings

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.getsharex.xerahs.mobile.core.data.SettingsRepository
import com.getsharex.xerahs.mobile.core.domain.CustomUploaderEntry
import androidx.lifecycle.viewmodel.compose.viewModel

@Composable
fun CustomUploaderConfigScreen(
    settingsRepository: SettingsRepository?,
    onBack: () -> Unit
) {
    if (settingsRepository == null) {
        Button(onClick = onBack) { Text("Back") }
        return
    }
    val viewModel: CustomUploaderConfigViewModel = viewModel(
        factory = object : androidx.lifecycle.ViewModelProvider.Factory {
            @Suppress("UNCHECKED_CAST")
            override fun <T : androidx.lifecycle.ViewModel> create(modelClass: Class<T>): T =
                CustomUploaderConfigViewModel(settingsRepository) as T
        }
    )
    val uploaders by viewModel.uploaders.collectAsState()
    val editing by viewModel.editingEntry.collectAsState()

    Column(modifier = Modifier.fillMaxSize().padding(16.dp)) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Button(onClick = onBack) { Text("Back") }
        }
        Spacer(modifier = Modifier.height(16.dp))
        Text(
            text = "Custom Uploader",
            style = MaterialTheme.typography.titleLarge
        )
        Spacer(modifier = Modifier.height(8.dp))
        LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            items(uploaders, key = { it.id }) { entry ->
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors()
                ) {
                    Row(
                        modifier = Modifier.padding(12.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Column(modifier = Modifier.weight(1f)) {
                            Text(
                                text = entry.name.ifBlank { "Unnamed" },
                                style = MaterialTheme.typography.titleSmall
                            )
                            Text(
                                text = entry.requestUrl.ifBlank { "No URL" },
                                style = MaterialTheme.typography.bodySmall
                            )
                        }
                        OutlinedButton(onClick = { viewModel.edit(entry) }) { Text("Edit") }
                        Spacer(modifier = Modifier.padding(4.dp))
                        OutlinedButton(onClick = { viewModel.delete(entry) }) { Text("Delete") }
                    }
                }
            }
        }
        Spacer(modifier = Modifier.height(8.dp))
        FloatingActionButton(
            onClick = { viewModel.addNew() }
        ) {
            Text("Add")
        }
    }

    editing?.let { entry ->
        CustomUploaderEditDialog(
            entry = entry,
            onDismiss = { viewModel.cancelEdit() },
            onSave = { updated -> viewModel.saveEdit(updated) }
        )
    }
}

@Composable
private fun CustomUploaderEditDialog(
    entry: CustomUploaderEntry,
    onDismiss: () -> Unit,
    onSave: (CustomUploaderEntry) -> Unit
) {
    var name by androidx.compose.runtime.mutableStateOf(entry.name)
    var requestUrl by androidx.compose.runtime.mutableStateOf(entry.requestUrl)
    var fileFormName by androidx.compose.runtime.mutableStateOf(entry.fileFormName)
    var body by androidx.compose.runtime.mutableStateOf(entry.body)

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(if (entry.id.isNotEmpty()) "Edit Uploader" else "New Uploader") },
        text = {
            Column {
                OutlinedTextField(
                    value = name,
                    onValueChange = { name = it },
                    label = { Text("Name") },
                    modifier = Modifier.fillMaxWidth()
                )
                Spacer(modifier = Modifier.height(8.dp))
                OutlinedTextField(
                    value = requestUrl,
                    onValueChange = { requestUrl = it },
                    label = { Text("Request URL") },
                    modifier = Modifier.fillMaxWidth()
                )
                Spacer(modifier = Modifier.height(8.dp))
                OutlinedTextField(
                    value = fileFormName,
                    onValueChange = { fileFormName = it },
                    label = { Text("File form name") },
                    modifier = Modifier.fillMaxWidth()
                )
                Spacer(modifier = Modifier.height(8.dp))
                OutlinedTextField(
                    value = body,
                    onValueChange = { body = it },
                    label = { Text("Body (optional)") },
                    modifier = Modifier.fillMaxWidth()
                )
            }
        },
        confirmButton = {
            Button(
                onClick = {
                    onSave(
                        entry.copy(
                            name = name.trim(),
                            requestUrl = requestUrl.trim(),
                            fileFormName = fileFormName.ifBlank { "file" },
                            body = body.trim()
                        )
                    )
                }
            ) {
                Text("Save")
            }
        },
        dismissButton = {
            OutlinedButton(onClick = onDismiss) { Text("Cancel") }
        }
    )
}
