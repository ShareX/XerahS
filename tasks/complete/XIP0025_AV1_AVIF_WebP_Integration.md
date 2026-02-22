# XIP0025: AV1/AVIF & WebP Support Integration

**Status**: ✅ COMPLETE

## Goal Description
Integrate AV1 (video), AVIF (image), and WebP (image) support into XerahS. This aligns XerahS with recent ShareX capabilities (PR #8151), enabling modern, high-efficiency media formats.

## User Review Required
> [!IMPORTANT]
> **AVIF Implementation Strategy**: `SkiaSharp` (used by XerahS) lacks native AVIF encoding in the current version. The plan proposes using **FFmpeg** as a fallback encoder for AVIF images. This requires `ffmpeg.exe` to be present and support AV1 codecs (e.g., `libsvtav1`, `libaom-av1`).

> [!NOTE]
> **WebP Support**: SkiaSharp natively supports WebP. Changes will primarily expose this capability in the UI and Enums.

## Proposed Changes

### XerahS.Common
#### [MODIFY] [Enums.cs](file:///C:/Users/liveu/source/repos/ShareX%20Team/XerahS/src/desktop/core/XerahS.Common/Enums.cs)
- Update `EImageFormat`: Add `WEBP` and `AVIF`.
- Update `ConverterVideoCodecs` (if located here, likely in `XerahS.Media`) to include `AV1`? (Actually verified it is in `XerahS.Media`).

- **Native WebP**: SkiaSharp natively supports WebP. We will expose this capability directly via `ImageHelpers` and Enums.
- **AVIF Strategy**: SkiaSharp currently lacks native AVIF writing. We will leverage XerahS's **existing** `FFmpegCLIManager` (in `XerahS.Media`) to handle AVIF encoding.
- **Architecture Refactor**: To avoid circular dependencies between `Common` and `Media` and ensure platform adaptability, we will introduce an **abstraction layer**.

### XerahS.Services.Abstractions
#### [NEW] `IImageEncoder.cs`
- Define interface `IImageEncoder`:
  ```csharp
  bool CanEncode(EImageFormat format);
  Task EncodeAsync(SKBitmap bitmap, string filePath, EImageFormat format, int quality);
  ```

### XerahS.Services (or XerahS.Media if appropriate for dependency)
#### [NEW] `SkiaImageEncoder.cs`
- Implements `IImageEncoder`.
- Handles `PNG`, `JPEG`, `BMP`, `WEBP` (Native Skia support).
- Calls `ImageHelpers.SaveBitmap`.

#### [NEW] `FFmpegImageEncoder.cs`
- Implements `IImageEncoder`.
- Handles `AVIF` (and potentially others if native fails).
- Uses `FFmpegCLIManager` (from `XerahS.Media`) to convert a temp PNG to AVIF.
- Ensures cross-platform compatibility by relying on the existing managed FFmpeg path resolution.

#### [MODIFY] `ImageEncoderService` (Composite)
- Example implementation that holds a list of `IImageEncoder` providers.
- Routes the save request to the appropriate encoder based on format.

### XerahS.Common
#### [MODIFY] [Enums.cs](file:///C:/Users/liveu/source/repos/ShareX%20Team/XerahS/src/desktop/core/XerahS.Common/Enums.cs)
- Update `EImageFormat`: Add `WEBP` and `AVIF`.
- (No dependency on Media here; ImageHelpers stays pure Skia).

### XerahS.Media
#### [MODIFY] [Enums.cs](file:///C:/Users/liveu/source/repos/ShareX%20Team/XerahS/src/desktop/core/XerahS.Media/Enums.cs)
- Ensure `ConverterVideoCodecs` has `av1` with appropriate description.

#### [MODIFY] [VideoConverterOptions.cs](file:///C:/Users/liveu/source/repos/ShareX%20Team/XerahS/src/desktop/core/XerahS.Media/VideoConverterOptions.cs)
- Verify `av1` case uses `libsvtav1` or `libaom-av1` correctly.
- Add `avif` to `GetFileExtension` if generic converter supports it.

### XerahS.UI
#### [MODIFY] Task Settings / Main UI
- Update settings UI to allow selecting these formats for capture/recording.
- **UI Design Compliance**: Any UI updates must adhere to standards defined in `.github\skills\design-ui-window\SKILL.md`:
    - **Visual Consistency**: Use existing grid layouts and spacing tokens.
    - **Accessibility**: Ensure all new controls have `AutomationProperties.Name` and proper focus order.
    - **Interaction**: Provide immediate feedback and clear affordances.

## Verification Plan

### Automated Tests
- **Image Saving**:
    - Unit test `ImageHelpers.SaveBitmap` with `.webp` path. Verify output file exists and header is WebP (RIFF...WEBP).
    - Unit test `ImageHelpers.SaveBitmap` with `.avif` path (if logic is testable without full UI).
- **Video Recording**:
    - Use `ScreenRecorder` with `av1` codec selected. Record 5s clip. Verify output is `.mkv` or `.mp4` and codec is `av1` (using `ffprobe` or properties).

### Manual Verification
1. **WebP Image**:
    - Open XerahS, set After Capture task "Save image to file".
    - Change image format to WebP in settings (if exposed) or manually save as "test.webp".
    - Check if file opens in browser.
2. **AVIF Image**:
    - Attempt to save/convert image to AVIF.
    - Verify file opens in Chrome/Windows.
3. **AV1 Screen Recording**:
    - Select `AV1` in Screen Recorder options.
    - Record screen.
    - Playback file in VLC.
