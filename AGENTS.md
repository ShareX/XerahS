# XerahS (formerly ShareX.Avalonia) Agent Instructions

**XerahS** - The Avalonia UI implementation of ShareX.
**Copyright (c) 2007-2026 ShareX Team.**

> **Single Source of Truth**: This document points to the definitive rules for this repository.

## ⚡ Critical Instructions
1. **Build Integrity**:
   - `dotnet build` must pass with **0 errors** before any push.
   - **NEVER** disable `<TreatWarningsAsErrors>`. Fix the warnings.
   - **Target Framework**: `net10.0-windows10.0.19041.0` (Do NOT use `net10.0-windows` alone).
   - **SkiaSharp**: Must use **2.88.9** (Do NOT upgrade to 3.x).

2. **Shell Best Practices**:
   - **No `&&` Chaining**: The agent's PowerShell environment does not support `&&`.
   - **Correct**: Use `;` (unconditional) or `if ($?) { ... }` (conditional).
   - **Example**: `git add .; if ($?) { git commit -m "..." }`

3. **Git Workflow**:
   - **Steps**: Stage (`git add .`) -> Commit -> Push.
   - **Commit Format**: `[vX.Y.Z] [Type] Use concise description`.
   - **Autonomous Execution**: If build passes, **EXECUTE** without asking for permission.

## 🐞 GitHub Issue Creation Workflow
When a bug or feature is identified or provided by user, follow this process:

1. **Inspect** the relevant code files, classes, components, functions.
2. **Identify** the most important affected class names, file paths, component names.
3. **Classify**:
   - If broken/incorrect/error/crash → **BUG** → Type "Fix" → label "bug"
   - If new feature/missing/improvement → **FEATURE REQUEST** → Type "Feature" → label "enhancement"
4. **Create a GitHub issue using gh CLI**:
   - Title: `[BUG]` or `[FEATURE]` + clear, concise title
   - Body: structured Markdown with:
     - Reproduction / expected vs actual (for bugs)
     - Use case / benefit (for features)
     - Bolded affected **ClassName**, **FilePath**, **Component**
     - Any logs / errors
   - **Formatting**: pass a real multi-line body to `gh` (PowerShell here-string or file) — do **not** embed `\n` escape sequences in the body string.
   - Command example: `gh issue create --title "[BUG] ..." --body "..." --label bug`
5. **Report**:
   - The exact gh command ran
   - The created issue URL
   - Confirmation of classification and labels

## 📂 Documentation Index

### Development
- [Coding Standards & License Headers](docs/development/CODING_STANDARDS.md) (Strict Nullability)
- [Release & Versioning](.github/skills/xerahs-workflow/SKILL.md)
- [Testing Guidelines](docs/development/TESTING.md)
- [Documentation Standards](docs/development/DOCUMENTATION_STANDARDS.md)

### Architecture
- [Porting Guide & Platform Abstractions](docs/architecture/PORTING_GUIDE.md)

### Planning
- [Roadmap & Status Snapshot](docs/planning/ROADMAP_SNAPSHOT_JAN_2025.md)
