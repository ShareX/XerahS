#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: prepare-flathub-source-build.sh [options]

Generate a Flathub source-build manifest candidate from a XerahS release tag
and check the source/dependency prerequisites needed before manual Flathub
submission.

Options:
  --tag <vX.Y.Z>          Release tag to use (default: v<Directory.Build.props Version>)
  --repo <owner/name>     GitHub repository (default: resolved from origin)
  --output <path>         Output manifest path (default: dist/flathub/com.xerahs.XerahS.yml)
  --skip-deps             Do not generate NuGet dependency source files
  --lint                  Run flatpak-builder-lint manifest on the generated manifest
  -h, --help              Show this help

This script does not open or automate a Flathub pull request.
USAGE
}

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Error: required command not found: $1" >&2
    exit 1
  fi
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
# shellcheck source=resolve-github-repo.sh
source "$SCRIPT_DIR/resolve-github-repo.sh"

resolve_version_from_props() {
  local version_file="$1"
  awk '
    {
      if ($0 ~ /<Version>[[:space:]]*[0-9]+\.[0-9]+\.[0-9]+[[:space:]]*<\/Version>/) {
        value = $0
        sub(/^.*<Version>[[:space:]]*/, "", value)
        sub(/[[:space:]]*<\/Version>.*$/, "", value)
        print value
        exit
      }
    }
  ' "$version_file" | tr -d '[:space:]'
}

ensure_local_tag_object() {
  local tag_name="$1"

  if git rev-parse -q --verify "refs/tags/${tag_name}^{commit}" >/dev/null; then
    return 0
  fi

  echo "Fetching release tag metadata for $tag_name..."
  git fetch --depth=1 origin "refs/tags/${tag_name}:refs/tags/${tag_name}"
}

resolve_tree_commit() {
  local tag_name="$1"
  local path="$2"
  local commit

  commit="$(git ls-tree "$tag_name" "$path" | awk '{ print $3 }' | tr -d '[:space:]')"
  if [[ -z "$commit" ]]; then
    echo "Error: failed to resolve submodule commit for $path at $tag_name" >&2
    exit 1
  fi

  echo "$commit"
}

resolve_submodule_url() {
  local name="$1"
  local url

  url="$(git config -f .gitmodules --get "submodule.${name}.url" || true)"
  if [[ -z "$url" ]]; then
    echo "Error: failed to resolve submodule URL for $name from .gitmodules" >&2
    exit 1
  fi

  case "$url" in
    git@github.com:*)
      url="https://github.com/${url#git@github.com:}"
      ;;
  esac
  url="${url%.git}.git"
  echo "$url"
}

ensure_commit_available() {
  local repo_path="$1"
  local remote_url="$2"
  local commit="$3"
  local label="$4"

  if git -C "$repo_path" cat-file -e "${commit}^{commit}" 2>/dev/null; then
    return 0
  fi

  echo "Fetching $label source commit $commit..."
  git -C "$repo_path" fetch --depth=1 "$remote_url" "$commit"
}

archive_git_tree() {
  local repo_path="$1"
  local commit="$2"
  local destination="$3"

  mkdir -p "$destination"
  git -C "$repo_path" archive "$commit" | tar -x -C "$destination"
}

create_release_snapshot() {
  local snapshot_dir="$1"

  rm -rf "$snapshot_dir"
  mkdir -p "$snapshot_dir"

  archive_git_tree "$repo_root" "$main_commit" "$snapshot_dir"
  archive_git_tree "$repo_root/ShareX.ImageEditor" "$image_editor_commit" "$snapshot_dir/ShareX.ImageEditor"
  archive_git_tree "$repo_root/Omacut" "$omacut_commit" "$snapshot_dir/Omacut"
}

generate_nuget_sources() {
  local snapshot_dir="$1"
  local output_file="$2"
  local generator_path="$snapshot_dir/.flathub-tools/flatpak-dotnet-generator.py"
  local partial_dir="$snapshot_dir/.flathub-tools/nuget-partials"
  local source_count
  local runtime
  local os_value
  local partial_file
  local image_editor_project="$snapshot_dir/ShareX.ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj"
  local ui_project="$snapshot_dir/src/desktop/app/XerahS.UI/XerahS.UI.csproj"
  local -a projects=(
    "$snapshot_dir/src/desktop/app/XerahS.App/XerahS.App.csproj"
    "$snapshot_dir/build/linux/XerahS.Packaging/XerahS.Packaging.csproj"
  )
  local plugin_project

  while IFS= read -r plugin_project; do
    projects+=("$plugin_project")
  done < <(find "$snapshot_dir/src/desktop/plugins" -mindepth 2 -maxdepth 2 -name "*.csproj" | sort)

  mkdir -p "$(dirname "$generator_path")"
  mkdir -p "$partial_dir"
  curl -fsSL \
    https://raw.githubusercontent.com/flatpak/flatpak-builder-tools/master/dotnet/flatpak-dotnet-generator.py \
    -o "$generator_path"

  echo "Generating NuGet dependency sources for Linux publish projects..."
  for runtime in linux-x64 linux-arm64; do
    for os_value in Unix Linux; do
      partial_file="$partial_dir/ShareX.ImageEditor-$os_value-$runtime.json"
      (
        cd "$snapshot_dir"
        python3 "$generator_path" \
          --dotnet 10 \
          --freedesktop 25.08 \
          --runtime "$runtime" \
          --destdir nuget-sources \
          "$partial_file" \
          "$image_editor_project" \
          --dotnet-args \
          -p:OS="$os_value" \
          -p:DefineConstants=LINUX \
          -p:EnableWindowsTargeting=true \
          -p:SelfContained=true \
          -p:PublishSingleFile=true \
          -p:RuntimeIdentifier="$runtime" \
          -p:RuntimeIdentifiers="$runtime" \
          -p:UseSharedCompilation=false \
          -p:BuildInParallel=false \
          -p:nodeReuse=false \
          -m:1 \
          --disable-build-servers
      )
    done
  done

  for runtime in linux-x64 linux-arm64; do
    for os_value in Unix Linux; do
      partial_file="$partial_dir/ShareX.ImageEditor-$os_value-$runtime-framework-dependent.json"
      (
        cd "$snapshot_dir"
        python3 "$generator_path" \
          --dotnet 10 \
          --freedesktop 25.08 \
          --runtime "$runtime" \
          --destdir nuget-sources \
          "$partial_file" \
          "$image_editor_project" \
          --dotnet-args \
          -p:OS="$os_value" \
          -p:DefineConstants=LINUX \
          -p:EnableWindowsTargeting=true \
          -p:RuntimeIdentifier="$runtime" \
          -p:RuntimeIdentifiers="$runtime" \
          -p:UseSharedCompilation=false \
          -p:BuildInParallel=false \
          -p:nodeReuse=false \
          -m:1 \
          --disable-build-servers
      )
    done
  done

  for runtime in linux-x64 linux-arm64; do
    partial_file="$partial_dir/XerahS.UI-Linux-$runtime.json"
    (
      cd "$snapshot_dir"
      python3 "$generator_path" \
        --dotnet 10 \
        --freedesktop 25.08 \
        --runtime "$runtime" \
        --destdir nuget-sources \
        "$partial_file" \
        "$ui_project" \
        --dotnet-args \
        -p:OS=Linux \
        -p:DefineConstants=LINUX \
        -p:EnableWindowsTargeting=true \
        -p:RuntimeIdentifier="$runtime" \
        -p:RuntimeIdentifiers="$runtime" \
        -p:UseSharedCompilation=false \
        -p:BuildInParallel=false \
        -p:nodeReuse=false \
        -m:1 \
        --disable-build-servers
    )
  done

  for plugin_project in "${projects[@]}"; do
    for runtime in linux-x64 linux-arm64; do
      partial_file="$partial_dir/$(basename "${plugin_project%.csproj}")-$runtime.json"
      (
        cd "$snapshot_dir"
        python3 "$generator_path" \
          --dotnet 10 \
          --freedesktop 25.08 \
          --runtime "$runtime" \
          --destdir nuget-sources \
          "$partial_file" \
          "$plugin_project" \
          --dotnet-args \
          -p:OS=Linux \
          -p:DefineConstants=LINUX \
          -p:EnableWindowsTargeting=true \
          -p:SelfContained=true \
          -p:PublishSingleFile=true \
          -p:RuntimeIdentifier="$runtime" \
          -p:RuntimeIdentifiers="$runtime" \
          -p:UseSharedCompilation=false \
          -p:BuildInParallel=false \
          -p:nodeReuse=false \
          -m:1 \
          --disable-build-servers
      )
    done
  done

  python3 - "$output_file" "$partial_dir"/*.json <<'PY'
import json
import sys

output = sys.argv[1]
entries = {}

for path in sys.argv[2:]:
    with open(path, encoding="utf-8") as handle:
        for item in json.load(handle):
            key = (
                item.get("type"),
                item.get("url"),
                item.get("sha512"),
                item.get("dest"),
                item.get("dest-filename"),
            )
            entries[key] = item

merged = sorted(entries.values(), key=lambda item: item.get("dest-filename", ""))
with open(output, "w", encoding="utf-8") as handle:
    json.dump(merged, handle, indent=4)
PY

  source_count="$(python3 -c 'import json,sys; print(len(json.load(open(sys.argv[1], encoding="utf-8"))))' "$output_file")"
  if [[ "$source_count" == "0" ]]; then
    echo "Error: NuGet dependency source generation produced zero entries." >&2
    exit 1
  fi
}

TAG_NAME=""
GH_TARGET_REPO=""
OUTPUT_PATH="dist/flathub/com.xerahs.XerahS.yml"
RUN_LINT=0
GENERATE_DEPS=1

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tag)
      TAG_NAME="$2"
      shift 2
      ;;
    --repo)
      GH_TARGET_REPO="$2"
      shift 2
      ;;
    --output)
      OUTPUT_PATH="$2"
      shift 2
      ;;
    --skip-deps)
      GENERATE_DEPS=0
      shift
      ;;
    --lint)
      RUN_LINT=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Error: unknown option '$1'" >&2
      usage >&2
      exit 1
      ;;
  esac
done

require_cmd git
require_cmd awk
require_cmd mkdir
require_cmd tar

repo_root="$(git rev-parse --show-toplevel 2>/dev/null || true)"
if [[ -z "$repo_root" ]]; then
  echo "Error: not inside a git repository." >&2
  exit 1
fi
cd "$repo_root"
repo_root="$(pwd -P)"

# Resolve OUTPUT_PATH to an absolute path so flatpak-run --filesystem=... grants
# work correctly. flatpak-run rejects relative filesystem locations with
# "Unknown filesystem location"; passing absolute paths avoids the failure.
case "$OUTPUT_PATH" in
  /*) ;;
  *) OUTPUT_PATH="$repo_root/$OUTPUT_PATH" ;;
esac

if [[ -z "$TAG_NAME" ]]; then
  version="$(resolve_version_from_props Directory.Build.props)"
  if [[ -z "$version" ]]; then
    echo "Error: failed to resolve current version from Directory.Build.props" >&2
    exit 1
  fi
  TAG_NAME="v${version}"
fi

if [[ ! "$TAG_NAME" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Error: --tag must look like vX.Y.Z (received '$TAG_NAME')" >&2
  exit 1
fi

if ! GH_TARGET_REPO="$(resolve_github_repo_prefer_origin "$GH_TARGET_REPO")"; then
  echo "Error: could not resolve GitHub repo from origin. Pass --repo owner/name (e.g. KovaForge/XerahS or ShareX/XerahS)." >&2
  exit 1
fi

ensure_local_tag_object "$TAG_NAME"

main_commit="$(git rev-parse "${TAG_NAME}^{commit}")"
image_editor_commit="$(resolve_tree_commit "$TAG_NAME" ShareX.ImageEditor)"
omacut_commit="$(resolve_tree_commit "$TAG_NAME" Omacut)"
image_editor_url="$(resolve_submodule_url ShareX.ImageEditor)"
omacut_url="$(resolve_submodule_url Omacut)"
main_url="https://github.com/${GH_TARGET_REPO}.git"
output_dir="$(dirname "$OUTPUT_PATH")"
generated_sources_dir="$output_dir/generated-sources"

mkdir -p "$output_dir"

if [[ $GENERATE_DEPS -eq 1 ]]; then
  require_cmd curl
  require_cmd find
  require_cmd flatpak
  require_cmd python3
  ensure_commit_available "$repo_root/ShareX.ImageEditor" "$image_editor_url" "$image_editor_commit" "ShareX.ImageEditor"
  ensure_commit_available "$repo_root/Omacut" "$omacut_url" "$omacut_commit" "Omacut"
  mkdir -p "$generated_sources_dir"
  snapshot_dir="$(mktemp -d "$repo_root/.flathub-source.XXXXXXXXXX")"
  trap 'rm -rf "$snapshot_dir"' EXIT
  create_release_snapshot "$snapshot_dir"
  generate_nuget_sources "$snapshot_dir" "$generated_sources_dir/nuget-sources.json"
fi

cat > "$OUTPUT_PATH" <<EOF
app-id: com.xerahs.XerahS
runtime: org.freedesktop.Platform
runtime-version: '25.08'
sdk: org.freedesktop.Sdk
command: xerahs
sdk-extensions:
  - org.freedesktop.Sdk.Extension.dotnet10

finish-args:
  # Display. Wayland native socket first so the XDG GlobalShortcuts portal
  # dialog renders with native chrome on GNOME/KDE/Hyprland. fallback-x11
  # allows XWayland when only X11 is available. --share=ipc is required by
  # Flathub lint whenever any x11 variant is present.
  - --socket=wayland
  - --socket=fallback-x11
  - --share=ipc
  - --device=dri
  - --share=network
  - --talk-name=org.kde.StatusNotifierWatcher

# Generated by .ai/skills/publish-release/scripts/prepare-flathub-source-build.sh.
# Source release: ${TAG_NAME}
# This is a source-build candidate for human review, not an automated Flathub PR.
modules:
  - name: xerahs
    buildsystem: simple
    build-options:
      env:
        DOTNET_CLI_HOME: /run/build/xerahs/.dotnet
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE: 'true'
        DOTNET_CLI_TELEMETRY_OPTOUT: 'true'
        NUGET_PACKAGES: /run/build/xerahs/.nuget/packages
        PATH: /usr/lib/sdk/dotnet10/bin:/app/bin:/usr/bin
        XERAHS_DOTNET_RESTORE_SOURCES: /run/build/xerahs/nuget-sources;/usr/lib/sdk/dotnet10/nuget/packages
        XERAHS_PLUGIN_JOBS: '2'
    build-commands:
      - |
        case "\$(uname -m)" in
          x86_64) export XERAHS_ARCHITECTURES=linux-x64 ;;
          aarch64) export XERAHS_ARCHITECTURES=linux-arm64 ;;
          *) echo "Unsupported Flatpak build architecture: \$(uname -m)" >&2; exit 1 ;;
        esac
        cat > NuGet.config <<'NUGETCONFIG'
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="flathub-generated" value="/run/build/xerahs/nuget-sources" />
            <add key="freedesktop-dotnet-sdk" value="/usr/lib/sdk/dotnet10/nuget/packages" />
          </packageSources>
        </configuration>
        NUGETCONFIG
        ./build/linux/package-linux.sh
        publish_dir="src/desktop/app/XerahS.App/bin/Release/net11.0/\${XERAHS_ARCHITECTURES}/publish"
        test -f "\${publish_dir}/XerahS"
        test -f "\${publish_dir}/xerahs-watchfolder-daemon"
        ls "\${publish_dir}"/libopenal.so* >/dev/null
        cp -rT "\${publish_dir}" /app
      - mkdir -p /app/bin
      - ln -s ../XerahS /app/bin/XerahS
      - ln -s ../XerahS /app/bin/xerahs
      - install -Dm644 src/desktop/app/XerahS.UI/Assets/ShareX.iconset/icon_512x512.png /app/share/icons/hicolor/512x512/apps/com.xerahs.XerahS.png
      - install -Dm644 flatpak/com.xerahs.XerahS.desktop /app/share/applications/com.xerahs.XerahS.desktop
      - install -Dm644 flatpak/com.xerahs.XerahS.metainfo.xml /app/share/metainfo/com.xerahs.XerahS.metainfo.xml
      - chmod 755 /app/XerahS
      - test -f /app/xerahs-watchfolder-daemon && chmod 755 /app/xerahs-watchfolder-daemon || true
    sources:
      - type: git
        url: ${main_url}
        tag: ${TAG_NAME}
        commit: ${main_commit}
        disable-submodules: true
      - type: git
        url: ${image_editor_url}
        commit: ${image_editor_commit}
        dest: ShareX.ImageEditor
      - type: git
        url: ${omacut_url}
        commit: ${omacut_commit}
        dest: Omacut
EOF

if [[ $GENERATE_DEPS -eq 1 ]]; then
  cat >> "$OUTPUT_PATH" <<'EOF'
      - generated-sources/nuget-sources.json
EOF
else
  cat >> "$OUTPUT_PATH" <<'EOF'
      # Before submission, add generated offline dependency sources for:
      #   - NuGet/.NET restore packages
EOF
fi

echo "Generated Flathub source-build manifest candidate:"
echo "  $OUTPUT_PATH"
echo ""
echo "Resolved source commits:"
echo "  XerahS             $main_commit ($TAG_NAME)"
echo "  ShareX.ImageEditor $image_editor_commit"
echo "  Omacut             $omacut_commit"

if [[ $GENERATE_DEPS -eq 1 && -f "$generated_sources_dir/nuget-sources.json" ]]; then
  echo "NuGet source file: $generated_sources_dir/nuget-sources.json"
  echo "NuGet source entries: $(jq 'length' "$generated_sources_dir/nuget-sources.json" 2>/dev/null || echo unknown)"
else
  echo "NuGet source file: missing"
fi


if [[ $RUN_LINT -eq 1 ]]; then
  require_cmd flatpak
  flatpak run --filesystem="$repo_root" --filesystem="$output_dir" --command=flatpak-builder-lint org.flatpak.Builder manifest "$OUTPUT_PATH"
fi

echo ""
echo "Next required work before Flathub submission:"
echo "  1. Build with flatpak-builder using the generated manifest with network disabled."
echo "  2. Run manifest and repo lint."
echo "  3. Record GNOME/KDE smoke tests."
echo "  4. Keep the GitHub release as pre-release until all gates pass."
