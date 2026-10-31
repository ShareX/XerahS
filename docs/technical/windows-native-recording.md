# Windows native recording: XIP0089 implementation and review

This implements the recording goal in [XIP0089](../proposals/xip/XIP0089-windows-native-recording-sharex-parity.md) for XerahS 0.33.0. The proposal remains the original design record; this document defines the implemented scope and corrects assumptions that do not match the current architecture.

## Upstream reference and scope

The port was compared with ShareX `develop` at [`0899cece0f9f3f98333004a0a2b2091cef42ed3a`](https://github.com/ShareX/ShareX/tree/0899cece0f9f3f98333004a0a2b2091cef42ed3a/ShareX.ScreenRecordingLib), fetched on 2026-10-11. The native recording library's latest change at review time was [`0cbe14d15ef2c81db3e230bd8c6e50e4021b8868`](https://github.com/ShareX/ShareX/commit/0cbe14d15ef2c81db3e230bd8c6e50e4021b8868). The WASAPI capture, two-source mixer, QPC clock, D3D11 conversion/texture pool and Media Foundation writer come from that library, with XerahS integration and lifecycle changes.

The accepted scope is H.264/AAC MP4 screen recording: selected displays, desktop regions spanning displays, direct window capture, popup-inclusive window-area capture, system audio, microphone, gains, true pause/resume, moving a paused region, encoder diagnostics, cleanup and startup fallback. Camera capture and overlays, HDR recording/tone mapping and process-specific loopback are deferred, as in the original proposal. HEVC, VP9, AV1 and GIF continue through the existing FFmpeg workflows.

## Architecture decisions

The GPU engine is a complete `IRecordingService` registered by Windows initialization. It owns one D3D11 device, capture sessions, texture pool, video processor, Media Foundation writer, audio thread and clock. Keeping those resources together avoids exposing COM/DXGI objects through portable interfaces. The proposed `IGpuCaptureSource`, `IGpuVideoEncoder` and separate audio-provider retrofit are therefore unnecessary. The older WGC/CPU frame + Media Foundation pair stays available; its interfaces and callers remain intact.

Optional portable interfaces expose recording completion and movable paused regions. `RecordingEnded` wakes the workflow when a window closes or changes capture size; the already-finalized file goes through normal completion. Runtime capabilities distinguish native pause/resume from segmented FFmpeg pause. A native pause holds the GPU, encoder and audio resources open and removes paused QPC time from both media timelines. Moving a paused region retains the output dimensions and changes only capture targets.

All Win32, WASAPI, WGC and Media Foundation code stays in `XerahS.Platform.Windows`. Portable settings and device descriptions contain no Windows API types. The native settings panel appears when the Windows device service is available. macOS and Linux retain their existing recording service selection.

## Settings and compatibility

| Setting | Default | Behavior |
| --- | --- | --- |
| `DisplayId` | unset | Native screen mode uses the requested display, or the display containing the requested area, or the primary display. |
| `SystemAudioDeviceId` | unset | WASAPI default multimedia render endpoint. |
| `MicrophoneDeviceId` | unset | Default communications microphone, then default multimedia microphone. Legacy friendly names are matched to endpoints. |
| `SystemAudioGain`, `MicrophoneGain` | 1.0 | Native mix gain, limited to 0–4. Both-source Windows FFmpeg fallback also applies these gains. |
| `WindowRecordingMode` | `IncludePopups` | Desktop area follows the window and includes menus/popups; occluding windows remain visible in this mode. `WindowOnly` uses direct HWND capture and excludes separate popups. |
| `RequireHardwareEncoder` | false | When enabled, the native encoder must be both hardware accelerated and D3D11 aware; rejection triggers startup fallback. This does not enforce a hardware encoder on FFmpeg. |
| `LowLatency` | true | Optional Media Foundation/CODECAPI hints and microphone shared-engine period negotiation. Unsupported microphone period negotiation uses a fresh default-period client; absent IAudioClient3 uses IAudioClient. |
| `HideCaptureBorder` | true | Requests borderless capture only where Windows supports and permits it. Failure keeps the Windows capture border. |
| `ForceFFmpeg` | false | Explicit backend preference. Selecting system audio or microphone no longer changes it. |

The full GPU service requires Windows 10 2004/build 19041 or later and a usable WGC session. Newer border and capture-interval APIs are optional and individually checked. The legacy WGC path keeps its previous version gates. There is no separate `UseGpuPipeline` switch: `UseModernCapture` and `ForceFFmpeg` already control native versus compatibility recording, and native failures automatically retry the registered fallback.

Unavailable selected endpoints/displays produce an actionable error rather than silently switching to another device. Native startup failure with requested audio retries FFmpeg; it never starts a video-only native session instead. On runtime audio endpoint failure, XerahS deliberately continues video with an AAC silence tail and raises a visible warning. This is an XerahS policy from XIP0089, not an exact copy of ShareX's fatal audio-failure policy. An encoder/GPU failure terminates that take and removes its partial output; it does not silently begin a new file from time zero.

The manager releases the failed service and clears its state before trying fallback. It refuses fallback if the output path remains occupied, protecting an existing recording or an incomplete file that could not be removed. The native worker reserves output with `CreateNew` and deletes only a file that it created. Finalization refuses to overwrite a file that appears at the planned output path after start; abort removes recorded segments and never deletes that foreign file. The workflow stop signal persists across segmented fallback resumes, and concurrent abort/stop operations are serialized so an aborted take is not published. Stop/finalization is idempotent; stop while paused finalizes the same native session. Cleanup joins capture/audio threads outside the manager lock.

Windows FFmpeg fallback translates WASAPI microphone endpoint IDs to DirectShow friendly names and mixes both requested sources instead of discarding the microphone when loopback is also enabled. It still requires an enabled DirectShow **Stereo Mix** input for system audio. Stereo Mix cannot select an arbitrary WASAPI render endpoint; a selected non-default output fails clearly. Fallback desktop/window capture has FFmpeg's existing occlusion and window-following limits. Install a matching-architecture FFmpeg on ARM64. These are compatibility backend limitations, not native recording parity claims.

## Automated verification

`Native Recording` CI builds the desktop solution on Windows and Linux with warnings as errors, then compiles the native Windows project for `win-x64` and `win-arm64`. Runtime identifiers are applied at project level, as required by newer .NET SDKs. It runs the recording regression suite on Windows x64 and Linux and rejects empty or partially passing test reports. The Windows Server 2025 runner meets the test assembly's Windows 10.0.26100 minimum; NUnit rejects that assembly on Server 2022 despite a successful test-command exit. ARM64 on the x64 runner is compilation only. Portable tests run the actual mixer, clock, mapping, layout and hardware-encoder policy sources even on Linux. The binding guardrail checks all XerahS UI templates on UI-only PRs; it also checks ImageEditor templates when its submodule pin changes. Both projects still build with warnings as errors. Seven pre-existing untyped ImageEditor picker templates at the unchanged pin remain a shared-library follow-up.

Coverage includes endpoint gain/clipping/silence, opposing 200 ppm endpoint drift for 60 simulated seconds, physical desktop intersections with negative origins and gaps, even dimensions and texture limits, option mapping, native startup/fallback failures, occupied-output protection, completion before/after a workflow begins waiting, pause/move/resume, stop while paused, repeated stop, restart and fatal failure. The recording view-model tests guard against audio selection forcing FFmpeg. CLI tests validate stream metadata, codec/audio formats, drift and active duration.

The CLI requires `ffprobe` on PATH or in the configured tools folder. Existing recording inspection is read-only and bypasses desktop/configuration bootstrap, so stdout contains only the JSON report:

```powershell
xerahscli verify-recording --input out.mp4 --native --expected-audio-streams 1 --expected-duration 15
```

It reports stream count through the stream array, codecs, dimensions, frame rate, sample rate, channels, stream/container durations and validation errors as JSON. It returns 0 on pass, 1 on a validation failure and 2 if inspection cannot run. Native validation expects one H.264 video stream and AAC 48 kHz stereo if present. A/V **stream duration** difference must be within 100 ms. That check cannot establish audible sync or start-offset sync; playback/clap checks remain necessary. Expected active duration has a 1-second allowance for encoder startup/finalization.

To record through a configured workflow and verify the native backend, including a pause:

```powershell
xerahscli verify-recording WORKFLOW_ID --native --duration 15 --pause-seconds 5 --random-region --region-size 720 --iterations 3 --output-dir C:\Temp\XerahS-recording-check
```

`--native` treats FFmpeg fallback as a failure. Workflow audio choices, devices and gains are copied without changing saved settings. `--duration` is active time; the pause adds wall-clock time. Each output gets a `.verification.json` report. Omitting `--native` keeps the previous FFmpeg verification lane.

## Hardware acceptance and follow-up

Compilation and deterministic tests do not establish live WGC, GPU-driver or endpoint behavior. Before claiming complete hardware parity, retain evidence from Windows x64 and ARM64 for:

1. Video-only, microphone-only, loopback-only and mixed recording, including a secondary display and a spanning region.
2. Popup-inclusive capture, covered direct window capture, a moving window, and window closure/resize delivering exactly one completed recording to the workflow.
3. Multiple pauses, moving a paused region across displays and stop while paused; duration excludes pauses and audible sync remains stable.
4. Microphone/output removal: visible warning and continued playable video with silence. Encoder/GPU failure: no incomplete MP4 offered as a successful recording.
5. Hardware-required and software-allowed policies, low-latency enabled/disabled, capture border denied/allowed, and startup FFmpeg fallback with audio.
6. A 30-minute loopback + microphone recording with clap/transient checks near both ends. Container duration comparison alone cannot measure perceived A/V drift.

The original PR reports short live ARM64 recordings on a Snapdragon X hardware encoder. Those results cover the earlier engine; they do not validate this branch's new UI/lifecycle changes or constitute a long-duration x64/ARM64 acceptance run. Keep these manual items explicit in the merge decision. HDR and camera overlays should be separate proposals with their own UI, color-format and acceptance criteria.
