#!/usr/bin/env bash
set -euo pipefail
repo_root="${1:-$(git rev-parse --show-toplevel)}"
status="$(git -C "$repo_root" submodule status --recursive)"
if [[ "$status" =~ (^|$'\n')[-+U] ]]; then
    echo 'Error: submodules are missing, conflicted, or do not match the parent commit:' >&2
    printf '%s\n' "$status" >&2
    echo 'Run git submodule update --init --recursive --checkout (without --remote) in a clean checkout.' >&2
    exit 1
fi
printf '%s\n' 'Verified submodules match their recorded commits.'
