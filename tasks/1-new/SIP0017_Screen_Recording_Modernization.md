# SIP0017: Screen Recording Modernization

## Goal
Upgrade the `ShareX.Avalonia` screen recording subsystem to utilize modern, high-performance, and OS-native APIs. The current process-based integration utilizing FFmpeg CLI is robust but lacks efficiency and modern OS integration (e.g., proper cursor composition, protected content handling). This proposal outlines a staged approach to implement robust recording providers for Windows, Linux, and macOS, with a focus on Windows.Graphics.Capture (WGC).

## Milestones

| Date | Commit | Description |
|------|--------|-------------|
| 2026-01-08 | `30f7273` | **Stage 1 Zero Build Errors**: UI integration complete (RecordingViewModel, RecordingToolbarView, RecordingView), COM interop fixed (IGraphicsCaptureItemInterop, IDirect3DDxgiInterfaceAccess), TFM standardized to net10.0-windows + TargetPlatformVersion=10.0.19041.0 |

## Implementation Plan

The implementation will be executed in seven distinct stages, prioritizing core recording capability, followed by advanced features, and finally cross-platform support.

### Stage 1: MVP Recording (Silent)
**Objective**: Implement the primary recording path on Windows using **Windows.Graphics.Capture (WGC)** and **Media Foundation** (no audio).

**Technical Requirements**:
*   **Capture Source**: Implement `WindowsGraphicsCaptureSource` wrapping WGC APIs.
*   **Encoder**: Implement `MediaFoundationEncoder` using `IMFSinkWriter` for H.264/MP4 output.
*   **UI Integration**: Wire up `Start`/`Stop` recording actions in the main UI via `MainViewModel` and `RecordingToolbarView`.

### Stage 2: Window & Region Parity
**Objective**: Achieve parity with existing region/window selection capabilities using modern APIs.

**Technical Requirements**:
*   **Window Selection**: Integrate `GraphicsCapturePicker` for targeting specific windows.
*   **Region Cropping**: Implement a post-capture crop pipeline (Capture Full -> Crop -> Encode) to support region recording.
*   **Cursor Overlay**: Implement software cursor rendering if WGC system cursor is disabled or unavailable.

### Stage 3: Advanced Native Encoding
**Objective**: Expose advanced encoding controls and leverage hardware acceleration.

**Technical Requirements**:
*   **Hardware Acceleration**: Verify and expose Media Foundation hardware encoder usage (NVENC, QSV, AMF).
*   **Quality Controls**: Add Bitrate and FPS controls to the UI.
*   **Configuration**: Bind these settings to the persistent `ScreenRecordingSettings`.

### Stage 4: FFmpeg Fallback & Auto-Switch
**Objective**: Ensure reliability by falling back to FFmpeg when native methods fail.

**Technical Requirements**:
*   **FFmpeg Service**: Implement `FFmpegRecordingService` (wrapping existing CLIManager architecture) implementing `IRecordingService`.
*   **Auto-Switch**: Implement logic in `ScreenRecorderService` to catch native exceptions and transparently switch to FFmpeg fallback.
*   **Trigger Conditions**: 
    1.  `PlatformNotSupportedException` (Win10 < 1803).
    2.  `COMException` during `IMFSinkWriter` initialization (Driver issues).
    3.  Explicit user preference (`ForceFFmpeg = true`).

### Stage 5: Migration & Presets
**Objective**: Migrate existing users and provide configuration compatibility.

**Technical Requirements**:
*   **Import Logic**: Parse existing ShareX config files for FFmpeg settings.
*   **UI Controls**: Add a "Modern vs Legacy" toggle in settings.

### Stage 6: Audio Support
**Objective**: Implement audio capture for system sound and microphone.

**Technical Requirements**:
*   **System Audio**: Implement `WasapiLoopbackCapture` to capture system audio output.
*   **Microphone**: Implement `WasapiMicrophoneCapture` for voice input.
*   **Mixing**: Integrate audio streams into the `MediaFoundationEncoder` sink writer.

### Stage 7: macOS & Linux Implementation
**Objective**: Expand native recording support to cross-platform targets.

**Technical Requirements**:
*   **Linux**: Implement `XDGPortalCaptureSource` (ScreenCast) via DBus. Use FFmpeg CLI for encoding initially.
*   **macOS**: Extend `ScreenCaptureKit` interop to support stream callbacks (continuous capture). Implement `AVAssetWriterInterop` for native encoding.

## Architectural Changes

We propose a modular interface-based architecture compliant with `AGENTS.md` platform abstraction rules.

### Services & Interfaces

```csharp
namespace ShareX.Avalonia.ScreenCapture.Recording;

public interface IRecordingService
{
    // Note: CancellationToken support deferred to future optimization
    Task StartRecordingAsync(RecordingOptions options);
    Task StopRecordingAsync();
    event EventHandler<RecordingErrorEventArgs> ErrorOccurred;
    event EventHandler<RecordingStatusEventArgs> StatusChanged;
}

public interface ICaptureSource : IDisposable
{
    // Windows: WindowsGraphicsCaptureSource, Linux: PipeWireCaptureSource, macOS: ScreenCaptureKitSource
    Task StartCaptureAsync();
    event EventHandler<FrameArrivedEventArgs> FrameArrived;
}

public interface IVideoEncoder : IDisposable
{
    // Windows: MediaFoundationEncoder, Fallback: FFmpegPipeEncoder
    void Initialize(VideoFormat format, string outputPath);
    void WriteFrame(FrameData frame);
    void Finalize();
}

public interface IAudioCapture : IDisposable
{
    // Windows: WasapiAudioCapture, Linux: PulseAudio, macOS: CoreAudio
    void Start();
    event EventHandler<AudioBufferEventArgs> AudioDataAvailable;
}
```

## Detailed Design

### Project Structure
New components will be organized as follows:
*   `ShareX.Avalonia.ScreenCapture` (Project)
    *   `Recording/` (Folder) - Core interfaces and logic.
    *   `Recording/Models/` (Folder) - Data models (`RecordingOptions`, etc.).
*   `ShareX.Avalonia.Platform.Windows` (Project)
    *   `Recording/` (Folder) - Windows implementations (`WindowsGraphicsCaptureSource`, `MediaFoundationEncoder`).
*   `ShareX.Avalonia.UI` (Project)
    *   `Views/RecordingToolbarView.axaml` - Overlay toolbar for recording control.

### Type Definitions

#### Enumerations
```csharp
public enum CaptureMode
{
    Screen,
    Window,
    Region
}

public enum RecordingStatus
{
    Idle,
    Initializing,
    Recording,
    Paused,
    Finalizing,
    Error
}

public enum VideoCodec
{
    H264,
    HEVC,
    VP9,
    AV1
}

public enum PixelFormat
{
    Bgra8888,
    Nv12,
    Rgba8888,
    Unknown
}
```

#### RecordingOptions
```csharp
public class RecordingOptions
{
    public CaptureMode Mode { get; set; } // Screen, Window, Region
    // Use IntPtr for Window Handle.
    // Windows: HWND. Linux: XID. macOS: WindowID (int cast to IntPtr).
    // Future refactor may introduce a typed WindowId struct if needed.
    public IntPtr TargetWindowHandle { get; set; }
    public Rectangle Region { get; set; }
    
    // OutputPath:
    // If null/empty -> PlatformManager.GetDefaultOutputPath() acts as fallback
    // Default Pattern: "ShareX/Screenshots/yyyy-MM/Date_Time.mp4"
    public string OutputPath { get; set; }
    
    public ScreenRecordingSettings Settings { get; set; } // Reference to config
}
```

#### FrameData
```csharp
public struct FrameData
{
    public IntPtr DataPtr; // Pointer to raw pixel data
    public int Stride;
    public int Width;
    public int Height;
    public long Timestamp; // 100ns units
    public PixelFormat Format; // BGRA32, NV12, etc.
}
```

#### VideoFormat
```csharp
public class VideoFormat
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int Bitrate { get; set; }
    public int FPS { get; set; }
    public VideoCodec Codec { get; set; }
}
```

#### Event Arguments
```csharp
public class RecordingErrorEventArgs : EventArgs
{
    public Exception Error { get; }
    public bool IsFatal { get; }
}

public class RecordingStatusEventArgs : EventArgs
{
    public RecordingStatus Status { get; } // Idle, Recording, Paused, Finalizing
    public TimeSpan Duration { get; }
}

public class FrameArrivedEventArgs : EventArgs
{
    public FrameData Frame { get; }
}

public class AudioBufferEventArgs : EventArgs
{
    public byte[] Buffer { get; }
    public int BytesRecorded { get; }
    public long Timestamp { get; }
}
```

### Dependency Injection & PlatformManager
Services will be registered in `App.axaml.cs` (or `Bootstrapper.cs`):

```csharp
// In ConfigureServices
services.AddSingleton<IRecordingService, ScreenRecorderService>();
```

**PlatformManager Responsibility**:
`ScreenRecorderService` does NOT instantiate platform classes directly. Instead, it uses `PlatformManager`:
- `PlatformManager` acts as the service locator for OS-specific implementations.
- Example: `_captureSource = PlatformManager.CreateCaptureSource();`
- This ensures `ScreenRecorderService` remains platform-agnostic code.

## Configuration & Persistence

To ensure persistent settings, the following data model and bindings will be implemented:

### Data Model
**Class**: `ShareX.Avalonia.Core.ScreenRecordingSettings` (Serialized in `WorkflowsConfig.json`)

```csharp
public class ScreenRecordingSettings
{
    public VideoCodec Codec { get; set; } = VideoCodec.H264;
    public int FPS { get; set; } = 30;
    public int BitrateKbps { get; set; } = 4000;
    public bool CaptureSystemAudio { get; set; } = false;
    public bool CaptureMicrophone { get; set; } = false;
    public string MicrophoneDeviceId { get; set; }
    public bool ForceFFmpeg { get; set; } = false;
}
```

### Configuration Storage Rules
1.  **ApplicationConfig.json**: Stores global defaults for the application.
2.  **WorkflowsConfig.json**: Stores specific settings for user-defined workflows.
3.  **Precedence**: Workflow-specific settings in `WorkflowsConfig.json` **override** global defaults in `ApplicationConfig.json`. If a workflow setting is missing, it falls back to the Application default.

### UI Integration
*   `MainViewModel.StartRecordingCommand`: Triggers `IRecordingService.StartRecordingAsync`.
*   `RecordingToolbarView`: Binds to `RecordingStatus` to show timer/Stop button.
*   `TaskSettingsViewModel`: Exposes `ScreenRecordingSettings` for configuration.

## Verification Plan

### Automated
```bash
# Build the solution to verify API contracts and type correctness
dotnet build ShareX.Avalonia.sln
```

### Manual Testing
1.  **MVP Record**: Start recording via Main Window -> Perform actions -> Stop via Toolbar. Verify `.mp4` file is playable.
2.  **Persistence**: Change FPS to 60. Restart app. Verify FPS remains 60.
3.  **Fallback**: Rename `mfplat.dll` (simulation). Start recording. Verify app logs "Falling back to FFmpeg" and recording succeeds.
4.  **Audio**: Enable Microphone. Record. Verify video has sound.

### Compatibility
*   **Windows 10/11**: Verify border behavior.
*   **High DPI**: Check for scaling artifacts.
