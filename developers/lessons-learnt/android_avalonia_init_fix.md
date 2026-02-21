# Android Avalonia: App Stuck at "Initializing XerahS..."

**Date:** 2026-02-21  
**Component:** XerahS.Mobile.Ava (Avalonia Android)  
**Status:** Resolved

## Overview

The Avalonia Android app appeared stuck on the "Initializing XerahS..." loading screen (or showed a blank/white screen). Previous investigation had focused on initialization logic (async init, loading view, reflection removal) and was documented as **UNRESOLVED** in `docs/technical/android_deadlock_handover_report.md`. The actual root cause was in the **Android host** setup, not in the init pipeline.

## Symptom

- App launches; loading view may flash or never appear.
- Screen stays on "Initializing XerahS..." or goes blank/white.
- Init logic runs on a background thread and completes; navigation to the main view never appears on screen.

## Root Cause

In `Platforms/Android/MainActivity.cs`, `OnCreate` contained a block that **cleared the content of the control that hosts the Avalonia view**:

```csharp
if (Avalonia.Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime &&
    singleViewLifetime.MainView is { } mainView &&
    mainView.Parent is ContentControl parent)
{
    parent.Content = null;  // ← This removed the entire Avalonia UI from the visual tree
}
base.OnCreate(savedInstanceState);
```

- `MainView` is the app’s navigation root (`TransitioningContentControl`). Its `Parent` is the Avalonia.Android host `ContentControl`.
- Setting `parent.Content = null` removes that host’s content, so the whole Avalonia tree (including the loading view and any later navigation) is no longer displayed.
- Init could complete and call `Navigate(uploadView)` on the UI thread, but the navigation root was no longer in the visual tree, so the user saw nothing.

## Lesson Learned

1. **Host view hierarchy is sacred.** With `ISingleViewApplicationLifetime`, the framework sets `MainView` as the content of a host control. Do **not** clear that host’s `Content` (e.g. in `OnCreate` or other lifecycle methods). Any code that does `someParent.Content = null` where that parent is the host of `MainView` will hide the entire UI.
2. **When “init never finishes” is actually “UI was removed.”** If the app seems stuck on a loading or blank screen but logs show init completing and navigation running, look for platform code that modifies the view tree (especially the parent of `MainView`) on the Android/iOS side.
3. **Cross-layer debugging.** The bug was in the Android Activity, not in Avalonia or in C# init. Tracing from “where is the view attached?” and “who clears Content?” led to the fix. Logging init steps (e.g. via Android logcat) helped confirm that init and navigation were running while the UI was missing.

## Fix

- **Remove** the block in `MainActivity.OnCreate` that sets `parent.Content = null` (and the unused `using Avalonia.Controls` if applicable).
- Keep `base.OnCreate(savedInstanceState)` and the rest of `OnCreate` (e.g. system bars, share intent, heartbeat) as-is.

## Additional Improvements Made

- **Logging:** Android logcat messages were added in `MobileApp.InitializeCoreAsync` (e.g. `[Init] Showing loading view`, `[Init] Background init completed`, `[Init] ShowUploadView done`) so init and navigation can be verified without attaching a debugger.
- **Yield before init:** A short `await Task.Delay(100)` after navigating to the loading view gives the UI thread a chance to render the loading screen before the background init runs.
- **Build and deploy:** `build/android/build-and-deploy-android-ava.ps1` builds the Avalonia Android app, installs via adb, and launches it on the emulator/device (launch via `adb shell monkey -p com.getsharex.xerahs -c android.intent.category.LAUNCHER 1` to avoid relying on the exact MainActivity class name).

## MAUI equivalent (no host-Content bug)

MAUI does not use a single host `ContentControl` the same way; there is no equivalent "clear parent content" bug. The handover report did document **MAUI white screen** where the loading page was not rendering before init. For MAUI Android we apply the same principle: **let the loading page render before starting heavy init**.

- In `XerahS.Mobile.Maui/Platforms/Android/MainActivity.cs`, do **not** call `app.InitializeCoreAsync()` immediately after `base.OnCreate`. Instead, defer the start by ~150 ms (e.g. `Task.Run` + `Task.Delay(150)` + `MainThread.BeginInvokeOnMainThread` to call `InitializeCoreAsync`). That allows `OnCreate` to return and the first frame of `LoadingPage` to paint before background init runs.
- Use consistent Android logcat tags in `App.InitializeCoreAsync` (e.g. `[Init] Loading page visible; starting background init.`, `[Init] Background init completed.`, `[Init] AppShell is now the root page.`) so init can be traced in logcat.

## References

- `docs/technical/android_deadlock_handover_report.md` — earlier init/deadlock investigation (async init, loading page, no reflection); status was UNRESOLVED until the host-Content fix.
- `src/XerahS.Mobile.Ava/Platforms/Android/MainActivity.cs` — remove any code that clears the parent of `MainView`.
- `src/XerahS.Mobile.Ava/MobileApp.axaml.cs` — `InitializeCoreAsync` and navigation flow.
- `src/XerahS.Mobile.Maui/Platforms/Android/MainActivity.cs` — defer init start; do not clear any host content.
- `src/XerahS.Mobile.Maui/App.xaml.cs` — `InitializeCoreAsync` and logging.
