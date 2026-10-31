#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: run-release-sequence.sh [sequence-options] [-- bump-script-options]

Run release flow in strict order:
1) run-maintenance skill
2) update-changelog skill
3) bump-version-commit-tag.sh
4) optional: monitor tag release workflow until complete

Sequence options:
  --skip-maintenance          Skip step 1 maintenance execution (explicit bypass)
  --assume-maintenance-done   Backward-compatible alias for --skip-maintenance
  --assume-changelog-done     Skip interactive confirmation for step 2
  --monitor                   Monitor tag release workflow after step 3
  --monitor-interval <sec>    Poll interval in seconds (default: 120)
  --release-wait <minutes>    How long to wait for the GitHub release before release notes (default: 30)
  --repo <owner/name>         GitHub repository for gh commands (default: origin remote owner/name)
  --push-remote <name>        Git remote used for branch/tag push (default: origin; pass through to bump script)
  --git-wrapper <cmd>         Git identity wrapper for commit/push (e.g. git-vladislava); also XERAHS_GIT_WRAPPER
  --set-prerelease            Force successful tag release as pre-release
  --no-prerelease             Force successful tag release as stable/latest
  --prepare-flathub-source    Generate Flathub source-build manifest candidate after the release is ready
  --prepare-distro-repo-source  Stamp PPA/COPR/OBS candidates after the release is ready (does not publish)
  --publish-distro-repos      Stamp then upload PPA/COPR/OBS (secrets-gated skip per backend)
  -h, --help                  Show this help

All other options are passed through to:
  ./.ai/skills/publish-release/scripts/bump-version-commit-tag.sh

Dual-repo note:
  Supports KovaForge/XerahS and ShareX/XerahS. Origin may use per-person SSH hosts
  (git@github-<alias>:Owner/Repo.git). Never rely on bare \`gh repo view\` for target
  inference on forks (it often resolves ShareX/XerahS upstream).

Release channel policy (default when neither --set-prerelease nor --no-prerelease is passed):
  - ShareX/XerahS     -> pre-release
  - KovaForge/XerahS  -> full release (latest)
USAGE
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
# shellcheck source=resolve-github-repo.sh
source "$SCRIPT_DIR/resolve-github-repo.sh"

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Error: required command not found: $1" >&2
    exit 1
  fi
}

auto_commit_pending_changes() {
  local status_output
  status_output="$(git status --short)"
  if [[ -z "$status_output" ]]; then
    return 0
  fi

  echo "Auto-committing uncommitted local changes before maintenance pull..."
  echo "  - git add -A"
  git add -A

  local commit_msg
  commit_msg="[skill] Auto-commit uncommitted changes before release maintenance"
  echo "  - git commit -m \"$commit_msg\""
  if ! git commit -m "$commit_msg"; then
    echo "Error: auto-commit failed. Check git status for details." >&2
    git status >&2
    exit 1
  fi
  echo "Auto-commit succeeded."
}

run_maintenance_chores() {
  echo "Step 1: running maintenance prep..."

  echo "  - git status --short"
  if [[ -n "$(git status --short)" ]]; then
    echo "Working tree has uncommitted local changes — auto-committing before maintenance pull."
    auto_commit_pending_changes
  fi
  echo "  - git submodule foreach --recursive status guard"
  git submodule foreach --recursive '
    if test -n "$(git status --short)"; then
      echo "Error: submodule has local changes: $displaypath" >&2
      git status --short >&2
      exit 1
    fi
  '
  echo "  - git pull --recurse-submodules"
  git pull --recurse-submodules
  echo "  - git submodule update --init --recursive"
  git submodule update --init --recursive
  if [[ -d "ShareX.ImageEditor/.git" || -f "ShareX.ImageEditor/.git" ]]; then
    echo "  - reattach ShareX.ImageEditor to develop"
    git -C ShareX.ImageEditor fetch origin --prune
    git -C ShareX.ImageEditor checkout develop
    git -C ShareX.ImageEditor pull --ff-only origin develop
    local image_editor_branch
    image_editor_branch="$(git -C ShareX.ImageEditor symbolic-ref --short HEAD 2>/dev/null || true)"
    if [[ "$image_editor_branch" != "develop" ]]; then
      echo "Error: ShareX.ImageEditor must be attached to develop after maintenance; current branch: ${image_editor_branch:-detached}" >&2
      exit 1
    fi
  fi
}

run_build_precheck() {
  echo "Step 3 pre-check: dotnet build src/desktop/XerahS.sln -m:1"
  require_cmd dotnet
  dotnet build src/desktop/XerahS.sln -m:1
}

resolve_version_from_props() {
  local version_file="$1"
  local version
  version="$(
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
    ' "$version_file" | tr -d '[:space:]' || true
  )"
  if [[ -z "$version" ]]; then
    echo "Error: failed to resolve <Version> from $version_file" >&2
    exit 1
  fi
  echo "$version"
}

passthrough_has_flag() {
  local flag="$1"
  local arg
  for arg in "${PASSTHROUGH_ARGS[@]}"; do
    if [[ "$arg" == "$flag" ]]; then
      return 0
    fi
  done
  return 1
}

find_tag_run_id() {
  local workflow_name="$1"
  local tag_name="$2"
  local gh_repo="$3"
  local attempt=1
  local max_attempts=30
  local run_id=""

  while [[ $attempt -le $max_attempts ]]; do
    run_id="$(gh run list \
      --repo "$gh_repo" \
      --workflow "$workflow_name" \
      --limit 50 \
      --json databaseId,headBranch \
      --jq "map(select(.headBranch==\"$tag_name\"))[0].databaseId // empty" 2>/dev/null || true)"

    if [[ -n "$run_id" ]]; then
      echo "$run_id"
      return 0
    fi

    echo "Waiting for workflow run for $tag_name (attempt $attempt/$max_attempts)..." >&2
    sleep 10
    attempt=$((attempt + 1))
  done

  return 1
}

monitor_release_run() {
  local run_id="$1"
  local interval="$2"
  local gh_repo="$3"
  local line
  local status=""
  local conclusion=""
  local run_url=""
  local failed_job_id=""
  local failed_job_name=""
  local log_file=""

  while true; do
    line="$(gh run view "$run_id" --repo "$gh_repo" --json status,conclusion,url --jq '[.status, (if (.conclusion == null or .conclusion == "") then "n/a" else .conclusion end), .url] | @tsv')"
    IFS=$'\t' read -r status conclusion run_url <<< "$line"

    echo "Run $run_id: status=$status conclusion=${conclusion:-n/a} url=$run_url"

    if [[ "$status" == "completed" ]]; then
      if [[ "$conclusion" == "success" ]]; then
        echo "Release workflow succeeded."
        return 0
      fi

      echo "Release workflow failed with conclusion '$conclusion'." >&2
      failed_job_id="$(gh run view "$run_id" --repo "$gh_repo" --json jobs --jq '.jobs[] | select(.conclusion=="failure") | .databaseId' | head -n 1 || true)"
      failed_job_name="$(gh run view "$run_id" --repo "$gh_repo" --json jobs --jq '.jobs[] | select(.conclusion=="failure") | .name' | head -n 1 || true)"

      if [[ -n "$failed_job_id" ]]; then
        log_file="release-run-${run_id}-job-${failed_job_id}.log"
        echo "First failing job: ${failed_job_name:-unknown} ($failed_job_id)"
        gh run view "$run_id" --repo "$gh_repo" --job "$failed_job_id" --log > "$log_file" 2>&1 || true
        echo "Saved failing job log to: $log_file"
      fi

      return 1
    fi

    sleep "$interval"
  done
}

wait_for_release() {
  local tag_name="$1"
  local gh_repo="$2"
  local attempt=1
  # A full release run takes ~18-20 minutes before the GitHub release exists.
  local max_attempts=$(( RELEASE_WAIT_MINUTES * 6 ))

  while [[ $attempt -le $max_attempts ]]; do
    if gh release view "$tag_name" --repo "$gh_repo" --json url >/dev/null 2>&1; then
      return 0
    fi
    echo "Waiting for release $tag_name (attempt $attempt/$max_attempts)..."
    sleep 10
    attempt=$((attempt + 1))
  done

  return 1
}

release_exists() {
  local tag_name="$1"
  local gh_repo="$2"

  gh release view "$tag_name" --repo "$gh_repo" --json url >/dev/null 2>&1
}

standard_release_notes_block() {
  cat <<'EOF'
Change log:
https://xerahs.com/changelog.html

### macOS Troubleshooting ("App is damaged")
If you see a message saying **"XerahS is damaged and can't be opened"**, it is due to macOS security (Gatekeeper) on quarantined downloads. To fix it:

1. Open **Terminal**.
2. Type the following command (do not hit Enter yet):
   ```bash
   xattr -cr 
   ```
3. Drag the **XerahS.app** file from Finder into the Terminal window (this pastes the full path).
4. Only now, press **Enter**.
EOF
}

ensure_standard_release_notes() {
  local tag_name="$1"
  local gh_repo="$2"
  local existing_body=""
  local updated_body_file=""
  local release_url=""

  require_cmd gh

  if ! wait_for_release "$tag_name" "$gh_repo"; then
    echo "Error: release $tag_name was not found. Cannot enforce standard release notes." >&2
    exit 1
  fi

  existing_body="$(gh release view "$tag_name" --repo "$gh_repo" --json body --jq '.body // ""')"
  if [[ "$existing_body" == *"https://xerahs.com/changelog.html"* ]] && [[ "$existing_body" == *"### macOS Troubleshooting (\"App is damaged\")"* ]]; then
    echo "Standard release notes block already present for $tag_name."
    return 0
  fi

  updated_body_file="$(mktemp)"
  {
    if [[ -n "$existing_body" ]]; then
      printf '%s\n\n' "$existing_body"
    fi
    standard_release_notes_block
  } > "$updated_body_file"

  gh release edit "$tag_name" --repo "$gh_repo" --notes-file "$updated_body_file" >/dev/null
  rm -f "$updated_body_file"

  release_url="$(gh release view "$tag_name" --repo "$gh_repo" --json url --jq '.url')"
  echo "Standard release notes block ensured: $release_url"
}

set_release_prerelease() {
  local tag_name="$1"
  local gh_repo="$2"
  local is_prerelease=""
  local release_url=""

  echo "Setting release $tag_name as pre-release on $gh_repo..."
  gh release edit "$tag_name" --repo "$gh_repo" --prerelease --latest=false >/dev/null

  is_prerelease="$(gh release view "$tag_name" --repo "$gh_repo" --json isPrerelease --jq '.isPrerelease')"
  release_url="$(gh release view "$tag_name" --repo "$gh_repo" --json url --jq '.url')"

  if [[ "$is_prerelease" != "true" ]]; then
    echo "Error: release $tag_name was not marked as pre-release." >&2
    exit 1
  fi

  echo "Release marked as pre-release: $release_url"
}

set_release_stable() {
  local tag_name="$1"
  local gh_repo="$2"
  local is_prerelease=""
  local is_latest=""
  local release_url=""

  echo "Setting release $tag_name as full release (latest) on $gh_repo..."
  gh release edit "$tag_name" --repo "$gh_repo" --prerelease=false --latest >/dev/null

  is_prerelease="$(gh release view "$tag_name" --repo "$gh_repo" --json isPrerelease --jq '.isPrerelease')"
  # gh release view has no isLatest field (gh 2.101); ask the API which release is latest.
  if [[ "$(gh api "repos/$gh_repo/releases/latest" --jq '.tag_name')" == "$tag_name" ]]; then
    is_latest="true"
  fi
  release_url="$(gh release view "$tag_name" --repo "$gh_repo" --json url --jq '.url')"

  if [[ "$is_prerelease" == "true" ]]; then
    echo "Error: release $tag_name is still marked as pre-release." >&2
    exit 1
  fi
  if [[ "$is_latest" != "true" ]]; then
    echo "Error: release $tag_name was not marked as latest." >&2
    exit 1
  fi

  echo "Release marked as full release (latest): $release_url"
}

default_prerelease_for_repo() {
  local gh_repo="$1"
  case "$gh_repo" in
    KovaForge/XerahS)
      echo 0
      ;;
    *)
      # ShareX/XerahS and any other target default to pre-release.
      echo 1
      ;;
  esac
}

apply_release_channel() {
  local tag_name="$1"
  local gh_repo="$2"
  local as_prerelease="$3"

  if [[ "$as_prerelease" -eq 1 ]]; then
    set_release_prerelease "$tag_name" "$gh_repo"
  else
    set_release_stable "$tag_name" "$gh_repo"
  fi
}

expected_release_asset_names() {
  local version="$1"
  cat <<EOF
XerahS-${version}-win-x64.exe
XerahS-${version}-win-x64.msi
XerahS-${version}-win-x64-portable.zip
XerahS-${version}-win-arm64.exe
XerahS-${version}-win-arm64.msi
XerahS-${version}-win-arm64-portable.zip
XerahS-${version}-mac-arm64.tar.gz
XerahS-${version}-mac-x64.tar.gz
XerahS-${version}-linux-x64.tar.gz
XerahS-${version}-linux-x64.deb
XerahS-${version}-linux-x64.rpm
XerahS-${version}-linux-x64.AppImage
XerahS-${version}-linux-arm64.tar.gz
XerahS-${version}-linux-arm64.deb
XerahS-${version}-linux-arm64.rpm
XerahS-${version}-linux-arm64.AppImage
com.xerahs.XerahS-${version}-linux-x64.flatpak
xerahs.${version}.nupkg
EOF
}

verify_release_assets() {
  local tag_name="$1"
  local gh_repo="$2"
  local version="${tag_name#v}"
  local assets_json=""
  local missing_list=""
  local expected=""
  local asset_count=0
  local attempt=1
  local max_attempts=36

  require_cmd gh
  require_cmd jq

  if ! wait_for_release "$tag_name" "$gh_repo"; then
    echo "Error: release $tag_name was not found on $gh_repo. Cannot verify assets." >&2
    exit 1
  fi

  while [[ $attempt -le $max_attempts ]]; do
    assets_json="$(gh release view "$tag_name" --repo "$gh_repo" --json assets)"
    asset_count="$(printf '%s' "$assets_json" | jq '.assets | length')"
    missing_list=""

    while IFS= read -r expected; do
      [[ -z "$expected" ]] && continue
      if ! printf '%s' "$assets_json" | jq -e --arg name "$expected" '.assets | any(.name == $name)' >/dev/null; then
        missing_list+="${expected}"$'\n'
      fi
    done < <(expected_release_asset_names "$version")

    if [[ -z "$missing_list" ]]; then
      echo "All required release assets are present on $gh_repo for $tag_name (count=$asset_count)."
      return 0
    fi

    echo "Waiting for required assets on $gh_repo/$tag_name (attempt $attempt/$max_attempts, found $asset_count)..."
    printf '%s' "$missing_list" | sed '/^$/d' | sed 's/^/  missing: /'
    sleep 10
    attempt=$((attempt + 1))
  done

  echo "Error: release $tag_name on $gh_repo is missing one or more required assets." >&2
  printf '%s' "$missing_list" | sed '/^$/d' | sed 's/^/  missing: /' >&2
  printf '%s' "$assets_json" | jq -r '.assets[].name' 2>/dev/null | sed 's/^/  present: /' >&2 || true
  exit 1
}

SKIP_MAINTENANCE=0
ASSUME_CHANGELOG_DONE=0
MONITOR=0
MONITOR_INTERVAL=120
RELEASE_WAIT_MINUTES=30
# empty = auto from repo policy; 1 = force prerelease; 0 = force stable
SET_PRERELEASE=""
PREPARE_FLATHUB_SOURCE=0
PREPARE_DISTRO_REPO_SOURCE=0
PUBLISH_DISTRO_REPOS=0
WORKFLOW_NAME="Release Build (All Platforms)"
GH_TARGET_REPO=""

PASSTHROUGH_ARGS=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --assume-maintenance-done)
      SKIP_MAINTENANCE=1
      shift
      ;;
    --skip-maintenance)
      SKIP_MAINTENANCE=1
      shift
      ;;
    --assume-changelog-done)
      ASSUME_CHANGELOG_DONE=1
      shift
      ;;
    --monitor)
      MONITOR=1
      shift
      ;;
    --monitor-interval)
      if [[ $# -lt 2 ]]; then
        echo "Error: --monitor-interval requires a value." >&2
        exit 1
      fi
      MONITOR_INTERVAL="$2"
      shift 2
      ;;
    --release-wait)
      if [[ $# -lt 2 || ! "$2" =~ ^[1-9][0-9]*$ ]]; then
        echo "Error: --release-wait requires a positive number of minutes." >&2
        exit 1
      fi
      RELEASE_WAIT_MINUTES="$2"
      shift 2
      ;;
    --repo)
      if [[ $# -lt 2 ]]; then
        echo "Error: --repo requires owner/name." >&2
        exit 1
      fi
      GH_TARGET_REPO="$2"
      shift 2
      ;;
    --push-remote)
      if [[ $# -lt 2 ]]; then
        echo "Error: --push-remote requires a remote name." >&2
        exit 1
      fi
      PASSTHROUGH_ARGS+=("--push-remote" "$2")
      shift 2
      ;;
    --git-wrapper)
      if [[ $# -lt 2 ]]; then
        echo "Error: --git-wrapper requires a command name." >&2
        exit 1
      fi
      PASSTHROUGH_ARGS+=("--git-wrapper" "$2")
      shift 2
      ;;
    --set-prerelease)
      SET_PRERELEASE=1
      MONITOR=1
      shift
      ;;
    --no-prerelease)
      SET_PRERELEASE=0
      MONITOR=1
      shift
      ;;
    --prepare-flathub-source)
      PREPARE_FLATHUB_SOURCE=1
      shift
      ;;
    --prepare-distro-repo-source)
      PREPARE_DISTRO_REPO_SOURCE=1
      shift
      ;;
    --publish-distro-repos)
      PUBLISH_DISTRO_REPOS=1
      PREPARE_DISTRO_REPO_SOURCE=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    --)
      shift
      PASSTHROUGH_ARGS+=("$@")
      break
      ;;
    *)
      PASSTHROUGH_ARGS+=("$1")
      shift
      ;;
  esac
done

if [[ ! "$MONITOR_INTERVAL" =~ ^[0-9]+$ ]] || [[ "$MONITOR_INTERVAL" -le 0 ]]; then
  echo "Error: --monitor-interval must be a positive integer." >&2
  exit 1
fi

repo_root="$(git rev-parse --show-toplevel 2>/dev/null || true)"
if [[ -z "$repo_root" ]]; then
  echo "Error: not inside a git repository." >&2
  exit 1
fi
cd "$repo_root"
repo_root="$(pwd -P)"

if ! GH_TARGET_REPO="$(resolve_github_repo_prefer_origin "$GH_TARGET_REPO")"; then
  echo "Error: could not resolve GitHub repo from origin. Pass --repo owner/name (e.g. KovaForge/XerahS or ShareX/XerahS)." >&2
  echo "Hint: do not rely on bare \`gh repo view\` on forks; it may resolve ShareX/XerahS upstream." >&2
  exit 1
fi
echo "GitHub repo target: $GH_TARGET_REPO"
echo "Origin remote URL : $(git remote get-url origin 2>/dev/null || echo '<missing>')"

if [[ -z "$SET_PRERELEASE" ]]; then
  SET_PRERELEASE="$(default_prerelease_for_repo "$GH_TARGET_REPO")"
fi
if [[ "$SET_PRERELEASE" -eq 1 ]]; then
  echo "Release channel   : pre-release"
else
  echo "Release channel   : full release (latest)"
fi

maintenance_skill="$repo_root/.ai/skills/run-maintenance/SKILL.md"
changelog_skill="$repo_root/.ai/skills/update-changelog/SKILL.md"
bump_script="$repo_root/.ai/skills/publish-release/scripts/bump-version-commit-tag.sh"
flathub_source_script="$repo_root/.ai/skills/publish-release/scripts/prepare-flathub-source-build.sh"
distro_repo_script="$repo_root/.ai/skills/publish-release/scripts/prepare-distro-repo-assets.sh"
distro_repo_publish_script="$repo_root/.ai/skills/publish-release/scripts/publish-distro-repos.sh"

if [[ ! -f "$maintenance_skill" ]]; then
  echo "Error: required skill file not found: $maintenance_skill" >&2
  exit 1
fi
if [[ ! -f "$changelog_skill" ]]; then
  echo "Error: required skill file not found: $changelog_skill" >&2
  exit 1
fi
if [[ ! -f "$bump_script" ]]; then
  echo "Error: required script file not found: $bump_script" >&2
  exit 1
fi
if [[ $PREPARE_FLATHUB_SOURCE -eq 1 && ! -f "$flathub_source_script" ]]; then
  echo "Error: required script file not found: $flathub_source_script" >&2
  exit 1
fi
if [[ $PREPARE_DISTRO_REPO_SOURCE -eq 1 && $PUBLISH_DISTRO_REPOS -eq 0 && ! -f "$distro_repo_script" ]]; then
  echo "Error: required script file not found: $distro_repo_script" >&2
  exit 1
fi
if [[ $PUBLISH_DISTRO_REPOS -eq 1 && ! -f "$distro_repo_publish_script" ]]; then
  echo "Error: required script file not found: $distro_repo_publish_script" >&2
  exit 1
fi

if [[ $SKIP_MAINTENANCE -eq 0 ]]; then
  run_maintenance_chores
else
  echo "Step 1 skipped by request (--skip-maintenance)."
fi

if [[ $ASSUME_CHANGELOG_DONE -eq 0 ]]; then
  echo "Step 2 required: run changelog update skill second:"
  echo "  $changelog_skill"
  read -r -p "Type 'done' after finishing step 2: " response
  if [[ "$response" != "done" ]]; then
    echo "Aborted: changelog step not confirmed."
    exit 1
  fi
fi

echo "Step 3: running bump/tag automation..."
run_build_precheck
bash "$bump_script" "${PASSTHROUGH_ARGS[@]}"

if passthrough_has_flag "--dry-run"; then
  if [[ $MONITOR -eq 1 ]]; then
    echo "Skipping monitor/release-channel steps because bump step used --dry-run."
  fi
  exit 0
fi

if passthrough_has_flag "--no-tag" || passthrough_has_flag "--no-push"; then
  if [[ $MONITOR -eq 1 ]]; then
    echo "Error: --monitor/--set-prerelease/--no-prerelease requires tag creation and push." >&2
    exit 1
  fi
  echo "Done: bump step completed without remote tag push."
  exit 0
fi

version_file="Directory.Build.props"
new_version="$(resolve_version_from_props "$version_file")"
tag_name="v${new_version}"

if [[ $MONITOR -eq 1 ]]; then
  require_cmd gh

  echo "Step 4: monitoring workflow '$WORKFLOW_NAME' for tag $tag_name every ${MONITOR_INTERVAL}s..."
  run_id="$(find_tag_run_id "$WORKFLOW_NAME" "$tag_name" "$GH_TARGET_REPO" || true)"
  if [[ -z "$run_id" ]]; then
    echo "Error: could not find workflow run for tag $tag_name." >&2
    exit 1
  fi

  echo "Found run id: $run_id"
  if ! monitor_release_run "$run_id" "$MONITOR_INTERVAL" "$GH_TARGET_REPO"; then
    if [[ $SET_PRERELEASE -eq 1 ]] && release_exists "$tag_name" "$GH_TARGET_REPO"; then
      echo "Release workflow failed after release creation; applying pre-release guard before exiting..."
      set_release_prerelease "$tag_name" "$GH_TARGET_REPO"
    fi
    echo "Release run failed. Fix the issue, then retry with the next patch release." >&2
    exit 1
  fi
fi

echo "Step 5: ensuring standard release notes for $tag_name..."
ensure_standard_release_notes "$tag_name" "$GH_TARGET_REPO"

echo "Step 6: verifying required release assets on $GH_TARGET_REPO..."
verify_release_assets "$tag_name" "$GH_TARGET_REPO"

echo "Step 7: applying release channel policy for $GH_TARGET_REPO..."
apply_release_channel "$tag_name" "$GH_TARGET_REPO" "$SET_PRERELEASE"

if [[ $PREPARE_FLATHUB_SOURCE -eq 1 ]]; then
  echo "Step 8: preparing Flathub source-build manifest candidate for $tag_name..."
  bash "$flathub_source_script" --tag "$tag_name" --repo "$GH_TARGET_REPO" --lint
fi

if [[ $PUBLISH_DISTRO_REPOS -eq 1 ]]; then
  echo "Step 9: publishing PPA/COPR/OBS for $tag_name (skips a backend without credentials)..."
  bash "$distro_repo_publish_script" --tag "$tag_name" --repo "$GH_TARGET_REPO"
elif [[ $PREPARE_DISTRO_REPO_SOURCE -eq 1 ]]; then
  echo "Step 9: stamping PPA/COPR/OBS candidates for $tag_name (does not publish)..."
  bash "$distro_repo_script" --tag "$tag_name" --repo "$GH_TARGET_REPO"
fi

echo "Release sequence completed for $tag_name on $GH_TARGET_REPO."
