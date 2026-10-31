---
name: triage-runtime-logs
description: Investigate supplied XerahS logs against current code. Implement fixes only when requested.
---

# Triage Runtime Logs

Use this skill when the user provides XerahS logs, exception traces, or repeated debug output and wants actionable results rather than a generic reading of the log.

## Scope

Typical triggers:

- startup log excerpts with JSON/config errors
- `System.InvalidOperationException`, `XamlLoadException`, hotkey, capture, history, or thumbnail errors
- questions like "are these bugs still valid?" or "fix the issues in this log"
- repeated noisy lines that need root-cause reduction

## Inputs

- pasted log excerpt, or a path to a log file
- current XerahS checkout
- optional repro command from the user

If the user only pastes a partial log, work from that first. Read more log context only when needed to confirm the root cause.

## Workflow

0. Check the knowledge base first ([diagnostics-knowledge-base](../diagnostics-knowledge-base/SKILL.md)).
   - `diagnostics.search()` the exception type, logger tag, or last message before a silent exit.
   - A matching signature may already carry the root cause, fix, and fixed-in version, plus other users' reports with system data and monitor layouts.

1. Collapse the log into distinct signatures.
   - Group duplicates by exception type, logger prefix, config path, or repeated message text.
   - Treat repeated per-row or per-hotkey errors as one root cause until proven otherwise.

2. Prioritize by user impact.
   - Crash, failed capture, broken history, broken startup, or data loss risk first.
   - Then correctness regressions.
   - Then noisy or misleading logging.

3. Check nearby repo guidance before changing code.
   - `developers/hardening/*.md`
   - `developers/guidelines/TROUBLESHOOTING_CAPTURE_ISSUE.md`
   - `developers/guidelines/HISTORY_TESTING_GUIDE.md`
   - `developers/guidelines/app_errors.txt`

4. Map each signature to code.
   - Search for the exact message text, exception type, logger tag, config type, or file path.
   - Read the owning subsystem before editing: history, capture, hotkeys, settings, updater, plugin loading, or editor integration.

5. Decide whether the issue is still live.
   - Prefer a targeted repro or focused build/test path over assumptions.
   - If the log looks stale, verify against current code before spending time on a fix.

6. When fixes are requested, implement the smallest robust set. For diagnosis-only requests, report verified causes and proposed fixes without editing code.
   - Prefer root-cause fixes over suppressing downstream logs.
   - Add guards, snapshots, validation, or parsing compatibility only where needed.
   - If the issue is only misleading logging, tighten the log so future triage is clearer.

7. Verify.
   - Run the narrowest relevant verification first.
   - If code changed, finish with `dotnet build` unless the repo rules explicitly exempt it.
   - For history or capture fixes, include a short manual repro path if automated coverage is weak.

8. Record durable lessons when warranted.
   - Add a concise prevention rule to `developers/lessons-learnt/general.md` or a topic file when the bug exposed a reusable guardrail.
   - Import the user's log if it is not in the knowledge base yet, then tick the issue off with `diagnostics.mark_fixed` (or `annotate_signature` when it is not fixed).

## Working Rules

- Do not report every repeated line as a separate bug.
- Do not fix speculative issues that the current code no longer has.
- Prefer subsystem-specific docs and hardening notes over re-deriving known context.
- When multiple signatures share one cause, explain that compression clearly in the result.
- Keep findings source-aware: cite the specific log signature, owning file, and verification path.

## Common Patterns

- Config enum parse errors: fix compatibility or migration once, not once per affected setting row.
- `Collection was modified` during background work: inspect concurrent enumeration and snapshot the collection or move mutation boundaries.
- History/capture regressions: cross-check the relevant troubleshooting/testing guide before changing implementation.
- Update, plugin, or hotkey noise: distinguish harmless status lines from actual registration or load failures.
