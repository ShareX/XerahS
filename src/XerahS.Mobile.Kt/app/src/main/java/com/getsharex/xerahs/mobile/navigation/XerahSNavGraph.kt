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
            PlaceholderHistoryScreen(onBack = { navController.popBackStack() })
        }
        composable(Screen.Settings.route) {
            PlaceholderSettingsScreen(onBack = { navController.popBackStack() })
        }
    }
}
