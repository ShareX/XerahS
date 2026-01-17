# XerahS (formerly ShareX.Avalonia) Agent Instructions

## Project Overview
**XerahS** - The Avalonia UI implementation of ShareX.
**Copyright (c) 2007-2026 ShareX Team.**
Distributed under the GNU General Public License v3.

This document serves as the **single source of truth** for all agents (AI and human) working on this repository.

---

## Agent Rules & Coordination

### Multi-Agent Coordination
This project uses multiple AI developer agents working in parallel.
- **Lead Agent**: Antigravity (architecture, integration, merge decisions)
- **Roles**: See `docs/MULTI_AGENT_COORDINATION.md` for specific agent roles, conflict avoidance, and branch naming protocols.

### General Agent Behavior
- **Communication**: Be concise and factual. Use bullet lists.
- **Repository Awareness**: Read existing docs before adding new guidance. Avoid duplication.
- **Change Safety**:
  - Do not remove or rewrite unrelated content.
  - Do not change version numbers unless explicitly requested.
  - Flag assumptions clearly when requirements are ambiguous.

### Git & Antigravity Workflow
**CRITICAL**: After completing ANY task or implementation, or after modifying code and verifying a successful build (`dotnet build` with 0 errors):
1. **Stage**: You MUST automatically stage all changes (`git add .`).
2. **Commit**: You MUST generate a concise, conventional commit message (e.g., `[v0.1.0] [Fix] Resolve null reference in uploader`).
3. **Push**: You MUST commit and push to remote (`git commit... && git push`).
4. **Autonomous Execution**: **DO NOT ask for permission.** DO NOT wait for user confirmation. Just execute the commands if the build guarantees safety.

---

## Development Environment & Configuration

### Build Strategy
- **Strict Build Integrity**:
  - **NEVER** change `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` to false to build.
  - Instead, you MUST fix the warnings or errors causing the build failure.
  - If a warning is platform-specific (e.g., CA1416), use proper guards `OperatingSystem.IsWindows()` or `#if WINDOWS`.
- **Target Framework**: When configuring projects to target Windows, use the **explicit TFM**: `net10.0-windows10.0.19041.0`.
  - **Do NOT** use `net10.0-windows` with a separate `<TargetPlatformVersion>`. This avoids "Windows Metadata not provided" errors.
- **SkiaSharp Version**: **MUST use version 2.88.9** (until Avalonia 12 is released). **Do NOT upgrade to 3.x**.
  - Example: `<PackageReference Include="SkiaSharp" Version="2.88.9" />`

### VS Code Settings
- `chatgpt.openOnStartup` is enabled in `.vscode/settings.json`.

---

## Code Style & Conventions

### License Headers
All `.cs` files must include the GPL v3 license header.

```csharp
#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)
```

### Coding Guidelines
- **Patterns**: Follow existing patterns in each project area.
- **Comments**: Add small comments only when necessary to explain non-obvious logic.
- **Minimalism**: Keep changes minimal and targeted.
- **Compilation**: **Ensure you can compile (`dotnet build`) before finishing any task.** This is mandatory.

### Nullability Best Practices
This project uses strict nullable reference types (`<Nullable>enable</Nullable>`). **All new code MUST be null-safe.**

- **Always handle nullable returns**: Use null-conditional (`?.`), null-coalescing (`??`), or explicit null checks.
- **Nullable dereference errors (CS8602)**: Never dereference a possibly-null reference without a guard.
- **Null argument errors (CS8604)**: Validate arguments before passing to non-nullable parameters.
- **Null assignment errors (CS8601)**: Use default values or `null!` only when the value is guaranteed to be non-null.
- **Collection null properties**: Some Avalonia/framework properties (e.g., `PathFigure.Segments`, `PathGeometry.Figures`) can be `null`. Use `??=` before accessing.
- **Late-initialized fields**: Use `null!` assertion only for fields guaranteed to be initialized before use (e.g., in constructor).
- **Chase all errors**: When nullability fixes surface new errors, continue fixing until the build succeeds with 0 errors.

**Example patterns:**
```csharp
// Safe access with null-conditional + null-coalescing
var result = settings?.CaptureSettings?.UseModernCapture ?? false;

// Null-coalescing assignment for collection properties
outerFigure.Segments ??= new PathSegments();
outerFigure.Segments.Add(...);

// Non-nullable parameter guard
if (string.IsNullOrEmpty(workflowId)) return;
```


---

## Architecture & Porting Rules

### Main Goal
Build `XerahS` by porting the ShareX backend first, then designing the Avalonia UI.
- **Priority**: Backend porting > UI design (until explicitly started).
- **No WinForms**: Do not reuse WinForms UI code. Only copy non-UI methods and data models.

### Platform Abstractions
All platform-specific functionality must be isolated behind interfaces in `XerahS.Platform.Abstractions`.
- **Forbidden**: Direct calls to `NativeMethods`, `Win32 P/Invoke`, `System.Windows.Forms`, or Windows handles in Common/Core/Backend projects.
- **Structure**:
  - `XerahS.Platform.Windows`: Concrete Windows implementation.
  - `XerahS.Platform.Linux/MacOS`: Stubs or implementations.
  - `XerahS.Platform.Abstractions`: Shared interfaces.

### Porting Logic
1. **Clean**: If a file has 0 native refs, port directly.
2. **Mixed**: If a file mixes logic and native calls:
   - Extract pure logic to `Common`/`Core`.
   - Move native code to `Platform.Windows`.
   - Replace callsites with interface calls.

---

## Versioning & Release

### Current Version
Managed centrally in `Directory.Build.props` (e.g., `0.1.0`).

### Rules
1. **Automated Version Bumping**:
   - **PATCH (x.x.X)**: Bug fixes, minor refactors (Complexity ≤ 3).
   - **MINOR (x.X.x)**: New features, significant UI (Complexity 4-7).
   - **MAJOR (X.x.x)**: Breaking changes (Complexity ≥ 8).
2. **How to Bump**: Update `<Version>` in `Directory.Build.props`. Do NOT update individual `.csproj` files.
3. **Commit Prefixes**: Use `[vX.Y.Z]` in commit messages relative to the new version.

---

## Testing & Verification

- **Requirement**: Suggest relevant tests if modifying executable code.
- **Conventions**: Align new tests with current conventions.
- **Historical Parity**:
  - Use `git show <hash>:<file>` to compare against historical ShareX behavior if needed.
  - Store references in `ref/` and clean up after validation.

---

## Documentation Standards

- **Update**: Update/add docs when behavior changes.
- **Commit**: All created `.md` files (including artifacts) must be committed.
- **Location**: Technical docs go in `docs/technical`. General docs in `docs/`.
- **Format**: Keep instructions in ASCII unless target file is Unicode.

---

## Implementation Status & Checklists

### Architecture Status
- **Common/Core/Uploaders**: Porting in progress.
- **Uploader Plugin System**:
  - ✅ Multi-Instance Provider Catalog (Dec 2024)
  - ✅ Dynamic Plugin System (Jan 2025) - Pure dynamic loading, isolation.
  - ⏳ File-Type Routing (Planned) - See `FILETYPE_ROUTING_SPEC.md`.
  - ⏳ Full Automation Workflow (Path B) - See `automation_workflow_plan.md`.

### Annotation Subsystem
- ✅ **Phase 1**: Core Models (Dec 2024) - `XerahS.Annotations` created.
- ⏳ **Phase 2**: Canvas Control (~6-8h) - Replace WinForms/GDI+ with Avalonia/Skia.

### Native & ARM64
- **Goal**: Native support for Windows ARM64; portable to Linux/Mac ARM64.
- **Tasks**:
  - Add `win-arm64` to build matrix.
  - Audit P/Invoke for pointer sizes (`nint`).
  - Provide ARM64 FFmpeg builds.

### Pending Backend Tasks (Highlights)
*See full gap report in previous docs for exhaustive list.*
- **ShareX.HelpersLib**: Many utilities ported (`FileDownloader`, `Encryption`, `Helpers`).
  - *Pending*: `BlackStyle*` controls, `ColorPicker` UI, `PrintHelper`.
- **ShareX.ScreenCaptureLib**:
  - *Pending*: Shapes, Tools (Crop, CutOut, Spotlight), Region Capture UI.

---
*Generated: 2026-01-13. Central instructions; symlinks point here.*
