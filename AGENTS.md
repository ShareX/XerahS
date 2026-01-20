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

2. **Git Workflow**:
   - **Steps**: Stage (`git add .`) -> Commit -> Push.
   - **Commit Format**: `[vX.Y.Z] [Type] Use concise description`.
   - **Autonomous Execution**: If build passes, **EXECUTE** without asking for permission.

## 📂 Documentation Index

### Development
- [Coding Standards & License Headers](docs/development/CODING_STANDARDS.md) (Strict Nullability)
- [Release & Versioning](docs/development/RELEASE_PROCESS.md)
- [Testing Guidelines](docs/development/TESTING.md)
- [Documentation Standards](docs/development/DOCUMENTATION_STANDARDS.md)

### Architecture
- [Porting Guide & Platform Abstractions](docs/architecture/PORTING_GUIDE.md)

### Planning
- [Roadmap & Status Snapshot](docs/planning/ROADMAP_SNAPSHOT_JAN_2025.md)
