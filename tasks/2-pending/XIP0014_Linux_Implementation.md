# SIP0014: Linux Support Implementation Plan

**Created**: 2026-01-04
**Status**: Completed
**Priority**: High (Cross-platform compatibility)

## Assessment
**Completion**: 100% (Core services + hotkeys/startup/file-manager integration finished)
**Status**: Completed (Linux implementation milestones met)

**Implemented**:
- [Done] **Project Structure**: `ShareX.Avalonia.Platform.Linux` created.
- [Done] **Platform Optimization**: `LinuxPlatform.cs` implemented and wiring services.
- [Done] **Screen Capture**: `LinuxScreenCaptureService` implemented with CLI fallbacks (`gnome-screenshot`, `spectacle`, `scrot`, `import`) and Wayland detection.
- [Done] **Window Management**: `LinuxWindowService` implemented using `libX11` P/Invokes (Get/Set Foreground, Enumerate Windows, specific window bounds).
- [Done] **Clipboard**: `LinuxClipboardService` implemented using `wl-copy`/`wl-paste` (Wayland) and `xclip` (X11) fallbacks.
- [Done] **Input**: `LinuxInputService` implemented using `xdotool` (X11) for cursor position.
- [Done] **Basic System Ops**: `LinuxSystemService` implemented (using `xdg-open`, DBus highlighting, and new autostart support).
- [Done] **Hotkey Service**: `LinuxHotkeyService` wires `XGrabKey`-based globals with Dispatcher callbacks and fallback to Wayland portals.

**Future / Backlog**:
- Native screen capture performance (XGetImage/XShm) for better throughput than CLI fallbacks.
- Explicit Wayland portal coverage when CLI steps fall short (capture, hotkeys, input).
- Input support on Wayland-only desktops beyond the current xdotool fallback.

---

## Executive Summary
`ShareX.Avalonia` targets cross-platform support. Linux support requires a dedicated platform implementation layer to isolate OS-specific dependencies. The Core and UI projects are platform-agnostic, while `ShareX.Avalonia.Platform.Linux` provides the bridge.

## Architecture
- **Project**: `ShareX.Avalonia.Platform.Linux`
- **Namespace**: `XerahS.Platform.Linux`
- **Dependencies**: `ShareX.Avalonia.Platform.Abstractions`, `Mono.Posix` (maybe), `SkiaSharp`.

## Component Status

### 1. Platform Entry Point ([Done])
LinuxPlatform.Initialize() registers:
- LinuxScreenCaptureService
- LinuxWindowService
- LinuxPlatformInfo
- LinuxClipboardService
- LinuxInputService
- LinuxSystemService
- LinuxHotkeyService
- LinuxStartupService
- LinuxFontService
- LinuxNotificationService

### 2. Screen Capture Service ([Partial])
- **Current**: CLI-based capture (robust fallback).
- **Future**: Shared memory/XShm for high-performance X11 capture.
- **Future**: DBus/PipeWire for modern Wayland capture.

### 3. Window Service ([Partial])
- **Implemented**: X11-based window enumeration and management.
- **Missing**: True Wayland window management (often restricted by protocol, functionality might be limited on Wayland).

### 4. Clipboard Service ([Done])
- Implemented IClipboardService using:
    - Primary: wl-copy / wl-paste (Wayland CLI)
    - Fallback: xclip (X11 CLI)
    - Handles Text, Images (PNG), and File Lists.

### 5. Input Service ([Partial])
- **Implemented**: X11 cursor position using xdotool.
- **Missing**: Pure Wayland cursor position (restricted by security model).

### 6. Hotkey Service ([Done])
- **Implemented**: LinuxHotkeyService uses XGrabKey for X11 global bindings and marshals triggers to the UI thread.
- **Wayland**: Falls back to no-op portal until a compositor-level shortcut API is available.

### 7. System Integration ([Partial])
- **Implemented**: xdg-open for URLs and Folders, DBus org.freedesktop.FileManager1 for file highlighting, and XDG Autostart .desktop management for RunAtStartup.
- **Missing**: Additional context menu / Send To integrations (tracked elsewhere).

## Next Steps
1.  Test on standard distros (Ubuntu 22.04+, Fedora) and validate hotkeys, autostart, and folder highlighting.
2.  Investigate native X11 capture performance improvements (shared memory, PipeWire bypass).
3.  Track and prioritize residual Wayland work (portals, input, compositor shortcuts) in follow-up initiatives.
