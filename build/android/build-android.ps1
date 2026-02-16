# Build script for XerahS Mobile Android projects
# Copyright (c) 2007-2026 ShareX Team
$ErrorActionPreference = "Stop"

# Configuration
$root = Resolve-Path "$PSScriptRoot\..\.."
$outputDir = Join-Path $root "dist\android"
if (!(Test-Path $outputDir)) { 
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null 
}

# Get Version from Directory.Build.props
$buildPropsPath = Join-Path $root "Directory.Build.props"
$version = dotnet msbuild $buildPropsPath -getProperty:Version
$version = $version.Trim()
Write-Host "Building XerahS Android version $version..." -ForegroundColor Cyan
Write-Host ""

# Set Java environment for Android builds (JDK 21 required)
if ([string]::IsNullOrEmpty($env:JAVA_HOME)) {
    $jdkPath = "C:\Program Files\Microsoft\jdk-21.0.5.11-hotspot"
    if (Test-Path $jdkPath) {
        $env:JAVA_HOME = $jdkPath
        $env:PATH = "$jdkPath\bin;$env:PATH"
        Write-Host "✓ Java environment configured: JDK 21" -ForegroundColor Green
    } else {
        Write-Host "❌ Error: JDK 21 not found. Please install Microsoft JDK 21" -ForegroundColor Red
        Write-Host "   Download from: https://learn.microsoft.com/en-us/java/openjdk/download"
        exit 1
    }
}

# Verify Java version
$javaVersion = & java -version 2>&1 | Select-Object -First 1
Write-Host "  Java Version: $javaVersion" -ForegroundColor Gray
Write-Host ""

# Define Projects to Build
$mobileUIProject = Join-Path $root "src\XerahS.Mobile.UI\XerahS.Mobile.UI.csproj"
$mobileAndroidProject = Join-Path $root "src\XerahS.Mobile.Android\XerahS.Mobile.Android.csproj"
$mobileMauiProject = Join-Path $root "src\XerahS.Mobile.Maui\XerahS.Mobile.Maui.csproj"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Building XerahS.Mobile.UI (Shared Library)" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
dotnet build $mobileUIProject -c Release
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ XerahS.Mobile.UI built successfully" -ForegroundColor Green
} else {
    Write-Host "❌ XerahS.Mobile.UI build failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Building XerahS.Mobile.Android" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
dotnet build $mobileAndroidProject -c Release -f net10.0-android
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ XerahS.Mobile.Android built successfully" -ForegroundColor Green
    
    # Copy APK to dist if it exists
    $apkSource = Join-Path $root "src\XerahS.Mobile.Android\bin\Release\net10.0-android\com.sharexteam.xerahs-Signed.apk"
    if (Test-Path $apkSource) {
        $apkDest = Join-Path $outputDir "XerahS-$version-Android.apk"
        Copy-Item $apkSource $apkDest -Force
        Write-Host "   APK copied to: $apkDest" -ForegroundColor Gray
    }
} else {
    Write-Host "❌ XerahS.Mobile.Android build failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Building XerahS.Mobile.Maui (Android)" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
dotnet build $mobileMauiProject -c Release -f net10.0-android
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ XerahS.Mobile.Maui (Android) built successfully" -ForegroundColor Green
    
    # Copy MAUI APK to dist if it exists
    $mauiApkSource = Join-Path $root "src\XerahS.Mobile.Maui\bin\Release\net10.0-android\com.sharexteam.xerahs-Signed.apk"
    if (Test-Path $mauiApkSource) {
        $mauiApkDest = Join-Path $outputDir "XerahS-$version-MAUI-Android.apk"
        Copy-Item $mauiApkSource $mauiApkDest -Force
        Write-Host "   APK copied to: $mauiApkDest" -ForegroundColor Gray
    }
} else {
    Write-Host "❌ XerahS.Mobile.Maui build failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "✅ All Android builds completed successfully!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host "Output directory: $outputDir" -ForegroundColor Gray
Write-Host "Version: $version" -ForegroundColor Gray
Write-Host ""
Write-Host "Note: iOS builds require macOS and are skipped on Windows." -ForegroundColor Yellow
