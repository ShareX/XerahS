pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}
dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
    }
}

rootProject.name = "XerahS.Mobile.Kt"
include(":app")
include(":core:common")
include(":core:data")
include(":core:domain")
include(":feature:upload")
include(":feature:history")
include(":feature:settings")
