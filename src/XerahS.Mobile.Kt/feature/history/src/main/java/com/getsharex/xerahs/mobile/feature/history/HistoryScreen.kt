package com.getsharex.xerahs.mobile.feature.history

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
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.getsharex.xerahs.mobile.core.data.HistoryRepository
import com.getsharex.xerahs.mobile.core.domain.HistoryEntry
import androidx.lifecycle.viewmodel.compose.viewModel

@Composable
fun HistoryScreen(
    historyRepository: HistoryRepository,
    onBack: () -> Unit,
    onCopyToClipboard: (String) -> Unit = {},
    viewModel: HistoryViewModel = viewModel(
        factory = object : androidx.lifecycle.ViewModelProvider.Factory {
            @Suppress("UNCHECKED_CAST")
            override fun <T : androidx.lifecycle.ViewModel> create(modelClass: Class<T>): T =
                HistoryViewModel(historyRepository) as T
        }
    )
) {
    val entries by viewModel.filteredEntries.collectAsState()
    val searchQuery by viewModel.searchQuery.collectAsState()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Button(onClick = onBack) { Text("Back") }
            Row {
                OutlinedButton(onClick = { viewModel.refresh() }) { Text("Refresh") }
                Spacer(modifier = Modifier.padding(4.dp))
                OutlinedButton(onClick = { viewModel.clearAll() }) { Text("Clear") }
            }
        }
        Spacer(modifier = Modifier.height(8.dp))
        OutlinedTextField(
            value = searchQuery,
            onValueChange = { viewModel.setSearchQuery(it) },
            modifier = Modifier.fillMaxWidth(),
            placeholder = { Text("Search") },
            singleLine = true
        )
        Spacer(modifier = Modifier.height(16.dp))
        Text(
            text = "History",
            style = androidx.compose.material3.MaterialTheme.typography.titleLarge
        )
        Spacer(modifier = Modifier.height(8.dp))
        LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            items(entries, key = { it.id }) { entry ->
                HistoryEntryCard(
                    entry = entry,
                    onCopyUrl = { onCopyToClipboard(entry.url) },
                    onDelete = { viewModel.deleteEntry(entry.id) }
                )
            }
        }
    }
}

@Composable
private fun HistoryEntryCard(
    entry: HistoryEntry,
    onCopyUrl: () -> Unit,
    onDelete: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors()
    ) {
        Column(modifier = Modifier.padding(12.dp)) {
            Text(
                text = entry.fileName,
                style = androidx.compose.material3.MaterialTheme.typography.titleSmall
            )
            if (entry.url.isNotBlank()) {
                Text(
                    text = entry.url,
                    style = androidx.compose.material3.MaterialTheme.typography.bodySmall,
                    color = androidx.compose.material3.MaterialTheme.colorScheme.primary
                )
                OutlinedButton(onClick = onCopyUrl) { Text("Copy URL") }
            }
            OutlinedButton(onClick = onDelete) { Text("Delete") }
        }
    }
}
