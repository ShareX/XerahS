#!/bin/bash
set -e

# Configuration
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
ROOT="$SCRIPT_DIR/../.."
PROJECT="$ROOT/src/XerahS.App/XerahS.App.csproj"
UI_PROJECT="$ROOT/src/XerahS.UI/XerahS.UI.csproj"
DIST_DIR="$ROOT/dist"

mkdir -p "$DIST_DIR"

# extract version from Directory.Build.props
VERSION=$(grep '<Version>' "$ROOT/Directory.Build.props" | sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' | tr -d '[:space:]')
echo "Building XerahS version $VERSION for macOS..."

publish_and_package() {
    ARCH=$1
    RID="osx-$ARCH"
    echo "------------------------------------------------"
    echo "Processing $RID..."
    
    # Clean previous publish
    PUBLISH_DIR="$ROOT/src/XerahS.App/bin/Release/net10.0/$RID/publish"
    rm -rf "$PUBLISH_DIR"
    
    # 1. Publish (triggers CreateMacOSAppBundle target in csproj)
    dotnet publish "$PROJECT" -c Release -r "$RID" -p:PublishSingleFile=false --self-contained true

    APP_BUNDLE_PATH="$PUBLISH_DIR/XerahS.app"
    if [ ! -d "$APP_BUNDLE_PATH" ]; then
        echo "Error: .app bundle not found at $APP_BUNDLE_PATH"
        exit 1
    fi

    # 1.5 Publish Plugins
    # Assuming plugins should be next to the executable in the bundle
    PLUGINS_DIR="$APP_BUNDLE_PATH/Contents/MacOS/Plugins"
    mkdir -p "$PLUGINS_DIR"
    
    echo "Publishing Plugins for $RID..."
    find "$ROOT/src/Plugins" -name "*.csproj" | while read PLUGIN_PROJECT; do
        PLUGIN_DIR=$(dirname "$PLUGIN_PROJECT")
        PLUGIN_NAME=$(basename "$PLUGIN_PROJECT" .csproj)
        
        # Try to find ID
        PLUGIN_ID="$PLUGIN_NAME"
        if [ -f "$PLUGIN_DIR/plugin.json" ]; then
             # Simple grep to extract value. 
             # "pluginId": "amazons3" -> amazons3
             ID_MATCH=$(grep -o '"pluginId"[[:space:]]*:[[:space:]]*"[^"]*"' "$PLUGIN_DIR/plugin.json" | cut -d'"' -f4)
             if [ ! -z "$ID_MATCH" ]; then
                 PLUGIN_ID="$ID_MATCH"
             fi
        fi
        
        echo "  Publishing $PLUGIN_NAME ($PLUGIN_ID)..."
        PLUGIN_OUT="$PLUGINS_DIR/$PLUGIN_ID"
        dotnet publish "$PLUGIN_PROJECT" -c Release -r "$RID" --self-contained true -o "$PLUGIN_OUT" > /dev/null

        # Deduplication
        # Main app files are in Contents/MacOS
        MAIN_APP_DIR="$APP_BUNDLE_PATH/Contents/MacOS"
        
        # Iterate files in plugin out
        for f in "$PLUGIN_OUT"/*; do
            if [ -f "$f" ]; then
                FNAME=$(basename "$f")
                if [ -f "$MAIN_APP_DIR/$FNAME" ]; then
                    rm "$f"
                fi
            fi
        done
    done

    # 2. Package
    
    TAR_NAME="XerahS-$VERSION-mac-$ARCH.tar.gz"
    TAR_PATH="$DIST_DIR/$TAR_NAME"
    
    echo "Creating archive: $TAR_PATH"
    # tar -C [dir] -czf [archive] [file] to avoid including full path
    tar -C "$PUBLISH_DIR" -czf "$TAR_PATH" "XerahS.app"
}

# Build for Apple Silicon (arm64)
publish_and_package "arm64"

# Build for Intel (x64)
publish_and_package "x64"

echo "------------------------------------------------"
echo "Done! Packages in $DIST_DIR"

