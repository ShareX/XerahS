# SIP0017: Screen Recording Modernization

## 1. ShareX FFmpeg Integration Findings

ShareX relies heavily on the `FFmpegCLIManager` and `ScreenRecorder` classes to orchestrate screen recording. The integration is process-based (CLI), not library-based.

**Code Paths:**
- `ScreenRecorder.cs`: Orchestrates the recording session. Handles `Start`, `Stop`, and `Progress` events.
- `FFmpegCLIManager.cs`: Wraps the `ffmpeg.exe` process, handling argument construction and std/err redirection.
- `ScreenRecordingOptions.cs` & `FFmpegOptions.cs`: Define the configuration model.

**FFmpeg Invocation:**
- ShareX constructs a CLI argument string based on the selected "Source" (`gdigrab`, `ddagrab`, `dshow`) and "Codec".
- It uses specialized DirectShow filters (`screen-capture-recorder`) for some modes, writing to the registry to configure them before starting FFmpeg.

**Configuration:**
- **Capture Source**: `gdigrab` (GDI), `ddagrab` (DXGI Desktop Duplication), `dshow` (DirectShow).
- **Encoders**: `libx264`, `nvenc` (NVIDIA), `amf` (AMD), `qsv` (Intel).
- **Audio**: `dshow` audio devices (e.g. `virtual-audio-capturer`).
- **Lifecycle**: `StartRecording()` launches the process. `StopRecording()` sends 'q' or kills the process. It tracks file output and manages a temporary logic for GIF generation.

## 2. Modern Capture Methods Research (Windows)

For ShareX.Avalonia, the primary recording path on Windows should utilize **Windows.Graphics.Capture (WGC)** and **Media Foundation**.

**Primary API: Windows.Graphics.Capture (WGC)**
- **Availability**: Windows 10 (1803)+.
- **Capabilities**: High-performance, GPU-resident frame capture. Supports specific Windows (with `GraphicsCapturePicker` or `WindowId`) and Monitors.
- **Features**: Cursor capture (system composited), Border requirement (yellow border, can be disabled in newer builds), Protected content handling.

**Encoding Strategy (Native):**
- **Media Foundation (MF)**: The native Windows multimedia API.
- **Sink Writer**: Use `IMFSinkWriter` to write frames directly to an MP4 container.
- **Hardware Acceleration**: MF automatically utilizes hardware encoders (NVIDIA NVENC, Intel QSV, AMD VCE) if available.
- **Benefits**: No external dependency (FFmpeg.exe), lower latency, consistent with OS behavior.

**Constraints:**
- **Windows Versions**: WGC requires Win10 1803+. Fallbacks needed for older OS (though ShareX.Avalonia likely targets 10+).
- **Audio**: WASAPI Loopback for system audio. `IAudioClient` for microphone.

## 3. Cross-Platform Recording Architecture

We propose a modular interface-based architecture to support modern native capture with clean fallbacks across Windows, Linux, and macOS.

### Services & Interfaces

```csharp
namespace ShareX.Avalonia.ScreenCapture.Recording;

public interface IRecordingService
{
    // Platform-agnostic entry point
    Task StartRecordingAsync(RecordingOptions options);
    Task StopRecordingAsync();
    event EventHandler<RecordingErrorEventArgs> ErrorOccurred;
    event EventHandler<RecordingStatusEventArgs> StatusChanged;
}

public interface ICaptureSource : IDisposable
{
    // Abstraction for frame acquisition
    // Windows: WindowsGraphicsCaptureSource
    // Linux: PipeWireCaptureSource / X11GrabSource
    // macOS: ScreenCaptureKitSource
    Task StartCaptureAsync();
    event EventHandler<FrameArrivedEventArgs> FrameArrived;
}

public interface IVideoEncoder : IDisposable
{
    // Abstraction for encoding logic
    // Windows: MediaFoundationEncoder
    // Universal Fallback: FFmpegPipeEncoder
    void Initialize(VideoFormat format, string outputPath);
    void WriteFrame(FrameData frame);
    void Finalize();
}

public interface IAudioCapture : IDisposable
{
    // Windows: WasapiAudioCapture
    // Linux: PulseAudioCapture / PipeWireAudioCapture
    // macOS: CoreAudioCapture
    void Start();
    event EventHandler<AudioBufferEventArgs> AudioDataAvailable;
}
```

### Platform-Specific Implementations

| Component | Windows | Linux | macOS |
|-----------|---------|-------|-------|
| **Capture Source** | `Windows.Graphics.Capture` (Primary)<br>`GoToMeeting` / `GDI` (Fallback) | `XDG Desktop BOrtal` (ScreenCast)<br>`X11Grab` (Fallback) | `ScreenCaptureKit` (Primary)<br>`AVFoundation` (Fallback) |
| **Encoder** | `Media Foundation` (H.264/HEVC) | `FFmpeg CLI` (VAAPI/NVENC) | `AVAssetWriter` (Native)<br>`FFmpeg CLI` (Fallback) |
| **Audio** | `WASAPI` Loopback | `PulseAudio` / `PipeWire` Monitor | `CoreAudio` Tap |

### Threading Model
- **Capture Thread**: WGC callbacks run on a thread pool thread (or dedicated DispatcherQueue).
- **Encoding Thread**: A dedicated thread (producer-consumer pattern) consumes frames to avoid blocking the capture callback. Low latency is critical.
- **Cancellation**: `CancellationToken` for async start/stop operations.

### Output
- Primary: **MP4 (H.264/AAC)** via Media Foundation.
- Metadata: Standard MP4 atoms.

## 4. FFmpeg Fallback Design

**Trigger Conditions:**
- Runtime Exception in Primary Path (e.g. WGC init failure).
- Windows version < 10.0.17134.
- User explicitly selects "Legacy (FFmpeg)" in settings.
- Specific non-standard codecs requested (e.g. GIF recording, WebM) that MF doesn't support easily.

**Integration:**
- Re-use `FFmpegCLIManager.cs` (already ported).
- **Binary Location**: Look in `Tools/ffmpeg.exe`, `PATH`, or embedded resource extraction.
- **Parameter Mapping**:
    - Map `WorkflowsConfig` "Video Quality" -> `CRF` or `Bitrate`.
    - Map "Source" -> `gdigrab` (if WGC fails).

**Output Consistency:**
- Ensure fallback produces the same file container (MP4) where possible.
- If GIF is requested, FFmpeg is the *primary* (or only) path for direct GIF recording (or record to MP4 -> Convert).

## 5. Staged Delivery Plan

### Stage 1: MVP Recording (Silent)
- Implement `WindowsGraphicsCaptureSource` (WGC wrapper).
- Implement `MediaFoundationEncoder` (Simple H.264 SinkWriter).
- Wire up `Start`/`Stop` in UI.
- **Deliverable**: Ability to record screen to MP4 (no audio) on Win10+.

### Stage 2: Audio Support
- Implement `WasapiLoopbackCapture` (System Audio).
- Implement `WasapiMicrophoneCapture`.
- Mix audio into `MediaFoundationEncoder`.

### Stage 3: Window & Region Parity
- Add `GraphicsCapturePicker` support for Window selection.
- Implement "Crop" logic for Region recording (capture Screen -> Crop -> Encode).
- Add Cursor overlay options (if WGC cursor is disabled).

### Stage 4: Advanced Native Encoding
- Expose Hardware Encoding toggles (verify MF picks HW).
- Bitrate/FPS control in Settings UI.

### Stage 5: FFmpeg Fallback & Auto-Switch
- Implement `FFmpegRecordingService` (implements `IRecordingService`).
- Add "Auto" selector logic in `ScreenRecorderService`.
- If `StartRecordingAsync` throws on Primary, catch -> Log -> Try Fallback.

### Stage 6: Migration & Presets
- Import `FFmpegOptions` from likely existing ShareX config.
- Add UI for "Modern vs Legacy" switch.

### Stage 7: macOS & Linux Implementation
- **Linux**: Implement `XDGPortalCaptureSource` using DBus to request ScreenCast sessions. Use FFmpeg CLI for encoding initially.
- **macOS**: Extend `ScreenCaptureKit` interop (already started for screenshots) to support continuous stream callbacks. Implement `AVAssetWriterInterop` for native encoding.


## 6. Testing Plan

### Functional Tests
- [ ] **Start/Stop**: Record 5s, 1m, 1h. Verify file playability.
- [ ] **Cancel**: Verify no partial files lock.
- [ ] **Window Close**: If recorded window closes, capture should stop or show black content.

### Compatibility Tests
- [ ] **Windows 10 vs 11**: Verify WGC behavior (borders).
- [ ] **High DPI**: Verify video resolution matches physical pixels (not logical).
- [ ] **Multi-Monitor**: Record secondary monitor.

### Fallback Tests
- [ ] **Force Fail**: Rename `mfplat.dll` (simulated) or use Mock to throw. Verify FFmpeg picks up.
- [ ] **Missing FFmpeg**: If Primary fails AND FFmpeg missing -> Error Dialog.

### Performance
- [ ] **CPU/GPU Usage**: Compare WGC+MF vs GDI+FFmpeg. (Expected: WGC+MF significantly lower CPU).
