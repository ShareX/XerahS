# Platform Abstraction Audit Report

**Date**: 2026-01-17
**Scope**: `XerahS` Solution, `ShareX.Editor`
**Auditor**: Antigravity (Agent)

---

## 1. Executive Summary
A line-by-line and automated audit was conducted to ensure compliance with Strict Platform Abstraction rules (as defined in `AGENTS.md`).

- **ShareX.Editor**: ✅ **COMPLIANT**. No native P/Invokes or Windows-specific references found in Views/ViewModels.
- **XerahS.Core**: ⚠️ **WARNING**. Contains "temporary" integration helpers leaking `Microsoft.Win32` and `DllImport`.
- **XerahS.Common**: ⚠️ **WARNING**. `RegistryHelpers` leaks Windows-specific logic into the common library.
- **XerahS.UI**: ⚠️ **WARNING**. `RecordingBorderWindow` contains direct P/Invokes for window styling.

---

## 2. Detailed Findings

### 2.1 XerahS.Core
| File | Violation | Severity | Description |
| :--- | :--- | :--- | :--- |
| `IntegrationHelper.cs` | `using Microsoft.Win32` | 🔴 Critical | Core business logic depends on Windows Registry types. |
| `IntegrationHelper.cs` | `[DllImport("shell32.dll")]` | 🔴 Critical | Native P/Invoke (`SHChangeNotify`) exists in Core. |

**Context**: The file is marked as "Temporary helper", but code rot risk is high.

### 2.2 XerahS.Common
| File | Violation | Severity | Description |
| :--- | :--- | :--- | :--- |
| `RegistryHelpers.cs` | `using Microsoft.Win32` | 🟠 High | Common library exposes Windows Registry helpers. While guarded with `[SupportedOSPlatform("windows")]`, this invites platform-specific coupling in other projects. |

### 2.3 XerahS.UI
| File | Violation | Severity | Description |
| :--- | :--- | :--- | :--- |
| `RecordingBorderWindow.axaml.cs` | `[DllImport("user32.dll")]` | 🟠 High | View contains direct P/Invokes (`GetWindowLong`, `SetWindowLong`) for click-through behavior. This will fail or crash on Linux/macOS. |

---

## 3. Actionable Remediation Plan

### Item 1: Abstract Shell Integration
**Target**: `XerahS.Core/Integration/IntegrationHelper.cs`
- **Action**: Create `IShellIntegrationService` in `Platform.Abstractions`.
- **Methods**:
  - `bool IsPluginExtensionRegistered();`
  - `void RegisterPluginExtension();`
  - `void UnregisterPluginExtension();`
- **Implementation**: Move existing logic to `XerahS.Platform.Windows.Services.WindowsShellIntegrationService`.
- **Injection**: Update consumer (likely `Bootstrapper` or `Settings`) to use interface.

### Item 2: Abstract Registry Access
**Target**: `XerahS.Common/Helpers/RegistryHelpers.cs`
- **Action**: Create `IRegistryService` (or similar configuration provider) in `Platform.Abstractions`.
- **Rationale**: `RegistryHelpers` is a static class. Static dependencies are hard to mock. Converting to a service allows Linux to use (for example) `~/.config` files or `GSettings` as an alternative storage backend if needed, or simply return "Not Supported" gracefully.
- **Immediate Fix**: Move `RegistryHelpers.cs` to `XerahS.Platform.Windows` and expose functionality via a service.

### Item 3: Abstract Window Styling (Click-Through)
**Target**: `XerahS.UI/Views/RecordingBorderWindow.axaml.cs`
- **Action**: Extend `IWindowService` or create `IWindowEffectsService`.
- **Method**: `void SetWindowClickThrough(IntPtr windowHandle);`
- **Implementation**:
  - **Windows**: Use existing `user32.dll` logic.
  - **Linux**: Implement using `XShapeCombineRectangles` (X11) or ignore (Wayland/Limitations).
  - **macOS**: `NSWindow` ignores mouse events property.

---

## 4. Compliance Checklist (Updated)
- [x] No `System.Windows.Forms` in Core.
- [x] No `Microsoft.Win32` in Core. ✅ (Fixed: Moved to Platform.Windows)
- [x] No `DllImport` in Core/UI. ✅ (Fixed: Abstracted via IWindowService)
- [x] `ShareX.Editor` Pure Avalonia.

## 5. Conclusion
~~The codebase is largely clean, but specific "convenience" helpers allow Windows dependencies to creep into Core/UI. Executing the 3 Action Items above will bring the solution to 100% compliance.~~

**✅ All 3 Action Items have been executed. The solution is now at 100% compliance with platform abstraction rules.**

### Summary of Changes
- Created `IShellIntegrationService` and moved shell integration logic to `WindowsShellIntegrationService`.
- Moved `RegistryHelpers.cs` and `CursorData.cs` to `XerahS.Platform.Windows`.
- Added `SetWindowClickThrough` to `IWindowService` and implemented in all platform services.
- Removed P/Invoke from `RecordingBorderWindow.axaml.cs`.
- Build passes with 0 errors.
