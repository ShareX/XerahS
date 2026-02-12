#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
ROOT="$SCRIPT_DIR/../.."
PROJECT="$ROOT/src/XerahS.App/XerahS.App.csproj"
DIST_DIR="$ROOT/dist"
NATIVE_LIB="$ROOT/native/macos/libscreencapturekit_bridge.dylib"
ICON_SOURCE="$ROOT/src/XerahS.UI/Assets/Logo.icns"

mkdir -p "$DIST_DIR"

VERSION=$(dotnet msbuild "$ROOT/Directory.Build.props" -getProperty:Version | tr -d '[:space:]')
if [ -z "$VERSION" ]; then
    echo "Error: Failed to resolve version from Directory.Build.props"
    exit 1
fi

echo "Building XerahS version $VERSION for macOS..."

build_native_library() {
    if [[ "$OSTYPE" == darwin* ]]; then
        echo "Building native ScreenCaptureKit library..."
        (
            cd "$ROOT/native/macos"
            make clean 2>/dev/null || true
            make
        )

        if [ ! -f "$NATIVE_LIB" ]; then
            echo "Error: Failed to build native library at $NATIVE_LIB"
            exit 1
        fi

        echo "Native library built successfully"
        return
    fi

    if [ ! -f "$NATIVE_LIB" ]; then
        echo "Warning: Native library not found at: $NATIVE_LIB"
        echo "Warning: Screen capture functionality will not work!"
        echo "Warning: Build on macOS first to generate the native library, or copy it manually."
        exit 1
    fi

    echo "Using pre-compiled native library: $NATIVE_LIB"
    echo "(To rebuild native library, run package-mac.sh on macOS)"
}

configure_macos_bundle_icon() {
    local app_bundle_path="$1"
    local resources_dir="$app_bundle_path/Contents/Resources"
    local plist_path="$app_bundle_path/Contents/Info.plist"
    local python_exec=""
    local metadata_updated="false"

    if [ ! -f "$ICON_SOURCE" ]; then
        echo "Warning: Icon not found at $ICON_SOURCE. macOS app icon will be missing."
        return
    fi

    if [ ! -f "$plist_path" ]; then
        echo "Warning: Info.plist not found at $plist_path. macOS app icon will be missing."
        return
    fi

    mkdir -p "$resources_dir"
    cp -f "$ICON_SOURCE" "$resources_dir/Logo.icns"

    if [ -x "/usr/libexec/PlistBuddy" ]; then
        /usr/libexec/PlistBuddy -c "Set :CFBundleIconFile Logo" "$plist_path" >/dev/null 2>&1 || \
            /usr/libexec/PlistBuddy -c "Add :CFBundleIconFile string Logo" "$plist_path"
        /usr/libexec/PlistBuddy -c "Set :CFBundleIconName Logo" "$plist_path" >/dev/null 2>&1 || \
            /usr/libexec/PlistBuddy -c "Add :CFBundleIconName string Logo" "$plist_path"
        metadata_updated="true"
    else
        for candidate in python3 python; do
            if command -v "$candidate" >/dev/null 2>&1 && "$candidate" -c "import plistlib" >/dev/null 2>&1; then
                python_exec="$candidate"
                break
            fi
        done
    fi

    if [ -n "$python_exec" ]; then
        "$python_exec" - "$plist_path" <<'PY'
import plistlib
import sys
from pathlib import Path

plist_path = Path(sys.argv[1])
with plist_path.open("rb") as fp:
    data = plistlib.load(fp)

data["CFBundleIconFile"] = "Logo"
data["CFBundleIconName"] = "Logo"

with plist_path.open("wb") as fp:
    plistlib.dump(data, fp, sort_keys=False)
PY
        metadata_updated="true"
    else
        local plist_tmp="${plist_path}.tmp"
        for key in CFBundleIconFile CFBundleIconName; do
            awk -v key="$key" -v value="Logo" '
                BEGIN {
                    key_seen = 0;
                    replace_next = 0;
                }
                {
                    if (replace_next == 1 && $0 ~ /<string>.*<\/string>/) {
                        print "  <string>" value "</string>";
                        replace_next = 0;
                        next;
                    }

                    if ($0 ~ ("<key>" key "</key>")) {
                        key_seen = 1;
                        replace_next = 1;
                        print;
                        next;
                    }

                    if ($0 ~ /<\/dict>/ && key_seen == 0) {
                        print "  <key>" key "</key>";
                        print "  <string>" value "</string>";
                    }

                    print;
                }
            ' "$plist_path" > "$plist_tmp" && mv "$plist_tmp" "$plist_path"
        done
        metadata_updated="true"
    fi

    if [ "$metadata_updated" != "true" ]; then
        echo "Warning: Neither /usr/libexec/PlistBuddy nor Python was found. Icon metadata update skipped."
    fi

    echo "Configured macOS icon metadata for $app_bundle_path"
}

publish_and_package() {
    local arch="$1"
    local rid="osx-$arch"
    local publish_dir="$ROOT/src/XerahS.App/bin/Release/net10.0/$rid/publish"
    local app_bundle_path="$publish_dir/XerahS.app"
    local plugins_dir="$app_bundle_path/Contents/MacOS/Plugins"
    local tar_name="XerahS-$VERSION-mac-$arch.tar.gz"
    local tar_path="$DIST_DIR/$tar_name"

    echo "------------------------------------------------"
    echo "Building for $rid..."

    rm -rf "$publish_dir"

    dotnet publish "$PROJECT" \
        -c Release \
        -r "$rid" \
        -p:PublishSingleFile=false \
        --self-contained true \
        -p:nodeReuse=false \
        -p:SkipBundlePlugins=true

    if [ ! -d "$app_bundle_path" ]; then
        echo "Error: .app bundle not found at $app_bundle_path"
        exit 1
    fi

    configure_macos_bundle_icon "$app_bundle_path"

    echo "Publishing Plugins for $rid..."
    mkdir -p "$plugins_dir"

    while IFS= read -r -d '' plugin_project; do
        local plugin_dir plugin_name plugin_id plugin_out main_app_dir id_match
        plugin_dir=$(dirname "$plugin_project")
        plugin_name=$(basename "$plugin_project" .csproj)
        plugin_id="$plugin_name"

        if [ -f "$plugin_dir/plugin.json" ]; then
            id_match=$(grep -o '"pluginId"[[:space:]]*:[[:space:]]*"[^"]*"' "$plugin_dir/plugin.json" | cut -d '"' -f4 || true)
            if [ -n "$id_match" ]; then
                plugin_id="$id_match"
            fi
        fi

        echo "  Publishing $plugin_name ($plugin_id)..."
        plugin_out="$plugins_dir/$plugin_id"
        dotnet publish "$plugin_project" \
            -c Release \
            -r "$rid" \
            --self-contained true \
            -p:nodeReuse=false \
            -o "$plugin_out" >/dev/null

        main_app_dir="$app_bundle_path/Contents/MacOS"
        for f in "$plugin_out"/*; do
            if [ -f "$f" ]; then
                local file_name
                file_name=$(basename "$f")
                if [ -f "$main_app_dir/$file_name" ]; then
                    rm -f "$f"
                fi
            fi
        done
    done < <(find "$ROOT/src/Plugins" -name "*.csproj" -print0)

    echo "Creating archive: $tar_name"
    if tar --help 2>/dev/null | grep -q -- "--mode"; then
        tar -C "$publish_dir" --mode='a+rx,u+w' -czf "$tar_path" "XerahS.app"
    else
        tar -C "$publish_dir" -czf "$tar_path" "XerahS.app"
    fi

    echo "Success: Generated $tar_name in dist."
}

build_native_library
publish_and_package "arm64"
publish_and_package "x64"

echo "------------------------------------------------"
echo "Done! Packages in $DIST_DIR"
