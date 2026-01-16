# XIP0019: Platform Abstraction & Architecture Audit

**Created**: 2026-01-17
**Status**: Draft
**Priority**: High (Prerequisite for stable Linux/macOS releases)

---

## 1. Objective
Conduct a comprehensive, line-by-line review of the `XerahS` codebase to ensure strict compliance with cross-platform abstraction rules. The goal is to eliminate hidden Windows dependencies in Core/Common libraries and ensure a robust, testable architecture following industry best practices.

## 2. Scope
The audit will cover the following projects:
1.  `XerahS.Core` (Must be 100% platform-agnostic)
2.  `XerahS.Common` (Must be 100% platform-agnostic)
3.  `XerahS.UI` (Avalonia-only; no Win32 P/Invokes)
4.  `XerahS.Platform.Abstractions` (The contract layer)

**Exclusions**:
- `XerahS.Platform.Windows` (Allowed to contain Windows specific code)
- `XerahS.Platform.Linux` (Allowed to contain Linux specific code)

## 3. Review Criteria (Industry Best Practices)

### 3.1 Namespace Contamination
- **Rule**: Core/Common projects MUST NOT reference:
    - `System.Windows.Forms`
    - `System.Drawing` (GDI+) - *Exception: Primitives like `Point` if strictly necessary, but `SkiaSharp` types preferred.*
    - `MS.Win32` / `Microsoft.Win32`
    - `WPF` namespaces (`System.Windows.Controls`, etc.)

### 3.2 Hardcoded Platform Assumptions
- **Rule**: Eliminate hardcoded paths or separators.
    - ❌ `C:\Program Files\`
    - ❌ `\\` (Backslash hardcoding) -> Use `Path.Combine()` or `Path.DirectorySeparatorChar`
    - ❌ `\r\n` (CRLF) for logic -> Use `Environment.NewLine`

### 3.3 Static Gateway Violations
- **Rule**: No static access to platform services in Core logic.
    - ❌ `Clipboard.SetText(...)`
    - ✅ `IClipboardService.SetText(...)` (Injected)
    - ❌ `MessageBox.Show(...)`
    - ✅ `IDialogService.ShowMessage(...)`

### 3.4 P/Invoke Leaks
- **Rule**: No `[DllImport]` or `extern` calls outside of `XerahS.Platform.*` projects.
    - All native calls must be wrapped in an abstraction interface.

## 4. Execution Plan

### Phase 1: Automated Scanning
- [ ] Run **roslyn** analyzers or `grep` searches for forbidden namespaces in Core.
- [ ] Audit `.csproj` files for forbidden `<Reference>` or `<PackageReference>` tags (e.g., `UseWindowsForms`).

### Phase 2: Line-by-Line Manual Review
- [ ] **Core/Common**: Verify Logic/IO operations.
- [ ] **UI**: Verify ViewModels rely purely on interfaces (DI), not static service locators where possible.

### Phase 3: Helper & Utility Audit
- [ ] Review `HelpersLib` ports. Ensure legacy static helpers (e.g. `SystemHelper`) don't internally call Win32 APIs.

## 5. Remediation Strategy
For every violation found:
1.  **Define Interface**: Create a capability interface in `XerahS.Platform.Abstractions`.
2.  **Move Implementation**: Move the offending code to `XerahS.Platform.Windows`.
3.  **Inject**: Update the call site to use the interface via Dependency Injection.

## 6. Definition of Done
- Zero references to `System.Windows.Forms` in `XerahS.Core`.
- Zero `[DllImport]` in `XerahS.Core` / `XerahS.UI`.
- Solution builds strictly on Linux (simulated or actual) without missing symbol errors.
- `AGENTS.md` rules are strictly enforced.
