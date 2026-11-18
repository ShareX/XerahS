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

using System.Text;
using SkiaSharp;

namespace XerahS.Common.Helpers;

/// <summary>
/// Writes an fp16 scRGB capture (1.0 == SDR white) as a 16-bit PNG that actually carries HDR:
/// SMPTE ST 2084 (PQ) transfer, BT.2020 primaries, signalled by a <c>cICP</c> chunk from the
/// third edition of the PNG specification.
/// <para>
/// A PNG sample cannot exceed 1.0, so HDR is stored the way video does it - as absolute luminance
/// on the PQ curve rather than as values above white. Viewers that understand <c>cICP</c> render
/// it as HDR10; the Windows built-in decoders (WIC/GDI+, and therefore Explorer, Photos and Paint)
/// ignore the chunk and show it through the sRGB curve instead, so the file looks washed out
/// there. Use Ultra HDR JPEG when the file has to look right in those viewers.
/// </para>
/// </summary>
public static class PngHdrWriter
{
    /// <summary>
    /// BT.2408 reference white: the luminance "1.0 == SDR white" is encoded as. Using a constant
    /// rather than the monitor's setting keeps the file identical on every machine.
    /// </summary>
    public const float ReferenceWhiteNits = 203f;

    private const float PqPeakNits = 10000f;

    // SMPTE ST 2084 constants.
    private const float M1 = 2610f / 16384f;
    private const float M2 = 2523f / 4096f * 128f;
    private const float C1 = 3424f / 4096f;
    private const float C2 = 2413f / 4096f * 32f;
    private const float C3 = 2392f / 4096f * 32f;

    /// <summary>cICP payload: BT.2020 primaries, PQ transfer, identity matrix, full range.</summary>
    private static readonly byte[] CicpPayload = [9, 16, 0, 1];

    /// <summary>
    /// Saves <paramref name="bitmap"/> as a 16-bit PQ PNG with a cICP chunk.
    /// Returns false for a non-fp16 bitmap or when encoding fails.
    /// </summary>
    public static bool TrySave(SKBitmap bitmap, string filePath, out float peakRatio)
    {
        peakRatio = 0f;

        using SKBitmap? pq = EncodePq16(bitmap, out peakRatio);
        if (pq is null)
        {
            return false;
        }

        using SKImage image = SKImage.FromBitmap(pq);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        if (data is null)
        {
            return false;
        }

        FileHelpers.CreateDirectoryFromFilePath(filePath);
        byte[] png = InsertChunk(data.ToArray(), "cICP", CicpPayload);
        File.WriteAllBytes(filePath, png);
        return true;
    }

    /// <summary>
    /// Converts an RgbaF16 scRGB bitmap into 16-bit PQ samples. <paramref name="peakRatio"/> is the
    /// brightest channel relative to SDR white, so callers can report whether the file really holds
    /// an HDR range. Returns null for a non-fp16 input.
    /// </summary>
    public static unsafe SKBitmap? EncodePq16(SKBitmap? bitmap, out float peakRatio)
    {
        peakRatio = 0f;
        if (bitmap is null || bitmap.ColorType != SKColorType.RgbaF16)
        {
            return null;
        }

        int width = bitmap.Width;
        int height = bitmap.Height;
        var destination = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba16161616, SKAlphaType.Opaque));

        byte* sourceBase = (byte*)bitmap.GetPixels();
        byte* destinationBase = (byte*)destination.GetPixels();
        int sourceStride = bitmap.RowBytes;
        int destinationStride = destination.RowBytes;
        float peak = 0f;

        for (int y = 0; y < height; y++)
        {
            byte* sourceRow = sourceBase + ((nint)y * sourceStride);
            ushort* destinationRow = (ushort*)(destinationBase + ((nint)y * destinationStride));

            for (int x = 0; x < width; x++)
            {
                byte* pixel = sourceRow + (x * 8);
                float red = ReadHalf(pixel);
                float green = ReadHalf(pixel + 2);
                float blue = ReadHalf(pixel + 4);

                peak = MathF.Max(peak, MathF.Max(red, MathF.Max(green, blue)));

                destinationRow[(x * 4) + 0] = EncodeChannel(red);
                destinationRow[(x * 4) + 1] = EncodeChannel(green);
                destinationRow[(x * 4) + 2] = EncodeChannel(blue);
                destinationRow[(x * 4) + 3] = ushort.MaxValue;
            }
        }

        peakRatio = float.IsFinite(peak) ? MathF.Max(peak, 0f) : 0f;
        return destination;
    }

    /// <summary>Linear scRGB (1.0 == SDR white) to a PQ-encoded 16-bit sample.</summary>
    internal static ushort EncodeChannel(float linear)
    {
        if (!float.IsFinite(linear) || linear <= 0f)
        {
            return 0;
        }

        float nits = linear * ReferenceWhiteNits;
        float normalized = MathF.Min(nits / PqPeakNits, 1f);
        float powInput = MathF.Pow(normalized, M1);
        float pq = MathF.Pow((C1 + (C2 * powInput)) / (1f + (C3 * powInput)), M2);
        return (ushort)Math.Clamp(MathF.Round(pq * 65535f), 0f, 65535f);
    }

    private static unsafe float ReadHalf(byte* channel)
    {
        ushort bits = (ushort)(channel[0] | (channel[1] << 8));
        float value = (float)BitConverter.UInt16BitsToHalf(bits);
        return float.IsFinite(value) ? MathF.Max(value, 0f) : 0f;
    }

    /// <summary>
    /// Inserts an ancillary chunk before the first IDAT, which is where a decoder looks for it.
    /// Only the new chunk is assembled; every existing chunk keeps its original bytes and CRC.
    /// </summary>
    internal static byte[] InsertChunk(byte[] png, string type, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(png);
        ArgumentNullException.ThrowIfNull(payload);

        int insertAt = FindFirstIdat(png);
        if (insertAt < 0)
        {
            return png;
        }

        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        if (typeBytes.Length != 4)
        {
            throw new ArgumentException("Chunk type must be four ASCII characters.", nameof(type));
        }

        var chunk = new byte[12 + payload.Length];
        WriteBigEndian(chunk, 0, payload.Length);
        typeBytes.CopyTo(chunk, 4);
        payload.CopyTo(chunk, 8);
        uint crc = Crc32(chunk, 4, 4 + payload.Length);
        WriteBigEndian(chunk, 8 + payload.Length, (int)crc);

        var result = new byte[png.Length + chunk.Length];
        Array.Copy(png, 0, result, 0, insertAt);
        Array.Copy(chunk, 0, result, insertAt, chunk.Length);
        Array.Copy(png, insertAt, result, insertAt + chunk.Length, png.Length - insertAt);
        return result;
    }

    private static int FindFirstIdat(byte[] png)
    {
        if (png.Length < 8 || png[0] != 0x89 || png[1] != 0x50 || png[2] != 0x4E || png[3] != 0x47)
        {
            return -1;
        }

        int position = 8;
        while (position + 12 <= png.Length)
        {
            int length = (png[position] << 24) | (png[position + 1] << 16) | (png[position + 2] << 8) | png[position + 3];
            if (length < 0)
            {
                return -1;
            }

            if (png[position + 4] == 'I' && png[position + 5] == 'D' && png[position + 6] == 'A' && png[position + 7] == 'T')
            {
                return position;
            }

            position += 12 + length;
        }

        return -1;
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] data, int offset, int count)
    {
        uint crc = 0xFFFFFFFFu;
        for (int i = offset; i < offset + count; i++)
        {
            crc ^= data[i];
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
