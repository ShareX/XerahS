# SIP0017 Implementation Plan

## Current Implementation Status by SIP Stage

### Stage 1: MVP Recording (Silent) — 🟢 100% Complete

| Component | Status | Notes |
|-----------|--------|-------|
| `IRecordingService` interface | ✅ Complete | Full interface with Start/Stop/Events |
| `ICaptureSource` interface | ✅ Complete | Includes StopCaptureAsync |
| `IVideoEncoder` interface | ✅ Complete | Initialize/WriteFrame/Finalize |
| `IAudioCapture` interface | ✅ Complete | Prepared for Stage 6 |
| `RecordingOptions` | ✅ Complete | All fields documented |
| `ScreenRecordingSettings` | ✅ Complete | FPS/Bitrate/Codec/Audio flags |
| `FrameData`, `VideoFormat` | ✅ Complete | Proper structs with init |
| All EventArgs classes | ✅ Complete | Constructors included |
| Enums (CaptureMode, RecordingStatus, VideoCodec, PixelFormat) | ✅ Complete | All documented |
| `WindowsGraphicsCaptureSource` | ✅ Complete | WGC via Vortice.Direct3D11 |
| `MediaFoundationEncoder` | ✅ Complete | IMFSinkWriter with BGRA input |
| `ScreenRecorderService` | ✅ Complete | Orchestration with factory pattern |
| Factory registration in `WindowsPlatform.InitializeRecording()` | ✅ Complete | Called in Program.cs |
| **UI Integration (StartRecordingCommand)** | ✅ Complete | Implemented in `RecordingViewModel` |
| **RecordingToolbarView** | ✅ Complete | Implemented as floating overlay |

### Stage 2: Window & Region Parity — 🟢 100% Complete

| Component | Status | Notes |
|-----------|--------|-------|
| `InitializeForWindow(IntPtr)` | ✅ Complete | Uses WGC CreateItemForWindow |
| `InitializeForPrimaryMonitor()` | ✅ Complete | Uses WGC CreateItemForMonitor |
| Region cropping logic | ✅ Complete | `RegionCropper` with unsafe pointer operations |
| Cursor overlay (software) | ✅ Complete | Configurable via `ShowCursor` setting |
| GraphicsCapturePicker integration | ❌ Deferred | Direct HWND works for current needs |

### Stage 3: Advanced Native Encoding — 🟡 ~30% Complete

| Component | Status | Notes |
|-----------|--------|-------|
| MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS | ✅ Complete | Enabled in encoder |
| Bitrate/FPS controls in Settings | ✅ Complete | ScreenRecordingSettings has fields |
| UI controls for Bitrate/FPS | ❌ Not Started | No settings UI for recording |
| Hardware encoder detection/display | ❌ Not Started | MF auto-detects but no UI indicator |

### Stage 4: FFmpeg Fallback & Auto-Switch — 🟢 100% Complete

| Component | Status | Notes |
|-----------|--------|-------|
| `FFmpegOptions` model | ✅ Complete | Full codec/source options |
| `FFmpegCaptureDevice` | ✅ Complete | GDIGrab, DDAGrab, etc. |
| `FFmpegRecordingService` | ✅ Complete | Full implementation with all capture modes |
| Auto-switch logic on exception | ✅ Complete | ScreenRecorderService catches PlatformNotSupported/COMException |
| `FallbackServiceFactory` registration | ✅ Complete | Registered in WindowsPlatform.InitializeRecording() |

### Stage 5: Migration & Presets — 🔴 Not Started

| Component | Status | Notes |
|-----------|--------|-------|
| ShareX config import logic | ❌ Not Started | |
| Modern vs Legacy toggle in UI | ❌ Not Started | |

### Stage 6: Audio Support — 🔴 Not Started

| Component | Status | Notes |
|-----------|--------|-------|
| `WasapiLoopbackCapture` | ❌ Not Started | |
| `WasapiMicrophoneCapture` | ❌ Not Started | |
| Audio mixing in encoder | ❌ Not Started | |

### Stage 7: macOS & Linux Implementation — 🔴 Not Started

| Component | Status | Notes |
|-----------|--------|-------|
| Linux XDGPortalCaptureSource | ❌ Not Started | |
| macOS ScreenCaptureKit recording | ❌ Not Started | |

---

## Alignment Assessment with SIP0017

### ✅ Aligned

1. **Interface-based architecture**: All core interfaces defined in `ShareX.Avalonia.ScreenCapture.ScreenRecording`.
2. **Platform abstraction**: Windows implementations in `ShareX.Avalonia.Platform.Windows.Recording`.
3. **Factory pattern**: `CaptureSourceFactory` and `EncoderFactory` in ScreenRecorderService.
4. **Modern native APIs**: Windows.Graphics.Capture + Media Foundation as primary path.
5. **FFmpeg as fallback only**: FFmpegRecordingService defined but not primary.
6. **Exception-based fallback triggers**: PlatformNotSupportedException, COMException caught.

### ⚠️ Minor Deviations

1. **No DI container**: Uses static factory functions instead of `IServiceCollection`. Acceptable for current complexity.
2. **Dynamic dispatch for initialization**: `ScreenRecorderService.InitializeCaptureSource` uses `dynamic` to call platform-specific methods. Works but not type-safe.

---

## Resolved Gaps from SIP Review

| Gap ID | Resolution |
|--------|------------|
| #1 Missing enum definitions | ✅ All enums in `RecordingEnums.cs` |
| #2 PlatformManager undefined | ✅ Using static factory pattern instead (CaptureSourceFactory/EncoderFactory) |
| #3 IntPtr for window handle | ✅ Documented as cross-platform approach |
| #4 Config storage precedence | ⚠️ Model exists but not integrated into SettingManager |
| #5 Output file naming | ✅ Default pattern in `GetOutputPath()` |
| #6 CancellationToken support | ⚠️ Deferred (documented in interface comments) |

---

## Remaining Implementation Work

### ✅ Completed: Stage 1 UI Integration

**Files created/modified:**

1. **[NEW]** `src/ShareX.Avalonia.UI/ViewModels/RecordingViewModel.cs`
   - Manages recording state
   - Exposes `StartRecordingCommand`, `StopRecordingCommand`
   - Binds to `ScreenRecorderService`

2. **[MODIFY]** `src/ShareX.Avalonia.UI/ViewModels/MainViewModel.cs`
   - Add recording commands or reference to RecordingViewModel

3. **[NEW]** `src/ShareX.Avalonia.UI/Views/RecordingToolbarView.axaml`
   - Floating toolbar with Start/Stop button
   - Timer display during recording
   - Status indicator

### ✅ Completed: Configuration Persistence

**Files modified:**

1. **[MODIFY]** `src/ShareX.Avalonia.Core/Settings/TaskSettings.cs`
   - ✅ Add `ScreenRecordingSettings` property

2. **[MODIFY]** `src/ShareX.Avalonia.Core/SettingManager.cs`
   - ✅ Ensure ScreenRecordingSettings serializes with WorkflowsConfig.json

### 🚀 Active: Stage 4 FFmpeg Fallback

**Files to create:**

1. **[NEW]** `src/ShareX.Avalonia.ScreenCapture/ScreenRecording/FFmpegRecordingService.cs`
   - Implements `IRecordingService`
   - Uses `FFmpegCLIManager` pattern
   - Wraps existing `FFmpegOptions`

2. **[MODIFY]** `src/ShareX.Avalonia.Platform.Windows/WindowsPlatform.cs`
   - Uncomment and complete `FallbackServiceFactory` registration

---

## Verification Plan

### Automated Build
```bash
dotnet build ShareX.Avalonia.sln
```

### Manual Testing (Stage 1 MVP)

1. **Start Recording Test**
   - Launch application
   - Click Start Recording button
   - Verify status changes to "Recording"
   - Wait 5 seconds
   - Click Stop Recording
   - Verify .mp4 file created in Documents/ShareX/Screenshots/yyyy-MM/

2. **Fallback Test (Stage 4)**
   - Rename `mfplat.dll` temporarily
   - Start recording
   - Verify fallback message in logs
   - Verify FFmpeg process started

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| WGC not available on older Windows | Medium | FFmpegRecordingService fallback |
| Media Foundation codec missing | Medium | Check IsAvailable before attempting |
| Frame rate mismatch between capture and encode | Low | Use timestamp from WGC, not fixed interval |
| Memory pressure from frame copies | Medium | Consider zero-copy GPU path in Stage 3 |

---

## Next Steps

1. ✅ Implement `RecordingViewModel` with commands
2. ✅ Integrate recording controls into MainWindow
3. ✅ Verify end-to-end recording works
4. ✅ Implement FFmpegRecordingService for fallback
5. ✅ Add settings persistence
6. ✅ Implement region cropping (Stage 2)
7. ✅ Add configurable cursor capture (Stage 2)
8. 🚀 Implement advanced encoding options (Stage 3)
9. 🚀 Add audio capture support (Stage 6)

---

## Recent Implementation (2026-01-08)

### Stage 2: Window & Region Parity - COMPLETED

**Commit:** `ccbd9b3` - "SIP0017: Complete Stage 2 Window & Region Parity implementation"

**New Components:**
1. **RegionCropper.cs** - Unsafe pointer-based frame cropping
   - Efficient row-by-row memory copying using `Buffer.MemoryCopy`
   - Supports BGRA32/RGBA32 pixel formats
   - Manual memory management with `Marshal.AllocHGlobal`/`FreeHGlobal`
   - Proper cleanup in `ScreenRecorderService.OnFrameCaptured` finally block

2. **ShowCursor Setting** - Configurable cursor capture
   - Added to `ScreenRecordingSettings` (default: true)
   - Implemented in `WindowsGraphicsCaptureSource.ShowCursor` property
   - Controls WGC's `IsCursorCaptureEnabled`
   - FFmpeg fallback uses `-draw_mouse 1` flag

**Technical Details:**
- Region capture strategy: Full screen capture + post-capture cropping
  - More efficient than native WGC region capture
  - Avoids WGC limitations with offset capture items
  - Minimal overhead (single memory copy per frame)

- Memory management: Cropped frames use separate allocations
  - `RegionCropper.CropFrame()` allocates with `Marshal.AllocHGlobal`
  - Caller must free using `RegionCropper.FreeCroppedFrame()`
  - `ScreenRecorderService` uses try/finally to ensure cleanup

- Unsafe code enabled in `ShareX.Avalonia.ScreenCapture.csproj`

**Build Status:** ✅ All projects compile successfully

### Stage 4: FFmpeg Fallback - COMPLETED

**Commit:** `eecc915` - "SIP0017: Complete Stage 1 MVP with FFmpeg fallback implementation"

**New Components:**
1. **FFmpegRecordingService.cs** - Complete FFmpeg fallback
   - Automatic FFmpeg path detection (Tools/, Program Files, PATH)
   - Support for all capture modes (Screen, Window, Region)
   - Multi-codec support (H264, HEVC, VP9, AV1)
   - Graceful error handling and process management

2. **Platform Integration** - Automatic modern/fallback selection
   - Enhanced `WindowsPlatform.InitializeRecording()` with fallback factory
   - Detection logic: WGC+MF preferred → FFmpeg fallback
   - Seamless switching based on system capabilities

**Dependencies:**
- Added ShareX.Avalonia.Media reference to ScreenCapture project
- Uses existing `FFmpegCLIManager` for process management
