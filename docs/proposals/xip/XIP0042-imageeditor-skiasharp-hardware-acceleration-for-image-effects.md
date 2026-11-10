# XIP0042 ImageEditor SkiaSharp hardware acceleration for image effects

**Status**: Superseded
**Version**: v0.22.257

## Summary

**Goal:** Use SkiaSharp's GPU backend for image effects where possible, and speed up remaining CPU paths.

**Scope:** ShareX.ImageEditor (ImageEditor) ΓÇö adjustment/filter effects and their application pipeline. **ImageEditor is shared and used by two hosts:**

- **XerahS** ΓÇö Avalonia desktop app; embeds EditorView. GPU wiring deferred to Phase 3 ΓÇö see ┬º3.
- **ShareX** ΓÇö WinForms app; opens the modern ImageEditor via `AvaloniaIntegration.ShowEditorDialog` (Avalonia window). Same ImageEditor codebase, different host process.

Any change to ImageEditor (effects, pipeline, or optional GPU path) **must remain compatible with both hosts**. The host may provide an optional `GRContext` when applying effects; when not provided (`null`), the software path is always used (no breaking change for either host). Neither host currently provides one ΓÇö intentional for Phases 1 and 2 (see ┬º3 for why).

**Recommended execution order:**

| Phase | Focus | Status |
|---|---|---|
| **1** | Per-pixel CPU effects (BlackAndWhite, ReplaceColor, SelectiveColor) | Γ£à DONE |
| **2** | CPU optimisations and code health | Mostly done; some profile-gated |
| **3** | GPU acceleration via canvas compositor + persistent `GRContext` | ΓÅ╕∩╕Å Deferred ΓÇö pursue when canvas compositor is designed |

---

## Current State (as of Feb 2026)

> **Jaex landed two rounds of optimisations/fixes; all remaining ImageEditor-side items were then implemented in `feature/XIP0042-optimizations`. A third round added diagnostics infrastructure and addressed build-time discoveries. The GPU path in the ImageEditor library is complete, but host wiring is intentionally deferred: the Avalonia 11 frame-scoped API (`AvaloniaLocator / ISkiaGpuWithPlatformGraphicsContext`) is fragile and cannot be used outside a render callback, and a persistent offscreen `GRContext` (the only viable path) has a readback cost that negates the benefit for the color-matrix/filter operations targeted in early phases. GPU wiring will be pursued at Phase 3 (canvas compositor). See ┬º3.**

### Round 1 (prior audit)
- Γ£à `ApplyPixelOperation` rewritten with `unsafe` pointer arithmetic for `Bgra8888` ΓÇö no `GetPixel`/`SetPixel`.

### Round 2 (this audit)
- Γ£à `GlowImageEffect` ΓÇö `AutoResize` param added; asymmetric canvas expansion (only expands on the side the glow/offset extends to).
- Γ£à `ShadowImageEffect` ΓÇö Removed `Darkness`, added `Color` property; same asymmetric expansion pattern as Glow.
- Γ£à `ReflectionImageEffect` ΓÇö Fixed flip matrix (proper `Translate + Scale` instead of pivot-based scale); canvas width now accounts for skew displacement.
- Γ£à `OutlineImageEffect` ΓÇö `OutlineOnly` mode added (DstOut erase of inner area).
- Γ£à `SliceImageEffect` ΓÇö Robust `minSliceHeight`/`maxSliceHeight`/`minSliceShift`/`maxSliceShift` bounds checking.

### Round 3 (post-implementation: diagnostics + build fixes)
- ΓÜá∩╕Å `SKSamplingOptions` (┬º2.4) **reverted** ΓÇö specific overloads (`SKCubicResampler.Mitchell`, `SKBitmap.Resize(info, SKSamplingOptions)`) absent in SkiaSharp 2.88.9; code remains on `SKFilterQuality` pending a SkiaSharp upgrade.
- Γ£à `ReadPixels` corrected to the **5-arg overload** in the GPU surface read-back path (Phase 3 build fix).
- Γ£à `SKCanvasControl` rewritten to use `ICustomDrawOperation` + `ISkiaSharpApiLeaseFeature` ΓÇö correct Avalonia 11 pattern for obtaining `GRContext` during custom Skia rendering.
- Γ£à `IEditorDiagnosticsSink` / `EditorDiagnosticEvent` / `EditorDiagnosticLevel` added to `ShareX.ImageEditor.Services` ΓÇö host-agnostic interface for observability.
- Γ£à `EditorServices.Diagnostics` property wired in ImageEditor; GPU path decision points (`SetGpuContext`, `ApplyColorFilter` GPU/CPU branch) emit structured diagnostic events.
- Γ£à `EditorDiagnosticsAdapter` added to XerahS (`XerahS.UI.Services`) ΓÇö routes `IEditorDiagnosticsSink` events to `DebugHelper.WriteLine` / `WriteException`.
- Γ£à Wired in `App.axaml.cs`: `EditorServices.Diagnostics = new EditorDiagnosticsAdapter()`.
- Γ£à `src/desktop/app/run-debug-app.sh` added ΓÇö convenience script to launch the desktop app in Debug configuration for GPU path verification.

### Implementation status

| Area | Status |
|---|---|
| **Phase 1a** ΓÇö BlackAndWhite via color matrix | Γ£à DONE |
| **Phase 1b** ΓÇö `ApplyPixelOperation` unsafe pointer loop | Γ£à DONE |
| **┬º2.1** ΓÇö `ColorizeImageEffect` refactor | Γ£à DONE |
| **┬º2.4** ΓÇö `SKFilterQuality` ΓåÆ `SKSamplingOptions` | ΓÜá∩╕Å Reverted ΓÇö SkiaSharp 2.88.9 missing overloads; revisit after upgrade |
| **┬º2.5** ΓÇö Redundant `Category` overrides in 7 filter effects | Γ£à DONE |
| **┬º2.7** ΓÇö `new Random()` per call in Slice/TornEdge | Γ£à DONE |
| **┬º2.8** ΓÇö Diagnostics / observability infrastructure | Γ£à DONE |
| ┬º2.2 GammaImageEffect LUT caching | ΓÜá∩╕Å Low priority ΓÇö profile first |
| ┬º2.3 BlurImageEffect allocation reduction | ΓÜá∩╕Å Low priority ΓÇö profile first |
| ┬º2.6 SelectiveColor `ToHsl` short-circuit | ΓÜá∩╕Å Profile first |
| **Phase 3** ΓÇö GRContext / GPU surface path (ImageEditor library) | Γ£à DONE (library ready; host wiring deferred) |
| **Phase 3** ΓÇö XerahS host wiring (`SetGpuContext`) | ΓÅ╕∩╕Å DEFERRED ΓÇö see ┬º3; revisit when canvas compositor is designed |
| **┬º3.5** ΓÇö GPU read-back threshold (160 000 px) | Γ£à DONE |

### Effect map

| Effect | Method | GPU-ready? | Notes |
|---|---|---|---|
| Brightness | `ApplyColorMatrix` | GPU deferred (┬º3) | |
| Contrast | `ApplyColorMatrix` | GPU deferred (┬º3) | |
| Saturation | `ApplyColorMatrix` | GPU deferred (┬º3) | |
| Hue | `ApplyColorMatrix` | GPU deferred (┬º3) | |
| Invert | `ApplyColorMatrix` | GPU deferred (┬º3) | |
| Grayscale | `ApplyColorMatrix` | GPU deferred (┬º3) | |
| Sepia | `ApplyColorMatrix` | GPU deferred (┬º3) | |
| Polaroid | `ApplyColorMatrix` | GPU deferred (┬º3) | |
| Alpha | `ApplyColorMatrix` | GPU deferred (┬º3) | |
| Gamma | `ApplyColorFilter` (table LUT) | GPU deferred (┬º3) | Rebuilds LUT every call ΓÇö ┬º2.2 |
| Colorize Γ£à | `ApplyColorFilter` helper | GPU deferred (┬º3) Γ£à | ┬º2.1 done |
| **BlackAndWhite** Γ£à | Two-pass color filter | GPU-eligible Γ£à | ┬º1a done |
| **ReplaceColor** | `ApplyPixelOperation` (unsafe Γ£à) | CPU only | Per-pixel; no matrix equivalent |
| **SelectiveColor** | `ApplyPixelOperation` (unsafe Γ£à) | CPU only | Per-pixel HSL; ┬º2.6 |
| Blur | `SKImageFilter.CreateBlur` | N/A | 3-bitmap chain ΓÇö ┬º2.3 |
| Sharpen | `SKImageFilter.CreateMatrixConvolution` | N/A | Clean |
| Pixelate | Downscale/upscale | N/A | `SKFilterQuality` ΓÇö ┬º2.4 reverted (SkiaSharp 2.88.9) |
| Border Γ£à | Canvas drawing | N/A | `Category` override removed ΓÇö ┬º2.5 done |
| Glow Γ£à | `SKColorFilter` + blur; asymmetric resize | N/A | `Category` override removed ΓÇö ┬º2.5 done |
| Reflection Γ£à | Canvas + gradient; correct flip + skew width | N/A | `Category` override removed ΓÇö ┬º2.5 done |
| Shadow Γ£à | `SKColorFilter` + blur; asymmetric resize | N/A | `Category` override removed ΓÇö ┬º2.5 done |
| Outline Γ£à | `SKImageFilter.CreateDilate` + DstOut | N/A | `Category` override removed ΓÇö ┬º2.5 done |
| TornEdge Γ£à | Path generation | N/A | `Random.Shared`; `Category` removed ΓÇö ┬º2.5, ┬º2.7 done |
| Slice Γ£à | Canvas drawing; robust bounds | N/A | `Random.Shared`; `Category` removed ΓÇö ┬º2.5, ┬º2.7 done |
| Resize | `SKBitmap.Resize` | N/A | `SKFilterQuality` ΓÇö ┬º2.4 reverted (SkiaSharp 2.88.9) |
| Rotate (orthogonal) | `ExtractSubset` / canvas transform | N/A | Optimised |
| Rotate (custom) | Canvas transform | N/A | Clean |
| Rotate3D | `SKMatrix44` | N/A | Clean |
| Rotate3DBox | `SKMatrix44` + shading | N/A | Clean |
| Flip | Canvas scale | N/A | Clean |
| AutoCrop | Pixel scan | N/A | Clean |
| RoundedCorners | Canvas clip path | N/A | Clean |
| Skew | `SKMatrix.CreateSkew` | N/A | Clean |

---

## 0. Dual-host compatibility (XerahS + ShareX)

ImageEditor lives in a shared codebase consumed by **XerahS** and **ShareX**. Both use the same Avalonia-based EditorWindow/EditorView when the modern editor is enabled (ShareX: `UseModernImageEditor` ΓåÆ `AvaloniaIntegration.ShowEditorDialog`). Compatibility rules:

| Rule | Rationale |
|---|---|
| **No host-specific APIs in ImageEditor core/effects** | ImageEditor must not reference XerahS-only or ShareX-only assemblies. Effect pipeline and `ApplyColorFilter` helper stay in `ShareX.ImageEditor` with no dependency on host. |
| **GRContext is optional and provided by the host** | The effect library accepts an optional `GRContext` (or delegate). When `null` or not set, the existing software path runs. No requirement for either host to provide it. Neither host provides it currently ΓÇö intentional (┬º3). |
| **Host GPU wiring deferred to Phase 3** | The Avalonia 11 render-lease API is frame-scoped and cannot be used at effect-application call sites. A persistent offscreen `GRContext` (Option B, ┬º3.3) is the only correct approach but its readback cost negates the benefit for Phase 1/2 color-matrix/filter operations. Revisit when the canvas compositor (Phase 3) is designed ΓÇö that is where GPU acceleration delivers a real, measurable win. |
| **Phase 1 (BlackAndWhite, ReplaceColor, SelectiveColor)** | Pure ImageEditor code; no host dependency. Behaves identically in both hosts. |
| **Validation** | When implementing, ensure ImageEditor builds and effect behavior is unchanged in both repo configurations (XerahS and ShareX). Optionally validate GPU path in XerahS and software fallback in both. |

---

## 1. Phase 1: Per-pixel effects

### 1a. BlackAndWhite ΓåÆ color matrix Γ£à DONE

**Current code** (`BlackAndWhiteImageEffect.Apply`):
```csharp
return ApplyPixelOperation(source, (color) =>
{
    float lum = 0.2126f * color.Red + 0.7152f * color.Green + 0.0722f * color.Blue;
    return lum > 127 ? SKColors.White : SKColors.Black;
});
```

**Problem:** Uses `ApplyPixelOperation` so it cannot use the GPU path even after Phase 3 is implemented. Also note it discards source alpha (replaces every pixel with fully-opaque White or Black ΓÇö intentional behaviour, not a bug, but document it).

**Proposed change:** Replace with a two-pass color-filter approach:
1. **Pass 1 ΓÇö luminance grayscale** (same matrix already used by `GrayscaleImageEffect` at strength=100):
   ```csharp
   float[] grayscale = {
       0.2126f, 0.7152f, 0.0722f, 0, 0,
       0.2126f, 0.7152f, 0.0722f, 0, 0,
       0.2126f, 0.7152f, 0.0722f, 0, 0,
       0,       0,       0,       1, 0
   };
   using var step1 = ApplyColorMatrix(source, grayscale);   // thread grContext when Phase 3 GPU is implemented
   ```
2. **Pass 2 ΓÇö hard threshold** via `SKColorFilter.CreateTable` with a step function:
   ```csharp
   byte[] table = new byte[256];
   for (int i = 0; i < 256; i++) table[i] = i < 128 ? (byte)0 : (byte)255;
   byte[] alphaTable = new byte[256];
   for (int i = 0; i < 256; i++) alphaTable[i] = 255; // force fully opaque (matches old behaviour)
   using var filter = SKColorFilter.CreateTable(alphaTable, table, table, table);
   return ApplyColorFilter(step1, filter);   // thread grContext when Phase 3 GPU is implemented
   ```
   - After Pass 1, all three RGB channels are equal (the luminance value). The table maps 0ΓÇô127 ΓåÆ 0, 128ΓÇô255 ΓåÆ 255. Alpha is forced to 255 (matches original behaviour of dropping source alpha).
   - This route goes through `ApplyColorFilter` and is therefore GPU-eligible when Phase 3 is implemented.

**Deliverables (Phase 1a):**
- `BlackAndWhiteImageEffect.Apply` uses the two-pass color-filter approach.
- No `ApplyPixelOperation` call remains in `BlackAndWhiteImageEffect`.
- Behaviour is pixel-for-pixel identical to the existing implementation (validate with test images including transparent regions).

### 1b. ReplaceColor and SelectiveColor: fast pixel loop Γ£à DONE (by Jaex, Round 1)

`Adjustments/ImageEffect.cs::ApplyPixelOperation` uses `unsafe` pointer arithmetic (`SKColor*`) for `Bgra8888` format; falls back to `source.Pixels` managed array for other color types. No `GetPixel`/`SetPixel`. **No further work required.**

---

## 2. Phase 2: CPU optimisations and code health

### 2.1 `ColorizeImageEffect` ΓÇö refactor to use helper Γ£à DONE

`ColorizeImageEffect.Apply` currently bypasses the base class helpers and manages its own `SKCanvas` / `SKBitmap`. This means it will **not** benefit from the GPU path after Phase 3 unless refactored.

**Current logic:**
- Full-strength: apply `SKColorFilter.CreateCompose(tint, grayscale)` in a single draw.
- Partial-strength (`strength < 100`): draw original, then draw source again with the composed filter at `paint.Color = (255,255,255, blendAlpha)`.

The partial-strength blending cannot be expressed as a single color filter (it requires two composited draws). The cleanest refactor:
1. `using var colorized = ApplyColorFilter(source, composedFilter);` ΓÇö GPU-eligible, gets the fully-colorized bitmap.
2. Create a new bitmap at source size; draw `source` first (as baseline), then draw `colorized` with `SKPaint { Color = new SKColor(255,255,255,blendAlpha) }` on top.

This keeps the visual result identical and gives the GPU path to the expensive color-filter step (step 1).

Once refactored, `ColorizeImageEffect` gains the GPU path automatically when Phase 3 is implemented.

### 2.2 `GammaImageEffect` ΓÇö optional LUT caching ΓÜá∩╕Å LOW PRIORITY

`GammaImageEffect.Apply` builds a 256-byte lookup table on every call:
```csharp
byte[] table = new byte[256];
for (int i = 0; i < 256; i++) { ... Math.Pow(val, 1.0 / Amount) ... }
```
For live preview (slider drag), this rebuilds on every frame. Cache `(Amount, table)` as a field; only rebuild when `Amount` changes. **Profile before implementing.**

### 2.3 `BlurImageEffect` ΓÇö intermediate bitmap reduction ΓÜá∩╕Å LOW PRIORITY

Current chain allocates **3 intermediate bitmaps**: `expanded` ΓåÆ `blurred` ΓåÆ `result` (crop). The crop step could be avoided by drawing with a negative translation + clip rect on `blurred`, saving one allocation per call. **Do only if profiling shows allocation pressure.**

### 2.4 `SKFilterQuality` ΓåÆ `SKSamplingOptions` ΓÜá∩╕Å Reverted ΓÇö pending SkiaSharp upgrade

SkiaSharp ΓëÑ 2.88 deprecated `SKFilterQuality` in favour of `SKSamplingOptions`. The migration was implemented and initially marked done, but **subsequently reverted** because SkiaSharp 2.88.9 (the version in use) does not expose the required overloads: `SKCubicResampler.Mitchell` and the `SKBitmap.Resize(SKImageInfo, SKSamplingOptions)` overload are absent in that build. Code reverts to `SKFilterQuality` to maintain a clean build (0 errors, 0 warnings).

**Planned replacements** (for when SkiaSharp is upgraded):

| File | Current (reverted) | Planned |
|---|---|---|
| `PixelateImageEffect.Apply` | `new SKPaint { FilterQuality = SKFilterQuality.None }` | `new SKPaint { SamplingOptions = new SKSamplingOptions(SKFilterMode.Nearest) }` |
| `ResizeImageEffect.Apply` | `source.Resize(info, SKFilterQuality.High)` | `source.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell))` |

**No behaviour change when applied.** Fixes compiler warnings and future-proofs against removal.

> **Action required:** Re-apply after upgrading SkiaSharp past 2.88.9 to a build that exposes these overloads. Regression risk is none ΓÇö pixelation uses nearest-neighbour and resize uses high-quality cubic; the replacements preserve both.

### 2.5 Redundant `Category` overrides in Filter effects Γ£à DONE

`Filters.ImageEffect` (the abstract base in `ShareX.ImageEditor.ImageEffects.Filters`) already sets:
```csharp
public override ImageEffectCategory Category => ImageEffectCategory.Filters;
```

Seven concrete filter effects redundantly re-declare this override when they could simply inherit it:

| File | Action |
|---|---|
| `GlowImageEffect.cs` | Remove `public override ImageEffectCategory Category => ...` |
| `ReflectionImageEffect.cs` | Remove `public override ImageEffectCategory Category => ...` |
| `ShadowImageEffect.cs` | Remove `public override ImageEffectCategory Category => ...` |
| `OutlineImageEffect.cs` | Remove `public override ImageEffectCategory Category => ...` |
| `BorderImageEffect.cs` | Remove `public override ImageEffectCategory Category => ...` |
| `SliceImageEffect.cs` | Remove `public override ImageEffectCategory Category => ...` |
| `TornEdgeImageEffect.cs` | Remove `public override ImageEffectCategory Category => ...` |

**Clean effects** (already inheriting without override): `BlurImageEffect`, `SharpenImageEffect`, `PixelateImageEffect`.

**No functional impact.** Do as cleanup alongside any other edit to these files. All seven concrete classes are in `namespace ShareX.ImageEditor.ImageEffects.Filters` so the base class resolves unambiguously.

### 2.6 `SelectiveColorImageEffect` ΓÇö per-pixel `ToHsl` cost ΓÜá∩╕Å PROFILE FIRST

Every pixel calls `c.ToHsl()` before range detection, even for pixels that end up unmodified. For large images (e.g. 4K Γëê 8M pixels), this is ~8M HSL decompositions per apply. A micro-optimisation:

- **Whites/Blacks/Neutrals** ranges depend only on `L` and `S`. Approximate lightness from raw RGB without full HSL decomposition: `L Γëê (max(R,G,B) + min(R,G,B)) / 510f * 100`. Skip `c.ToHsl()` for desaturated/near-white/near-black pixels; only call it for chromatic pixels. Adds branch complexity. **Profile first** ΓÇö the unsafe pointer loop is already fast; `ToHsl` may not be the bottleneck.

### 2.7 `SliceImageEffect` and `TornEdgeImageEffect` ΓÇö `new Random()` per call Γ£à DONE

Both `SliceImageEffect.Apply` and `TornEdgeImageEffect.Apply` create `new Random()` at the start of every call:
```csharp
Random rand = new Random();
```

**Two problems:**
1. **Determinism during rapid calls:** if `Apply` is triggered multiple times within the same OS timer tick (e.g. live-preview slider), both instances may be seeded identically and produce the same sequence ΓÇö visually identical "random" output on consecutive calls.
2. **Unnecessary allocation:** small but avoidable.

**Fix:** Use `Random.Shared` (.NET 6+, thread-safe, no allocation):
```csharp
var rand = Random.Shared;
```

Drop the local `Random` variable; replace all `rand.Next(...)` calls in the method body with `Random.Shared.Next(...)`.

> **Regression risk:** None. The fix produces statistically equivalent randomness. The visual output of Slice and TornEdge will differ between calls as intended (no determinism loss).

### 2.8 Diagnostics / observability infrastructure Γ£à DONE

Added a host-agnostic diagnostics interface to ImageEditor so GPU path decisions and errors are observable at the host level without coupling ImageEditor to any specific logging framework.

**ImageEditor side (`ShareX.ImageEditor.Services`):**
- `IEditorDiagnosticsSink` ΓÇö interface with single method `Report(EditorDiagnosticEvent)`.
- `EditorDiagnosticEvent` ΓÇö struct carrying `Source`, `Message`, `Level` (`EditorDiagnosticLevel`: Info/Warning/Error), and optional `ExceptionText`.
- `EditorServices.Diagnostics` ΓÇö static nullable `IEditorDiagnosticsSink?` property; set by the host at startup.
- Emit points: `SetGpuContext()` (context acquired / cleared), `ApplyColorFilter()` (GPU branch taken / CPU fallback reason), and `SKCanvasControl` render callbacks.

**XerahS host side (`XerahS.UI.Services`):**
- `EditorDiagnosticsAdapter : IEditorDiagnosticsSink` ΓÇö routes events to `DebugHelper`: Info ΓåÆ `WriteLine`, Warning ΓåÆ `WriteLine("WARN ΓÇª")`, Error ΓåÆ `WriteException` or `WriteLine("ERROR ΓÇª")`.
- Wired in `App.axaml.cs` (composition root): `EditorServices.Diagnostics = new EditorDiagnosticsAdapter()`.

**Developer tooling:**
- `src/desktop/app/run-debug-app.sh` ΓÇö shell script that resolves `dotnet` (PATH or `~/.dotnet/dotnet`) and launches `XerahS.App.csproj` in Debug configuration, making GPU diagnostics visible on stdout/in the IDE debug output window.

---

## 3. Phase 3: GPU acceleration ΓÇö canvas compositor and persistent GRContext

> **Library side Γ£à DONE. Host wiring ΓÅ╕∩╕Å DEFERRED.** The `ApplyColorFilter`/`ApplyColorMatrix` GPU path exists and is correct in ImageEditor. Host wiring is intentionally not pursued until this phase: the only viable API path (persistent offscreen `GRContext`, ┬º3.3 Option B) has a per-effect readback cost that negates the benefit for color-matrix/filter operations on screenshot-sized images, and the Avalonia 11 frame-scoped lease API cannot be used outside a render callback. The `GRContext?` parameter remains in the API as a zero-cost forward-compatibility hook. Sections ┬º3.1ΓÇô3.4 document the API constraints; ┬º3.3 gives the threading analysis; ┬º3.5 documents the threshold already in place.

### Why GPU belongs here and not earlier

**The performance case does not close for Phase 1/2 operations:**

| Factor | GPU (Option B) | CPU (current) |
|--------|---------------|---------------|
| Avalonia 11 API stability | Γ£à Independent of Avalonia if Option B | Γ£à No dependency |
| Implementation complexity | Γ¥î High ΓÇö platform GL/Vulkan bootstrap per OS | Γ£à Already done |
| Effect throughput (1080p image, color matrix) | ΓÜá∩╕Å Marginal at best; upload+readback Γëê 5ΓÇô15 ms | Γ£à ~1ΓÇô3 ms with unsafe pointer loop |
| Effect throughput (4K image, color matrix) | ΓÜá∩╕Å Possible win ~2ΓÇô4├ù for compute, but readback still costly | Γ£à ~8ΓÇô15 ms with unsafe pointer loop |
| Background-thread safe | Γ£à Yes (Option B only) | Γ£à Yes |
| Cross-platform risk | Γ¥î GL/EGL/WGL differences; Vulkan not guaranteed | Γ£à None |
| Stability / crash risk | Γ¥î GL driver bugs, context loss, out-of-VRAM | Γ£à None |
| Net benefit for screenshot-sized images | Γ¥î Usually negative (readback dominates) | Γ£à Already fast |

Phase 1/2 effects (brightness, contrast, hue, saturation, color matrix) are a single pass of arithmetic over each pixel. The bottleneck is memory bandwidth, not compute. The optimized CPU path (unsafe pointer arithmetic, `Bgra8888` layout, cache-friendly linear scan) is already within 2ΓÇô4 cycles/pixel on modern CPUs. A GPU path that uploads the bitmap, dispatches a shader, and reads back the result adds at minimum one full-image PCIe round-trip at each end ΓÇö for a 1080p image that is ~8 MB ├ù 2 = ~16 MB of PCIe traffic, which at 16 GB/s takes ~1 ms just for transfers, comparable to the entire CPU-side compute. For typical XerahS usage (screenshots, not 4K video frames), GPU is not a win here.

**Where GPU does pay off:** The canvas compositor. When multiple annotation layers, effects, and overlays are applied and the result is rendered back to the screen without a CPU readback step, pixels never leave the GPU and the benefit is real. That is the correct place to invest in a persistent `GRContext`. Once the canvas compositor is on-GPU, extending the same `GRContext` to color-matrix/filter effects becomes viable (pixels already on-GPU, no upload cost; only a single final readback).

**Recommended action when this phase is started:**

1. Design the canvas compositor to use a persistent offscreen `GRContext` (Option B, ┬º3.3).
2. Use that same context for `ApplyColorFilter`/`ApplyColorMatrix` when a composited image is being processed ΓÇö at that point the upload cost is already paid and the readback happens once at the end.
3. For standalone effect application (no compositor), keep the CPU path ΓÇö it remains correct and fast.
4. If `ApplyPixelOperation` (ReplaceColor, SelectiveColor) is measurably slow on large images, prefer SIMD intrinsics or parallelised loops over GPU.

### 3.1 Obtain GRContext from Avalonia (host-specific)

> ΓÜá∩╕Å **FRAGILE API WARNING (Avalonia 11 / SkiaSharp 2.88.x).** The pattern originally considered ΓÇö
> `AvaloniaLocator.Current.GetService<IPlatformGraphics>()` ΓåÆ cast to `ISkiaGpuWithPlatformGraphicsContext`
> ΓåÆ `TryGetGrContext()` ΓÇö has **two serious problems** in Avalonia 11 that make it unsuitable:
>
> 1. **`AvaloniaLocator` is deprecated / internals-hostile in Avalonia 11.** The service-locator pattern
>    used in Avalonia 0.10 was redesigned in Avalonia 11; `AvaloniaLocator.Current` still compiles but is
>    no longer a stable surface ΓÇö it can return `null`, change between minor releases, or be removed. The
>    `SKCanvasControl ΓåÆ ICustomDrawOperation + ISkiaSharpApiLeaseFeature` rewrite in the editor canvas
>    was specifically done to move *away* from this pattern. Re-introducing it in host wiring would
>    undo that direction.
>
> 2. **`ISkiaSharpApiLease` / `ISkiaSharpApiLeaseFeature` is frame-scoped.** It is only valid inside an
>    active render-pass callback (e.g., inside `Render(DrawingContext)` or an `ICustomDrawOperation`).
>    Effect application is triggered by user action (clicking Apply), which happens entirely **outside**
>    any render frame. Dispatching to the UI thread does not help ΓÇö being on the UI thread does not mean
>    a render frame is active. The lease acquired in a render callback is already invalidated by the time
>    the next line of non-render code runs.
>
> **Consequence:** Option A in ┬º3.3 ("dispatch to UI/render thread, acquire `TryGetGrContext()` there")
> does **not work** for effect application. It only works for drawing *to the screen*, not for
> computing a new `SKBitmap` from an existing one at arbitrary call sites.
>
> The only viable runtime path for GPU-accelerated effect application is **Option B** (┬º3.3) ΓÇö a
> persistent offscreen `GRContext` created independently of Avalonia's renderer.

- **XerahS:** No `GRContext` acquisition is currently wired; all effects take the CPU path. When Phase 3 is implemented, a persistent offscreen context (Option B) is required ΓÇö see ┬º3.3.
- **ShareX:** Same. Leave context unset; effects use the software path (no functional change from today).
- **Constraint (if Option B is implemented):** The persistent `GRContext` must be created on a thread
  that owns the underlying GL/Vulkan context and must be disposed cleanly when the process exits. It is
  **not** Avalonia's context and is not affected by Avalonia's render lifecycle.

### 3.2 Centralized "apply color filter (with optional GPU)" helper

- **Where:** `ShareX.ImageEditor/Core/ImageEffects/Adjustments/ImageEffect.cs` ΓÇö update the existing `ApplyColorFilter` and `ApplyColorMatrix` helpers.
- **Current signature (CPU-only):**
  ```csharp
  protected static SKBitmap ApplyColorFilter(SKBitmap source, SKColorFilter filter)
  ```
- **New signature (optional GPU):**
  ```csharp
  protected static SKBitmap ApplyColorFilter(SKBitmap source, SKColorFilter filter, GRContext? grContext = null)
  ```
- **GPU path (when `grContext != null`):**
  - Create `SKSurface` with `SKSurface.Create(grContext, budgeted: true, imageInfo)` (offscreen GPU render target sized to `source.Width x source.Height`).
  - Draw: `canvas.Clear(transparent); canvas.DrawBitmap(source, 0, 0, paintWithFilter);`
  - Flush: `surface.Canvas.Flush()` then `surface.Snapshot().ToRasterImage()` (or `surface.PeekPixels()` into a new `SKBitmap`) to read back pixels.
  - Return that bitmap and dispose the surface.
- **CPU fallback (when `grContext == null`):** Keep the current `new SKCanvas(result)` implementation unchanged.
- **`ApplyColorMatrix` delegates to `ApplyColorFilter`** (already does); just threads `grContext` through.

### 3.3 Threading and context lifetime

- Effects are today invoked from ViewModels / dialogs and may run on background threads (e.g. for preview or apply).

- **Option A ΓÇö dispatch to UI/render thread + acquire Avalonia lease:** Γ¥î **NOT VIABLE for effect application.** Dispatching to the UI thread does not open a render frame. `ISkiaSharpApiLeaseFeature.Lease()` returns `null` outside an active render callback. This option only works for *drawing to the screen* inside `Render(DrawingContext)` / `ICustomDrawOperation`. It cannot be used to compute a new `SKBitmap` on demand.

- **Option B ΓÇö persistent offscreen GRContext:** Create a dedicated `GRContext` once (e.g., `GRContext.CreateGl(GRGlInterface.Create())` on Linux/macOS, or the equivalent Vulkan path) that is independent of Avalonia's renderer. Use it on a dedicated background thread (or the thread that created it). This is the **only correct approach** for effect application.
  - Pros: works at any call site, background-thread safe, not fragile to Avalonia version changes.
  - Cons: requires platform-specific GL/Vulkan bootstrap (EGL on Linux, WGL on Windows, CGL on macOS); adds ~50ΓÇô200 ms startup cost; GPUΓåÆCPU readback cost often negates benefit for small/medium images.

- **GPU threshold:** For images below roughly 2 MP (e.g. `width*height < 2_000_000`), the upload + compute + readback round-trip for simple color matrix/filter operations is rarely faster than the optimized CPU path. GPU benefit is mainly visible for operations that can stay on-GPU (compositing, multi-pass effects). See ┬º3.5.

### 3.4 Wiring the context into the effect pipeline

- **`MainViewModel` / `EditorCore`** (code that calls `effect.Apply(source)`) should not know about Skia's GPU; keep the public API as `Func<SKBitmap, SKBitmap>`. All wiring stays inside ImageEditor or is injected by the host.
- Introduce an **optional** "effect context" or "render context" that **either host** can set (e.g. when the editor view is loaded), which carries an optional `GRContext` (or a delegate that returns a scoped GRContext). The effect base or a small "effect runner" in the same assembly can check this and pass the context into `ApplyColorFilter` when calling into the adjustment base.
- Each host that wants GPU: obtain `GRContext` (e.g. at process startup via Option B), store it in a thread-safe way or pass it via a closure to the effect runner. The runner calls existing `Effect.Apply(source)`; the implementation of `ApplyColorFilter` inside the effect library uses the injected context when available. If no context is provided (e.g. ShareX not yet wired), the software path runsΓÇöidentical behavior for both hosts.
- **`ColorizeImageEffect` note:** Refactored to use the `ApplyColorFilter` helper (see ┬º2.1). It will benefit from the GPU path automatically when Phase 3 is wired.

**Deliverables (Phase 3):**

- Host (XerahS): create persistent offscreen `GRContext` via Option B (┬º3.3); provide it to the effect pipeline via `EditorServices.SetGpuContext()`. ShareX: optionally wire the same; otherwise leave unset (software path).
- Document that GPU is used only when context is available and above the pixel threshold (┬º3.5).
- Ensure ImageEditor builds and effect behavior is unchanged in both XerahS and ShareX.
- Confirm via `run-debug-app.sh` that diagnostics (┬º2.8) emit "GPU path taken" for color-matrix effects on sufficiently large images.

### 3.5 GPU read-back threshold Γ£à DONE (library code in place)

GPU read-back (`surface.Snapshot().ToRasterImage()` or `PeekPixels`) has fixed overhead that dominates for small images. Add a heuristic in `ApplyColorFilter`:

```csharp
const int GpuPixelThreshold = 160_000; // ~400├ù400
if (grContext != null && source.Width * source.Height >= GpuPixelThreshold)
{
    // GPU path
}
else
{
    // CPU path (even if grContext is available)
}
```

Tune the threshold empirically on real hardware. Document the chosen value. Note: based on the Phase 1/2 analysis, the practical threshold for standalone effect application is closer to 2 MP ΓÇö the 160 000 px value may be too low and should be revisited when Phase 3 is profiled.

---

## 4. Summary table

| Phase | What | Status | Outcome |
|---|---|---|---|
| **1b** | `ApplyPixelOperation` unsafe pointer loop | Γ£à **DONE** (Round 1) | ReplaceColor/SelectiveColor at native speed. |
| **Glow** | AutoResize + asymmetric expansion | Γ£à **DONE** (Round 2) | Glow canvas grows only toward the offset. |
| **Shadow** | Color property + asymmetric expansion | Γ£à **DONE** (Round 2) | Consistent with Glow. |
| **Reflection** | Fixed flip matrix; skew width expansion | Γ£à **DONE** (Round 2) | Correct visual output with skew. |
| **Outline** | OutlineOnly DstOut mode | Γ£à **DONE** (Round 2) | Ring-only outline render. |
| **Slice** | Robust bounds | Γ£à **DONE** (Round 2) | No crash on edge-case slider values. |
| **1a** | BlackAndWhite via two-pass color filter | Γ£à **DONE** | No per-pixel loop; GPU-eligible when Phase 3 wired. |
| **2.7** | `Random.Shared` in Slice/TornEdge | Γ£à **DONE** | No determinism issue on rapid calls; no allocation. |
| **2.4** | `SKFilterQuality` ΓåÆ `SKSamplingOptions` | ΓÜá∩╕Å **Reverted** ΓÇö SkiaSharp 2.88.9 missing overloads | Re-apply after SkiaSharp upgrade. |
| **2.5** | Remove redundant `Category` overrides (7 files) | Γ£à **DONE** | Code consistency. |
| **2.1** | `ColorizeImageEffect` refactor to use helper | Γ£à **DONE** | Colorize gains GPU path when Phase 3 wired. |
| **3 (library)** | GPU surface via `GRContext` for `ApplyColorFilter`/`ApplyColorMatrix` | Γ£à **DONE** | ImageEditor GPU path ready; host wiring deferred to Phase 3. |
| **3 (host wiring)** | XerahS calls `SetGpuContext(grContext)` to activate GPU path | ΓÅ╕∩╕Å **DEFERRED** | Pursue when canvas compositor is designed (┬º3). |
| **3.5** | GPU read-back threshold | Γ£à **DONE** | 160 000 px threshold in place; revisit value when Phase 3 is profiled. |
| **2.8** | Diagnostics / observability (`IEditorDiagnosticsSink`) | Γ£à **DONE** (Round 3) | GPU path decisions visible in host debug output. |
| **2.2** | `GammaImageEffect` LUT caching | ΓÜá∩╕Å Low priority | Minor live-preview speedup. |
| **2.3** | `BlurImageEffect` allocation reduction | ΓÜá∩╕Å Low priority | One fewer SKBitmap per blur call. |
| **2.6** | `SelectiveColor` `ToHsl` short-circuit | ΓÜá∩╕Å Profile first | Potential speedup at 4K scale. |

**Remaining open items:**

- **Phase 3 host wiring ΓÇö deferred:** Pursue when the canvas compositor is designed. Use persistent offscreen `GRContext` (Option B, ┬º3.3); the Avalonia 11 frame-scoped lease API cannot be used outside a render callback. The `GRContext?` parameter stays in the API as a forward-compatibility hook at zero cost.

- **┬º2.4** ΓÇö Re-apply `SKSamplingOptions` migration after upgrading SkiaSharp past 2.88.9.

- **┬º2.2, ┬º2.3, ┬º2.6** ΓÇö Profile-gated; only implement if profiling confirms they are bottlenecks.

---

## 5. Conclusions for future readers

This section summarizes Avalonia-related constraints, the impact of refactoring vs re-architecting, and performance implications so future readers can avoid repeating the same analysis.

### 5.1 Avalonia limitations (TL;DR)

| Limitation | Summary |
|-----------|---------|
| **AvaloniaLocator** | In Avalonia 11 it is deprecated / internals-hostile. It can return `null`, change between minor releases, or be removed. Do not rely on it for host wiring. |
| **Skia GPU lease** | `ISkiaSharpApiLease` / `ISkiaSharpApiLeaseFeature` (and thus `TryGetGrContext()`) are valid **only inside an active render pass** (e.g. `Render(DrawingContext)` or `ICustomDrawOperation`). The lease is invalid as soon as the callback returns. Being on the UI thread does **not** mean a render frame is active. |
| **Effect application** | Applying effects (e.g. on "Apply") happens at arbitrary call sites, **outside** any render callback. So you cannot acquire Avalonia's `GRContext` at effect-application time; it only works for drawing to the screen during a render pass. |
| **Viable path today** | For GPU-accelerated effect application, **Option B** (┬º3.3) ΓÇö a persistent offscreen `GRContext` created **independently** of Avalonia's renderer ΓÇö is the only viable approach under the current architecture. Option A (Avalonia's frame-scoped lease) does not work at arbitrary call sites. |

### 5.2 Would ImageEditor refactoring or re-architecting eliminate these issues?

- **Refactoring alone** (cleaner types, layering, code health) does **not** eliminate the Avalonia limitations. They are constraints of the host/framework, not of ImageEditor structure.
- **Re-architecting** so that effect application is **invoked from inside the Skia render callback** (e.g. "Apply" schedules work that runs in the next `ICustomDrawOperation` / render pass) could **work around** the frame-scoped lease: Option A (Avalonia's `GRContext`) would then become viable at the cost of:
  - **Latency:** Result available one frame later (schedule ΓåÆ next render ΓåÆ effect runs ΓåÆ result).
  - **Control flow:** More moving parts (schedule, render callback, callback to push result back).
  - **Coupling:** Effect pipeline or host tied to "run during render" instead of "run at any time."

The document's conclusion that "Option B is the only viable path" applies to the **current** architecture (effect application at arbitrary call sites). If you re-architect to run effects inside the render callback, Option A is theoretically viable; the trade-offs above should be weighed before doing so.

### 5.3 Performance impact of the render-callback approach

- **Latency (responsiveness):** The render-callback re-architecture adds **at least one frame of delay** (e.g. ~16 ms at 60 fps) before the effect result is ready. The user sees the result one frame later ΓÇö so **responsiveness** can feel slightly worse.
- **Throughput (compute):** The effect **computation** itself is not made slower by this design. If the GPU path is used in that callback, compute can be the same or faster than CPU for large images. The cost is the fixed wait for the next frame, not a slower algorithm.
- **Net for XIP0042-style usage:** For typical screenshot-sized images and color-matrixΓÇôstyle effects, the XIP already concludes that GPU often does not win (┬º3, table: upload + readback cost). So with the render-callback approach you would add one frame of latency **without** a clear compute win for those cases ΓÇö overall UX can be slightly slower (one-frame delay) without a compensating speedup.

---

## 6. References

- [docs/planning/IMAGEEDITOR_SKIA_GPU_PLAN.md](../../planning/IMAGEEDITOR_SKIA_GPU_PLAN.md)
- [Avalonia Skia ΓÇô TryGetGrContext](https://api-docs.avaloniaui.net/docs/M_Avalonia_Skia_ISkiaGpuWithPlatformGraphicsContext_TryGetGrContext)
- [SkiaSharp GRContext](https://learn.microsoft.com/en-us/dotnet/api/skiasharp.grcontext)
- [SkiaSharp SKSamplingOptions migration](https://github.com/mono/SkiaSharp/wiki/API-Changes-2.88)
- Base helpers: [ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Adjustments/ImageEffect.cs](../ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Adjustments/ImageEffect.cs)
- BlackAndWhite (┬º1a): [ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Adjustments/BlackAndWhiteImageEffect.cs](../ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Adjustments/BlackAndWhiteImageEffect.cs)
- Colorize (┬º2.1): [ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Adjustments/ColorizeImageEffect.cs](../ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Adjustments/ColorizeImageEffect.cs)
- Pixelate (┬º2.4): [ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/PixelateImageEffect.cs](../ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/PixelateImageEffect.cs)
- Resize (┬º2.4): [ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Manipulations/ResizeImageEffect.cs](../ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Manipulations/ResizeImageEffect.cs)
- Slice (┬º2.7): [ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/SliceImageEffect.cs](../ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/SliceImageEffect.cs)
- TornEdge (┬º2.7): [ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/TornEdgeImageEffect.cs](../ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/TornEdgeImageEffect.cs)
- Diagnostics interface (┬º2.8): `ImageEditor/src/ShareX.ImageEditor/Services/IEditorDiagnosticsSink.cs`; `EditorDiagnosticEvent.cs`; `EditorServices.cs`
- Diagnostics adapter (┬º2.8): [src/desktop/app/XerahS.UI/Services/EditorDiagnosticsAdapter.cs](../src/desktop/app/XerahS.UI/Services/EditorDiagnosticsAdapter.cs)
- Debug launch script (┬º2.8): [src/desktop/app/run-debug-app.sh](../../../src/desktop/app/run-debug-app.sh)
- ShareX host: `ShareX/TaskHelpers.cs` (`OpenImageEditor`, `UseModernImageEditor` ΓåÆ `AvaloniaIntegration.ShowEditorDialog`); `ShareX.ImageEditor/Helpers/AvaloniaIntegration.cs`
