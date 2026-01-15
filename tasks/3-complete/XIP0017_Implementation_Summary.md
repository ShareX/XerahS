# SIP0017 Implementation Summary
## Screen Recording Modernization - Stage 1 MVP

**Date:** 2026-01-08
**Status:** Stage 1 Core Implementation Complete
**Verdict:** Ready for integration and testing

---

## Executive Summary

Successfully implemented the core components for modern screen recording using Windows.Graphics.Capture and Media Foundation as specified in SIP0017. All critical interfaces, platform implementations, and orchestration services have been created following existing ShareX.Avalonia architectural patterns.

**What Was Delivered:**
- ✅ Core recording interfaces (`IRecordingService`, `ICaptureSource`, `IVideoEncoder`, `IAudioCapture`)
- ✅ Complete data models and enumerations
- ✅ Windows.Graphics.Capture implementation (`WindowsGraphicsCaptureSource`)
- ✅ Media Foundation H.264 encoder (`MediaFoundationEncoder`)
- ✅ Platform-agnostic orchestration service (`ScreenRecorderService`)
- ✅ Event-based error handling and status reporting
- ✅ Dynamic factory pattern for platform abstraction

---

## Implementation Details

### 1. Files Created

#### ShareX.Avalonia.ScreenCapture Project

**`/src/ShareX.Avalonia.ScreenCapture/Recording/Models/RecordingEnums.cs`**
- Defines `CaptureMode` (Screen, Window, Region)
- Defines `RecordingStatus` (Idle, Initializing, Recording, Paused, Finalizing, Error)
- Defines `VideoCodec` (H264, HEVC, VP9, AV1) - Stage 1 uses H264 only
- Defines `PixelFormat` (Bgra32, Nv12, Rgba32, Unknown)

**`/src/ShareX.Avalonia.ScreenCapture/Recording/Models/RecordingModels.cs`**
- `RecordingOptions` - Configuration for starting recording (mode, region, path, settings)
- `ScreenRecordingSettings` - Persistent settings (codec, FPS, bitrate, audio flags, ForceFFmpeg)
- `FrameData` - Raw frame data structure (pointer, stride, dimensions, timestamp, format)
- `VideoFormat` - Encoder configuration (width, height, FPS, bitrate, codec)
- Event args: `RecordingErrorEventArgs`, `RecordingStatusEventArgs`, `FrameArrivedEventArgs`, `AudioBufferEventArgs`

**`/src/ShareX.Avalonia.ScreenCapture/Recording/IRecordingService.cs`**
- `IRecordingService` - Main recording interface (StartRecordingAsync, StopRecordingAsync, events)
- `ICaptureSource` - Platform capture abstraction (StartCaptureAsync, StopCaptureAsync, FrameArrived event)
- `IVideoEncoder` - Encoder abstraction (Initialize, WriteFrame, Finalize)
- `IAudioCapture` - Audio capture interface (Stage 6)

**`/src/ShareX.Avalonia.ScreenCapture/Recording/ScreenRecorderService.cs`**
- Platform-agnostic orchestration service
- Coordinates ICaptureSource and IVideoEncoder
- Uses factory pattern for platform-specific implementations
- Event-based status and error reporting
- Automatic output path generation (ShareX/Screenshots/yyyy-MM/Date_Time.mp4)
- Frame capture pipeline with error handling

#### ShareX.Avalonia.Platform.Windows Project

**`/src/ShareX.Avalonia.Platform.Windows/Recording/WindowsGraphicsCaptureSource.cs`**
- Implements `ICaptureSource` using Windows.Graphics.Capture API
- Requires Windows 10 version 1803+ (build 17134)
- Static `IsSupported` property for version detection
- `InitializeForWindow(IntPtr hwnd)` - Capture specific window
- `InitializeForPrimaryMonitor()` - Capture primary screen
- Direct3D11 integration for frame access
- BGRA32 pixel format support
- Cursor capture enabled by default
- COM interop for Direct3D surface access
- Proper resource disposal and thread safety

**`/src/ShareX.Avalonia.Platform.Windows/Recording/MediaFoundationEncoder.cs`**
- Implements `IVideoEncoder` using Media Foundation IMFSinkWriter
- H.264 codec in MP4 container
- Hardware encoding hint enabled (MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS)
- Static `IsAvailable` property for Media Foundation detection
- RGB32 input format (matches BGRA32 from WGC)
- Configurable bitrate and FPS
- Proper sample timing in 100ns units
- Comprehensive COM interop definitions
- Safe cleanup and error handling

---

## Design Decisions Made

### 1. Rectangle Type Resolution
**Gap Identified:** RecordingOptions.Region type was ambiguous
**Decision:** Used `System.Drawing.Rectangle` for cross-platform compatibility
**Rationale:** Already used throughout TaskSettings.cs, familiar to codebase, cross-platform via .NET

### 2. PixelFormat Naming
**Gap Identified:** Inconsistency between enum (Bgra8888) and comments (BGRA32)
**Decision:** Used `Bgra32` in enum to match industry standard naming
**Rationale:** Media Foundation uses 32-bit naming convention, clearer than 8888

### 3. Factory Pattern for Platform Abstraction
**Gap Identified:** How ScreenRecorderService stays platform-agnostic
**Decision:** Static factory properties (`CaptureSourceFactory`, `EncoderFactory`)
**Rationale:** Matches existing ShareX.Avalonia patterns (PlatformServices static locator), simple to initialize

### 4. Threading Model
**Gap Identified:** Which thread raises FrameArrived event
**Decision:** Event raised on WGC capture thread; encoder responsible for marshaling if needed
**Rationale:** Avoids unnecessary thread switches in hot path, gives encoder control over threading

### 5. Output Path Strategy
**Decision:** Default pattern `ShareX/Screenshots/yyyy-MM/Date_Time.mp4` in Documents folder
**Rationale:** Matches existing screenshot behavior, familiar to users, auto-creates monthly subdirectories

### 6. Error Handling Strategy
**Decision:** `IsFatal` flag in `RecordingErrorEventArgs` distinguishes recoverable vs fatal errors
**Rationale:** Allows UI to decide whether to continue (dropped frame warning) vs stop (encoder failure)

### 7. COM Interface Definitions
**Decision:** Embedded minimal COM interface definitions in MediaFoundationEncoder
**Rationale:** Avoids external dependencies, only exposes methods actually used, self-contained

### 8. Dynamic Dispatch for Platform Init
**Decision:** Use `dynamic` keyword in `ScreenRecorderService.InitializeCaptureSource()`
**Rationale:** Allows calling `InitializeForWindow`/`InitializeForPrimaryMonitor` without coupling to WindowsGraphicsCaptureSource type

---

## Integration Steps Required

### Step 1: Add Project References

**ShareX.Avalonia.ScreenCapture.csproj** needs reference to:
```xml
<!-- Already has these -->
<ProjectReference Include="..\ShareX.Avalonia.Common\..." />
```

**ShareX.Avalonia.Platform.Windows.csproj** needs reference to:
```xml
<ProjectReference Include="..\ShareX.Avalonia.ScreenCapture\ShareX.Avalonia.ScreenCapture.csproj" />
```

Add NuGet package for Windows.Graphics.Capture:
```xml
<PackageReference Include="Microsoft.Windows.SDK.Contracts" Version="10.0.22621.48" />
```

### Step 2: Extend TaskSettingsCapture

**File:** `src/ShareX.Avalonia.Core/Models/TaskSettings.cs`

Add property to `TaskSettingsCapture` class (after line 212):
```csharp
public XerahS.ScreenCapture.Recording.ScreenRecordingSettings NativeRecordingSettings = new();
```

### Step 3: Initialize Platform Factories

**File:** `src/ShareX.Avalonia.Platform.Windows/WindowsPlatform.cs`

Add to initialization method:
```csharp
using XerahS.Platform.Windows.Recording;
using XerahS.ScreenCapture.Recording;

public static void InitializeRecording()
{
    // Check if native recording is supported
    if (WindowsGraphicsCaptureSource.IsSupported && MediaFoundationEncoder.IsAvailable)
    {
        ScreenRecorderService.CaptureSourceFactory = () => new WindowsGraphicsCaptureSource();
        ScreenRecorderService.EncoderFactory = () => new MediaFoundationEncoder();
    }
    else
    {
        // Stage 4: Set up FFmpeg fallback here
        // ScreenRecorderService.FallbackServiceFactory = () => new FFmpegRecordingService();
    }
}
```

Call from `Program.cs` during platform initialization:
```csharp
WindowsPlatform.Initialize(...);
WindowsPlatform.InitializeRecording(); // Add this line
```

### Step 4: Register Recording Service

**Option A:** Add to PlatformServices (recommended for consistency)

**File:** `src/ShareX.Avalonia.Platform.Abstractions/PlatformServices.cs`

```csharp
public static class PlatformServices
{
    // ... existing services ...
    public static IRecordingService? Recording { get; set; }
}
```

Initialize in `WindowsPlatform.Initialize()`:
```csharp
PlatformServices.Recording = new ScreenRecorderService();
```

**Option B:** Direct instantiation in UI layer (simpler for MVP)
```csharp
// In ViewModel or wherever recording is triggered
using var recorder = new ScreenRecorderService();
```

### Step 5: Wire Up UI Commands (Example)

**File:** `src/ShareX.Avalonia.UI/ViewModels/MainViewModel.cs` or relevant ViewModel

```csharp
using XerahS.ScreenCapture.Recording;

private IRecordingService? _recordingService;

[RelayCommand]
private async Task StartRecording()
{
    _recordingService = PlatformServices.Recording ?? new ScreenRecorderService();

    _recordingService.StatusChanged += OnRecordingStatusChanged;
    _recordingService.ErrorOccurred += OnRecordingError;

    var options = new RecordingOptions
    {
        Mode = CaptureMode.Screen,
        Settings = SettingManager.Settings.DefaultTaskSettings.CaptureSettings.NativeRecordingSettings
    };

    try
    {
        await _recordingService.StartRecordingAsync(options);
    }
    catch (Exception ex)
    {
        // Handle error - show notification to user
    }
}

[RelayCommand]
private async Task StopRecording()
{
    if (_recordingService != null)
    {
        await _recordingService.StopRecordingAsync();
    }
}

private void OnRecordingStatusChanged(object? sender, RecordingStatusEventArgs e)
{
    // Update UI - show recording indicator, timer, etc.
    StatusText = $"Recording: {e.Status} - {e.Duration:mm\\:ss}";
}

private void OnRecordingError(object? sender, RecordingErrorEventArgs e)
{
    if (e.IsFatal)
    {
        // Show error dialog, stop recording
        MessageBox.Show($"Recording failed: {e.Error.Message}");
    }
}
```

---

## FFmpeg Fallback Integration (Stage 4)

### Trigger Conditions

The SIP specifies three fallback triggers:

1. **`PlatformNotSupportedException`** - Windows 10 < 1803
2. **`COMException`** - IMFSinkWriter initialization failure (driver issues)
3. **Explicit user preference** - `ScreenRecordingSettings.ForceFFmpeg = true`

### Implementation Approach

**File:** `src/ShareX.Avalonia.ScreenCapture/Recording/FFmpegRecordingService.cs` (to be created)

```csharp
public class FFmpegRecordingService : IRecordingService
{
    private readonly FFmpegCLIManager _ffmpeg = new();

    public Task StartRecordingAsync(RecordingOptions options)
    {
        // Convert RecordingOptions to FFmpegOptions
        var ffmpegOptions = ConvertToFFmpegOptions(options);

        // Use existing FFmpegCLIManager infrastructure
        string args = BuildFFmpegArgs(ffmpegOptions, options);
        _ffmpeg.Run(args);

        return Task.CompletedTask;
    }

    // ... implementation using existing FFmpegCLIManager
}
```

**Integration point:**
```csharp
// In WindowsPlatform.InitializeRecording()
if (!WindowsGraphicsCaptureSource.IsSupported || !MediaFoundationEncoder.IsAvailable)
{
    // Fall back to FFmpeg
    ScreenRecorderService.FallbackServiceFactory = () => new FFmpegRecordingService();
}
```

**User preference check:**
```csharp
// In StartRecording command
if (settings.NativeRecordingSettings.ForceFFmpeg)
{
    _recordingService = new FFmpegRecordingService();
}
else
{
    _recordingService = new ScreenRecorderService(); // May auto-fallback if native fails
}
```

---

## Testing Plan

### Automated Build Test

```bash
cd "c:\Users\liveu\source\repos\ShareX Team\ShareX.Avalonia"
dotnet build ShareX.Avalonia.sln
```

Expected: No compilation errors

### Manual Testing (Stage 1 MVP)

1. **Basic Recording Test:**
   - Launch application
   - Trigger Start Recording command
   - Verify status changes: Idle → Initializing → Recording
   - Perform on-screen actions for 5-10 seconds
   - Trigger Stop Recording
   - Verify status changes: Recording → Finalizing → Idle
   - Locate output MP4 file in `Documents\ShareX\Screenshots\{yyyy-MM}\`
   - Play file in media player - verify smooth playback, correct content

2. **Window Capture Test:**
   - Open a specific application window (e.g., Notepad)
   - Start recording with `Mode = CaptureMode.Window`, `TargetWindowHandle = {hwnd}`
   - Verify only that window is captured

3. **Settings Persistence Test:**
   - Change FPS from 30 to 60 in NativeRecordingSettings
   - Restart application
   - Verify FPS remains 60

4. **Error Handling Test:**
   - Simulate Media Foundation unavailable (rename mfplat.dll temporarily)
   - Start recording
   - Verify error event fired with `IsFatal = true`
   - Verify graceful failure (no crash)
   - Restore mfplat.dll

5. **Compatibility Test:**
   - Test on Windows 10 version 1803+
   - Test on Windows 11
   - Verify cursor capture works
   - Check for DPI scaling artifacts on high-DPI displays

---

## Known Limitations & Future Work

### Stage 1 Limitations

1. **No Audio Support** - Video only, no system audio or microphone (Stage 6)
2. **No Region Cropping** - Region mode falls back to full screen (Stage 2)
3. **H.264 Only** - HEVC/VP9/AV1 codecs not yet implemented (Stage 3)
4. **No Hardware Encoder Selection UI** - Uses default MF encoder (Stage 3)
5. **No Pause/Resume** - Only start/stop supported (Stage 6)
6. **FFmpeg Fallback Not Implemented** - Manual fallback only (Stage 4)
7. **No Cross-Platform Support** - Windows only (Stage 7)

### Next Stages

**Stage 2: Window & Region Parity**
- Implement `GraphicsCapturePicker` for window selection UI
- Post-capture crop pipeline for region recording
- Software cursor overlay if WGC cursor disabled

**Stage 3: Advanced Native Encoding**
- Hardware encoder detection (NVENC, QSV, AMF)
- Expose bitrate/FPS controls in UI
- Bind to TaskSettingsViewModel

**Stage 4: FFmpeg Fallback & Auto-Switch**
- Implement `FFmpegRecordingService` wrapper
- Auto-detect and switch on exceptions
- Migration from existing FFmpeg settings

**Stage 5: Migration & Presets**
- Import logic for existing ShareX config
- "Modern vs Legacy" toggle in settings UI

**Stage 6: Audio Support**
- WasapiLoopbackCapture for system audio
- WasapiMicrophoneCapture for mic input
- Mix audio streams into encoder

**Stage 7: macOS & Linux**
- XDG Portal (Linux) via DBus
- ScreenCaptureKit (macOS) with AVAssetWriter

---

## Files Modified

None - all new files created. Integration requires manual edits to:
- `TaskSettings.cs` - add NativeRecordingSettings property
- `WindowsPlatform.cs` - add InitializeRecording() method
- `Program.cs` or `App.axaml.cs` - call InitializeRecording()
- ViewModel (TBD) - wire up Start/Stop recording commands

---

## Build Instructions

### Prerequisites
- .NET 10 SDK
- Windows 10 SDK (version 10.0.22621.0 or later for Windows.Graphics.Capture)
- Visual Studio 2022 (or Rider/VS Code with C# Dev Kit)

### Add Required NuGet Package

```bash
cd "src/ShareX.Avalonia.Platform.Windows"
dotnet add package Microsoft.Windows.SDK.Contracts --version 10.0.22621.48
```

### Build

```bash
cd "c:\Users\liveu\source\repos\ShareX Team\ShareX.Avalonia"
dotnet restore
dotnet build src/ShareX.Avalonia.ScreenCapture/ShareX.Avalonia.ScreenCapture.csproj
dotnet build src/ShareX.Avalonia.Platform.Windows/ShareX.Avalonia.Platform.Windows.csproj
dotnet build ShareX.Avalonia.sln
```

Expected output: Build succeeded, 0 errors

### Run

```bash
dotnet run --project src/ShareX.Avalonia.App/ShareX.Avalonia.App.csproj
```

---

## Code Quality & Best Practices

### Followed Existing Patterns

✅ License headers on all files
✅ XerahS namespace convention
✅ XML documentation comments on all public APIs
✅ Dispose pattern with lock-based thread safety
✅ Event-based async patterns
✅ Static service locator (PlatformServices)
✅ Factory pattern for platform abstraction
✅ COM interop with proper cleanup

### Security Considerations

✅ No command injection vectors (native APIs, not CLI)
✅ Proper resource disposal (IDisposable on all classes)
✅ Thread-safe state management (lock keyword)
✅ Exception handling with fatal/non-fatal classification
✅ No hardcoded paths (uses Environment.SpecialFolder)

### Performance Optimizations

✅ Direct memory copy for frame data (unsafe pointer operations)
✅ COM object lifetime management (minimize allocations)
✅ Hardware encoding hint enabled
✅ Frame pool for buffer reuse (WGC manages)
✅ Event-driven architecture (no polling)

---

## Conclusion

**Stage 1 MVP Implementation: ✅ COMPLETE**

All core components for native Windows screen recording have been implemented according to SIP0017 specifications. The code is ready for:

1. **Integration** - Follow steps in "Integration Steps Required" section
2. **Testing** - Execute manual testing plan
3. **Refinement** - Address any issues found during testing

**Next Action:** Integrate into existing codebase, build, and perform Stage 1 manual testing.

**Estimated Integration Time:** 30-60 minutes
**Estimated Testing Time:** 1-2 hours
**Risk Level:** Low - all critical path code implemented and follows existing patterns

---

**Implementation completed by:** Claude Code
**Review required:** ShareX Team
**Approval for Stage 2:** Pending Stage 1 testing success
