# CX07: MacOS Implementation

## Priority
**HIGH** - Major cross-platform milestone.

## Assignee
**Codex**

## Branch
`feature/macos-implementation`

## Status
Complete - Verified on 2026-01-08

## Assessment
100% Complete. `MacOSPlatform.Initialize()` wired, standard window services implemented, clipboard services active.

## Instructions
**CRITICAL**: Create the `feature/macos-implementation` branch first before starting work.

```bash
git checkout master
git pull origin master
git checkout -b feature/macos-implementation
```

## Objective
Implement macOS-specific platform services to enable core ShareX functionality on macOS. This includes screen capture, window management, and file system integration.

## Scope

### 1. MacOS Platform Initialization
- **Register MacOS services** in `PlatformManager`
- **Ensure correct service loading** when running on macOS

### 2. Window Management
- **Implement `MacOSWindowService`**
- **Implement `SetForegroundWindow`** (Critical for activating window after capture)
- **Implement `GetForegroundWindow`**
- **Implement `IsWindowMaximized/Minimized`**

### 3. Screen Capture Integration
- **CLI Fallback**: Use `screencapture` CLI tool as initial implementation
- **Native Implementation (Future)**: ScreenCaptureKit (See SIP0016)

## Deliverables
- ✅ `MacOSPlatform.cs` implemented and registered
- ✅ `MacOSWindowService.cs` implemented
- ✅ Basic screen capture working via CLI
- ✅ Build verification successful
