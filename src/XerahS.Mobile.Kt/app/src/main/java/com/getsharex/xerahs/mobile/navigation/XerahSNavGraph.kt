package com.getsharex.xerahs.mobile.navigation

import androidx.compose.runtime.Composable
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.getsharex.xerahs.mobile.ui.screens.LoadingScreen
import com.getsharex.xerahs.mobile.ui.screens.PlaceholderHistoryScreen
import com.getsharex.xerahs.mobile.ui.screens.PlaceholderSettingsScreen
import com.getsharex.xerahs.mobile.ui.screens.PlaceholderUploadScreen

@Composable
fun XerahSNavGraph(
    navController: NavHostController = rememberNavController()
) {
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
            PlaceholderUploadScreen(
                onOpenHistory = { navController.navigate(Screen.History.route) },
                onOpenSettings = { navController.navigate(Screen.Settings.route) }
            )
        }
        composable(Screen.History.route) {
            PlaceholderHistoryScreen(onBack = { navController.popBackStack() })
        }
        composable(Screen.Settings.route) {
            PlaceholderSettingsScreen(onBack = { navController.popBackStack() })
        }
    }
}
