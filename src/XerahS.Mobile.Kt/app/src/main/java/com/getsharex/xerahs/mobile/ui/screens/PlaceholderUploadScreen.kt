package com.getsharex.xerahs.mobile.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp

@Composable
fun PlaceholderUploadScreen(
    onOpenHistory: () -> Unit,
    onOpenSettings: () -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(20.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Text(
                text = "XerahS",
                style = MaterialTheme.typography.titleLarge
            )
            Row {
                Button(onClick = onOpenHistory) { Text("History") }
                Button(onClick = onOpenSettings) { Text("Settings") }
            }
        }
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(vertical = 24.dp),
            verticalArrangement = Arrangement.Center,
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(
                text = "Share & Upload",
                style = MaterialTheme.typography.titleMedium
            )
            Text(
                text = "Share files to XerahS to upload them.",
                style = MaterialTheme.typography.bodyMedium
            )
            Button(onClick = { }) {
                Text("Choose Photo or File")
            }
        }
    }
}
