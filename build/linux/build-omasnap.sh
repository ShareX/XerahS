#!/usr/bin/env bash
# Build the vendored OmaSnap capture engine (XIP0088) and stage it into a XerahS
# publish folder as:
#   <publish>/omasnap/omasnap
#   <publish>/omasnap/licenses/LICENSE-MIT   (OmaSnap, MIT)
#   <publish>/omasnap/licenses/LICENSE-OFL   (bundled fonts, SIL OFL 1.1)
#   <publish>/omasnap/licenses/LICENSE-ISC   (Lucide icons, ISC)
#
# OmaSnap is optional. When its sources, CMake/Ninja, Qt6 or LayerShellQt are missing,
# or any license notice cannot be found, this script prints a warning and exits 0
# without staging anything; XerahS then reports OmaSnap as absent at runtime and uses
# its existing capture chain.
#
# Usage: build-omasnap.sh <publish-dir> [omasnap-source-dir]
# Env:   OMASNAP_SOURCE_DIR   overrides the source folder (default: native/omasnap)
#        OMASNAP_REQUIRED=1   turn every soft failure into an error (CI jobs that must ship it)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

PUBLISH_DIR="${1:-}"
SOURCE_DIR="${2:-${OMASNAP_SOURCE_DIR:-$ROOT/native/omasnap}}"
REQUIRED="${OMASNAP_REQUIRED:-0}"

if [ -z "$PUBLISH_DIR" ]; then
    echo "Usage: $0 <publish-dir> [omasnap-source-dir]" >&2
    exit 2
fi

skip() {
    if [ "$REQUIRED" = "1" ]; then
        echo "Error: OmaSnap is required but cannot be built: $1" >&2
        exit 1
    fi

    echo "Warning: skipping OmaSnap: $1" >&2
    echo "         XerahS will be packaged without the OmaSnap capture engine." >&2
    exit 0
}

[ -f "$SOURCE_DIR/CMakeLists.txt" ] || skip "no OmaSnap sources at $SOURCE_DIR (run: git submodule update --init native/omasnap)"
command -v cmake >/dev/null 2>&1 || skip "cmake is not installed"
command -v ninja >/dev/null 2>&1 || skip "ninja is not installed"
command -v pkg-config >/dev/null 2>&1 || skip "pkg-config is not installed"
pkg-config --exists Qt6Core Qt6Gui Qt6Widgets 2>/dev/null || skip "Qt6 development files (qt6-base) are not installed"
pkg-config --exists wayland-client 2>/dev/null || skip "wayland-client development files are not installed"
pkg-config --exists LayerShellQtInterface 2>/dev/null \
    || [ -d /usr/lib/cmake/LayerShellQt ] || [ -d /usr/lib64/cmake/LayerShellQt ] \
    || skip "LayerShellQt (layer-shell-qt) is not installed"

# Locate the three notices before building so a missing one never ships a binary alone.
find_notice() {
    local pattern
    for pattern in "$@"; do
        local match
        match="$(find "$SOURCE_DIR" -path "$SOURCE_DIR/build" -prune -o -type f -iname "$pattern" -print 2>/dev/null | sort | head -n 1)"
        if [ -n "$match" ]; then
            printf '%s' "$match"
            return 0
        fi
    done
    return 1
}

MIT_NOTICE="$(find_notice 'LICENSE' 'LICENSE.md' 'LICENSE.txt' 'COPYING')" || skip "OmaSnap MIT license file not found"
OFL_NOTICE="$(find_notice 'OFL.txt' 'OFL*.txt' '*OFL*' )" || skip "font OFL license not found"
ISC_NOTICE="$(find_notice 'LICENSE-lucide*' '*lucide*LICENSE*' 'lucide*.txt' 'ISC*')" || skip "Lucide ISC license not found"

# OMASNAP_BUILD_DIR keeps the CMake build folder between runs so unchanged sources are not
# recompiled (local packaging sets it; CI leaves it unset and builds in a fresh temp folder).
STAGING_DIR="$(mktemp -d)"
if [ -n "${OMASNAP_BUILD_DIR:-}" ]; then
    BUILD_DIR="$OMASNAP_BUILD_DIR"
    mkdir -p "$BUILD_DIR"
    trap 'rm -rf "$STAGING_DIR"' EXIT
else
    BUILD_DIR="$(mktemp -d)"
    trap 'rm -rf "$BUILD_DIR" "$STAGING_DIR"' EXIT
fi

echo "Building OmaSnap from $SOURCE_DIR..."
if ! cmake -S "$SOURCE_DIR" -B "$BUILD_DIR" -G Ninja \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_INSTALL_PREFIX="$STAGING_DIR"; then
    skip "CMake configure failed"
fi

if ! cmake --build "$BUILD_DIR"; then
    skip "OmaSnap build failed"
fi

if ! cmake --install "$BUILD_DIR"; then
    skip "OmaSnap install step failed"
fi

BINARY="$(find "$STAGING_DIR" -type f -name omasnap -perm -u+x | head -n 1)"
[ -n "$BINARY" ] || BINARY="$(find "$BUILD_DIR" -maxdepth 2 -type f -name omasnap -perm -u+x | head -n 1)"
[ -n "$BINARY" ] || skip "build produced no omasnap executable"

TARGET_DIR="$PUBLISH_DIR/omasnap"
rm -rf "$TARGET_DIR"
install -d "$TARGET_DIR/licenses"
install -m 755 "$BINARY" "$TARGET_DIR/omasnap"
install -m 644 "$MIT_NOTICE" "$TARGET_DIR/licenses/LICENSE-MIT"
install -m 644 "$OFL_NOTICE" "$TARGET_DIR/licenses/LICENSE-OFL"
install -m 644 "$ISC_NOTICE" "$TARGET_DIR/licenses/LICENSE-ISC"

# Shared data (fonts, icons) installed next to the binary by CMake, if any.
if [ -d "$STAGING_DIR/share/omasnap" ]; then
    cp -a "$STAGING_DIR/share/omasnap" "$TARGET_DIR/share"
fi

echo "OmaSnap staged at $TARGET_DIR"
"$TARGET_DIR/omasnap" --version 2>/dev/null || true
