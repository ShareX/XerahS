# SIP0014: Linux Support Implementation Plan

**Created**: 2026-01-04
**Status**: In Progress
**Priority**: High (Cross-platform compatibility)

## Assessment
**Completion**: ~50%
**Status**: In Progress / Pending

**Implemented**:
- ✅ **Project Structure**: `ShareX.Avalonia.Platform.Linux` created.
- ✅ **Platform Optimization**: `LinuxPlatform.cs` implemented and wiring services.
- ✅ **Screen Capture**: `LinuxScreenCaptureService` implemented with CLI fallbacks (`gnome-screenshot`, `spectacle`, `scrot`, `import`) and Wayland detection.
- ✅ **Window Management**: `LinuxWindowService` implemented using `libX11` P/Invokes (Get/Set Foreground, Enumerate Windows, specific window bounds).

**Missing / To Do**:
- ❌ **Clipboard**: `StubClipboardService` is currently a stub. Needs `xclip`/`wl-copy` or native implementation.
- ❌ **Hotkeys**: `StubHotkeyService` is a stub. Needs global hotkey handling (XGrabKey / Wayland compositor shortcuts).
- ❌ **Input**: `StubInputService` is a stub.
- ❌ **Native Screen Capture**: Future optimization using native X11 `XGetImage` instead of CLI tools for better performance.
- ❌ **Wayland Native**: Screen capture currently uses portal or CLI; explicit Portal API implementation might be needed if CLI fails or is slow.

---

## Executive Summary
ShareX.Avalonia targets cross-platform support. Linux support requires a dedicated platform implementation layer to isolate OS-specific dependencies. The Core and UI projects are platform-agnostic, while `ShareX.Avalonia.Platform.Linux` provides the bridge.

## Architecture
- **Project**: `ShareX.Avalonia.Platform.Linux`
- **Namespace**: `XerahS.Platform.Linux`
- **Dependencies**: `ShareX.Avalonia.Platform.Abstractions`, `Mono.Posix` (maybe), `SkiaSharp`.

## Component Status

### 1. Platform Entry Point (✅ Done)
`LinuxPlatform.Initialize()` registers:
- `LinuxScreenCaptureService`
- `LinuxWindowService`
- `LinuxPlatformInfo`
- [Stubs for others]

### 2. Screen Capture Service (✅ Partial)
- **Current**: CLI-based capture (robust fallback).
- **Future**: Shared memory/XShm for high-performance X11 capture.
- **Future**: DBus/PipeWire for modern Wayland capture.

### 3. Window Service (✅ Partial)
- **Implemented**: X11-based window enumeration and management.
- **Missing**: True Wayland window management (often restricted by protocol, functionality might be limited on Wayland).

### 4. Clipboard Service (❌ Pending)
- Need to implement `IClipboardService` using:
    - `xclip` / `xsel` (CLI)
    - or Avalonia's `Application.Current.Clipboard` (if sufficient, but usually need lower level control for files/images sometimes).

### 5. Hotkey Service (❌ Pending)
- Critical for ShareX.
- **X11**: `XGrabKey`.
- **Wayland**: Global shortcuts portal.

## Next Steps
1.  Implement `LinuxClipboardService`.
2.  Implement `LinuxHotkeyService`.
3.  Test on standard distros (Ubuntu 22.04+, Fedora).
