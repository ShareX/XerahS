# Implementation Plan - MediaLib Basics

Implementing core media utilities in `XerahS.Media` as per `XIP0006`.

## User Review Required
- **VideoHelpers**: will rely on `FFmpegCLIManager` which exists in `XerahS.Media`. Usage requires a valid FFmpeg path. I will assume the caller provides this path or it's available via configuration.

## Proposed Changes

### XerahS.Media

#### [NEW] [VideoHelpers.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/XerahS/src/XerahS.Media/VideoHelpers.cs)
- Implementing video metadata reading and thumbnail extraction.
- **Approach**: Wrapper around existing `FFmpegCLIManager` to avoid code duplication.
- Features: `GetVideoInfo` (using `FFmpegCLIManager`), `TakeSnapshot` (using `FFmpegCLIManager` or `Process` directly if needed for simplicity).
- **New Feature**: `ConvertToGif(string inputPath, string outputPath)` using FFmpeg palettegen/paletteuse filters for high quality GIF.

### XerahS.Core

#### [MODIFY] [WorkerTask.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/XerahS/src/XerahS.Core/Tasks/WorkerTask.cs)
- Add case handlers for `ScreenRecorderGIF`, `StartScreenRecorderGIF`, `ScreenRecorderGIFActiveWindow`, `ScreenRecorderGIFCustomRegion`.
- In `HandleStartRecordingAsync`, detect if job is GIF.
- After recording stops, call `VideoHelpers.ConvertToGif`.
- Update `Info.FilePath` to the new GIF file and handle cleanup of temporary MP4.


### XerahS.CLI

#### [NEW] [VerifyGifRecordingCommand.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/XerahS/src/XerahS.CLI/Commands/VerifyGifRecordingCommand.cs)
- Command to verify GIF recording workflow.
- Arguments: `--duration` (default 5s), `--output` (optional).
- Logic:
    1. Triggers `ScreenRecorderGIF` workflow.
    2. Waits for duration.
    3. Stops recording.
    4. Verifies output file exists and is a valid GIF (using `SKCodec` to check frames).

## Verification Plan

### Automated Tests
- **CLI Verification**: Run `xerahs-cli verify-gif-recording --duration 5`.

### Manual Verification
- Run the CLI command and check the generated GIF file.
