#!/usr/bin/env bash
# Build-server maintenance must not hold up a successfully published package.
set -euo pipefail
shutdown_seconds="${1:-15}"
if ! [[ "$shutdown_seconds" =~ ^[1-9][0-9]*$ ]]; then
    echo 'Error: build-server shutdown timeout must be a positive number of seconds.' >&2
    exit 2
fi

echo 'Requesting dotnet build-server shutdown...'
# --foreground limits timeout's signals to this shutdown client. Do not kill
# unrelated compiler/build processes that may share this user's SDK servers.
if timeout --foreground --kill-after=5s "${shutdown_seconds}s" dotnet build-server shutdown; then
    exit 0
else
    shutdown_status=$?
fi
printf 'Warning: dotnet build-server shutdown did not complete (exit %s); continuing packaging.\n' "$shutdown_status" >&2
