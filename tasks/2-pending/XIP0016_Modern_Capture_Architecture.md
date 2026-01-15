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

### Stage 3: macOS - Native ScreenCaptureKit Integration
**Objective**: Replace the CLI-based `screencapture` subprocess calls with the native `ScreenCaptureKit` framework (available macOS 12.3+) for higher performance and better integration.

> [!NOTE]
> The macOS platform has a fallback `MacOSScreenshotService` using the `screencapture` CLI tool. This will be retained as a fallback for macOS versions prior to 12.3 (Monterey).

**Current Implementation Status**: ✅ Complete
- ✅ `MacOSScreenshotService.cs` - CLI-based fallback (complete)
- ✅ `MacOSScreenCaptureKitService.cs` - Native ScreenCaptureKit (complete)

**Technical Approach**:

Since direct P/Invoke from C# to Swift is not possible due to differing calling conventions, we will:

1. **Create a Native Objective-C Library** (`libscreencapturekit_bridge.dylib`):
   - Wrap ScreenCaptureKit's `SCScreenshotManager.CaptureImage` API
   - Expose C-compatible functions for P/Invoke
   - Handle memory management (ARC on native side, proper cleanup on C# side)
   - Return raw PNG/JPEG bytes to avoid complex struct marshalling

2. **C# P/Invoke Layer** (`MacOSScreenCaptureKitService.cs`):
   - Use `[LibraryImport]` to call native functions
   - Marshal byte arrays for image data transfer
   - Decode returned bytes with SkiaSharp
   - Implement fallback to CLI-based service on error or unsupported OS

**Native Library API Design**:
```c
// screencapturekit_bridge.h
int sck_capture_fullscreen(uint8_t** out_data, int* out_length);
int sck_capture_rect(float x, float y, float w, float h, uint8_t** out_data, int* out_length);
int sck_capture_window(uint32_t window_id, uint8_t** out_data, int* out_length);
void sck_free_buffer(uint8_t* data);
int sck_is_available(); // Returns 1 if macOS 12.3+, 0 otherwise
```

**Requirements**:
*   **macOS 12.3+ (Monterey)**: ScreenCaptureKit requires this minimum version
*   **Screen Recording Permission**: User must grant permission in System Preferences > Privacy & Security > Screen Recording
*   **Xcode Command Line Tools**: Required for building native library

## Architectural Changes
*   The `IScreenCaptureService` interface already exists in `ShareX.Avalonia.Platform.Abstractions`.
*   Each stage will implement/replace a concrete service:
    *   **Stage 1**: `WindowsModernCaptureService` ✅ Complete
    *   **Stage 2**: `LinuxScreenCaptureService` ✅ Complete
    *   **Stage 3**: `MacOSScreenCaptureKitService` 🔄 In Progress
        *   Native library: `native/macos/libscreencapturekit_bridge.dylib`
        *   Fallback: `MacOSScreenshotService` (CLI-based) for macOS < 12.3
*   The system will auto-detect the OS and version to select the best available provider, falling back to legacy methods (GDI+/CLI) only when modern APIs are unavailable.

## Stage 3 Implementation Files

### Native Library (Objective-C)
- **[NEW]** `native/macos/screencapturekit_bridge.m` - Objective-C wrapper for ScreenCaptureKit
- **[NEW]** `native/macos/screencapturekit_bridge.h` - C-compatible header for P/Invoke
- **[NEW]** `native/macos/Makefile` - Build script for dylib

### C# Service
- **[NEW]** `src/ShareX.Avalonia.Platform.MacOS/Native/ScreenCaptureKitInterop.cs` - P/Invoke declarations
- **[NEW]** `src/ShareX.Avalonia.Platform.MacOS/MacOSScreenCaptureKitService.cs` - Native capture service
- **[MODIFY]** `src/ShareX.Avalonia.Platform.MacOS/MacOSPlatform.cs` - Register new service with fallback logic

### CI/CD
- **[NEW]** `.github/workflows/macos-build.yml` - Automated build for native library and solution

## Verification Plan

### Automated (Build)
```bash
# Build native library on macOS
cd native/macos && make

# Build the .NET solution
dotnet build ShareX.Avalonia.sln
```

### Manual Testing (macOS 12.3+)
1. Run the application on macOS
2. Trigger screenshot capture (fullscreen, region, window)
3. Verify screenshot appears correctly
4. Check debug logs for `[ScreenCaptureKit]` entries confirming native API usage

### Fallback Testing (macOS < 12.3 or native library missing)
1. Rename/remove `libscreencapturekit_bridge.dylib`
2. Run the application
3. Verify capture still works (falls back to CLI)
4. Check debug logs for `[MacOSCapture]` entries indicating CLI fallback

