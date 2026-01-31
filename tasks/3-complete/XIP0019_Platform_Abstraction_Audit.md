# XIP0019: Platform Abstraction & Architecture Audit

**Created**: 2026-01-17
**Status**: Remediation In Progress
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
- [x] Run **roslyn** analyzers or `grep` searches for forbidden namespaces in Core.
- [x] Audit `.csproj` files for forbidden `<Reference>` or `<PackageReference>` tags (e.g., `UseWindowsForms`).

### Phase 2: Line-by-Line Manual Review
- [x] **Core/Common**: Verify Logic/IO operations.
- [x] **UI**: Verify ViewModels rely purely on interfaces (DI), not static service locators where possible.

### Phase 3: Helper & Utility Audit
- [x] Review `HelpersLib` ports. Ensure legacy static helpers (e.g. `SystemHelper`) don't internally call Win32 APIs.

## 5. Audit Findings (2026-01-27)

### Critical Violations
1.  **XerahS.Common/NativeMethods.cs**
    -   **Violation**: Contains extensive `[DllImport]` calls (Win32 APIs) including `user32.dll`, `gdi32.dll`, `dwmapi.dll`.
    -   **Rule**: "No `[DllImport]` or `extern` calls outside of `XerahS.Platform.*` projects."
    -   **Action**: Must be moved to `XerahS.Platform.Windows` and abstracted via `XerahS.Platform.Abstractions`.

2.  **XerahS.Core/Tasks/WorkflowTask.cs**
    -   **Violation**: Uses `System.Drawing.Image`, `System.Drawing.Imaging.ImageFormat` (GDI+).
    -   **Rule**: "`XerahS.Core` (Must be 100% platform-agnostic)... `System.Drawing` (GDI+) - *Exception: Primitives like `Point` if strictly necessary, but `SkiaSharp` types preferred.*"
    -   **Action**: Replace with `SkiaSharp` or `Avalonia.Media.Imaging`.

### Major Violations
1.  **XerahS.Core/Tasks/WorkflowTask.cs**
    -   **Violation**: Static access to `PlatformServices.Clipboard.SetText(url)`.
    -   **Rule**: "No static access to platform services in Core logic."
    -   **Action**: Inject `IClipboardService` into `WorkflowTask`.

### Observations
1.  **XerahS.Common/ShareXTheme.cs**
    -   Uses `System.Drawing.Color`. While arguably a primitive, Avalonia types or SkiaSharp types are preferred for full portability.
2.  **XerahS.Core.csproj**
    -   TargetFrameworks include `net10.0-windows10.0.26100.0`. While this enables Windows APIs, the goal is to make Core platform-agnostic.
3.  **XerahS.UI ViewModels**
    -   **Violation**: `ColorPickerViewModel` (and likely others) uses `Platform.Abstractions.PlatformServices.Clipboard` (Static Gateway) instead of constructor injection of `IClipboardService`.
    -   **Action**: Refactor ViewModels to accept `IClipboardService` via constructor.

## 6. Remediation Progress (2026-01-27)

### Completed ✅
1.  **WorkflowTask.cs** - Refactored to use `SKBitmap` instead of `System.Drawing.Image` and accepts `IClipboardService` via constructor.
2.  **ColorPickerViewModel.cs** - Refactored to accept `IClipboardService` via constructor instead of static `PlatformServices`.
3.  **TroubleshootingHelper.cs** - Updated references to use `XerahS.Platform.Windows.NativeMethods`.

**Files moved to `XerahS.Platform.Windows`:**
- `NativeMethods.cs` (namespace updated)
- `InputManager.cs` (namespace updated)
- `KeyboardHook.cs` (namespace updated)
- `InputHelpers.cs` (namespace updated)
- `NativeMethods_Helpers.cs` (namespace updated)
- `DWMManager.cs` (namespace updated)

### Remaining 🔄
**Files in `XerahS.Common/Native` still containing `NativeMethods` references:**
- `WindowInfo.cs`
- `TimerResolutionManager.cs`
- `TaskbarManager.cs`
- `WshShell.cs`

**Other files with `NativeMethods` dependencies:**
- `ImageFilesCache.cs`
- `Helpers/ClipboardHelpersEx.cs`

## 7. Remediation Strategy
For every violation found:
1.  **Define Interface**: Create a capability interface in `XerahS.Platform.Abstractions`.
2.  **Move Implementation**: Move the offending code to `XerahS.Platform.Windows`.
3.  **Inject**: Update the call site to use the interface via Dependency Injection.

## 8. Definition of Done
- Zero references to `System.Windows.Forms` in `XerahS.Core`.
- Zero `[DllImport]` in `XerahS.Core` / `XerahS.UI`.
- Solution builds strictly on Linux (simulated or actual) without missing symbol errors.
- `AGENTS.md` rules are strictly enforced.
