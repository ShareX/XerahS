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

4. **Async init with Loading Screen + `Task.Run(() => app.InitializeCoreAsync())`:** Adding a `LoadingPage` visible immediately, creating async wrappers `LoadInitialSettingsAsync` and `InitializeBuiltInProvidersAsync`, and kicking off init from `Task.Run` in `MainActivity.OnCreate`. **Still showing white screen.** The `LoadingPage` is not rendering before the block occurs.
5. **Direct Bundled Provider Registration (No Reflection):** Discovered that `InitializeBuiltInProviders` performs a slow `Assembly.GetTypes()` reflection scan. Replaced this entirely in both MAUI and Avalonia by explicitly instantiating `new AmazonS3Provider()` and registering it directly. This ran instantly within the `Task.Run` block, ensuring **zero** blocking XerahS code on the Main Thread. **Still showing white screen.**

## Current Status: ⚠️ UNRESOLVED

The white screen is **still occurring** as of 2026-02-20, despite verifying that absolutely all XerahS initialization logic (settings IO, directory creation, plugin registration) executes instantly on a background thread pool without touching the UI thread. The `LoadingPage` is not being displayed before the initialization finishes.

### What Is In Place
- `LoadingPage.xaml` and `LoadingPage.xaml.cs` created for MAUI, with a random 2-letter build ID for deployment verification.
- `MobileLoadingView.axaml` created for Avalonia.
- `LoadInitialSettingsAsync()` added to `SettingsManager`.
- Direct `MobileApp.RegisterBundledProvider()` and `ProviderCatalog.RegisterProvider()` APIs added to bypass `Assembly.GetTypes()` on mobile.
- `App.xaml.cs` (MAUI) and `MobileApp.axaml.cs` (Avalonia) run ALL initialization inside a single `Task.Run(() => { ... }).ConfigureAwait(false)`.
- Android logcat error reporting added to bubble up exceptions.

### Suspected Remaining Causes
- **MAUI rendering pipeline:** Even with `Task.Run`, MAUI may be waiting for its own internal initialization pipeline to complete before rendering the first page. The `CreateWindow` / MAUI shell setup may be blocking the render thread before `LoadingPage` is ever drawn.
- **`OnCreate` timing:** MAUI's `OnCreate` base call (`base.OnCreate(savedInstanceState)`) internally initialises the MAUI application and the first page synchronously. It's possible the blocking happens inside `base.OnCreate` before we even reach our `Task.Run` call.
- **MAUI Splash Theme:** The `Theme` is set to `@style/Maui.SplashTheme` — the white screen may be the native Android splash theme transitioning. The MAUI app may be correctly running, but the splash-to-content transition is still blocking.

### Recommended Next Steps
1. Add logcat logging at the very top of `OnCreate`, before `base.OnCreate`, to measure how long the base call takes.
2. Try switching `Theme` in `[Activity(...)]` to a regular non-splash theme to verify whether the issue originates from the MAUI splash theme.
3. Investigate whether `App.CreateWindow` is being called before or after the app is visible, and add logcat logging there.
4. Consider using Android's native `SplashScreen` API (API 31+) as the true splash screen rather than relying on MAUI's `LoadingPage` rendering.

