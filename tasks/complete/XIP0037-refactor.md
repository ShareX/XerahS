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

## Pain Point 3: `ScreenRecordingManager` — Duplicated Logic & State Complexity (1,003 lines) ✅ REFACTORED

> **Status:** Completed
> **Approach:** Extract shared recording core + remove Console.WriteLine debug spam

### What was done

Consolidated the two nearly-identical recording start methods and removed all `Console.WriteLine` debug statements:

1. **Extracted `StartRecordingCoreAsync`** — The 2-attempt recording loop (lock → create service → wire events → start → fallback on failure) was copy-pasted between `StartRecordingAsync` and `StartRecordingInternalAsync`. Now both call a single shared `StartRecordingCoreAsync(optionsToStart, preferFallback)` method.

2. **Removed 14 `Console.WriteLine` calls** — Each had a proper `DebugHelper.WriteLine` or `TroubleshootingHelper.Log` call right next to it, making them purely redundant debug spam.

3. **Simplified `StartRecordingInternalAsync`** — Reduced from ~100 lines to 6 lines (init check + fallback decision + prepare options + delegate to core).

**Line count:** 1,003 → ~870 lines (~130 lines removed)

### Remaining opportunities (not addressed)
- Extract `RecordingSession` state object to encapsulate the 11 mutable fields
- Replace `ShouldForceFallback()` with a strategy/decision-table pattern
- These are lower-priority and would benefit from broader test coverage first

---

## Priority Matrix

| Pain Point | Impact on Velocity | Maintenance Risk | Estimated Effort | Priority | Status |
|---|---|---|---|---|---|
| 1. God ViewModels | **Critical** (most-touched files) | High | 2-3 days | P0 | ✅ Done |
| 2. Platform duplication | High | **Critical** (bug propagation) | 2-3 days | P1 | ✅ Done |
| 3. ScreenRecordingManager | Medium | High (deadlock risk) | 1-2 days | P2 | ✅ Done |
