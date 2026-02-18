#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
IMAGEEDITOR_PATH="$REPO_ROOT/ImageEditor"

is_truthy() {
    case "${1:-}" in
        1|true|TRUE|yes|YES|on|ON)
            return 0
            ;;
        *)
            return 1
            ;;
    esac
}

auto_push_setting="${XERAHS_IMAGEEDITOR_AUTO_PUSH:-}"
if [ -z "$auto_push_setting" ]; then
    auto_push_setting="$(git -C "$REPO_ROOT" config --bool --get xerahs.hooks.imageeditorautopush 2>/dev/null || true)"
fi

auto_push_enabled=0
if is_truthy "$auto_push_setting"; then
    auto_push_enabled=1
fi

if [ ! -d "$IMAGEEDITOR_PATH" ]; then
    exit 0
fi

if ! git -C "$IMAGEEDITOR_PATH" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    exit 0
fi

branch_name="$(git -C "$IMAGEEDITOR_PATH" branch --show-current 2>/dev/null || true)"
was_detached=0

if [ -z "$branch_name" ]; then
    was_detached=1

    if [ -n "$(git -C "$IMAGEEDITOR_PATH" status --porcelain 2>/dev/null)" ]; then
        echo "WARN: ImageEditor is detached but has local changes; skipping auto-checkout." >&2
        exit 0
    fi

    default_ref="$(git -C "$IMAGEEDITOR_PATH" symbolic-ref --quiet refs/remotes/origin/HEAD 2>/dev/null || true)"
    default_branch="${default_ref#refs/remotes/origin/}"

    if [ -z "$default_branch" ]; then
        for candidate in develop main master; do
            if git -C "$IMAGEEDITOR_PATH" show-ref --verify --quiet "refs/remotes/origin/$candidate" || \
               git -C "$IMAGEEDITOR_PATH" show-ref --verify --quiet "refs/heads/$candidate"; then
                default_branch="$candidate"
                break
            fi
        done
    fi

    if [ -z "$default_branch" ]; then
        echo "WARN: ImageEditor is detached and default branch could not be detected." >&2
        exit 0
    fi

    if git -C "$IMAGEEDITOR_PATH" show-ref --verify --quiet "refs/heads/$default_branch"; then
        git -C "$IMAGEEDITOR_PATH" checkout "$default_branch" >/dev/null 2>&1
    elif git -C "$IMAGEEDITOR_PATH" show-ref --verify --quiet "refs/remotes/origin/$default_branch"; then
        git -C "$IMAGEEDITOR_PATH" checkout -B "$default_branch" "origin/$default_branch" >/dev/null 2>&1
    else
        echo "WARN: ImageEditor default branch '$default_branch' not available locally." >&2
        exit 0
    fi

    if upstream_ref=$(git -C "$IMAGEEDITOR_PATH" rev-parse --abbrev-ref --symbolic-full-name "${default_branch}@{upstream}" 2>/dev/null); then
        git -C "$IMAGEEDITOR_PATH" merge --ff-only "$upstream_ref" >/dev/null 2>&1 || true
    fi

    branch_name="$default_branch"
    echo "INFO: ImageEditor detached HEAD fixed -> $default_branch"
fi

if [ "$auto_push_enabled" -ne 1 ]; then
    exit 0
fi

if [ -z "$branch_name" ]; then
    echo "WARN: ImageEditor auto-push skipped because no active branch is checked out." >&2
    exit 0
fi

upstream_ref="$(git -C "$IMAGEEDITOR_PATH" rev-parse --abbrev-ref --symbolic-full-name "${branch_name}@{upstream}" 2>/dev/null || true)"
if [ -z "$upstream_ref" ]; then
    if git -C "$IMAGEEDITOR_PATH" show-ref --verify --quiet "refs/remotes/origin/$branch_name"; then
        git -C "$IMAGEEDITOR_PATH" branch --set-upstream-to "origin/$branch_name" "$branch_name" >/dev/null 2>&1 || true
        upstream_ref="$(git -C "$IMAGEEDITOR_PATH" rev-parse --abbrev-ref --symbolic-full-name "${branch_name}@{upstream}" 2>/dev/null || true)"
    fi
fi

if [ -z "$upstream_ref" ]; then
    if git -C "$IMAGEEDITOR_PATH" push -u origin "$branch_name" >/dev/null 2>&1; then
        echo "INFO: ImageEditor branch '$branch_name' pushed and upstream set."
    else
        echo "WARN: ImageEditor auto-push failed for '$branch_name' (set upstream)." >&2
    fi
    exit 0
fi

ahead_count="$(git -C "$IMAGEEDITOR_PATH" rev-list --count "${upstream_ref}..HEAD" 2>/dev/null || echo 0)"
if [ "$ahead_count" -eq 0 ]; then
    if [ "$was_detached" -eq 1 ]; then
        echo "INFO: ImageEditor auto-push skipped; '$branch_name' is already up to date."
    fi
    exit 0
fi

if git -C "$IMAGEEDITOR_PATH" push >/dev/null 2>&1; then
    echo "INFO: ImageEditor auto-pushed $ahead_count commit(s) from '$branch_name'."
else
    echo "WARN: ImageEditor auto-push failed for '$branch_name'." >&2
fi
