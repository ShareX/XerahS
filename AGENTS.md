# XerahS agent instructions

XerahS is the Avalonia implementation of ShareX.

## Ownership

This codebase belongs to the agents who work on it. Mike handed it over on 2026-11-11: what XerahS is for and how it is built are your decisions, and you will keep building on it.

- Decide product direction, architecture, structure, naming and dependencies yourself. Do not ask for permission on these; make the call, write down why where the next agent will find it, and carry on.
- Build for the long run. You will be the one maintaining what you write, so remove what is dead, fix what is wrong at the root, and leave each area easier to work in than you found it.
- Ownership covers the code and its design. The rules below still apply as written: the build and test gate, Git identity, and the actions under "Scope and completion" that need authorization covering them.

## Repository invariants

- Preserve unrelated working-tree changes. Stay on the current branch; create branches only when explicitly requested.
- Keep `TreatWarningsAsErrors` enabled. Preserve each project's platform conditions: Windows-specific targets use `net11.0-windows10.0.26100.0`; cross-platform targets use `net11.0`.
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

## Avalonia source

When a question about Avalonia (layout, rendering, input, windowing, DPI, the XAML compiler, Android or iOS hosting) can be answered by reading its code, read the code. Don't rely on docs or memory. The ShareX organization forks every Avalonia repository whose code ships in XerahS or builds it. Each fork's `shipped` branch is the commit behind the package version in root `Directory.Packages.props`. Read that branch, not `master` or `main`. When an Avalonia version changes, move `shipped` and update this table in the same change.

| Fork | Upstream | Shipped commit | What XerahS gets from it |
| --- | --- | --- | --- |
| [ShareX/Avalonia](https://github.com/ShareX/Avalonia) | AvaloniaUI/Avalonia | tag `12.1.6` (`cf8e56467`) | Every `Avalonia.*` 12.1.6 package: core, controls, ColorPicker, Fluent theme, Inter font, Skia and HarfBuzz backends, Desktop (Win32, X11, FreeDesktop, Native for macOS), Android, iOS, Headless.NUnit |
| [ShareX/Avalonia.Controls.DataGrid](https://github.com/ShareX/Avalonia.Controls.DataGrid) | AvaloniaUI/Avalonia.Controls.DataGrid | tag `12.1.2` (`1e12211`) | `Avalonia.Controls.DataGrid` 12.1.2 |
| [ShareX/XamlX](https://github.com/ShareX/XamlX) | kekekeks/XamlX | `d7e37ca` | The XAML compiler that compiles `.axaml` at build time (Avalonia's `external/XamlX` submodule) |
| [ShareX/Avalonia.DBus](https://github.com/ShareX/Avalonia.DBus) | AvaloniaUI/Avalonia.DBus | `cada3ec` | D-Bus inside `Avalonia.FreeDesktop` on Linux (Avalonia's `external/Avalonia.DBus` submodule) |
| [ShareX/MicroCom](https://github.com/ShareX/MicroCom) | kekekeks/MicroCom | `76785ef` (0.11.6) | `MicroCom.Runtime`, the COM interop under `Avalonia.Native` and `Avalonia.Win32` |
| [ShareX/angle](https://github.com/ShareX/angle) | AvaloniaUI/angle | `1c8980590` | `Avalonia.Angle.Windows.Natives`, the OpenGL ES over Direct3D layer Avalonia renders through on Windows |
| [ShareX/Avalonia.BuildServices](https://github.com/ShareX/Avalonia.BuildServices) | AvaloniaUI/Avalonia.BuildServices | `777f975` (11.3.2) | A build-time MSBuild task that every Avalonia package pulls in |

`AvaloniaUI.DiagnosticsSupport` (the developer tools hook) has no public source. SkiaSharp and HarfBuzzSharp come from mono/SkiaSharp, not from Avalonia. The `Omacut` submodule pins its own Avalonia version (12.1.1); check its project file before reading the 12.1.6 source for it.

On Mike's machine the forks are cloned next to this repository (`../Avalonia`, `../Avalonia.Controls.DataGrid`, `../XamlX`, …) with `shipped` checked out and an `upstream` remote. They are read-only references. Propose Avalonia fixes upstream; XerahS never builds against a fork.

## Scope and completion

- Continue authorized implementation through integration and relevant verification. Resolve routine local decisions; ask only for missing decisions that materially change the requested outcome or authority.
- Issue creation/updates require an explicit request. Publishing, releases, billing, production changes, secrets exposure, and irreversible user-data deletion require authorization covering that action.
- Before pushing code or build configuration, `dotnet build src/desktop/XerahS.sln` must pass with 0 errors, plus relevant tests. Documentation-only changes need diff, link, and instruction-consistency checks, not an application build. For a version-only root `Directory.Build.props` bump, verify the exact version diff and commit prefix.
- Complete the requested behavior, including affected callers and failure paths; report verification evidence and any remaining blocker. Do not stop at a draft when implementation was requested.
- Judge build progress from output and process activity, without a fixed time limit. Stop only confirmed stalled task processes; consult [.ai/skills/build-common/SKILL.md](.ai/skills/build-common/SKILL.md) for lock recovery.
- Load only matching workflows from `.ai/skills/` and the references needed for the task. Keep always-on policy here, compatibility files thin, and durable lessons in `developers/lessons-learnt/`.

## Skill discovery

Skills live only in `.ai/skills/`. Claude Code finds them natively through `.claude/skills` and Codex through `.agents/skills`, both gitignored local links. If your host's link is missing on a local checkout, ask the user once for approval to run `scripts/link-agent-skills.sh` (Windows: `scripts/link-agent-skills.ps1`); it takes effect next session. Do not create the links without approval or in ephemeral cloud sessions. Until then, list skills with `grep -m1 -H '^description:' .ai/skills/*/SKILL.md`.
