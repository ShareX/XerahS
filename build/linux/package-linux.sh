#!/bin/bash
set -euo pipefail

# Configuration
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
ROOT="$SCRIPT_DIR/../.."
PROJECT="$ROOT/src/desktop/app/XerahS.App/XerahS.App.csproj"
OUTPUT_DIR="$ROOT/dist"

if [ ! -d "$OUTPUT_DIR" ]; then
    mkdir -p "$OUTPUT_DIR"
fi

# Get Version from Directory.Build.props
VERSION=$(grep '<Version>' "$ROOT/Directory.Build.props" | sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' | tr -d '[:space:]')
echo "Building XerahS version $VERSION for Linux..."

# Define Architectures to Build
ARCHITECTURES=("linux-x64" "linux-arm64")

for ARCH in "${ARCHITECTURES[@]}"; do
    echo ""
    echo "=========================================="
    echo "Building for Architecture: $ARCH"
    echo "=========================================="
    
    # 1. Clean & Publish
    PUBLISH_DIR="$ROOT/src/desktop/app/XerahS.App/bin/Release/net10.0/$ARCH/publish"
    
    if [ -d "$PUBLISH_DIR" ]; then
        rm -rf "$PUBLISH_DIR"
    fi

    echo "Running dotnet publish ($ARCH)..."
    dotnet publish "$PROJECT" -c Release -r "$ARCH" -p:OS=Linux -p:DefineConstants=LINUX -p:PublishSingleFile=true --self-contained true -p:EnableWindowsTargeting=true

    # 1.5 Publish Plugins
    echo "Publishing Plugins ($ARCH)..."
    PLUGINS_DIR="$PUBLISH_DIR/Plugins"
    mkdir -p "$PLUGINS_DIR"

    PLUGIN_COUNT=0
    while IFS= read -r -d '' PLUGIN_PROJECT; do
        PLUGIN_DIR=$(dirname "$PLUGIN_PROJECT")
        PLUGIN_NAME=$(basename "$PLUGIN_PROJECT" .csproj)

        # Determine plugin ID from plugin.json when available
        PLUGIN_ID="$PLUGIN_NAME"
        if [ -f "$PLUGIN_DIR/plugin.json" ]; then
            ID_MATCH=$(grep -o '"pluginId"[[:space:]]*:[[:space:]]*"[^"]*"' "$PLUGIN_DIR/plugin.json" | cut -d'"' -f4 || true)
            if [ -n "${ID_MATCH:-}" ]; then
                PLUGIN_ID="$ID_MATCH"
            fi
        fi

        echo "  Publishing Plugin: $PLUGIN_NAME ($PLUGIN_ID) for $ARCH"
        PLUGIN_OUTPUT="$PLUGINS_DIR/$PLUGIN_ID"
        rm -rf "$PLUGIN_OUTPUT"
        mkdir -p "$PLUGIN_OUTPUT"

        dotnet publish "$PLUGIN_PROJECT" \
            -c Release \
            -r "$ARCH" \
            -p:OS=Linux \
            -o "$PLUGIN_OUTPUT" \
            --no-self-contained \
            -p:PublishSingleFile=false \
            -p:EnableWindowsTargeting=true > /dev/null

        # Ensure plugin.json exists for runtime discovery
        if [ ! -f "$PLUGIN_OUTPUT/plugin.json" ] && [ -f "$PLUGIN_DIR/plugin.json" ]; then
            cp "$PLUGIN_DIR/plugin.json" "$PLUGIN_OUTPUT/plugin.json"
        fi

        # Cleanup: Remove files that already exist in the main app directory (deduplication)
        for f in "$PLUGIN_OUTPUT"/*; do
            if [ -f "$f" ]; then
                FNAME=$(basename "$f")
                if [ -f "$PUBLISH_DIR/$FNAME" ]; then
                    rm "$f"
                fi
            fi
        done

        if [ ! -f "$PLUGIN_OUTPUT/plugin.json" ]; then
            echo "Error: plugin.json missing for plugin '$PLUGIN_ID' in $PLUGIN_OUTPUT"
            exit 1
        fi

        PLUGIN_COUNT=$((PLUGIN_COUNT + 1))
    done < <(find "$ROOT/src/desktop/plugins" -mindepth 2 -maxdepth 2 -name "*.csproj" -print0)

    if [ "$PLUGIN_COUNT" -eq 0 ]; then
        echo "Error: No plugins were published for $ARCH."
        exit 1
    fi

    if ! find "$PLUGINS_DIR" -mindepth 2 -maxdepth 2 -name "plugin.json" | grep -q .; then
        echo "Error: No plugin manifests found under $PLUGINS_DIR after publish."
        exit 1
    fi

    echo "Published $PLUGIN_COUNT plugins to startup Plugins folder: $PLUGINS_DIR"

    # 2. Package
    echo "Packaging ($ARCH)..."
    echo "Note: rpmbuild is required to produce RPM packages."
    PACKAGING_TOOL="$ROOT/build/linux/XerahS.Packaging/XerahS.Packaging.csproj"
    dotnet run --project "$PACKAGING_TOOL" -- "$PUBLISH_DIR" "$OUTPUT_DIR" "$VERSION" "$ARCH"
done

echo ""
echo "Done! All packages in $OUTPUT_DIR"
