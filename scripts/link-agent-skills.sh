#!/usr/bin/env bash
# Link host skill folders to the single source in .ai/skills so Claude Code
# (.claude/skills) and Codex (.agents/skills) discover skills natively.
# Idempotent. Never replaces a real directory or file. Links are gitignored.
# Usage: scripts/link-agent-skills.sh [--check] [--quiet]
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
check=0; quiet=0
for arg in "$@"; do
  case "$arg" in
    --check) check=1 ;;
    --quiet) quiet=1 ;;
    *) echo "Unknown option: $arg" >&2; exit 2 ;;
  esac
done
say() { [ "$quiet" -eq 1 ] || echo "$@"; }

case "$(uname -s 2>/dev/null || echo unknown)" in
  MINGW*|MSYS*|CYGWIN*)
    # Git Bash 'ln -s' copies instead of linking; use the PowerShell junction script.
    ps="$(command -v pwsh || command -v powershell || true)"
    if [ -n "$ps" ]; then
      ps_args=()
      [ "$check" -eq 1 ] && ps_args+=(-Check)
      [ "$quiet" -eq 1 ] && ps_args+=(-Quiet)
      exec "$ps" -NoProfile -ExecutionPolicy Bypass -File "$repo_root/scripts/link-agent-skills.ps1" "${ps_args[@]}"
    fi
    echo "PowerShell not found; run scripts/link-agent-skills.ps1 manually." >&2
    exit 1
    ;;
esac

missing=0
for host in .claude .agents; do
  link="$repo_root/$host/skills"
  if [ -L "$link" ] && [ "$(readlink "$link")" = "../.ai/skills" ]; then
    say "OK: $host/skills"
    continue
  fi
  if [ -e "$link" ] || [ -L "$link" ]; then
    echo "SKIP: $host/skills exists and is not a link to .ai/skills; leaving it unchanged." >&2
    continue
  fi
  missing=1
  if [ "$check" -eq 1 ]; then
    say "MISSING: $host/skills"
    continue
  fi
  mkdir -p "$repo_root/$host"
  ln -s ../.ai/skills "$link"
  say "LINKED: $host/skills -> .ai/skills"
done

[ "$check" -eq 1 ] && exit "$missing"
exit 0
