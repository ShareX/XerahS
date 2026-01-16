# SIP0014: Linux Support Implementation Plan

**Created**: 2026-01-04
**Status**: In Progress
**Priority**: High (Cross-platform compatibility)

## Assessment
**Completion**: ~70% (Core Services Done; Hotkeys, Startup, & Polish Pending)
**Status**: In Progress / Pending

**Implemented**:
- ✅ **Project Structure**: `ShareX.Avalonia.Platform.Linux` created.
- ✅ **Platform Optimization**: `LinuxPlatform.cs` implemented and wiring services.
- ✅ **Screen Capture**: `LinuxScreenCaptureService` implemented with CLI fallbacks (`gnome-screenshot`, `spectacle`, `scrot`, `import`) and Wayland detection.
- ✅ **Window Management**: `LinuxWindowService` implemented using `libX11` P/Invokes (Get/Set Foreground, Enumerate Windows, specific window bounds).
- ✅ **Clipboard**: `LinuxClipboardService` implemented using `wl-copy`/`wl-paste` (Wayland) and `xclip` (X11) fallbacks.
- ✅ **Input**: `LinuxInputService` implemented using `xdotool` (X11) for cursor position.
- ✅ **Basic System Ops**: `LinuxSystemService` implemented (using `xdg-open` for opening URLs/Folders).

**Missing / To Do**:
- ❌ **Hotkeys**: `StubHotkeyService` is a stub. Needs global hotkey handling (XGrabKey / Wayland compositor shortcuts).
- ❌ **Startup Integration**: Auto-start on login (XDG Autostart standard `~/.config/autostart`).
- ❌ **Advanced File Manager**: "Show in Folder" currently just opens the folder. Need DBus (`org.freedesktop.FileManager1`) to actually *select* the file.
- ❌ **Native Screen Capture**: Future optimization using native X11 `XGetImage` instead of CLI tools for better performance.
- ❌ **Wayland Native**: Screen capture currently uses portal or CLI; explicit Portal API implementation might be needed if CLI fails or is slow.
- ❌ **Input (Wayland)**: Cursor position currently relies on `xdotool` which may not work on pure Wayland without XWayland or specific compositor protocols.

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
- `LinuxClipboardService`
- `LinuxInputService`
- `LinuxSystemService`
- [Stubs for others]

### 2. Screen Capture Service (✅ Partial)
- **Current**: CLI-based capture (robust fallback).
- **Future**: Shared memory/XShm for high-performance X11 capture.
- **Future**: DBus/PipeWire for modern Wayland capture.

### 3. Window Service (✅ Partial)
- **Implemented**: X11-based window enumeration and management.
- **Missing**: True Wayland window management (often restricted by protocol, functionality might be limited on Wayland).

### 4. Clipboard Service (✅ Done)
- Implemented `IClipboardService` using:
    - Primary: `wl-copy` / `wl-paste` (Wayland CLI)
    - Fallback: `xclip` (X11 CLI)
    - Handles Text, Images (PNG), and File Lists.

### 5. Input Service (✅ Partial)
- **Implemented**: X11 cursor position using `xdotool`.
- **Missing**: Pure Wayland cursor position (restricted by security model).

### 6. Hotkey Service (❌ Pending)
- Critical for ShareX.
- **X11**: `XGrabKey`.
- **Wayland**: Global shortcuts portal.

### 7. System Integration (✅ Basic / ❌ Partial)
- **Implemented**: `xdg-open` for URLs and Folders.
- **Missing**: XDG Autostart (Startup), DBus File Manager Selection.

## Next Steps
1.  Implement `LinuxHotkeyService` (Critical for complete feature set).
2.  Implement `Startup` logic (write .desktop file to autostart).
3.  Improve "Show in Folder" using DBus.
4.  Test on standard distros (Ubuntu 22.04+, Fedora).
5.  Investigate native X11 capture performance improvements.
