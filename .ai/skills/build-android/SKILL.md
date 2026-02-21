---
name: build-android
description: Build and deploy XerahS Android apps (Avalonia and MAUI) to emulator/device via adb. Handles file locks, EmbedAssembliesIntoApk, init white-screen, single-node builds (-m:1), and cold-boot emulator. Never wait more than 5 minutes for a build—if it exceeds that, treat as failure and fix locks or parallelism.
metadata:
  keywords:
    - build
    - android
    - maui
    - avalonia
    - adb
    - apk
    - deploy
    - file-lock
    - EmbedAssembliesIntoApk
    - white-screen
    - init
    - cold-boot
    - emulator
    - no-snapshot-load
    - single-node
    - m:1
---

You are an expert Android build and deploy specialist for XerahS mobile (Avalonia and MAUI).

Follow these instructions when building or deploying Android apps. **Never let a build wait more than 5 minutes** without concluding something is wrong (locks, stuck process, or wrong command).

<task>
  <goal>Build XerahS.Mobile.Ava or XerahS.Mobile.Maui for Android and deploy via adb.</goal>
  <goal>Avoid file locks by using single-node builds and killing lingering dotnet processes.</goal>
  <goal>If a build runs longer than 5 minutes, stop and fix the cause (locks, parallelism, or stale processes).</goal>
  <goal>Ensure MAUI APK works when installed via adb (EmbedAssembliesIntoApk).</goal>
</task>

<context>
  <scripts>
    <maui>build/android/build-and-deploy-android-maui.ps1</maui>
    <ava>build/android/build-and-deploy-android-ava.ps1</ava>
  </scripts>
  <package_id>com.getsharex.xerahs</package_id>
  <adb_default>%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe (or ANDROID_HOME)</adb_default>
  <maui_apk>src\XerahS.Mobile.Maui\bin\Debug\net10.0-android\com.getsharex.xerahs-Signed.apk</maui_apk>
  <ava_apk_dir>src\XerahS.Mobile.Ava\bin\Debug\net10.0-android</ava_apk_dir>
</context>

## 5-minute build rule (critical)

**Do not wait more than 5 minutes for an Android build to complete.** A normal single-node Android build finishes in about 2–5 minutes. If the build has not completed within 5 minutes:

1. **Treat it as a failure** — something else is wrong.
2. **Stop the build** (cancel or let the command timeout).
3. **Fix the cause** before retrying:
   - **Lingering processes**: A previous `dotnet` build may be holding the APK or DLLs. Find and stop the process (see "Pre-build: release locks" below).
   - **Clean failing**: If `dotnet clean` fails with "file in use", the lock is often the APK or a DLL; the error message names the process (e.g. ".NET Host (PID)").
   - **Parallelism**: Use `-m:1` so only one project builds at a time and ImageEditor/plugin DLLs are not raced.

Do **not** increase the timeout to 10+ minutes. Fix locks and parallelism instead.

---

## Pre-build: release locks

File locks are the main cause of Android build failures. **Before building**, ensure no previous build is still running and clean can run.

### 1. Identify what is locked

- **Clean fails** with `The process cannot access the file '...' because it is being used by another process`. The message often says **which process** (e.g. `.NET Host (27788)` or `VBCSCompiler`).
- **Build fails** with `CompileAvaloniaXamlTask` / `Cannot open '...ShareX.ImageEditor.dll' for writing` or `...XerahS.AmazonS3.Plugin.dll ... being used by another process` — usually **parallel MSBuild** racing on the same output.
- **Build fails** with `XA0142` / `llvm-objcopy.EXE: error: permission denied` or **`XAWAS7024: The file is locked by: ".NET Host (PID)"** on paths like `obj\Debug\net10.0-android\android-arm64\wrapped\lib_*.dll.so` — a previous build’s .NET Host is still holding wrapped assembly outputs. Stop that PID, then clean and rebuild.

### 2. Stop the process holding the lock

If the error names a PID (e.g. 27788):

```powershell
Stop-Process -Id 27788 -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
```

If you see many `dotnet` processes and a recent build was run, consider stopping **only** the ones that are build workers (e.g. those that have been running for a long time). Avoid killing the IDE’s dotnet (e.g. OmniSharp) if possible; closing other terminals/builds first is safer.

### 3. Use single-node build to avoid races (required)

**NEVER run two Android (or solution) builds at the same time.** ImageEditor and the AmazonS3 plugin share outputs that the Avalonia XAML task and MSBuild copy; parallel MSBuild nodes race on the same DLLs and Android wrapped `.so` files and cause lock errors. **That is why the Android build feels slow:** we must use **`-m:1`** (single node), so the build runs sequentially with no parallelism.

- **MAUI**: The script `build-and-deploy-android-maui.ps1` already uses **`-m:1`**. Do not remove it.
- **Avalonia**: The script `build-and-deploy-android-ava.ps1` uses **`-m:1`** by default. Do not remove it.

Example:

```powershell
dotnet build $projectPath -f net10.0-android -c Debug -m:1
```

### 4. Clean after releasing locks

After stopping the locking process:

```powershell
dotnet clean "src\XerahS.Mobile.Maui\XerahS.Mobile.Maui.csproj" -f net10.0-android -c Debug -v minimal
# For Avalonia:
dotnet clean "src\XerahS.Mobile.Ava\XerahS.Mobile.Ava.csproj" -f net10.0-android -c Debug -v minimal
```

If clean still fails, manually remove `obj` folders that are locked (e.g. `ImageEditor\src\ShareX.ImageEditor\obj`, `src\Plugins\ShareX.AmazonS3.Plugin\obj`, `src\XerahS.Mobile.Ava\obj\Debug\net10.0-android`, or the MAUI `obj\Debug\net10.0-android` if the APK is locked).

---

## Cold-boot emulator (optional)

To start an Android emulator with a **cold boot** (no snapshot load) before building/deploying:

1. **List AVDs:** `& "$env:LOCALAPPDATA\Android\Sdk\emulator\emulator.exe" -list-avds`
2. **Start emulator:** `Start-Process -FilePath $emulator -ArgumentList "-avd","<AVD_NAME>","-no-snapshot-load" -WindowStyle Normal`
3. **Wait for device:** Poll until `adb devices` shows a line ending with `device` (e.g. up to 120 s). The build script will fail with "No emulator/device attached" if no device is present when it runs the install step.
4. **Then** run the build-and-deploy script. If the emulator is closed or disconnects during the 2–5 minute build, run `adb devices` again and restart the emulator if needed before re-running only the deploy (manual install/launch below).

---

## MAUI-specific: EmbedAssembliesIntoApk

When the MAUI app is installed via **adb install** (not via Visual Studio deploy), the APK must **contain** the .NET assemblies. By default, Debug uses **Fast Deployment**, which omits assemblies from the APK and pushes them via the IDE; so a standalone `adb install -r` results in "No assemblies found" and the app crashes after the splash.

- **Fix**: In `XerahS.Mobile.Maui.csproj`, set **`EmbedAssembliesIntoApk`** to **`true`** for Android (already done). Then rebuild; the APK will be larger and work with adb install.
- **Trade-off**: Build is slightly slower; no change to behavior once embedded.

---

## MAUI white screen / init

If the MAUI app shows a **white screen** after the logo (similar to the old Avalonia "stuck" issue):

- **Cause**: The loading page has not painted before heavy init runs (or with `EmbedAssembliesIntoApk`, first frame is slower). Starting init too soon leaves the user seeing a blank screen.
- **Fix**: In `MainActivity.cs`, **defer** calling `InitializeCoreAsync` by **~400 ms** (e.g. `Task.Run` + `Task.Delay(400)` + `MainThread.BeginInvokeOnMainThread`). Do not call it immediately in `OnCreate`. See `developers/lessons-learnt/android_avalonia_init_fix.md` (MAUI section).

---

## Avalonia-specific: host Content

Avalonia Android had a bug where **`parent.Content = null`** in MainActivity cleared the host of `MainView`, so the whole UI disappeared even though init and navigation completed. **Do not** set the host’s `Content` to null. See `developers/lessons-learnt/android_avalonia_init_fix.md`.

---

## Build and deploy commands

### MAUI (recommended: use script)

```powershell
.\build\android\build-and-deploy-android-maui.ps1
# If you need a clean first (e.g. after fixing locks):
.\build\android\build-and-deploy-android-maui.ps1 -Clean
```

- Script uses **`-m:1`** and runs clean (if `-Clean`), build, then adb install and launch.
- **Timeout**: Run with a **5-minute** cap. If the build does not finish in 5 minutes, stop and fix locks/processes.

### Avalonia

```powershell
.\build\android\build-and-deploy-android-ava.ps1
.\build\android\build-and-deploy-android-ava.ps1 -Clean
```

The Avalonia script **always** uses **`-m:1`** to avoid file locks. Ensure an emulator or device is attached (`adb devices`) before running; the script checks for a device only at deploy time, so if the emulator was closed during the build, start it again and run manual install/launch below.

### Manual install and launch (when APK already exists)

Use when the APK is already built but deploy failed (e.g. no device at script run time), or to re-launch without rebuilding. Set `$adb` from `ANDROID_HOME` or `%LOCALAPPDATA%\Android\Sdk` if needed.

**MAUI:**
```powershell
$adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
& $adb install -r "src\XerahS.Mobile.Maui\bin\Debug\net10.0-android\com.getsharex.xerahs-Signed.apk"
& $adb shell monkey -p com.getsharex.xerahs -c android.intent.category.LAUNCHER 1
```

**Avalonia:**
```powershell
$apk = (Get-ChildItem "src\XerahS.Mobile.Ava\bin\Debug\net10.0-android\*.apk" | Select-Object -First 1).FullName
& $adb install -r $apk
& $adb shell monkey -p com.getsharex.xerahs -c android.intent.category.LAUNCHER 1
```

---

## Success criteria

- Build completes in **under 5 minutes** (typically 2–3 min with `-m:1` and no locks).
- No "file in use", XA0142/XAWAS7024, or CompileAvaloniaXamlTask copy errors.
- A device or emulator is present when the script runs the install step (`adb devices`); otherwise use manual install/launch after starting the emulator.
- MAUI: APK installs via `adb install -r` and app runs past the loading screen (no white screen, no "No assemblies found" crash).
- Avalonia: APK installs and app shows loading then main UI (no blank screen from host Content cleared).

---

## References

- `developers/lessons-learnt/android_avalonia_init_fix.md` — Avalonia host-Content bug, MAUI defer-init and white screen
- `build/android/README.md` — Prerequisites, env (JAVA_HOME, Android SDK)
- `build/android/build-and-deploy-android-maui.ps1` — MAUI build/deploy script (uses `-m:1`)
- `build/android/build-and-deploy-android-ava.ps1` — Avalonia build/deploy script (uses `-m:1`)
- `.ai/skills/build-linux-binary/SKILL.md` — Same `/m:1` and "never run two builds" guidance for Linux
