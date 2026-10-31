---
name: build-android
description: Build and deploy XerahS Android apps (Kotlin/Mobile.Kt, Avalonia, or MAUI) through Gradle, dotnet, and adb. Use only for Android build or deployment work.
metadata:
  keywords:
    - build
    - android
    - kotlin
    - mobile.kt
    - gradle
    - compose
    - maui
    - avalonia
    - adb
    - apk
    - deploy
    - JAVA_HOME
    - KeyboardOptions
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

You are an expert Android build and deploy specialist for XerahS mobile (Kotlin/Mobile.Kt, Avalonia, and MAUI).

Follow these instructions when building or deploying Android apps. Treat elapsed time as a diagnostic signal, not a fixed failure threshold; stop only when progress is genuinely stalled.

<task>
  <goal>Build XerahS.Mobile.Ava or XerahS.Mobile.Maui for Android and deploy via adb.</goal>
  <goal>Avoid file locks by using single-node builds and releasing only verified task-owned lock holders.</goal>
  <goal>If a build is genuinely stalled, stop and fix locks, parallelism, or stale processes before retrying.</goal>
  <goal>Ensure MAUI APK works when installed via adb (EmbedAssembliesIntoApk).</goal>
</task>

<context>
  <scripts>
    <maui>build/android/build-and-deploy-android-maui.ps1</maui>
    <ava>build/android/build-and-deploy-android-ava.ps1</ava>
  </scripts>
  <package_id>com.getsharex.xerahs</package_id>
  <package_id_kt>com.getsharex.xerahs.mobile</package_id_kt>
  <adb_default>%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe (adb is often not in PATH; use full path)</adb_default>
  <maui_apk>src\mobile-experimental\XerahS.Mobile.Maui\bin\Debug\net10.0-android\com.getsharex.xerahs-Signed.apk</maui_apk>
  <ava_apk_dir>src\mobile-experimental\XerahS.Mobile.Ava\bin\Debug\net10.0-android</ava_apk_dir>
  <kt_project_root>src\mobile\android</kt_project_root>
  <kt_apk>src\mobile\android\app\build\outputs\apk\debug\app-debug.apk</kt_apk>
</context>

## Shared Build Guardrails

Before Android-specific work, follow [build-common](../build-common/SKILL.md) for shared stalled-build recovery, no-concurrent-build, `-m:1`, TFM, and SkiaSharp rules. This Android skill owns Android-specific prerequisites, emulator/device handling, APK output paths, and deploy commands.

## Kotlin (src/mobile/android): prerequisites and first-build lessons

The Kotlin app uses **Gradle** (not dotnet). The project root is **`src/mobile/android`** (where `settings.gradle.kts` and `gradlew.bat` live).

### 1. JAVA_HOME is required

**First build often fails with:** `JAVA_HOME is not set and no 'java' command could be found in your PATH`.

- Gradle needs a JDK. On Windows, **adb** and **java** are often not in PATH; the IDE uses the embedded JBR.
- **Fix:** Set `JAVA_HOME` to the JDK used by Android Studio before running Gradle, then run the build in the same process (e.g. one PowerShell command that sets env and invokes gradlew):

```powershell
$env:JAVA_HOME = "C:\Program Files\Android\Android Studio\jbr"
cd "src\mobile\android"
.\gradlew.bat assembleDebug
```

- If the repo is under a path with spaces (e.g. `ShareX Team\XerahS`), use quoted paths and ensure `cd` is to the Kotlin project root.

### 2. Compose KeyboardOptions: correct import and dependency

**Symptom:** `Unresolved reference: KeyboardOptions` when compiling a module that uses `KeyboardOptions` in a Compose `TextField`/`OutlinedTextField`.

- **Wrong:** `import androidx.compose.ui.text.input.KeyboardOptions` — that package does not expose `KeyboardOptions` in the way the compiler resolves for the dependency set in use.
- **Right:** `import androidx.compose.foundation.text.KeyboardOptions`. `KeyboardCapitalization` stays `androidx.compose.ui.text.input.KeyboardCapitalization`; `KeyboardType` is `androidx.compose.ui.text.input.KeyboardType`.

- **Module dependency:** The module that uses `KeyboardOptions` (e.g. `feature:settings`) must depend on **Compose Foundation**, or the build will still fail with "Unresolved reference: KeyboardOptions". In that module’s `build.gradle.kts`:

```kotlin
implementation(libs.compose.foundation)
```

- **URL / no auto-capitalize:** For URL or domain fields, use `KeyboardType.Uri` together with `KeyboardCapitalization.None` so the soft keyboard does not auto-capitalize; some IMEs ignore capitalization when the type is already `Uri`.

### 3. adb not in PATH

**Symptom:** `The term 'adb' is not recognized` when running install or logcat from a script or terminal.

- Use the full path to adb, e.g. `$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe`, or set `ANDROID_HOME` / `PATH` so that `adb` is found. The build skill assumes adb is invoked via full path when PATH is not configured.

### 4. Kotlin build and deploy (summary)

| Step | Command / path |
|------|----------------|
| Project root | `src\mobile\android` |
| Build | Set `JAVA_HOME` then `.\gradlew.bat assembleDebug` |
| APK | `app\build\outputs\apk\debug\app-debug.apk` |
| Install | `adb install -r <path-to-app-debug.apk>` |

---

## Pre-build: release locks

File locks are the main cause of Android build failures. **Before building**, ensure no previous build is still running and clean can run.

### 1. Identify what is locked

- **Clean fails** with `The process cannot access the file '...' because it is being used by another process`. The message often says **which process** (e.g. `.NET Host (27788)` or `VBCSCompiler`).
- **Build fails** with `CompileAvaloniaXamlTask` / `Cannot open '...ShareX.ImageEditor.dll' for writing` or `...XerahS.AmazonS3.Plugin.dll ... being used by another process` — usually **parallel MSBuild** racing on the same output.
- **Build fails** with `XA0142` / `llvm-objcopy.EXE: error: permission denied` or **`XAWAS7024: The file is locked by: ".NET Host (PID)"** on paths like `obj\Debug\net10.0-android\android-arm64\wrapped\lib_*.dll.so` — a previous build’s .NET Host is still holding wrapped assembly outputs. Stop that PID, then clean and rebuild.

### 2. Release the lock and build single-node

Stop only the verified task-owned lock holder, as in [build-common](../build-common/SKILL.md). Never run two Android or solution builds at once: `ShareX.ImageEditor` and the AmazonS3 plugin share outputs and wrapped `.so` files. Both `build-and-deploy-android-maui.ps1` and `build-and-deploy-android-ava.ps1` use `-m:1`; do not remove it. Manual builds use `dotnet build <project> -f net10.0-android -c Debug -m:1`.

### 3. Clean after releasing locks

After stopping the locking process:

```powershell
dotnet clean "src\mobile-experimental\XerahS.Mobile.Maui\XerahS.Mobile.Maui.csproj" -f net10.0-android -c Debug -v minimal
# For Avalonia:
dotnet clean "src\mobile-experimental\XerahS.Mobile.Ava\XerahS.Mobile.Ava.csproj" -f net10.0-android -c Debug -v minimal
```

If clean still fails, manually remove `obj` folders that are locked (e.g. `ShareX.ImageEditor\src\ShareX.ImageEditor\obj`, `src\desktop\plugins\AmazonS3.Plugin\obj`, `src\mobile-experimental\XerahS.Mobile.Ava\obj\Debug\net10.0-android`, or the MAUI `obj\Debug\net10.0-android` if the APK is locked).

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

### Kotlin (src/mobile/android)

Set JAVA_HOME and build from the Kotlin project root (see "Kotlin: prerequisites and first-build lessons" above). Then install with adb (full path if adb is not in PATH).

```powershell
$env:JAVA_HOME = "C:\Program Files\Android\Android Studio\jbr"
cd "src\mobile\android"   # or full path, e.g. C:\...\XerahS\src\mobile\android
.\gradlew.bat assembleDebug
```

Install and optionally launch:

```powershell
$adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
& $adb install -r "src\mobile\android\app\build\outputs\apk\debug\app-debug.apk"
& $adb shell monkey -p com.getsharex.xerahs.mobile -c android.intent.category.LAUNCHER 1
```

If the first build fails with JAVA_HOME or KeyboardOptions, apply the fixes in the Kotlin section above before retrying.

### MAUI (recommended: use script)

```powershell
.\build\android\build-and-deploy-android-maui.ps1
# If you need a clean first (e.g. after fixing locks):
.\build\android\build-and-deploy-android-maui.ps1 -Clean
```

- Script uses **`-m:1`** and runs clean (if `-Clean`), build, then adb install and launch.
- **Timeout**: Do not use a fixed cap; stop only when output or process state shows the build is stalled.

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
& $adb install -r "src\mobile-experimental\XerahS.Mobile.Maui\bin\Debug\net10.0-android\com.getsharex.xerahs-Signed.apk"
& $adb shell monkey -p com.getsharex.xerahs -c android.intent.category.LAUNCHER 1
```

**Avalonia:**
```powershell
$apk = (Get-ChildItem "src\mobile-experimental\XerahS.Mobile.Ava\bin\Debug\net10.0-android\*.apk" | Select-Object -First 1).FullName
& $adb install -r $apk
& $adb shell monkey -p com.getsharex.xerahs -c android.intent.category.LAUNCHER 1
```

---

## Success criteria

- Build completes successfully with the expected APK and deployment result.
- No "file in use", XA0142/XAWAS7024, or CompileAvaloniaXamlTask copy errors.
- A device or emulator is present when the script runs the install step (`adb devices`); otherwise use manual install/launch after starting the emulator.
- **Kotlin:** JAVA_HOME set; no "Unresolved reference: KeyboardOptions" (use `foundation.text.KeyboardOptions` and add `compose.foundation` to the module); APK installs via `adb install -r` and app runs.
- MAUI: APK installs via `adb install -r` and app runs past the loading screen (no white screen, no "No assemblies found" crash).
- Avalonia: APK installs and app shows loading then main UI (no blank screen from host Content cleared).

---

## References

- `.ai/skills/build-common/SKILL.md` - Shared timeout, lock recovery, no-concurrent-build, `-m:1`, TFM, and SkiaSharp guardrails

- `developers/lessons-learnt/android_avalonia_init_fix.md` — Avalonia host-Content bug, MAUI defer-init and white screen
- `build/android/README.md` — Prerequisites, env (JAVA_HOME, Android SDK)
- `build/android/build-and-deploy-android-maui.ps1` — MAUI build/deploy script (uses `-m:1`)
- `build/android/build-and-deploy-android-ava.ps1` — Avalonia build/deploy script (uses `-m:1`)
- `.ai/skills/build-android/SKILL.md` — This skill; Kotlin JAVA_HOME, Compose KeyboardOptions/foundation, adb path
