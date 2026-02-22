# XIP0037: Top 3 Refactoring Pain Points (src) — Deep Analysis

## Summary

Comprehensive audit of `src/` identifying the three highest-impact structural pain points that drag on daily development velocity and long-term maintainability.

---

## Pain Point 1: God ViewModels — `TaskSettingsViewModel` (1,403 lines) & `SettingsViewModel` (1,293 lines)

### What

Two monolithic ViewModels in `src/desktop/app/XerahS.UI/ViewModels/` that violate Single Responsibility Principle, each managing 8+ unrelated concerns in a single class.

### Where

| File | Lines | Properties/Methods | Regions |
|------|-------|--------------------|---------|
| `TaskSettingsViewModel.cs` | 1,403 | 124+ | 8 (`#region` blocks) |
| `SettingsViewModel.cs` | 1,293 | 117+ | mixed concerns |

### Why it hurts

- **Navigability:** Every settings-related change requires scrolling through 1,400-line files.
- **Boilerplate explosion:** `TaskSettingsViewModel` repeats an identical 10-line property wrapper 100+ times (~800 lines of pure ceremony).
- **Hidden subsystems:** `SettingsViewModel` embeds ~350 lines of watch folder daemon orchestration (start/stop/retry/scope normalization) that should be a standalone service/ViewModel.
- **Fragile auto-save:** `SettingsViewModel.OnPropertyChanged` checks 12+ property name exclusions to decide when to auto-save — violates Open/Closed Principle and breaks every time a transient property is added.
- **Duplicate concerns:** Both ViewModels expose AfterCapture/AfterUpload flag properties with identical bit-flag logic.

### XAML Bindings Impacted

| XAML View | DataType | Tab Structure |
|-----------|----------|---------------|
| `TaskSettingsPanel.axaml` | `TaskSettingsViewModel` | Capture, Index Folder, File, Upload, Notifications, Advanced |
| `TaskImageSettingsPanel.axaml` | `TaskSettingsViewModel` | Image settings |
| `TaskVideoSettingsPanel.axaml` | `TaskSettingsViewModel` | Video/recording settings |
| `ApplicationSettingsView.axaml` | `SettingsViewModel` | General, Theme, Paths, Watch Folders, Integration, History, Proxy, Advanced |
| `SettingsView.axaml` | `SettingsViewModel` | Landing page |
| `DestinationSettingsView.axaml` | `SettingsViewModel` | Destination settings |
| `HotkeySettingsView.axaml` | `SettingsViewModel` | Hotkey settings |

### Refactor direction

**TaskSettingsViewModel:** Split into child ViewModels exposed as properties. Extract `FFmpegSettingsViewModel` (most complex: 7 private fields + 3 async commands + 5 helper methods). Remaining regions become partial-class files or child ViewModels as complexity warrants.

**SettingsViewModel:** Extract `WatchFolderDaemonViewModel` (~350 lines of daemon lifecycle orchestration). Replace `OnPropertyChanged` exclusion list with attribute-based or set-based auto-save filtering.

---

## Pain Point 2: Platform Service Duplication — Watch Folder Daemon (~1,700 lines across 3 files)

### What

Three nearly identical `IWatchFolderDaemonService` implementations across Windows, macOS, and Linux with ~95% shared logic.

### Where

| File | Lines |
|------|-------|
| `src/platform/XerahS.Platform.Windows/Services/WindowsWatchFolderDaemonService.cs` | 576 |
| `src/platform/XerahS.Platform.MacOS/Services/MacOSWatchFolderDaemonService.cs` | 576 |
| `src/platform/XerahS.Platform.Linux/Services/LinuxWatchFolderDaemonService.cs` | 572 |

### Why it hurts

- **Bug duplication risk:** A fix in one OS must be manually replicated to two others.
- **Review burden:** Reviewers must diff all three files to verify consistency.
- **Shared logic:** `RestartAsync()`, `StopAsync()` polling loop, `GetStatusAsync()`, and error handling patterns are ~95% identical.

### Refactor direction

Extract `WatchFolderDaemonServiceBase` abstract class with template methods. Platform subclasses override only:
- `RunDaemonCommand()`
- `GetDaemonPath()`
- Platform-specific process management

**Estimated savings:** ~1,000 lines of duplicated boilerplate.

Same pattern also affects hotkey services (Windows 479, Linux 462, macOS 509, Wayland Portal 646 lines).

---

## Pain Point 3: `ScreenRecordingManager` — Duplicated Logic & State Complexity (1,003 lines)

### What

The recording lifecycle manager has two nearly identical entry points and excessive mutable state.

### Where

`src/desktop/core/XerahS.Core/Managers/ScreenRecordingManager.cs` (1,003 lines)

### Why it hurts

- **95% code duplication:** `StartRecordingAsync()` (lines 172-298) and `StartRecordingInternalAsync()` (lines 619-719) are copy-pasted variants.
- **11 private state fields** track session state (`_isPaused`, `_abortRequested`, `_isFinalized`, `_cachedFinalPath`, etc.) with multiple lock acquisition points — deadlock risk.
- **71-line conditional chain:** `ShouldForceFallback()` (lines 428-498) has 7+ branches making backend selection logic impenetrable.
- **Debug spam:** `Console.WriteLine` statements in production code (lines 235-268).

### Refactor direction

1. Extract `RecordingSession` state object to encapsulate the 11 fields.
2. Consolidate the two start methods into a single method with a `restartSegment` parameter.
3. Replace `ShouldForceFallback()` with a strategy/decision-table pattern.
4. Remove `Console.WriteLine` debug statements.

---

## Priority Matrix

| Pain Point | Impact on Velocity | Maintenance Risk | Estimated Effort | Priority |
|---|---|---|---|---|
| 1. God ViewModels | **Critical** (most-touched files) | High | 2-3 days | P0 |
| 2. Platform duplication | High | **Critical** (bug propagation) | 2-3 days | P1 |
| 3. ScreenRecordingManager | Medium | High (deadlock risk) | 1-2 days | P2 |
