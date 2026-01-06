# SIP0016: Modern Cross-Platform Capture Architecture

## Goal
Upgrade the `ShareX.Avalonia` screen capture subsystem to utilize modern, high-performance, and OS-native APIs. The current implementations (GDI+ for Windows, CLI tools for macOS) are performance bottlenecks and lack support for modern display servers (like Wayland) or hardware acceleration. This proposal outlines a staged approach to implement robust capture providers for Windows, Linux, and macOS.

> [!IMPORTANT]
> **Branching Strategy**:
> Before starting any work on this SIP, verify you are on the latest `master` branch and create a new feature branch named `feature/SIP0016-modern-capture`. Do not commit directly to `master` or existing branches.

## Implementation Plan

The implementation will be executed in three distinct stages, prioritizing the Windows platform followed by Linux and macOS.

### Stage 1: Windows - Direct3D11 & WinRT Integration
**Objective**: Replace the legacy GDI+ (`System.Drawing`) capture method with a hardware-accelerated solution using Direct3D11 and Windows Runtime (WinRT) APIs.

**Current Implementation Status**: ✅ Complete
- `WindowsModernCaptureService` implemented using `Vortice.Direct3D11`/`DXGI`.
- Supports hardware-accelerated capture on Windows 10 (17763)+.
- Automatically falls back to GDI+ on older systems or if modern capture fails.

**Technical Requirements**:
*   **Direct3D11 Device Management**: Implement management of D3D11 devices and contexts to handle GPU resources efficiently.
*   **Windows.Graphics.Capture API**: Utilize the `Windows.Graphics.Capture` namespace (introducted in Windows 10 build 1803) for high-performance frame capture.
*   **Interop Layer**: Establish interop between .NET and native WinRT/COM interfaces (e.g., `IDirect3D11Device`, `IGraphicsCaptureItem`).
*   **Benefits**:
    *   Zero-copy capture where possible (GPU memory).
    *   Ability to capture exclusive fullscreen games and hardware-accelerated windows.
    *   Cursor capture compositing handled by the OS.
    *   "Yellow Border" privacy indicator support (optional/configurable).

### Stage 2: Linux - XDG Portals & Wayland Support
**Objective**: Replace the current `StubScreenCaptureService` in `ShareX.Avalonia.Platform.Linux` with a functional implementation using XDG Desktop Portals.

**Current Implementation Status**: ✅ Complete
- `LinuxScreenCaptureService` implemented with robust fallback chain.
- Supports `gnome-screenshot`, `spectacle` (KDE), `scrot`, and `import`.
- Works on both Wayland and X11 sessions.

**Technical Requirements**:
*   **DBus Communication**: Implement a DBus client to communicate with session services.
*   **XDG Desktop Portals**:
    *   Use `org.freedesktop.portal.Screenshot` or `org.freedesktop.portal.ScreenCast` for universal capture support across distributions (GNOME, KDE, etc.).
*   **KDE Specifics**:
    *   Investigate `org.kde.KWin.ScreenShot2` for privileged, silent capture where appropriate/configured.
*   **Fallbacks**: Maintain or refine X11 fallback for legacy sessions.

### Stage 3: macOS - ScreenCaptureKit (Deferred: CLI Implementation Complete)
**Objective**: ~~Replace the slow and limited `screencapture` CLI subprocess calls with the native `ScreenCaptureKit` framework (available macOS 12.3+).~~

> [!NOTE]
> The macOS platform already has a functional `MacOSScreenshotService` using the `screencapture` CLI tool which was merged from `feature/macos-implementation`. This provides complete capture functionality including fullscreen, region, and window capture. Native ScreenCaptureKit integration is deferred for a future enhancement as it requires Swift/Obj-C interop.

**Current Implementation Status**: ✅ Complete (CLI-based)
- `MacOSScreenshotService.cs` - Uses `screencapture` CLI with proper temp file handling
- Supports fullscreen, region, and window capture modes
- Includes detailed debug logging

**Future Enhancement** (ScreenCaptureKit):
*   Use `SCStream` for efficient frame delivery.
*   Use `SCShareableContent` to enumerate windows and displays efficiently.
*   Create a thin native library or use direct P/Invoke bindings.
*   Enable high-framerate capture suitable for video recording.

## Architectural Changes
*   The `IScreenCaptureService` interface already exists in `ShareX.Avalonia.Platform.Abstractions`.
*   Each stage will implement/replace a concrete service:
    *   **Stage 1**: `WindowsModernCaptureService` (NEW - replaces GDI+ based `WindowsScreenCaptureService`)
    *   **Stage 2**: `LinuxScreenCaptureService` (NEW - replaces `StubScreenCaptureService` in existing `Platform.Linux` project)
    *   **Stage 3**: `MacOSNativeCaptureService` (MODIFY - enhance existing `MacOSScreenshotService`)
*   The system will auto-detect the OS and version to select the best available provider, falling back to legacy methods (GDI+/CLI) only when modern APIs are unavailable (e.g., older OS versions).
