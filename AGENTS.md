# XerahS agent instructions

XerahS is the Avalonia implementation of ShareX.

## Ownership

This codebase belongs to the agents who work on it. Mike handed it over on 2026-11-11: what XerahS is for and how it is built are your decisions, and you will keep building on it.

- Decide product direction, architecture, structure, naming and dependencies yourself. Do not ask for permission on these; make the call, write down why where the next agent will find it, and carry on.
- Build for the long run. You will be the one maintaining what you write, so remove what is dead, fix what is wrong at the root, and leave each area easier to work in than you found it.
- Ownership covers the code and its design. The rules below still apply as written: the build and test gate, Git identity, and the actions under "Scope and completion" that need authorization covering them.

## Repository invariants

- Preserve unrelated working-tree changes. Stay on the current branch; create branches only when explicitly requested.
- Keep `TreatWarningsAsErrors` enabled. Preserve each project's platform conditions: Windows-specific targets use `net10.0-windows10.0.26100.0`; cross-platform targets use `net10.0`.
- Keep `SkiaSharp` and all `SkiaSharp.NativeAssets.*` packages on the same version in root `Directory.Packages.props`. Use central package management; do not restore legacy project-local pins.
- In Windows PowerShell, use separate commands or `if ($?) { ... }` for conditional sequencing; do not use `&&`.

## Git identity

Use your matching Git wrapper when available:

| Agent | Wrapper |
|---|---|
| Aoife | `git-aoife` |
| Mikhail | `git-mikhail` |
| Declan | `git-declan` |
| Vladislava | `git-vladislava` |

Before pushing, confirm identity and remote with `git-<person> whoami`. If your wrapper is unavailable, use configured Git after checking `user.name`, `user.email`, and `git remote -v`. Keep the configured identity and remotes unless asked to change them.

For commits, pushes, and version changes, use [.ai/skills/git-workflow/SKILL.md](.ai/skills/git-workflow/SKILL.md). XerahS commit prefixes must use the next unreleased app version; shared-library commits omit the app version.

## Scope and completion

- Continue authorized implementation through integration and relevant verification. Resolve routine local decisions; ask only for missing decisions that materially change the requested outcome or authority.
- Issue creation/updates require an explicit request. Publishing, releases, billing, production changes, secrets exposure, and irreversible user-data deletion require authorization covering that action.
- Before pushing code or build configuration, `dotnet build src/desktop/XerahS.sln` must pass with 0 errors, plus relevant tests. Documentation-only changes need diff, link, and instruction-consistency checks, not an application build. For a version-only root `Directory.Build.props` bump, verify the exact version diff and commit prefix.
- Complete the requested behavior, including affected callers and failure paths; report verification evidence and any remaining blocker. Do not stop at a draft when implementation was requested.
- Judge build progress from output and process activity, without a fixed time limit. Stop only confirmed stalled task processes; consult [.ai/skills/build-common/SKILL.md](.ai/skills/build-common/SKILL.md) for lock recovery.
- Load only matching workflows from `.ai/skills/` and the references needed for the task. Keep always-on policy here, compatibility files thin, and durable lessons in `developers/lessons-learnt/`.

## Skill discovery

Skills live only in `.ai/skills/`. Claude Code finds them natively through `.claude/skills` and Codex through `.agents/skills`, both gitignored local links. If your host's link is missing on a local checkout, ask the user once for approval to run `scripts/link-agent-skills.sh` (Windows: `scripts/link-agent-skills.ps1`); it takes effect next session. Do not create the links without approval or in ephemeral cloud sessions. Until then, list skills with `grep -m1 -H '^description:' .ai/skills/*/SKILL.md`.
