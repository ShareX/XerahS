#!/usr/bin/env bash
set -euo pipefail
helper="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../linux" && pwd)/shutdown-build-servers.sh"
fixture="$(mktemp -d)"
trap 'rm -rf "$fixture"' EXIT
mkdir "$fixture/bin"
cat > "$fixture/bin/dotnet" <<'MOCK'
#!/usr/bin/env bash
[[ "$*" == 'build-server shutdown' ]] || exit 99
case "$XERAHS_TEST_SHUTDOWN_MODE" in
    success) echo 'Mock shutdown completed' ;;
    failure) exit 42 ;;
    hang) exec sleep 4 ;;
esac
MOCK
chmod +x "$fixture/bin/dotnet"
PATH="$fixture/bin:$PATH" XERAHS_TEST_SHUTDOWN_MODE=success bash "$helper" 1 > "$fixture/success" 2>&1
if grep -q Warning "$fixture/success"; then echo 'FAIL: successful shutdown produced a warning' >&2; exit 1; fi
PATH="$fixture/bin:$PATH" XERAHS_TEST_SHUTDOWN_MODE=failure bash "$helper" 1 > "$fixture/failure" 2>&1
grep -q 'exit 42' "$fixture/failure"
PATH="$fixture/bin:$PATH" XERAHS_TEST_SHUTDOWN_MODE=hang bash "$helper" 1 > "$fixture/timeout" 2>&1
grep -q 'exit 124' "$fixture/timeout"
if bash "$helper" invalid > /dev/null 2>&1; then echo 'FAIL: invalid timeout accepted' >&2; exit 1; fi
printf '%s\n' 'PASS: successful, failed and stalled shutdown clients; invalid timeout rejected'
