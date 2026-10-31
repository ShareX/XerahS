# XIP0089 Windows Native Recording Parity with ShareX.ScreenRecordingLib

**Status**: Proposed
**Created**: 2026-10-11
**Updated**: 2026-10-11
**Target version**: next minor (feature; bump minor)
**Area**: Windows | Screen Recording | Audio | Video Encoding | Architecture
**Related**: XIP0016 (modern cross-platform capture architecture), XIP0017 (screen recording modernization), XIP0021 (verify-recording CLI), XIP0072 (screen recording bug fixes / stride safety), XIP0084 (Windows region capture parity)
**Goal**: The native Windows recording path (WGC + Media Foundation) records audio (system loopback + microphone, mixed and A/V-synced), records any monitor, spanning regions and windows directly, keeps frames on the GPU from capture to encoder, and supports real pause/resume, cleanup of partial files, hardware-encoder validation, low-latency encoding and border-free capture. It reuses ShareX's `ShareX.ScreenRecordingLib` (GPL-3.0, compatible with XerahS) as the reference and port source, and keeps XerahS's FFmpeg fallback, version gates, logging, capture/encode abstraction, and the macOS and Linux backends unchanged.

---

## Overview

A code review of XerahS `develop` @ `6b0f7dc9` found four gaps in the Windows native path compared with ShareX `develop` `ShareX.ScreenRecordingLib`:

1. No audio. `ScreenRecordingSettings.CaptureSystemAudio` / `CaptureMicrophone` exist (`RecordingModels.cs:100,103`) but only the FFmpeg path uses them. `IVideoEncoder` has no audio stream.
2. Primary monitor only. `WindowsGraphicsCaptureSource.InitializeForPrimaryMonitor()` calls `GetPrimaryMonitorHandle()` (`:308`), which is `MonitorFromPoint(0,0, MONITOR_DEFAULTTOPRIMARY)` (`:495-497`). Every `CaptureMode` (Screen, Window, Region) calls it (`ScreenRecorderService.cs:291,303,310`), and the output is cropped on the CPU.
3. Several CPU copies per frame: GPU→staging readback + `Map` (`WindowsGraphicsCaptureSource.cs:426-452`), `RegionCropper.CropFrame` for Region/Window (`ScreenRecorderService.cs:~375`), and `MFCreateMemoryBuffer` + `CopyFrame` into a new buffer per frame (`MediaFoundationEncoder.cs:241-249`).
4. `ScreenRecorderService` implements `IRecordingService` but not the existing `IPausableRecordingService` (`IRecordingService.cs:69-72`). There's no partial-file cleanup, no hardware-encoder validation, no low-latency settings, and the yellow WGC border isn't suppressed.

The work fits behind the existing seams: `ICaptureSource`, `IVideoEncoder`, the static factories on `ScreenRecorderService` (`CaptureSourceFactory`, `EncoderFactory`, `FallbackServiceFactory`, `NativeRecordingServiceFactory`, `:64-83`) and `IPausableRecordingService`. Nothing here gets replaced wholesale. New capabilities ship as optional interfaces, so the CPU `FrameData` contract keeps working for any other implementer.

Key principles:
- Additive interfaces. The macOS (`MacOSNativeRecordingService`) and Linux (`WaylandPortalRecordingService`, `FFmpegEncodingBridge`) backends compile and behave the same, and they don't have to implement anything new.
- Any native failure still routes to `HandleFatalError(ex, true)`, which triggers the FFmpeg fallback (`ScreenRecorderService.cs:178-189`).
- Keep the Windows version gates: 1803/17134 for WGC (`WindowsGraphicsCaptureSource.cs:178-203`) and 20H1/19041 for monitor capture (`:279-287`). Add new gates only for new APIs (see "Version gates").
- Every new component logs through `TroubleshootingHelper.Log("ScreenRecorder", <TAG>, ...)` like the existing code.
- Port ShareX classes as `internal` types in `XerahS.Platform.Windows`. Keep the GPL headers and add attribution.

## Source map

| ShareX (`ShareX.ScreenRecordingLib/…`) | XerahS target | Plugs into |
|---|---|---|
| `Audio/WasapiCapture.cs` (+ `Native/AudioInterfaces.cs`, `ComPtr.cs`) | new `src/platform/XerahS.Platform.Windows/Recording/Audio/WasapiAudioCapture.cs` | new `IAudioCaptureSource` (RegionCapture.ScreenRecording) |
| `Audio/AudioMixer.cs` | new `…/Recording/Audio/AudioMixer.cs` | owned by the encoder pipeline |
| `RecordingClock.cs` | new `…/Recording/RecordingClock.cs` (pure, testable; could move to `XerahS.RegionCapture/ScreenRecording` since it has no Win32 dependencies) | `ScreenRecorderService` timestamps, pause |
| `AudioCaptureDevices.cs` | new `…/Recording/Audio/WindowsAudioDeviceEnumerator.cs` | settings UI device pickers |
| `Video/GraphicsCapture.cs` (monitor enumeration/stitching `:84-99`, `IsBorderRequired` `:163`) | `WindowsGraphicsCaptureSource.cs` (extend) + new `…/Recording/MultiMonitorCaptureTargets.cs` | `ICaptureSource` + new `IRegionCaptureSource` |
| `Video/GraphicsDevice.cs`, `VideoTexturePool.cs` | new `…/Recording/Gpu/RecordingGraphicsDevice.cs`, `VideoTexturePool.cs` (reuse `Direct3D11Interop` where it fits) | shared device for capture + encoder |
| `Video/GpuVideoProcessor.cs` | new `…/Recording/Gpu/GpuVideoProcessor.cs` | between `IGpuCaptureSource` and `IGpuVideoEncoder` |
| `Encoding/MediaFoundationWriter.cs` | `MediaFoundationEncoder.cs` (evolve in place; it keeps `IVideoEncoder`) | `IVideoEncoder` + new `IGpuVideoEncoder`, `IAudioSink` |
| `ScreenRecorder.cs` (run loop, pause `:99-131,206-237`, failure delete `:322`) | `ScreenRecorderService.cs` | `IRecordingService`, `IPausableRecordingService` |
| `RecordingOptions.cs` (gains, `RequireHardwareEncoder`, device ids) | `RecordingModels.cs` `ScreenRecordingSettings` | task settings / UI |
| `Native/FrameTimer.cs` | new `…/Recording/FrameTimer.cs` | constant-frame-rate pacing |

Not ported (out of scope): `CameraCapture`, `CameraOverlay`, `CameraCircleShader`, `CameraCaptureDevices`. `GpuVideoProcessor.Convert(..., CameraOverlay? camera)` keeps the parameter shape so a later XIP can add the camera.

## New abstractions (in `XerahS.RegionCapture/ScreenRecording/IRecordingService.cs`)

```csharp
public interface IAudioCaptureSource : IDisposable   // Windows only for now
{
    void Initialize(AudioCaptureOptions options);     // loopback/mic, device ids, gains
    void Start(); void Stop();
    Exception? Failure { get; }
}
public interface IAudioSink { void WriteAudio(ReadOnlySpan<short> pcm48kStereo, long frameOffset); }

public interface IRegionCaptureSource : ICaptureSource   // optional
{
    void InitializeForMonitor(IntPtr hmonitor);
    void InitializeForRegion(PixelRect virtualDesktopRegion);  // multi-monitor stitch
    void InitializeForWindowDirect(IntPtr hwnd);              // WGC window item
    bool BorderRequired { set; }
}
public interface IGpuCaptureSource : ICaptureSource      // optional
{ event EventHandler<GpuFrameArrivedEventArgs> GpuFrameArrived; } // texture + device + timestamp
public interface IGpuVideoEncoder : IVideoEncoder        // optional
{ void WriteTexture(GpuFrame frame, long timestamp100ns, long duration, bool discontinuity); }
```

`ScreenRecorderService` checks with `is` at runtime. When the capture source and encoder both support the GPU path, it uses it. Otherwise it falls back to `FrameArrived` + `WriteFrame(FrameData)`, so unit-test fakes and any non-Windows implementer keep working. New settings in `ScreenRecordingSettings` (default to today's behaviour):
`SystemAudioDeviceId`, `MicrophoneDeviceId`, `SystemAudioGain = 1`, `MicrophoneGain = 1`, `WindowCaptureMode { MonitorCropIncludePopups (default, current), DirectWindow }`, `MonitorHandle/DisplayId`, `RequireHardwareEncoder = false` (XerahS keeps falling back, unlike the ShareX default `true`), `LowLatency = true`, `HideCaptureBorder = true`, `UseGpuPipeline = true`.

---

## Commit 1: Audio (WASAPI loopback + mic, AudioMixer, RecordingClock)

**Port**
- `RecordingClock`: copy almost verbatim. It's the single time base: `Now` in 100 ns ticks from `Stopwatch`, `Start(firstFrameTimestamp)`, `Pause/Resume` accumulating `pausedTime`, and `TryMap(ts, out relative, out segment)`. The segment number changes on each resume, so the mixer can resync.
- `AudioMixer`: 48 kHz stereo float input, per-source `StreamState` with resampling `Phase/Step` drift correction, gain per source, `Write(source, timestamp, frames, packet, gain, discontinuity)` and `Read(firstFrame, Span<short>)` as s16 output, plus a discontinuity counter.
- `WasapiCapture` → `WasapiAudioCapture : IAudioCaptureSource`: `IAudioClient3` event-driven, loopback on the render endpoint (default or `SystemAudioDeviceId`), mic on the capture endpoint, one capture thread, `Failure` surfaced. Use the existing `ComPtr`/COM interop style or port ShareX's `Native/AudioInterfaces.cs`.
- `MediaFoundationEncoder`: add an AAC audio stream to the sink writer (`MFAudioFormat_AAC`, 48k/2ch/16-bit input PCM, 192 kbps like `RecordingOptions.AudioBitrate`) and implement `IAudioSink.WriteAudio` (`MediaFoundationWriter.WriteAudio :182`). Add the audio stream only when audio is enabled, so video-only files stay identical.
- `ScreenRecorderService`: if `Settings.CaptureSystemAudio || CaptureMicrophone` and the platform registered `AudioCaptureSourceFactory` (new static `Func<IAudioCaptureSource>?`, registered by Windows platform init next to `CaptureSourceFactory`), create the audio source and mixer. Video timestamps switch from `frame.SystemRelativeTime` raw to `clock.TryMap`. Audio drains in 10 ms blocks up to the current video time (`ScreenRecorder.WriteAudioUntil :342`). If the audio device fails mid-recording, log it and keep recording video, with the same "audio disabled" semantics as ShareX. If audio init fails, the whole recording falls back to FFmpeg only when the user requires audio. Otherwise record video-only and warn.

**Tests (tests/XerahS.Tests)**
- `RegionCapture/RecordingClockTests.cs`: start/elapsed, pause excludes time, `TryMap` rejects pre-start and paused timestamps, segment increments on resume.
- `Platform/Windows/AudioMixerTests.cs`: gain scaling and clipping to s16, two-source sum, drift correction (source at 48 010 Hz stays within N samples after 60 s simulated), silence fill on gaps, discontinuity counter.
- `RegionCapture/ScreenRecorderServiceTests.cs` (extend): with fake `IAudioCaptureSource` + fake encoder implementing `IAudioSink`, audio is requested only when the settings flags are set, audio failure keeps video recording, and no audio factory means video-only with no throw.
- Marshalling smoke (Windows-only, `[SupportedOSPlatform]`/skip elsewhere): device enumeration returns without throwing.

## Commit 2: Multi-monitor, region stitching, direct window capture, border hide

**Port**
- Replace `GetPrimaryMonitorHandle()` usage with explicit targets. Keep `InitializeForPrimaryMonitor()` as a wrapper so current callers and tests compile.
- `InitializeForMonitor(hmonitor)`: WGC `CreateForMonitor` per monitor (resolve the monitor from the `CaptureMode.Screen` setting or from the monitor containing the cursor, reusing `XerahS.RegionCapture/Platform/Windows/NativeMonitorService.cs`).
- `InitializeForRegion(rect)`: port `GraphicsCapture.cs:80-99`. Round width/height to even, enforce the 16384 D3D11 limit, `EnumDisplayMonitors`, intersect each monitor with the region, and create one capture item per intersecting monitor as a `Target(item, srcRectInMonitor, dstOffsetInCanvas, hmonitor)`. On each tick, `CopySubresourceRegion` each target's latest frame into one canvas texture (GPU stitch). This replaces CPU `RegionCropper` for Region mode on Windows. `RegionCropper` stays for other platforms and the CPU fallback.
- Window mode: add `WindowCaptureMode`. `MonitorCropIncludePopups` (default) keeps today's behaviour (`ScreenRecorderService.cs:294-305`: monitor capture + dynamic bounds via `PlatformServices.Window.GetWindowBounds`). Fix it to pick the monitor(s) the window is on and do the crop on the GPU through the region path. `DirectWindow` uses the existing `InitializeForWindow(hwnd)` (`:233-246`, `CreateItemForWindow`), which captures the window alone, keeps working when occluded, and excludes popups. If the window closes or resizes, ShareX ends or rescales (`capture.HasEnded`, `ScreenRecorder.cs:186`).
- Border hide: `session.IsBorderRequired = false` (`GraphicsCapture.cs:163`) when `HideCaptureBorder`. That needs Windows 10 20H1+ / `GraphicsCaptureSession.IsBorderRequired` contract presence (check with `ApiInformation.IsPropertyPresent`) plus `GraphicsCaptureAccess.RequestAccessAsync(Borderless)` on Windows 11. Ignore it gracefully with a log line where it isn't supported. `IsCursorCaptureEnabled` keeps mapping to `ShowCursor`.
- Mixed DPI and rotation: compute everything in physical pixels (per-monitor-v2 aware). The existing `BitmapRotationHelperTests` cover rotation helpers. Reuse them for rotated monitors.

**Tests**
- `Platform/Windows/MultiMonitorCaptureTargetsTests.cs`: extract the intersection math into a pure static `MultiMonitorCaptureTargets.Compute(region, monitors)` and test single monitor, region spanning 2 or 3 monitors (including negative-origin left monitor), region touching a gap between non-adjacent monitors (black fill), odd sizes rounded to even, >16384 rejected, no intersection throws.
- `RegionCapture/ScreenRecorderServiceTests.cs`: a fake `IRegionCaptureSource` receives `InitializeForRegion` for Region mode, `InitializeForWindowDirect` only when `WindowCaptureMode.DirectWindow`, and the popup-inclusive default still uses monitor + crop. A fake without `IRegionCaptureSource` still gets `InitializeForPrimaryMonitor` (backward compatibility).
- `RecordCommandRegionParsingTests` stays green. Add a case for a region with negative coordinates.

## Commit 3: GPU-resident frame path

**Port**
- One shared `RecordingGraphicsDevice` (from `GraphicsDevice.cs`): a D3D11 device with `VideoSupport | BgraSupport`, multithread-protected, and a DXGI device manager (`MFCreateDXGIDeviceManager`) handed to the sink writer via `MF_SINK_WRITER_D3D_MANAGER`. The capture source creates its WinRT device from this device instead of a private one (`WindowsGraphicsCaptureSource.cs:~296-302`).
- `WindowsGraphicsCaptureSource` implements `IGpuCaptureSource`. `OnFrameArrived` copies the WGC surface into a pooled texture (`VideoTexturePool`) or the stitch canvas, then raises `GpuFrameArrived`. There's no staging or `Map`. The CPU readback (`:426-452`) only runs when a CPU subscriber exists (debug frame dumps, non-GPU encoder).
- `GpuVideoProcessor`: `ID3D11VideoProcessor` BGRA→NV12 (BT.709, limited range), scaling the canvas to the even output size, with `Convert(outputView, frameNumber)` into an encoder-bindable texture (`MediaFoundationWriter.EncoderBindFlags`).
- `MediaFoundationEncoder` implements `IGpuVideoEncoder.WriteTexture`. It wraps the NV12 texture with `MFCreateDXGISurfaceBuffer` → `MFCreateSample`, sets time and duration, and calls `WriteSample`. This replaces `MFCreateMemoryBuffer` + `CopyFrame` (`:238-249`) on the GPU path. Input media type becomes NV12 when GPU and RGB32 stays for the CPU path. Release samples through ShareX's `SampleReleaseCallback` pattern so pooled textures get recycled.
- Constant frame rate: `FrameTimer` paces the encode loop at `FPS`. It duplicates the last texture when WGC delivers no new frame (static screen) and counts drops (ShareX `nextFrame/written/dropped`).
- HDR monitors: XerahS has `DxgiHdrToneMapper` for screenshots. In this commit the capture pool requests `B8G8R8A8UIntNormalized` as it does today (SDR). HDR is listed as follow-up.

**Tests**
- `Platform/Windows/GpuPipelineSelectionTests.cs`: a pure selector `RecordingPipelineSelector.Select(source, encoder, settings)` picks GPU only when both support it and `UseGpuPipeline`, otherwise CPU, and the debug frame dump forces a CPU readback subscription.
- `Platform/Windows/FramePacingTests.cs`: given capture timestamps with gaps or bursts, the pacer emits exact CFR timestamps, duplicates on gaps and drops on bursts with the right counters.
- Keep `BgraRowCopyHelperTests` and the stride tests from XIP0072. The CPU path must stay correct.
- Optional Windows-only integration test (skipped without a D3D11 device, as in CI): create the device, video processor, a 64×64 BGRA texture → NV12, and read back the Y plane for a known colour.

## Commit 4: Pause/resume, partial-file cleanup, HW validation, low latency

**Port**
- `ScreenRecorderService : IPausableRecordingService`. `PauseRecordingAsync` calls `clock.Pause()`, stops pulling audio, and keeps the capture session alive (ShareX `Pause :99-109`). `ResumeRecordingAsync` calls `clock.Resume()` and marks the next video sample and the mixer as a discontinuity (ShareX `:206-237`). The output timeline has no gap, and `_stopwatch` / `RecordingStatus.Paused` gets updated. Check how `ScreenRecordingManager` / `RecordingViewModel` / `RecordingToolbarView` already call `IPausableRecordingService` for FFmpeg, and wire the same UI path.
- Partial files: on any failure after `Initialize` (including when the native path fails and the FFmpeg fallback takes over), dispose the sink writer and then `File.Delete(outputPath)` in a guarded try/catch (ShareX `:318-324`). Never delete on a successful stop. Respect XIP0059 (recover partial recording on external portal stop): that is Linux/portal-only, so make sure the Windows delete doesn't run in that flow.
- Hardware-encoder validation (`MediaFoundationWriter.cs:43-66,~130-150`): enumerate H.264/HEVC MFTs (`MFTEnumEx`, `MFT_ENUM_FLAG_HARDWARE|SORTANDFILTER`), record `VideoEncoderInfo(Name, IsHardwareAccelerated, IsD3D11Aware)` and log it. If `RequireHardwareEncoder` and no D3D11-aware hardware MFT exists, throw `NotSupportedException`, which goes to `HandleFatalError(ex, true)` and the FFmpeg fallback. On win-arm64 (Qualcomm), validate that the HEVC/H.264 MFT is D3D11-aware, and if not use the CPU path with a software MFT instead of failing.
- Low latency: `MF_LOW_LATENCY = 1`, `MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS = 1` on sink writer attributes (`MediaFoundationWriter.cs:50-51`), and `CODECAPI_AVEncCommonLowLatency` via `ICodecAPI` on the encoder transform (`ConfigureLowLatency :152-157`). Also set the rate control and GOP like ShareX does (check that file when implementing).
- Extend the `VerifyRecordingCommand` CLI (XIP0021) to report stream count, codecs, duration, and audio presence, so the manual checklist below can be scripted.

**Tests**
- `RegionCapture/ScreenRecorderServiceTests.cs`: pause sets status to Paused and stops `WriteFrame` calls. On resume, the next frame is marked as a discontinuity and its timestamps continue from the pre-pause value (no gap). Pause when not recording is a no-op. When an encoder throws mid-recording, the partial file is deleted (temp dir). A successful stop keeps the file. `NotSupportedException` from hardware validation triggers `FallbackServiceFactory`.
- `Tasks/RecordingRestartTests.cs` stays green. Add pause → stop → restart.
- `Platform/Windows/MediaFoundationEncoderSelectionTests.cs`: pure policy `EncoderSelectionPolicy.Choose(IReadOnlyList<VideoEncoderInfo>, requireHw)` for the cases hardware+D3D11, hardware only, and software only, with require on and off.
- `RegionCapture/RecordingCodecSupportPolicyTests.cs`: extend for the audio + codec combos (AAC only with MP4).

---

## Version gates

| Feature | Minimum | Check |
|---|---|---|
| WGC window capture | 10.0.17134 (existing) | unchanged |
| WGC monitor capture | 10.0.19041 (existing) | unchanged |
| `IsCursorCaptureEnabled` | 19041 | existing |
| `IsBorderRequired` | 20348 / Win11 (property present) | `ApiInformation.IsPropertyPresent` + log |
| `IAudioClient3` | Win10 | if missing, fall back to `IAudioClient` |
| Process loopback (not in scope) | 20348 | - |

## Risks

- COM threading: WGC frame events on the capture thread, the WASAPI thread, and MF on the encode thread all share one D3D11 device. You need `ID3D10Multithread.SetMultithreadProtected(true)` or a single encode thread that owns the immediate context (ShareX's model). Today's code relies on the event thread owning the context (comment at `:428-429`), and that assumption changes.
- arm64: the ShareX lib is primarily tested on x64. Qualcomm MFTs may reject NV12 DXGI input or the low-latency CODECAPI, so keep the CPU fallback, and make the low-latency setting non-fatal on failure.
- A/V drift on long recordings: covered by mixer tests, but needs real-device validation (30+ min).
- Multi-monitor with different refresh rates gives uneven frame arrival per target, so stitch on the pacer tick, not on frame arrival.
- Mixed DPI: physical vs logical coordinates between `NativeMonitorService`, region selector and WGC.
- Behaviour change: Window mode crop now uses the window's monitor instead of the primary, which is a fix but changes output on multi-monitor setups.
- `IsBorderRequired` access prompts on some Windows 11 builds, so it must not block start.
- The fallback contract: exceptions thrown in the new code must stay `PlatformNotSupportedException`/`COMException`/`NotSupportedException` where a fallback is intended. Add `NotSupportedException` to the fallback catch list in `ScreenRecorderService.cs:178-189`, and make sure it isn't swallowed elsewhere.
- Licensing: both projects are GPL-3.0. Keep the ShareX copyright headers on ported files.
- Static factories are process-global, so tests must reset them (existing `ScreenRecorderServiceTests` pattern).

## Manual verification checklist (win-arm64)

1. `dotnet build -c Release -r win-arm64` (and win-x64 to confirm no regression). `dotnet test tests/XerahS.Tests` is green on Windows, and also on Linux/macOS (Windows-only tests skipped).
2. Settings: native capture on, FFmpeg not forced, system audio + mic on.
3. Record 15 s of full screen on the **secondary** monitor while playing audio and speaking.
4. `ffprobe -v error -show_streams -of compact out.mp4` shows exactly 1 `codec_type=video` (h264/hevc, expected WxH, ~target fps) and 1 `codec_type=audio` (aac, 48000 Hz, 2 ch). Durations within 100 ms of each other. Or use `xerahs verify-recording out.mp4`.
5. Play it: audio audible, lip/clap sync within about 1 frame at start and end.
6. A region spanning two monitors gives a stitched output with no seams, and odd sizes become even.
7. Window mode default: open a context menu during recording and it appears in the video. `DirectWindow`: occlude the window and it still records, popups excluded.
8. Pause 5 s, resume. The output has no 5 s gap, audio continues aligned, and ffprobe duration ≈ wall time − pause.
9. No yellow border on Windows 11 with HideCaptureBorder on.
10. The log shows the selected encoder name with hardware/D3D11 flags, and the low-latency attribute applied (or a warning).
11. Force failure (unplug the mic mid-record, or kill the encoder via debug flag). The video continues or the partial file is deleted, and the FFmpeg fallback starts when expected.
12. Video-only (audio off): a single video stream, and the file plays.
13. Smoke-test macOS and Linux: a recording still works (no shared-code regressions).

## Line-reference verification (against `6b0f7dc9` / ShareX `develop`, 2026-10-11)

| Reference | Result |
|---|---|
| `WindowsGraphicsCaptureSource.cs:308` | Matches: the `GetPrimaryMonitorHandle()` call. The `MonitorFromPoint(0,0)` itself is at `:497` (method `:495`) |
| `WindowsGraphicsCaptureSource.cs:426-452` | Matches: `CopyResource` to staging (`:432`), `Map` (`:434`), `FrameArrived` with the mapped pointer, `Unmap` |
| `MediaFoundationEncoder.cs:241` | Matches: `MFCreateMemoryBuffer`. The copy itself is `CopyFrame` at about `:249` |
| `ScreenRecorderService.cs:178-186` | Matches: `PlatformNotSupportedException`/`COMException` → `HandleFatalError(ex, true)`. The block continues to about `:189` |
| ShareX `GraphicsCapture.cs:84-99` | Matches: `EnumDisplayMonitors` + per-monitor intersection → targets. The setup starts at `:80` |
| ShareX `ScreenRecorder.cs:192-330` | Roughly: that's the body of `Run()` (`:149-335`). Pause/resume API is `:99-131`, the pause loop `:206-237`, failure `File.Delete` `:322` |
| Extra finding | `RegionCropper.CropFrame` is a further CPU copy for Region/Window modes, not in the original list |
