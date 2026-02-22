# XIP0037: Top 3 Refactoring Pain Points (src) — Deep Analysis

## Summary

Comprehensive audit of `src/` identifying the three highest-impact structural pain points that drag on daily development velocity and long-term maintainability.

---

## Pain Point 1: God ViewModels — `TaskSettingsViewModel` (1,403 lines) & `SettingsViewModel` (1,293 lines) ✅ REFACTORED

> **Status:** Completed in commit `86286af`
> **Approach:** Partial-class decomposition + `HashSet<string>` auto-save exclusion

### What was done

Split both monolithic ViewModels into navigable partial-class files with zero XAML changes and zero behavioral changes:

**TaskSettingsViewModel** (1,403 → 9 files):
| File | Contents | Lines |
|------|----------|-------|
| `TaskSettingsViewModel.cs` | Core: constructor, Model, Job, enum collections | ~100 |
| `TaskSettingsViewModel.Capture.cs` | Capture settings properties | ~180 |
| `TaskSettingsViewModel.FFmpeg.cs` | FFmpeg detection/download/diagnostics | ~290 |
| `TaskSettingsViewModel.Upload.cs` | File naming/upload settings | ~120 |
| `TaskSettingsViewModel.AfterCapture.cs` | After-capture flag properties | ~90 |
| `TaskSettingsViewModel.AfterUpload.cs` | After-upload flag properties | ~70 |
| `TaskSettingsViewModel.General.cs` | Sound, toast, notification properties | ~200 |
| `TaskSettingsViewModel.Image.cs` | Image format, quality, thumbnails | ~80 |
| `TaskSettingsViewModel.IndexFolder.cs` | Browse commands + indexer properties | ~230 |

**SettingsViewModel** (1,293 → 7 files):
| File | Contents | Lines |
|------|----------|-------|
| `SettingsViewModel.cs` | Core: constructor, LoadSettings, SaveSettings, OnPropertyChanged | ~260 |
| `SettingsViewModel.WatchFolders.cs` | Watch folder list management | ~200 |
| `SettingsViewModel.WatchFolderDaemon.cs` | Daemon lifecycle orchestration | ~350 |
| `SettingsViewModel.TaskSettings.cs` | Task settings forwarding properties | ~200 |
| `SettingsViewModel.AppSettings.cs` | Tray actions, history, OS integration | ~180 |
| `SettingsViewModel.Proxy.cs` | Proxy properties + change handlers | ~80 |
| `SettingsViewModel.Integration.cs` | Plugin, file associations, startup | ~50 |

**OnPropertyChanged fix:** Replaced 12-condition exclusion chain with `HashSet<string> AutoSaveExclusions`.

---

## Pain Point 2: Platform Service Duplication — Watch Folder Daemon (~1,700 lines across 3 files) ✅ REFACTORED

> **Status:** Completed
> **Approach:** Extract `WatchFolderDaemonServiceBase` abstract class with shared process execution utilities

### What was done

Created `WatchFolderDaemonServiceBase` in `XerahS.Platform.Abstractions/Services/` and refactored all three platform implementations to inherit from it.

**Base class provides (~200 lines):**
- `RestartAsync` — concrete implementation (stop then start), previously copy-pasted in all 3 files
- `ResolveDaemonPath` — unified daemon path resolution with platform-specific executable candidate arrays
- `RunProcessAsync` — core process runner (captures stdout/stderr, handles timeout), replaces 3 identical implementations
- `RunProcessWithArgumentsAsync` — variant using ArgumentList for safe argument passing
- `EscapeShellSingleQuotedString` — shared by macOS and Linux
- `RunPrivilegedShellScriptAsync` — temp-file-based privileged script execution pattern shared by macOS and Linux
- `CommandResult` — unified record struct replacing `ScCommandResult`, `LaunchCtlResult`, `SystemCommandResult`

**Line count changes:**
| File | Before | After | Saved |
|------|--------|-------|-------|
| `WatchFolderDaemonServiceBase.cs` (new) | — | ~200 | — |
| `WindowsWatchFolderDaemonService.cs` | 457 | ~310 | ~147 |
| `MacOSWatchFolderDaemonService.cs` | 576 | ~370 | ~206 |
| `LinuxWatchFolderDaemonService.cs` | 572 | ~340 | ~232 |
| **Total** | **1,605** | **~1,220** | **~385 net** |

**Key improvements:**
- Bug fixes in process execution only need to be made once in the base class
- Unified `CommandResult` type eliminates 3 structurally-identical record structs
- Platform subclasses focus purely on platform-specific logic (sc.exe/launchctl/systemctl, elevation, config files)
- Same pattern can be applied to hotkey services (Windows 479, Linux 462, macOS 509, Wayland Portal 646 lines)

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
