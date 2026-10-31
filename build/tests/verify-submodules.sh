#!/usr/bin/env bash
set -euo pipefail
verifier="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)/verify-submodules.sh"
fixture="$(mktemp -d)"
trap 'rm -rf "$fixture"' EXIT
export GIT_CONFIG_NOSYSTEM=1
export GIT_CONFIG_GLOBAL=/dev/null
export GIT_AUTHOR_NAME='Packaging Test' GIT_AUTHOR_EMAIL='test@example.invalid'
export GIT_COMMITTER_NAME="$GIT_AUTHOR_NAME" GIT_COMMITTER_EMAIL="$GIT_AUTHOR_EMAIL"
git init -q "$fixture/dependency"
printf 'pinned\n' > "$fixture/dependency/api"
git -C "$fixture/dependency" add api
git -C "$fixture/dependency" commit -qm pinned
pinned="$(git -C "$fixture/dependency" rev-parse HEAD)"
git init -q "$fixture/parent"
git -C "$fixture/parent" -c protocol.file.allow=always submodule add -q "$fixture/dependency" editor
git -C "$fixture/parent" commit -qam parent
bash "$verifier" "$fixture/parent"
printf 'incompatible new API\n' > "$fixture/parent/editor/api"
git -C "$fixture/parent/editor" commit -qam drift
if bash "$verifier" "$fixture/parent" >/dev/null 2>&1; then
    echo 'FAIL: independently advanced dependency was accepted' >&2; exit 1
fi
git -C "$fixture/parent/editor" checkout -q "$pinned"
bash "$verifier" "$fixture/parent"
git -C "$fixture/parent" submodule deinit -f editor
if bash "$verifier" "$fixture/parent" >/dev/null 2>&1; then
    echo 'FAIL: uninitialized dependency was accepted' >&2; exit 1
fi
printf '%s\n' 'PASS: pinned, drifted and missing dependency regressions'
