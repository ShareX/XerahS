#!/bin/bash
# Build script for XerahS Mobile Android projects on Linux
# Copyright (c) 2007-2026 ShareX Team
set -e

# Configuration
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
ROOT="$SCRIPT_DIR/../.."
OUTPUT_DIR="$ROOT/dist/android"

if [ ! -d "$OUTPUT_DIR" ]; then
    mkdir -p "$OUTPUT_DIR"
fi

# Get Version from Directory.Build.props
VERSION=$(grep '<Version>' "$ROOT/Directory.Build.props" | sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' | tr -d '[:space:]')
echo "Building XerahS Android version $VERSION..."
echo ""

# Set Java environment for Android builds (JDK 21 required)
if [ -z "$JAVA_HOME" ]; then
    if [ -d "/usr/lib/jvm/java-21-openjdk" ]; then
        export JAVA_HOME=/usr/lib/jvm/java-21-openjdk
        export PATH="$JAVA_HOME/bin:$PATH"
        echo "✓ Java environment configured: JDK 21"
    else
        echo "❌ Error: JDK 21 not found. Please install java-21-openjdk-devel"
        echo "   sudo dnf install -y java-21-openjdk-devel"
        exit 1
    fi
fi

# Verify Java version
JAVA_VERSION=$(java -version 2>&1 | head -1 | awk -F '"' '{print $2}')
echo "  Java Version: $JAVA_VERSION"
echo ""

# Define Projects to Build
MOBILE_UI_PROJECT="$ROOT/src/XerahS.Mobile.UI/XerahS.Mobile.UI.csproj"
MOBILE_ANDROID_PROJECT="$ROOT/src/XerahS.Mobile.Android/XerahS.Mobile.Android.csproj"
MOBILE_MAUI_PROJECT="$ROOT/src/XerahS.Mobile.Maui/XerahS.Mobile.Maui.csproj"

echo "=========================================="
echo "Building XerahS.Mobile.UI (Shared Library)"
echo "=========================================="
dotnet build "$MOBILE_UI_PROJECT" -c Release
if [ $? -eq 0 ]; then
    echo "✅ XerahS.Mobile.UI built successfully"
else
    echo "❌ XerahS.Mobile.UI build failed"
    exit 1
fi

echo ""
echo "=========================================="
echo "Building XerahS.Mobile.Android"
echo "=========================================="
dotnet build "$MOBILE_ANDROID_PROJECT" -c Release -f net10.0-android
if [ $? -eq 0 ]; then
    echo "✅ XerahS.Mobile.Android built successfully"
    
    # Copy APK to dist if it exists
    APK_SOURCE="$ROOT/src/XerahS.Mobile.Android/bin/Release/net10.0-android/com.sharexteam.xerahs-Signed.apk"
    if [ -f "$APK_SOURCE" ]; then
        cp "$APK_SOURCE" "$OUTPUT_DIR/XerahS-$VERSION-Android.apk"
        echo "   APK copied to: $OUTPUT_DIR/XerahS-$VERSION-Android.apk"
    fi
else
    echo "❌ XerahS.Mobile.Android build failed"
    exit 1
fi

echo ""
echo "=========================================="
echo "Building XerahS.Mobile.Maui (Android)"
echo "=========================================="
dotnet build "$MOBILE_MAUI_PROJECT" -c Release -f net10.0-android
if [ $? -eq 0 ]; then
    echo "✅ XerahS.Mobile.Maui (Android) built successfully"
    
    # Copy MAUI APK to dist if it exists
    MAUI_APK_SOURCE="$ROOT/src/XerahS.Mobile.Maui/bin/Release/net10.0-android/com.sharexteam.xerahs-Signed.apk"
    if [ -f "$MAUI_APK_SOURCE" ]; then
        cp "$MAUI_APK_SOURCE" "$OUTPUT_DIR/XerahS-$VERSION-MAUI-Android.apk"
        echo "   APK copied to: $OUTPUT_DIR/XerahS-$VERSION-MAUI-Android.apk"
    fi
else
    echo "❌ XerahS.Mobile.Maui build failed"
    exit 1
fi

echo ""
echo "=========================================="
echo "✅ All Android builds completed successfully!"
echo "=========================================="
echo "Output directory: $OUTPUT_DIR"
echo "Version: $VERSION"
echo ""
echo "Note: iOS builds require macOS and are skipped on Linux."
