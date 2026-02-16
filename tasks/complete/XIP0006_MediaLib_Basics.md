# CX03: MediaLib Basics - Image Combiner & Video Utilities

## Priority
**MEDIUM** - Builds on CX02 foundation

## Status
Completed - Verified on 2026-01-27

## Assessment
100% Complete. `ImageCombiner.cs`, `GifHelpers.cs`, and `VideoHelpers.cs` implemented in `src/XerahS.Media`.

## Objective
Port core media processing utilities from `ShareX.MediaLib` to `XerahS.Media`. Focus on image combining and basic video/GIF utilities.

Ref: `ShareX.MediaLib` (original WinForms project)

## Scope

### 1. Image Combiner
Port image combining/stitching functionality:
- **Combine multiple images** (vertical/horizontal stacking)
- **Alignment options** (top/center/bottom for vertical, left/center/right for horizontal)
- **Spacing/padding** between images
- **Background color** for gaps
- Use **SkiaSharp** for rendering (consistent with CX02)

**Target Location**: `src/XerahS.Media/ImageCombiner.cs`

### 2. GIF Utilities (Basic)
Port basic GIF handling:
- **Frame extraction** from GIF
- **Metadata reading** (frame count, dimensions, delays)
- **Quality detection** (check if GIF is optimized)

**Target Location**: `src/XerahS.Media/GifHelpers.cs`

### 3. Video Info Reader
Port video metadata reading:
- **FFprobe wrapper** (read duration, resolution, codec, bitrate)
- **Thumbnail extraction** from video
- Use **System.Diagnostics.Process** for FFprobe

**Target Location**: `src/XerahS.Media/VideoHelpers.cs`

**Note**: For now, just read video info - actual video encoding/conversion can be CX04.

## Guidelines
- **Use SkiaSharp** for all image operations (consistency with CX02)
- **No WinForms** dependencies
- **Nullable context** enabled
- **XML doc comments** for public APIs
- **Error handling** - gracefully handle missing FFprobe/invalid files

## Deliverables
- `ImageCombiner.cs` with stacking/alignment features
- `GifHelpers.cs` with frame extraction and metadata
- `VideoHelpers.cs` with FFprobe integration
- Build succeeds on `feature/backend-gaps`

## Estimated Effort
**Medium** - 4-6 hours
