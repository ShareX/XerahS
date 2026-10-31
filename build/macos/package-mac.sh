#!/bin/bash
set -euo pipefail

# Builds stay serial (-m:1, BuildInParallel=false) to avoid shared-output races, but the Roslyn
# compiler server stays on: it only reuses the compiler process, and cold-starting csc for every
# project roughly doubled build time. The MSBuild server remains off, as before.
export DOTNET_CLI_USE_MSBUILD_SERVER=0

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
ROOT="$SCRIPT_DIR/../.."
PROJECT="$ROOT/src/desktop/app/XerahS.App/XerahS.App.csproj"
DIST_DIR="$ROOT/dist"
NATIVE_LIB="$ROOT/native/macos/libscreencapturekit_bridge.dylib"
ICON_SOURCE="$ROOT/src/desktop/app/XerahS.UI/Assets/Logo.icns"
ENTITLEMENTS="$SCRIPT_DIR/entitlements.plist"

# XIP0078 P2 signing controls:
#   MACOS_SIGN_IDENTITY   Developer ID Application identity. When set, the bundle is signed
#                         with hardened runtime + entitlements and a DMG is produced.
#   MACOS_NOTARY_PROFILE  notarytool keychain profile name. When set together with
#                         MACOS_SIGN_IDENTITY, the DMG is notarized and stapled.
#   MACOS_SKIP_SIGNING=1  Skip signing entirely (pre-XIP0078 behavior).
# Without MACOS_SIGN_IDENTITY the bundle is ad-hoc signed (interim step: no Gatekeeper
# benefit, but a sealed bundle for from-source users; harmless on arm64 where the linker
# ad-hoc signs everything anyway).

mkdir -p "$DIST_DIR"

VERSION=$(dotnet msbuild "$ROOT/Directory.Build.props" -getProperty:Version | tr -d '[:space:]')
if [ -z "$VERSION" ]; then
    echo "Error: Failed to resolve version from Directory.Build.props"
    exit 1
fi

echo "Building XerahS version $VERSION for macOS..."

restore_project_assets_for_os() {
    local project_path="$1"
    local os_value="$2"

    dotnet restore "$project_path" \
        -p:OS="$os_value" \
        -p:nodeReuse=false \
        -p:BuildInParallel=false \
        -m:1
}

restore_scoped_intermediate_assets() {
    local image_editor_project="$ROOT/ShareX.ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj"
    local ui_project="$ROOT/src/desktop/app/XerahS.UI/XerahS.UI.csproj"

    if [ ! -f "$image_editor_project" ]; then
        echo "Error: ShareX.ImageEditor project not found: $image_editor_project"
        exit 1
    fi
    if [ ! -f "$ui_project" ]; then
        echo "Error: XerahS.UI project not found: $ui_project"
        exit 1
    fi

    echo "Restoring scoped intermediate assets for macOS packaging..."
    restore_project_assets_for_os "$image_editor_project" "Unix"
    restore_project_assets_for_os "$ui_project" "Unix"
}

dotnet_publish_serial() {
    dotnet publish "$@" \
        -p:nodeReuse=false \
        -p:BuildInParallel=false \
        -m:1
}

validate_daemon_bundle() {
    local app_bundle_path="$1"
    local daemon_path="$app_bundle_path/Contents/MacOS/xerahs-watchfolder-daemon"

    if [ ! -f "$daemon_path" ]; then
        echo "Error: Missing daemon executable in app bundle: $daemon_path"
        exit 1
    fi

    # .NET 10+ single-file publish embeds runtimeconfig; no sidecar is written
    # to the .app bundle. See XerahS.App.csproj PublishWatchFolderDaemon for the
    # matching logic on the build side.
}

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

restore_scoped_intermediate_assets

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

sign_file() {
    local identity="$1"
    local file="$2"

    if [ "$identity" = "-" ]; then
        codesign --force -s - "$file" >/dev/null 2>&1 || \
            echo "Warning: ad-hoc signing failed for $file"
    else
        codesign --force --options runtime --timestamp -s "$identity" "$file"
    fi
}

sign_app_bundle() {
    local app_bundle_path="$1"

    if [[ "$OSTYPE" != darwin* ]]; then
        echo "Skipping code signing (not running on macOS)."
        return
    fi

    if [ "${MACOS_SKIP_SIGNING:-0}" = "1" ]; then
        echo "Skipping code signing (MACOS_SKIP_SIGNING=1)."
        return
    fi

    local identity="${MACOS_SIGN_IDENTITY:--}"
    if [ "$identity" = "-" ]; then
        echo "Ad-hoc signing app bundle (set MACOS_SIGN_IDENTITY for Developer ID signing)..."
    else
        echo "Signing app bundle with identity '$identity' (hardened runtime + entitlements)..."
        if [ ! -f "$ENTITLEMENTS" ]; then
            echo "Error: entitlements file not found: $ENTITLEMENTS"
            exit 1
        fi
    fi

    # Clear quarantine/xattrs that can break nested codesign on CI runners.
    xattr -cr "$app_bundle_path" 2>/dev/null || true

    # Sign innermost-first: every nested Mach-O (dylibs, apphost executables, daemon,
    # plugin native deps), then the bundle itself. Managed PE assemblies are not
    # signable by codesign and are skipped via the file(1) check.
    while IFS= read -r -d '' candidate; do
        if file -b "$candidate" 2>/dev/null | grep -q 'Mach-O'; then
            sign_file "$identity" "$candidate"
        fi
    done < <(find "$app_bundle_path/Contents/MacOS" -type f -print0)

    if [ "$identity" = "-" ]; then
        # .NET publish layouts mix Mach-O hosts with PE assemblies. Ad-hoc signing must
        # use --deep so codesign does not hard-fail on unsigned nested managed DLLs
        # (seen on macos-15 with System.Diagnostics.Contracts.dll and similar).
        # Ad-hoc signatures have no Gatekeeper value anyway; do not fail the release
        # matrix when interim ad-hoc sealing is imperfect.
        if ! codesign --force --deep -s - "$app_bundle_path"; then
            echo "Warning: ad-hoc deep sign failed; retrying without --deep..."
            codesign --force -s - "$app_bundle_path" || \
                echo "Warning: ad-hoc app bundle signing failed; continuing with unsigned interim bundle."
        fi
        if codesign --verify "$app_bundle_path" >/dev/null 2>&1; then
            echo "Ad-hoc code signature verified for $app_bundle_path"
        else
            echo "Warning: ad-hoc signature verify failed for $app_bundle_path; continuing."
        fi
        return 0
    fi

    codesign --force --options runtime --timestamp \
        --entitlements "$ENTITLEMENTS" \
        -s "$identity" "$app_bundle_path"

    codesign --verify --strict "$app_bundle_path"
    echo "Code signature verified for $app_bundle_path"
}

create_dmg_and_notarize() {
    local app_bundle_path="$1"
    local arch="$2"

    if [[ "$OSTYPE" != darwin* ]] || [ -z "${MACOS_SIGN_IDENTITY:-}" ] || [ "${MACOS_SKIP_SIGNING:-0}" = "1" ]; then
        return
    fi

    local dmg_path="$DIST_DIR/XerahS-$VERSION-mac-$arch.dmg"
    echo "Creating DMG: $(basename "$dmg_path")"
    hdiutil create -volname "XerahS" -srcfolder "$app_bundle_path" -ov -format UDZO "$dmg_path"

    if [ -n "${MACOS_NOTARY_PROFILE:-}" ]; then
        echo "Submitting DMG for notarization (profile: $MACOS_NOTARY_PROFILE)..."
        xcrun notarytool submit "$dmg_path" --keychain-profile "$MACOS_NOTARY_PROFILE" --wait
        xcrun stapler staple "$dmg_path"
        echo "Notarized and stapled $(basename "$dmg_path")"
    else
        echo "MACOS_NOTARY_PROFILE not set; DMG created but not notarized."
    fi
}

publish_and_package() {
    local arch="$1"
    local rid="osx-$arch"
    local publish_dir="$ROOT/src/desktop/app/XerahS.App/bin/Release/net10.0/$rid/publish"
    local app_bundle_path="$publish_dir/XerahS.app"
    local plugins_dir="$app_bundle_path/Contents/MacOS/Plugins"
    local tar_name="XerahS-$VERSION-mac-$arch.tar.gz"
    local tar_path="$DIST_DIR/$tar_name"

    echo "------------------------------------------------"
    echo "Building for $rid..."

    # Ensure compiler/build servers from prior arch are not holding files.
    dotnet build-server shutdown >/dev/null 2>&1 || true
    rm -rf "$publish_dir"

    dotnet_publish_serial "$PROJECT" \
        -c Release \
        -r "$rid" \
        -p:PublishSingleFile=false \
        --self-contained true \
        -p:SkipBundlePlugins=true

    if [ ! -d "$app_bundle_path" ]; then
        echo "Error: .app bundle not found at $app_bundle_path"
        exit 1
    fi

    validate_daemon_bundle "$app_bundle_path"

    configure_macos_bundle_icon "$app_bundle_path"

    echo "Publishing Plugins for $rid..."
    mkdir -p "$plugins_dir"

    local plugin_count=0
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
        rm -rf "$plugin_out"
        mkdir -p "$plugin_out"
        dotnet_publish_serial "$plugin_project" \
            -c Release \
            -r "$rid" \
            --no-self-contained \
            -p:PublishSingleFile=false \
            -o "$plugin_out" >/dev/null

        if [ ! -f "$plugin_out/plugin.json" ] && [ -f "$plugin_dir/plugin.json" ]; then
            cp "$plugin_dir/plugin.json" "$plugin_out/plugin.json"
        fi

        if [ ! -f "$plugin_out/plugin.json" ]; then
            echo "Error: plugin.json missing for plugin '$plugin_id' in $plugin_out"
            exit 1
        fi

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

        plugin_count=$((plugin_count + 1))
    done < <(find "$ROOT/src/desktop/plugins" -name "*.csproj" -print0)

    if [ "$plugin_count" -eq 0 ]; then
        echo "Error: No plugins were published for $rid"
        exit 1
    fi

    if ! find "$plugins_dir" -mindepth 2 -maxdepth 2 -name "plugin.json" | grep -q .; then
        echo "Error: No plugin manifests found under $plugins_dir after publish."
        exit 1
    fi

    dotnet build-server shutdown >/dev/null 2>&1 || true

    echo "Published $plugin_count plugins to startup Plugins folder: $plugins_dir"

    sign_app_bundle "$app_bundle_path"

    echo "Creating archive: $tar_name"
    if tar --help 2>/dev/null | grep -q -- "--mode"; then
        tar -C "$publish_dir" --mode='a+rx,u+w' -czf "$tar_path" "XerahS.app"
    else
        tar -C "$publish_dir" -czf "$tar_path" "XerahS.app"
    fi

    create_dmg_and_notarize "$app_bundle_path" "$arch"

    echo "Success: Generated $tar_name in dist."
}

build_native_library
publish_and_package "arm64"
publish_and_package "x64"

echo "------------------------------------------------"
echo "Done! Packages in $DIST_DIR"
