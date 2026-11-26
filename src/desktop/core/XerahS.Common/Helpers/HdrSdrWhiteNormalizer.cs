#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.
*/

#endregion License Information (GPL v3)

using SkiaSharp;

namespace XerahS.Common.Helpers;

/// <summary>
/// Rescales an fp16 scRGB capture so that <c>1.0</c> means "SDR white", which is the invariant the
/// tone curves and the encoders rely on.
///
/// DXGI desktop duplication delivers linear scRGB where <c>1.0 == 80 cd/m2</c> by definition, but
/// Windows composites SDR content at the user's configured SDR white level (typically 200-300
/// cd/m2, i.e. 2.5-3.75 in scRGB). Without this rescale a normal desktop sits far above 1.0, so
/// every encoder clamps it: the SDR output blows out to white, and the tone curves get anchored at
/// the wrong pivot and shift the palette.
/// </summary>
public static class HdrSdrWhiteNormalizer
{
    /// <summary>
    /// Divides the RGB channels of an RgbaF16 bitmap by <paramref name="sdrWhiteScRgb"/> in place
    /// (alpha is left untouched). No-op for non-fp16 bitmaps or when the white level is already 1.0.
    /// </summary>
    /// <returns>True when the pixels were modified.</returns>
    public static unsafe bool NormalizeToSdrWhite(SKBitmap? bitmap, float sdrWhiteScRgb)
    {
        if (bitmap is null || bitmap.ColorType != SKColorType.RgbaF16)
        {
            return false;
        }

        if (!float.IsFinite(sdrWhiteScRgb) || sdrWhiteScRgb <= 0f || MathF.Abs(sdrWhiteScRgb - 1f) < 0.001f)
        {
            return false;
        }

        float scale = 1f / sdrWhiteScRgb;
        int width = bitmap.Width;
        int height = bitmap.Height;
        int stride = bitmap.RowBytes;
        byte* basePtr = (byte*)bitmap.GetPixels();

        for (int y = 0; y < height; y++)
        {
            byte* row = basePtr + (y * stride);
            for (int x = 0; x < width; x++)
            {
                byte* pixel = row + (x * 8);
                ScaleChannel(pixel + 0, scale);
                ScaleChannel(pixel + 2, scale);
                ScaleChannel(pixel + 4, scale);
            }
        }

        return true;
    }

    private static unsafe void ScaleChannel(byte* channel, float scale)
    {
        ushort bits = (ushort)(channel[0] | (channel[1] << 8));
        float value = (float)BitConverter.UInt16BitsToHalf(bits);
        if (!float.IsFinite(value))
        {
            // Leave NaN/Inf half values as they are; they are not meaningful luminance.
            return;
        }

        float scaled = MathF.Max(value * scale, 0f);
        if (scaled > 65504f)
        {
            scaled = 65504f; // largest half value
        }

        ushort outBits = BitConverter.HalfToUInt16Bits((Half)scaled);
        channel[0] = (byte)(outBits & 0xff);
        channel[1] = (byte)(outBits >> 8);
    }

    /// <summary>
    /// Converts a Windows <c>DISPLAYCONFIG_SDR_WHITE_LEVEL</c> value (always expressed relative to
    /// the scRGB reference of 80 cd/m2, in 1/1000 units) into an scRGB multiplier.
    /// </summary>
    public static float FromDisplayConfigWhiteLevel(uint displayConfigWhiteLevel)
    {
        if (displayConfigWhiteLevel == 0)
        {
            return 1f;
        }

        return displayConfigWhiteLevel / 1000f;
    }

    /// <summary>
    /// Converts an scRGB multiplier (1.0 == 80 cd/m2) into absolute nits, for diagnostics.
    /// </summary>
    public static float ScRgbToNits(float scRgb) => scRgb * 80f;
}
