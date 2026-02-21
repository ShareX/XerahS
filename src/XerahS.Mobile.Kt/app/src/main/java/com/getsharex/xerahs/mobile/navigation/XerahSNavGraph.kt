package com.getsharex.xerahs.mobile.navigation

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

    NavHost(
        navController = navController,
        startDestination = Screen.Loading.route
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
                    onBack = { navController.popBackStack() }
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
