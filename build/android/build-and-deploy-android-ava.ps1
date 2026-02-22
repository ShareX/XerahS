# Build and deploy XerahS.Mobile.Ava (Avalonia) to Android emulator/device via adb
# Copyright (c) 2007-2026 ShareX Team
$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot\..\.."
$projectPath = Join-Path $root "src\mobile-experimental\XerahS.Mobile.Ava\XerahS.Mobile.Ava.csproj"
$packageId = "com.getsharex.xerahs"

# Android SDK platform-tools (adb)
$adbPath = $env:ANDROID_HOME
if ([string]::IsNullOrEmpty($adbPath)) { $adbPath = "$env:LOCALAPPDATA\Android\Sdk" }
$adb = Join-Path $adbPath "platform-tools\adb.exe"
if (!(Test-Path $adb)) {
    Write-Host "Error: adb not found at $adb. Set ANDROID_HOME or ensure Android SDK is at %LOCALAPPDATA%\Android\Sdk" -ForegroundColor Red
    exit 1
}

# Optional: clean first (use -Clean to avoid file-in-use errors)
$doClean = $args -contains "-Clean"
if ($doClean) {
    Write-Host "Cleaning..." -ForegroundColor Cyan
    dotnet clean $projectPath -f net10.0-android -c Debug | Out-Null
    if ($LASTEXITCODE -ne 0) { exit 1 }
}

Write-Host "Building XerahS.Mobile.Ava (Debug, net10.0-android)..." -ForegroundColor Cyan
dotnet build $projectPath -f net10.0-android -c Debug -m:1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit 1
}

$apkDir = Join-Path $root "src\mobile-experimental\XerahS.Mobile.Ava\bin\Debug\net10.0-android"
$apk = Get-ChildItem -Path $apkDir -Filter "*.apk" -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
if ([string]::IsNullOrEmpty($apk)) {
    Write-Host "Error: No APK found in $apkDir" -ForegroundColor Red
    exit 1
}

Write-Host "APK: $apk" -ForegroundColor Gray
Write-Host "Checking devices..." -ForegroundColor Cyan
$devices = & $adb devices
if ($devices -notmatch "device$") {
    Write-Host "Error: No emulator/device attached. Run an emulator or connect a device, then run: adb devices" -ForegroundColor Red
    exit 1
}

Write-Host "Installing on device/emulator..." -ForegroundColor Cyan
& $adb install -r $apk
if ($LASTEXITCODE -ne 0) {
    Write-Host "Install failed." -ForegroundColor Red
    exit 1
}

Write-Host "Launching app..." -ForegroundColor Cyan
& $adb shell monkey -p $packageId -c android.intent.category.LAUNCHER 1
Write-Host "Done. App should be open on the emulator/device." -ForegroundColor Green
