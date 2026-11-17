#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System.Buffers.Binary;
using SkiaSharp;
using XerahS.Common;
using XerahS.Common.Helpers;

namespace XerahS.Core.Helpers;

/// <summary>
/// HDR → SDR tonemapping operating on raw F16 pixels (no Skia draw calls).
/// Pure per-pixel functions, deterministic and unit-testable (HDR_CAPTURE_PLAN §6.2).
/// </summary>
public static class HdrTonemapper
{
    // SMPTE ST 2084 (PQ) EOTF constants
    private const float PqM1 = 2610f / 16384f;
    private const float PqM2 = 2523f / 32f;
    private const float PqC1 = 3424f / 4096f;
    private const float PqC2 = 2413f / 128f;
    private const float PqC3 = 2392f / 128f;
    private const float PqMaxNits = 10000f;

    // sRGB piecewise transfer breakpoint
    private const float SrgbLinearSegmentMax = 0.0031308f;

    // scRGB-linear capture: 1.0 == 80 cd/m² (HDR_CAPTURE_PLAN §4.2, TODO HDR-02).
    private const float ScRgbToNits = 80f;

    // Gamma lift applied to the white-normalized signal. Because scRGB is already normalized to
    // SDR reference white (1.0 == 80 cd/m²), Reinhard/ACES can operate on it directly without
    // first dividing by the HDR display peak; doing so was crushing SDR white to ~8% of full scale.
    private const float WhitePreservationGamma = 0.5f;

    // ITU-R BT.2446 Method C (forward) constants. The curve is defined against the BT.2446
    // reference white (203 nits); the output is re-anchored below so SDR white maps to 1.0.
    private const float Bt2446ReferenceWhiteNits = 203f;
    private const float Bt2446LinearLength = 0.58535f;
    private const float Bt2446K1 = 0.83802f;
    private const float Bt2446K3 = 0.74204f;

    private static readonly float Bt2446Knee = Bt2446LinearLength / Bt2446K1;
    private static readonly float Bt2446K2 = Bt2446LinearLength * (1f - Bt2446K3);
    private static readonly float Bt2446K4 = Bt2446LinearLength - (Bt2446LinearLength * (1f - Bt2446K3)) * MathF.Log(1f - Bt2446K3);

    // scRGB → BT.2446 signal at SDR reference white (80 nits). 80/203 sits in the linear segment,
    // so this is simply the normalized value scaled by the linear strength.
    private static readonly float Bt2446SdrWhiteNormalized = ScRgbToNits / Bt2446ReferenceWhiteNits;
    private static readonly float Bt2446SdrWhiteCurve = Bt2446SdrWhiteNormalized * Bt2446K1;

    /// <summary>
    /// SMPTE ST 2084 (PQ) EOTF: normalized code value V → absolute luminance in cd/m².
    /// L = ((max(V^(1/m2) − c1, 0)) / (c2 − c3·V^(1/m2)))^(1/m1) · 10000; V clamped to [0, 1].
    /// </summary>
    public static float PqEotf(float v)
    {
        if (!float.IsFinite(v))
        {
            return 0f;
        }

        v = Math.Clamp(v, 0f, 1f);

        float vPow = MathF.Pow(v, 1f / PqM2);
        float numerator = MathF.Max(vPow - PqC1, 0f);
        float denominator = PqC2 - PqC3 * vPow;

        return MathF.Pow(numerator / denominator, 1f / PqM1) * PqMaxNits;
    }

    /// <summary>
    /// Standard piecewise sRGB OETF (linear segment ≤ 0.0031308), clamped to [0, 1].
    /// </summary>
    public static float SrgbOetf(float linear)
    {
        if (!float.IsFinite(linear))
        {
            return 0f;
        }

        linear = Math.Clamp(linear, 0f, 1f);

        if (linear <= SrgbLinearSegmentMax)
        {
            return 12.92f * linear;
        }

        return 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;
    }

    /// <summary>
    /// Reinhard curve: L' = L / (1 + L·k) with k = 1/peak. Invalid peak (≤0/NaN/Inf) passes L through unchanged.
    /// </summary>
    public static float TonemapReinhard(float linear, float peak)
    {
        if (!float.IsFinite(peak) || peak <= 0f)
        {
            return linear;
        }

        if (!float.IsFinite(linear))
        {
            return 0f;
        }

        return linear / (1f + linear * (1f / peak));
    }

    /// <summary>
    /// ACES fitted curve (RRT+ODT approximation): (x·(2.51x + 0.03)) / (x·(2.43x + 0.59) + 0.14), clamped to [0, 1].
    /// </summary>
    public static float TonemapAces(float linear)
    {
        if (!float.IsFinite(linear))
        {
            return 0f;
        }

        linear = MathF.Max(linear, 0f);

        float result = (linear * (2.51f * linear + 0.03f)) / (linear * (2.43f * linear + 0.59f) + 0.14f);
        return Math.Clamp(result, 0f, 1f);
    }

    // After-capture steps that require an 8-bit SDR image. With Auto, an HDR (fp16) capture is
    // tonemapped iff any of these is selected; SaveImageToFile alone keeps fp16 so the 16-bit PNG
    // path is preserved (HDR_CAPTURE_PLAN §6.3/§6.5).
    private const AfterCaptureTasks SdrOnlyAfterCaptureTasks =
        AfterCaptureTasks.ShowAfterCaptureWindow |
        AfterCaptureTasks.AddImageEffects |
        AfterCaptureTasks.AnnotateMedia |
        AfterCaptureTasks.CopyImageToClipboard |
        AfterCaptureTasks.UploadImageToHost;

    /// <summary>
    /// Decides whether an HDR (fp16) capture must be converted to 8-bit SDR before the
    /// after-capture pipeline runs (HDR_CAPTURE_PLAN §6.3). None never tonemaps; every other
    /// mode tonemaps only when an SDR-only step is selected, so SDR content is never needlessly
    /// tonemapped when the destination can carry HDR.
    /// </summary>
    internal static bool ShouldTonemapToSdr(HdrToneMappingMode mode, AfterCaptureTasks afterCaptureJob)
    {
        return mode != HdrToneMappingMode.None
            && (afterCaptureJob & SdrOnlyAfterCaptureTasks) != 0;
    }

    /// <summary>
    /// Tonemaps an RgbaF16 bitmap (scRGB-linear, 1.0 == 80 cd/m²) to 8-bit Bgra8888 (premul).
    /// scRGB is scene-referred and already normalized to SDR reference white, so the tone curves
    /// operate on it directly and only compress the HDR range above 1.0 (HDR_CAPTURE_PLAN §6.2).
    /// Returns null for any non-F16 input. None only clamps; every other mode applies its curve.
    /// </summary>
    public static SKBitmap? TonemapToBgra8888(SKBitmap hdrBitmap, HdrToneMappingMode mode)
    {
        if (hdrBitmap is null || hdrBitmap.ColorType != SKColorType.RgbaF16)
        {
            return null;
        }

        int width = hdrBitmap.Width;
        int height = hdrBitmap.Height;

        // One content analysis per bitmap: it provides the Hable anchor and tells us whether the
        // frame has any headroom above SDR white at all.
        var (whiteLevel, peakRatio) = HdrContentAnalyzer.Analyze(hdrBitmap);
        float sdrWhite = MathF.Max(whiteLevel, 1e-3f);

        // Effectively-SDR content (a normal desktop, an SDR photo or video on an HDR display) must
        // bypass the tone curves: there is nothing above SDR white to compress, and any curve only
        // moves midtones away from what the screen shows. Measured against a GDI grab of the same
        // frame, the bypassed output matches the screen within 0.3/255 while Hable lifts the mean
        // luminance by ~13%.
        bool effectivelySdr = peakRatio <= 1.05f;

        SKBitmap result = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb()));

        try
        {
            ReadOnlySpan<byte> src = hdrBitmap.GetPixelSpan();
            Span<byte> dst = result.GetPixelSpan();
            int srcStride = hdrBitmap.RowBytes;
            int dstStride = result.RowBytes;

            for (int y = 0; y < height; y++)
            {
                int srcRow = y * srcStride;
                int dstRow = y * dstStride;

                for (int x = 0; x < width; x++)
                {
                    int srcOffset = srcRow + x * 8;
                    int dstOffset = dstRow + x * 4;

                    float r = DecodeHalf(src, srcOffset);
                    float g = DecodeHalf(src, srcOffset + 2);
                    float b = DecodeHalf(src, srcOffset + 4);
                    float a = DecodeHalf(src, srcOffset + 6);

                    // Max-channel luminance; every channel is scaled by one factor to preserve hue.
                    float lum = MathF.Max(r, MathF.Max(g, b));
                    float scale = 1f;

                    if (lum > 0f)
                    {
                        float mapped = effectivelySdr ? lum : mode switch
                        {
                            HdrToneMappingMode.TonemapReinhard => TonemapReinhardNormalized(lum),
                            HdrToneMappingMode.TonemapAces => TonemapAcesNormalized(lum),
                            HdrToneMappingMode.TonemapBt2446C => TonemapBt2446C(lum),
                            HdrToneMappingMode.TonemapHable => TonemapHableNormalized(lum, sdrWhite),
                            HdrToneMappingMode.None => lum,
                            _ => TonemapHableNormalized(lum, sdrWhite) // legacy Auto
                        };
                        scale = mapped / lum;
                    }

                    dst[dstOffset] = ToByte(SrgbOetf(b * scale));
                    dst[dstOffset + 1] = ToByte(SrgbOetf(g * scale));
                    dst[dstOffset + 2] = ToByte(SrgbOetf(r * scale));
                    dst[dstOffset + 3] = ToByte(a);
                }
            }

            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    /// White-normalized Reinhard: scRGB is already normalized to SDR white (1.0 == 80 cd/m²), so
    /// Ld = sqrt(L/(1+L)) keeps SDR white bright and rolls off only the HDR range above 1.0.
    /// </summary>
    private static float TonemapReinhardNormalized(float luminanceScRgb)
    {
        float normalized = MathF.Max(luminanceScRgb, 0f);
        float mapped = normalized / (1f + normalized);
        return MathF.Pow(mapped, WhitePreservationGamma);
    }

    /// <summary>
    /// White-normalized ACES: Ld = ACES(sqrt(L)) with scRGB 1.0 (80 nits) landing near display white.
    /// </summary>
    private static float TonemapAcesNormalized(float luminanceScRgb)
    {
        float normalized = MathF.Max(luminanceScRgb, 0f);
        return TonemapAces(MathF.Pow(normalized, WhitePreservationGamma));
    }

    /// <summary>
    /// ITU-R BT.2446 Method C HDR→SDR tone curve. Takes linear scRGB luminance (1.0 == 80 cd/m²)
    /// and returns display-linear SDR. The signal is normalized to BT.2446 reference white
    /// (203 nits) and compressed by a linear segment + logarithmic shoulder, then re-anchored so
    /// SDR white (80 nits) maps to 1.0; HDR highlights roll above 1.0 and clamp on encode.
    /// </summary>
    public static float TonemapBt2446C(float luminanceScRgb)
    {
        if (!float.IsFinite(luminanceScRgb))
        {
            return 0f;
        }

        float normalized = MathF.Max(luminanceScRgb, 0f) * ScRgbToNits / Bt2446ReferenceWhiteNits;

        float toneMapped = normalized < Bt2446Knee
            ? normalized * Bt2446K1
            : MathF.Log(normalized / Bt2446Knee - Bt2446K3) * Bt2446K2 + Bt2446K4;

        return MathF.Max(toneMapped / Bt2446SdrWhiteCurve, 0f);
    }

    /// <summary>
    /// Hable/Uncharted-2 filmic curve anchored at the capture's SDR white level: the signal is
    /// normalized so SDR white maps to 1.0, then the filmic shoulder compresses only the range
    /// above it. Chosen by visual assessment of real HDR photographs: natural sky/highlight
    /// rolloff with near-reference colors. On effectively-SDR captures the white level is ~1.0
    /// and the curve acts as a mild contrast curve instead of clipping highlights.
    /// </summary>
    public static float TonemapHableNormalized(float luminanceScRgb, float sdrWhite)
    {
        if (!float.IsFinite(luminanceScRgb))
        {
            return 0f;
        }

        float white = float.IsFinite(sdrWhite) && sdrWhite > 1e-3f ? sdrWhite : 1f;
        float x = MathF.Max(luminanceScRgb, 0f) / white;
        return Math.Clamp(HableCurve(x) / HableCurve(1f), 0f, 1f);
    }

    private static float HableCurve(float v)
    {
        const float A = 0.15f, B = 0.50f, C = 0.10f, D = 0.20f, E = 0.02f, F = 0.30f;
        return ((v * (A * v + C * B) + D * E) / (v * (A * v + B) + D * F)) - E / F;
    }

    private static float DecodeHalf(ReadOnlySpan<byte> buffer, int offset)
    {
        ushort bits = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(offset, 2));
        float value = (float)BitConverter.UInt16BitsToHalf(bits);
        return float.IsFinite(value) ? MathF.Max(value, 0f) : 0f;
    }

    private static byte ToByte(float value)
    {
        return (byte)MathF.Round(Math.Clamp(value, 0f, 1f) * 255f);
    }
}