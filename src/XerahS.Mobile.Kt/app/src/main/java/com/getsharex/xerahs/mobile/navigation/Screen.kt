package com.getsharex.xerahs.mobile.navigation

/**
 * Sealed class representing all navigation destinations.
 * Optional path args (e.g. shared file paths) can be passed via Upload.
 */
sealed class Screen(val route: String) {
    data object Loading : Screen("loading")
    data object Upload : Screen("upload") {
        fun withPaths(paths: List<String>): String =
            if (paths.isEmpty()) route else "upload?paths=${paths.joinToString(",")}"
    }
    data object History : Screen("history")
    data object Settings : Screen("settings")
    data object S3Config : Screen("settings/s3")
    data object CustomUploaderConfig : Screen("settings/custom")
}
