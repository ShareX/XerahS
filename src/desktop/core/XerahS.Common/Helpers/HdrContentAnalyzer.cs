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

namespace XerahS.Common.Helpers;

/// <summary>
/// Content analysis for HDR (fp16 scRGB) captures: estimated SDR white level and how much
/// content sits above it. Used to decide whether a capture is effectively SDR (windows on an
/// HDR desktop) and should therefore be saved as display-referred sRGB, or carries real HDR
/// highlights and must keep its extended-range linear samples.
/// </summary>
public static class HdrContentAnalyzer
{
    /// <summary>
    /// Analyzes an HDR capture. Returns the estimated SDR white level (the 99th percentile of
    /// the per-pixel maximum channel) and the peak/white ratio. A ratio close to 1.0 means the
    /// frame has no content above SDR white (effectively SDR). Non-fp16 input returns (1, 1).
    /// </summary>
    public static (float WhiteLevel, float PeakRatio) Analyze(SKBitmap? hdrBitmap)
    {
        if (hdrBitmap is null || hdrBitmap.ColorType != SKColorType.RgbaF16)
        {
            return (1f, 1f);
        }

        ReadOnlySpan<byte> src = hdrBitmap.GetPixelSpan();
        int stride = hdrBitmap.RowBytes;
        int w = hdrBitmap.Width;
        int h = hdrBitmap.Height;
        int step = Math.Max(1, Math.Max(w, h) / 320);

        var samples = new List<float>(1024);
        float peak = 0f;
        for (int y = 0; y < h; y += step)
        {
            for (int x = 0; x < w; x += step)
            {
                int o = y * stride + x * 8;
                float r = DecodeHalf(src, o);
                float g = DecodeHalf(src, o + 2);
                float b = DecodeHalf(src, o + 4);
                float lum = MathF.Max(r, MathF.Max(g, b));
                samples.Add(lum);
                if (lum > peak) peak = lum;
            }
        }

        if (samples.Count == 0)
        {
            return (1f, 1f);
        }

        samples.Sort();
        // Keep the percentile out of the top-most samples: on tiny images (a handful of pixels)
        // the p99 index would land exactly on the peak and misclassify HDR content as SDR.
        int p99Index = Math.Clamp((int)(samples.Count * 0.99), 0, Math.Max(0, samples.Count - 2));
        float p99 = samples[p99Index];
        float white = MathF.Max(p99, 1e-3f);
        float ratio = white > 0 ? peak / white : 1f;
        return (white, ratio);
    }

    /// <summary>
    /// True when the capture has no meaningful content above its SDR white level (peak is within
    /// <paramref name="toleranceRatio"/> of white), i.e. an SDR desktop frame that should be saved
    /// display-referred so it looks identical in standard viewers.
    /// </summary>
    public static bool IsEffectivelySdr(SKBitmap? hdrBitmap, out float peakRatio, float toleranceRatio = 1.05f)
    {
        var (_, ratio) = Analyze(hdrBitmap);
        peakRatio = ratio;
        return ratio <= toleranceRatio;
    }

    private static float DecodeHalf(ReadOnlySpan<byte> buffer, int offset)
    {
        ushort bits = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(offset, 2));
        float value = (float)BitConverter.UInt16BitsToHalf(bits);
        return float.IsFinite(value) ? MathF.Max(value, 0f) : 0f;
    }
}