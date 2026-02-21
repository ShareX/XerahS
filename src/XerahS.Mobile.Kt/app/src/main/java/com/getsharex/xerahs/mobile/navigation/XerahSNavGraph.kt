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

package com.getsharex.xerahs.mobile.navigation

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.widget.Toast
import androidx.compose.runtime.Composable
import androidx.compose.ui.platform.LocalContext
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.getsharex.xerahs.mobile.XerahSApplication
import com.getsharex.xerahs.mobile.ui.screens.LoadingScreen
import com.getsharex.xerahs.mobile.ui.screens.PlaceholderHistoryScreen
import com.getsharex.xerahs.mobile.ui.screens.PlaceholderSettingsScreen
import com.getsharex.xerahs.mobile.feature.upload.UploadScreen
import com.getsharex.xerahs.mobile.ui.screens.PlaceholderUploadScreen
import com.getsharex.xerahs.mobile.feature.history.HistoryScreen
import com.getsharex.xerahs.mobile.feature.settings.SettingsHubScreen
import com.getsharex.xerahs.mobile.feature.settings.S3ConfigScreen
import com.getsharex.xerahs.mobile.feature.settings.CustomUploaderConfigScreen

@Composable
fun XerahSNavGraph(
    navController: NavHostController = rememberNavController()
) {
    val context = LocalContext.current
    val app = context.applicationContext as? XerahSApplication
    val clipboardManager = context.getSystemService(Context.CLIPBOARD_SERVICE) as? ClipboardManager
    val onCopyToClipboard: (String) -> Unit = { text ->
        clipboardManager?.setPrimaryClip(ClipData.newPlainText("url", text))
        Toast.makeText(context, "Copied", Toast.LENGTH_SHORT).show()
    }

    val hasPendingShare = app?.pendingSharedPaths?.isNotEmpty() == true
    NavHost(
        navController = navController,
        startDestination = if (hasPendingShare) Screen.Upload.route else Screen.Loading.route
    ) {
        composable(Screen.Loading.route) {
            LoadingScreen(
                onInitComplete = {
                    navController.navigate(Screen.Upload.route) {
                        popUpTo(Screen.Loading.route) { inclusive = true }
                    }
                }
            )
        }
        composable(Screen.Upload.route) {
            val worker = app?.uploadQueueWorker
            if (worker != null) {
                val pending = synchronized(app.pendingSharedPaths) {
                    app.pendingSharedPaths.removeFirstOrNull()
                }
                UploadScreen(
                    worker = worker,
                    onOpenHistory = { navController.navigate(Screen.History.route) },
                    onOpenSettings = { navController.navigate(Screen.Settings.route) },
                    onPickFiles = null,
                    onCopyToClipboard = onCopyToClipboard,
                    initialPaths = pending
                )
            } else {
                PlaceholderUploadScreen(
                    onOpenHistory = { navController.navigate(Screen.History.route) },
                    onOpenSettings = { navController.navigate(Screen.Settings.route) }
                )
            }
        }
        composable(Screen.History.route) {
            val historyRepo = app?.historyRepository
            if (historyRepo != null) {
                HistoryScreen(
                    historyRepository = historyRepo,
                    onBack = { navController.popBackStack() },
                    onCopyToClipboard = onCopyToClipboard
                )
            } else {
                PlaceholderHistoryScreen(onBack = { navController.popBackStack() })
            }
        }
        composable(Screen.Settings.route) {
            val settingsRepo = app?.settingsRepository
            if (settingsRepo != null) {
                SettingsHubScreen(
                    settingsRepository = settingsRepo,
                    onBack = { navController.popBackStack() },
                    onNavigateToS3 = { navController.navigate(Screen.S3Config.route) },
                    onNavigateToCustomUploader = { navController.navigate(Screen.CustomUploaderConfig.route) },
                    onRefresh = { }
                )
            } else {
                PlaceholderSettingsScreen(onBack = { navController.popBackStack() })
            }
        }
        composable(Screen.S3Config.route) {
            S3ConfigScreen(
                settingsRepository = app?.settingsRepository,
                onBack = { navController.popBackStack() }
            )
        }
        composable(Screen.CustomUploaderConfig.route) {
            CustomUploaderConfigScreen(
                settingsRepository = app?.settingsRepository,
                onBack = { navController.popBackStack() }
            )
        }
    }
}
