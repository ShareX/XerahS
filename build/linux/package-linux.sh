#!/bin/bash
set -e

# Configuration
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
ROOT="$SCRIPT_DIR/../.."
PROJECT="$ROOT/src/XerahS.App/XerahS.App.csproj"
OUTPUT_DIR="$ROOT/dist"

if [ ! -d "$OUTPUT_DIR" ]; then
    mkdir -p "$OUTPUT_DIR"
fi

# Get Version from Directory.Build.props
VERSION=$(grep '<Version>' "$ROOT/Directory.Build.props" | sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' | tr -d '[:space:]')
echo "Building XerahS version $VERSION for Linux..."

# 1. Clean & Publish
PUBLISH_DIR="$ROOT/src/XerahS.App/bin/Release/net10.0/linux-x64/publish"
if [ -d "$PUBLISH_DIR" ]; then
    rm -rf "$PUBLISH_DIR"
fi

echo "Running dotnet publish..."
dotnet publish "$PROJECT" -c Release -r linux-x64 -p:OS=Linux -p:DefineConstants=LINUX -p:PublishSingleFile=true --self-contained true

# 1.5 Publish Plugins
echo "Publishing Plugins..."
PLUGINS_DIR="$PUBLISH_DIR/Plugins"
mkdir -p "$PLUGINS_DIR"

find "$ROOT/src/Plugins" -maxdepth 1 -mindepth 1 -type d | while read PLUGIN_DIR; do
    PLUGIN_PROJECT=$(find "$PLUGIN_DIR" -name "*.csproj" | head -n 1)
    if [ -z "$PLUGIN_PROJECT" ]; then
        continue
    fi
    
    PLUGIN_NAME=$(basename "$PLUGIN_PROJECT" .csproj)
    
    # Try to determine plugin ID from plugin.json
    PLUGIN_ID="$PLUGIN_NAME"
    if [ -f "$PLUGIN_DIR/plugin.json" ]; then
        ID_MATCH=$(grep -o '"pluginId"[[:space:]]*:[[:space:]]*"[^"]*"' "$PLUGIN_DIR/plugin.json" | cut -d'"' -f4)
        if [ ! -z "$ID_MATCH" ]; then
            PLUGIN_ID="$ID_MATCH"
        fi
    fi

    echo "  Publishing Plugin: $PLUGIN_NAME ($PLUGIN_ID)"
    PLUGIN_OUTPUT="$PLUGINS_DIR/$PLUGIN_ID"
    dotnet publish "$PLUGIN_PROJECT" -c Release -r linux-x64 -p:OS=Linux -o "$PLUGIN_OUTPUT" --no-self-contained > /dev/null

    # Cleanup: Remove files that already exist in the main app directory (deduplication)
    for f in "$PLUGIN_OUTPUT"/*; do
        if [ -f "$f" ]; then
            FNAME=$(basename "$f")
            if [ -f "$PUBLISH_DIR/$FNAME" ]; then
                rm "$f"
            fi
        fi
    done
done

# 2. Package
echo "Packaging..."
echo "Note: rpmbuild is required to produce RPM packages."
PACKAGING_TOOL="$ROOT/build/linux/XerahS.Packaging/XerahS.Packaging.csproj"
dotnet run --project "$PACKAGING_TOOL" -- "$PUBLISH_DIR" "$OUTPUT_DIR" "$VERSION" "linux-x64"

echo "Done! Packages in $OUTPUT_DIR"


