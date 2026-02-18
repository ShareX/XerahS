#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
IMAGEEDITOR_PATH="$REPO_ROOT/ImageEditor"

if [ ! -d "$IMAGEEDITOR_PATH" ]; then
    exit 0
fi

if ! git -C "$IMAGEEDITOR_PATH" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    exit 0
fi

if git -C "$IMAGEEDITOR_PATH" symbolic-ref -q HEAD >/dev/null 2>&1; then
    exit 0
fi

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

echo "INFO: ImageEditor detached HEAD fixed -> $default_branch"
