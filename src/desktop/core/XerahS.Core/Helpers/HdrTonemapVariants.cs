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
using XerahS.Common.Helpers;

namespace XerahS.Core.Helpers;

/// <summary>
/// Experimental HDR→SDR variants used by the self-test / visual selection workflow.
/// Each variant is a hue-preserving luminance mapping (same curve applied to R, G and B)
/// plus an optional anchor at the capture's estimated SDR white level. The production
/// modes live in <see cref="HdrTonemapper"/>; these variants exist so a real capture can be
/// rendered through several candidate curves and compared side by side.
/// </summary>
public static class HdrTonemapVariants
{
    /// <summary>
    /// A candidate tonemap: maps linear scRGB luminance to display-linear luminance.
    /// <paramref name="sdrWhite"/> is the estimated SDR white level of the capture (scRGB).
    /// </summary>
    public sealed record Variant(string Id, string Label, Func<float, float, float> MapLuminance);

    public static IReadOnlyList<Variant> All { get; } = new[]
    {
        new Variant("clamp", "None: clamp at 1.0 (current)", (l, _) => MathF.Min(l, 1f)),
        new Variant("clamp-norm", "Clamp at estimated SDR white", (l, w) => MathF.Min(l / SafeWhite(w), 1f)),
        new Variant("reinhard", "Reinhard (current)", (l, _) => ReinhardNormalized(l)),
        new Variant("reinhard-norm", "Reinhard anchored at SDR white", (l, w) => ReinhardNormalized(l / SafeWhite(w))),
        new Variant("aces", "ACES (current)", (l, _) => AcesNormalized(l)),
        new Variant("aces-norm", "ACES anchored at SDR white", (l, w) => AcesNormalized(l / SafeWhite(w))),
        new Variant("bt2446c", "BT.2446C (current)", (l, _) => HdrTonemapper.TonemapBt2446C(l)),
        new Variant("bt2446c-norm", "BT.2446C anchored at SDR white", (l, w) => HdrTonemapper.TonemapBt2446C(l / SafeWhite(w))),
        new Variant("hable-norm", "Hable filmic anchored at SDR white", (l, w) => HableNormalized(l / SafeWhite(w))),
        new Variant("gamma-norm", "Gamma 2.2 at SDR white (viewer-like)", (l, w) => MathF.Pow(MathF.Min(l / SafeWhite(w), 1f), 2.2f)),
    };

    /// <summary>
    /// Estimated SDR white level (scRGB linear) of an HDR capture: the 99th percentile of the
    /// per-pixel maximum channel. SDR windows on an HDR desktop all sit at the compositor's SDR
    /// white, so this robust percentile lands on that level; true HDR highlights exceed it.
    /// Returns 1.0 for non-fp16 input.
    /// </summary>
    public static float EstimateSdrWhite(SKBitmap hdrBitmap)
    {
        var (white, _) = HdrContentAnalyzer.Analyze(hdrBitmap);
        return white;
    }

    /// <summary>
    /// Analyzes an HDR capture: estimated SDR white level (p99 of max channel) and the peak
    /// luminance ratio relative to that white. A ratio close to 1.0 means the capture carries
    /// no content above SDR white — i.e. an effectively SDR frame on an HDR desktop.
    /// </summary>
    public static (float WhiteLevel, float PeakRatio) AnalyzeHdrContent(SKBitmap hdrBitmap)
    {
        return HdrContentAnalyzer.Analyze(hdrBitmap);
    }

    /// <summary>
    /// Applies one variant to an HDR (fp16) capture. Returns an 8-bit sRGB bitmap (owned by the
    /// caller) or null for non-fp16 input.
    /// </summary>
    public static SKBitmap? Apply(SKBitmap hdrBitmap, Variant variant, float sdrWhite)
    {
        if (hdrBitmap is null || hdrBitmap.ColorType != SKColorType.RgbaF16 || variant is null)
        {
            return null;
        }

        int w = hdrBitmap.Width;
        int h = hdrBitmap.Height;
        var result = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb()));

        try
        {
            ReadOnlySpan<byte> src = hdrBitmap.GetPixelSpan();
            Span<byte> dst = result.GetPixelSpan();
            int srcStride = hdrBitmap.RowBytes;
            int dstStride = result.RowBytes;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int so = y * srcStride + x * 8;
                    int doff = y * dstStride + x * 4;

                    float r = DecodeHalf(src, so);
                    float g = DecodeHalf(src, so + 2);
                    float b = DecodeHalf(src, so + 4);
                    float a = DecodeHalf(src, so + 6);

                    float lum = MathF.Max(r, MathF.Max(g, b));
                    float scale = 1f;
                    if (lum > 0f)
                    {
                        float mapped = variant.MapLuminance(lum, sdrWhite);
                        scale = mapped / lum;
                    }

                    dst[doff] = ToByte(SrgbOetf(b * scale));
                    dst[doff + 1] = ToByte(SrgbOetf(g * scale));
                    dst[doff + 2] = ToByte(SrgbOetf(r * scale));
                    dst[doff + 3] = ToByte(a);
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

    // ── curves ───────────────────────────────────────────────────────────────

    private static float SafeWhite(float white) => float.IsFinite(white) && white > 1e-3f ? white : 1f;

    private static float ReinhardNormalized(float luminance)
    {
        float n = MathF.Max(luminance, 0f);
        return MathF.Pow(n / (1f + n), 0.5f);
    }

    private static float AcesNormalized(float luminance)
    {
        float n = MathF.Max(luminance, 0f);
        float x = MathF.Pow(n, 0.5f);
        float result = (x * (2.51f * x + 0.03f)) / (x * (2.43f * x + 0.59f) + 0.14f);
        return Math.Clamp(result, 0f, 1f);
    }

    private static float HableNormalized(float x)
    {
        static float H(float v)
        {
            const float A = 0.15f, B = 0.50f, C = 0.10f, D = 0.20f, E = 0.02f, F = 0.30f;
            return ((v * (A * v + C * B) + D * E) / (v * (A * v + B) + D * F)) - E / F;
        }

        return Math.Clamp(H(MathF.Max(x, 0f)) / H(1f), 0f, 1f);
    }

    private static float DecodeHalf(ReadOnlySpan<byte> buffer, int offset)
    {
        ushort bits = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(offset, 2));
        float value = (float)BitConverter.UInt16BitsToHalf(bits);
        return float.IsFinite(value) ? MathF.Max(value, 0f) : 0f;
    }

    private static float SrgbOetf(float linear)
    {
        if (!float.IsFinite(linear)) return 0f;
        linear = Math.Clamp(linear, 0f, 1f);
        if (linear <= 0.0031308f) return 12.92f * linear;
        return 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;
    }

    private static byte ToByte(float value) => (byte)MathF.Round(Math.Clamp(value, 0f, 1f) * 255f);
}