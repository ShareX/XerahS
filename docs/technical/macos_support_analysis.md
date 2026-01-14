# MacOS Support Analysis

## Overview
This document summarizes the current state of macOS support in `ShareX.Avalonia`. The implementation is located primarily in the `ShareX.Avalonia.Platform.MacOS` project. Unlike the Linux implementation, macOS support is considered **feature-complete** (MVP level) with robust fallbacks.

**Current Status**: Complete (MVP)
**Architecture**: Platform-specific implementation via `XerahS.Platform.MacOS` namespace.

## Implemented Features

### 1. Platform Detection & Information
- **Class**: `MacOSPlatformInfo.cs`
- **Capabilities**:
  - Correctly identifies `PlatformType.MacOS`.
  - Checks for elevation via `root` user check.

### 2. Screen Capture
- **Classes**: `MacOSScreenshotService.cs` (CLI), `MacOSScreenCaptureKitService.cs` (Native).
- **Strategy**: Dual-mode (Native with CLI fallback).
- **Native Implementation (`ScreenCaptureKit`)**:
  - Requires macOS 12.3+.
  - Uses `libscreencapturekit_bridge.dylib` via P/Invoke.
  - Supports high-performance Fullscreen and Rect capture.
- **CLI Fallback (`screencapture`)**:
  - Used when native library is missing or on older macOS versions.
  - Handles **Interactive Region** capture (delegates to `screencapture -i`).
  - Handles **Window** capture (delegates to `screencapture -w`).
  - Robust but slower than native API.

### 3. Window Management
- **Class**: `MacOSWindowService.cs`
- **Technology**: AppleScript (`osascript`) via `System Events`.
- **Capabilities**:
  - **Window Info**: Retrieves bounds, title, process ID of the frontmost window.
  - **Window Control**: Can Minimize, Maximize (Zoom), and Move/Resize windows.
  - **Activation**: Activates windows using AppleScript (`tell application "..." to activate`).
- **Limitations**:
  - `GetAllWindows` currently only returns the **frontmost** window due to AppleScript performance/complexity trade-offs.
  - Slower than native C API P/Invokes (Quartz Window Services), but safer and easier to implement without complex bridging.

### 4. Clipboard Support
- **Class**: `MacOSClipboardService.cs`
- **Technology**: Hybrid `pbcopy`/`pbpaste` + AppleScript.
- **Capabilities**:
  - **Text**: Uses `pbcopy` and `pbpaste` processes (fast/standard).
  - **Images**: Uses AppleScript (`the clipboard as «class PNGf»`) to read/write PNG data.
  - **Files**: Uses AppleScript to get/set file lists (`POSIX file` format).
- **Status**: Fully functional for core ShareX needs.

## Initialization
The platform is initialized in `ShareXBootstrap.cs`:
- Checks `OperatingSystem.IsMacOS()`.
- Calls `MacOSPlatform.Initialize()`.
- Recording capabilities are initialized asynchronously.
- Logging confirms if Native or CLI capture services are being used.

## Comparison with Linux Support

| Feature | MacOS Status | Linux Status | Difference |
| :--- | :--- | :--- | :--- |
| **Capture Stability** | ✅ High (Native + CLI) | ⚠️ Medium (CLI only) | MacOS has a native API path (`ScreenCaptureKit`). |
| **Window Mgmt** | ✅ Functional (AppleScript) | ⚠️ Partial (X11 only) | MacOS works for all apps; Linux X11 logic fails on Wayland. |
| **Clipboard** | ✅ Complete | ❌ Missing | MacOS has full read/write for text/images/files. |
| **Installation** | ✅ Easy (Self-contained) | ⚠️ Complex | MacOS logic is bundled; Linux requires external tools (`scrot` etc). |

## Recommendations
1.  **Optimization**: `MacOSWindowService` relies heavily on `osascript` execution. This is slow (hundreds of ms). Future optimization should look into `CGWindowListCopyWindowInfo` (Quartz Window Services) for high-performance window enumeration.
2.  **Native Window Capture**: Currently `ScreenCaptureKit` service falls back to CLI for window capture. Implementing `SCContentFilter` for window IDs would provide high-performance window capture without the interactive CLI overlay.
