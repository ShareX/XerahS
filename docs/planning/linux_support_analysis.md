# Linux Support Analysis

## Overview
This document summarizes the current state of Linux support in `ShareX.Avalonia`. The implementation is located primarily in the `ShareX.Avalonia.Platform.Linux` project, with abstraction layers allowing for cross-platform compatibility in the core application.

**Current Status**: Partial / In-Progress
**Architecture**: Platform-specific implementation via `XerahS.Platform.Linux` namespace, injected at runtime.

## Implemented Features

### 1. Platform Detection & Information
- **Class**: `LinuxPlatformInfo.cs`
- **Capabilities**:
  - Correctly identifies `PlatformType.Linux`.
  - Reports OS version and architecture.
  - Checks for elevation (root privileges) using `geteuid` P/Invoke.

### 2. Screen Capture
- **Class**: `LinuxScreenCaptureService.cs`
- **Strategy**: CLI-based fallback chain.
- **Workflow**:
  - Detects **Wayland** environment via `XDG_SESSION_TYPE` environment variable.
  - Tries the following tools in order:
    1. `gnome-screenshot` (GNOME)
    2. `spectacle` (KDE Plasma)
    3. `scrot` (Generic X11)
    4. `import` (ImageMagick)
- **Region Capture**: Implemented by capturing the full screen and cropping the result in memory using `SkiaSharp`.
- **Window Capture**: Currently falls back to capturing the window bounds (via `GetWindowBounds`) from a full-screen screenshot.

### 3. Window Management
- **Class**: `LinuxWindowService.cs`
- **Technology**: X11 (libX11) P/Invokes.
- **Capabilities**:
  - `GetWindowBounds`, `GetWindowText`, `GetWindowClassName`.
  - `GetAllWindows`: Iterates the X11 window tree.
  - `IsWindowVisible`: Checks X11 map state.
  - `SetForegroundWindow`: Uses `XSetInputFocus` and `XRaiseWindow`.
- **Wayland Limitation**: The current implementation relies heavily on X11. It attempts to open an X display (`XOpenDisplay`). If running purely on Wayland (without XWayland), this service may fail or degrade to a safe fallback (internally catches exceptions).

## Initialization
The platform is initialized in `ShareXBootstrap.cs` and `Program.cs`.
- Checks `OperatingSystem.IsLinux()`.
- Calls `XerahS.Platform.Linux.LinuxPlatform.Initialize()`.
- Recording capabilities are initialized asynchronously to prevent UI blocking.

## Gaps & Missing Features

Based on code analysis and `SIP0014` status:

| Feature | Status | Notes |
| :--- | :--- | :--- |
| **Clipboard** | ❌ Missing | No Linux-specific `IClipboardService` found. SIP0014 mentions need for `xclip` / `wl-copy` or native implementation. |
| **Hotkeys** | ❌ Missing | Global hotkeys are not implemented. Requires `XGrabKey` (X11) or Global Shortcuts Portal (Wayland). |
| **Native Wayland Support** | ⚠️ Partial | Screen capture relies on CLI tools which *may* work on Wayland if the tool supports it (e.g. spectacle/gnome-screenshot), but no direct use of XDG Desktop Portals in C#. |
| **Window Input** | ❌ Missing | No implementation for simulating input (mouse/keyboard) on Linux. |
| **Performance** | ⚠️ Suboptimal | Screen capture currently uses file-based intermediate storage (`sharex_screenshot_GUID.png`) rather than shared memory buffers (Shm). |

## Recommendations
1.  **Prioritize Hotkeys**: Global hotkeys are essential for ShareX's core workflow.
2.  **Implement Clipboard**: Basic text/image copy support is needed.
3.  **Evaluate Desktop Portals**: For better Wayland compatibility and functional parity, consider implementing the `XDG Desktop Portal` DBus API directly instead of relying on CLI wrappers.
