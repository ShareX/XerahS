# XerahS Mobile Initialization Handover Report

## Overview
This report documents the investigation and resolution of UI responsiveness issues on Android, specifically the deadlocking of the Settings and History buttons and the application hanging on a white screen during startup.

## Issues Encountered
1. **UI Thread Deadlocks:** The application's UI thread was being blocked during startup, making the Settings and History buttons unresponsive and causing the app to visually freeze.
2. **MAUI White Screen:** The MAUI application would display a blank white screen instead of rendering the user interface for several seconds on launch.
3. **Android Watchdog Kills:** The synchronous initialization was taking too long on the Android Main Thread, risking the Android OS watchdog killing the application for being unresponsive (ANR).
4. **Race Conditions:** Attempting to offload initialization naively to a background thread caused race conditions where ViewModels attempted to access `SettingsManager` or `ProviderCatalog` before they were fully initialized.

## Root Cause Analysis
The root cause of these issues was the synchronous execution of heavy initialization tasks on the Android Main Thread:
- `SettingsManager.LoadInitialSettings()` performed synchronous disk I/O and JSON parsing for various configuration files.
- `ProviderCatalog.InitializeBuiltInProviders()` used heavy C# Reflection to scan assemblies for uploader providers.

## Fixes Tried (And Failed)
1. **Naive `Task.Run` in `MainActivity.cs`:** Wrapping the original initialization sequence in `Task.Run` without a loading screen caused a race condition. The UI Views and ViewModels were constructed on the main thread and immediately threw exceptions or behaved unpredictably because the `Settings` and `Providers` were still being loaded in the background.
2. **`await` inside Initialization without a Splash Screen:** Attempting to make `App.xaml.cs` (MAUI) or `MobileApp.axaml.cs` (Avalonia) asynchronous without providing an initial visual state caused the OS to wait for the initialization to finish before rendering anything, resulting in prolonged white screens and ANR risks.
3. **Fire-and-forget `InitializeCoreAsync`:** In MAUI `MainActivity.cs`, calling `_ = app.InitializeCoreAsync();` without `Task.Run` still executed the synchronous parts of the method on the UI thread before hitting the first yielding `await`, which continued to block the initial layout pass and display a white screen.
4. **Async init with Loading Screen + `Task.Run(() => app.InitializeCoreAsync())`:** Adding a `LoadingPage` visible immediately, creating async wrappers `LoadInitialSettingsAsync` and `InitializeBuiltInProvidersAsync`, and kicking off init from `Task.Run` in `MainActivity.OnCreate`. **Still showing white screen.** The `LoadingPage` is not rendering before the block occurs.

## Current Status: ⚠️ UNRESOLVED

The white screen is **still occurring** as of 2026-02-20. The `LoadingPage` is not being displayed before the initialization hangs the rendering pipeline.

### What Is In Place
- `LoadingPage.xaml` and `LoadingPage.xaml.cs` created for MAUI, with a random 2-letter build ID for deployment verification.
- `MobileLoadingView.axaml` created for Avalonia.
- `LoadInitialSettingsAsync()` added to `SettingsManager`.
- `InitializeBuiltInProvidersAsync()` added to `ProviderCatalog`.
- `App.xaml.cs` sets `LoadingPage` as the initial window page and has `InitializeCoreAsync()` which swaps to `AppShell` when done.
- `MainActivity.cs` calls `Task.Run(() => app.InitializeCoreAsync())`.
- Android logcat error reporting added to both silence `try/catch` blocks to bubble up exceptions.

### Suspected Remaining Causes
- **MAUI rendering pipeline:** Even with `Task.Run`, MAUI may be waiting for its own internal initialization pipeline to complete before rendering the first page. The `CreateWindow` / MAUI shell setup may be blocking the render thread before `LoadingPage` is ever drawn.
- **`OnCreate` timing:** MAUI's `OnCreate` base call (`base.OnCreate(savedInstanceState)`) internally initialises the MAUI application and the first page synchronously. It's possible the blocking happens inside `base.OnCreate` before we even reach our `Task.Run` call.
- **MAUI Splash Theme:** The `Theme` is set to `@style/Maui.SplashTheme` — the white screen may be the native Android splash theme transitioning. The MAUI app may be correctly running, but the splash-to-content transition is still blocking.

### Recommended Next Steps
1. Add logcat logging at the very top of `OnCreate`, before `base.OnCreate`, to measure how long the base call takes.
2. Try switching `Theme` in `[Activity(...)]` to a regular non-splash theme to verify whether the issue originates from the MAUI splash theme.
3. Investigate whether `App.CreateWindow` is being called before or after the app is visible, and add logcat logging there.
4. Consider using Android's native `SplashScreen` API (API 31+) as the true splash screen rather than relying on MAUI's `LoadingPage` rendering.

