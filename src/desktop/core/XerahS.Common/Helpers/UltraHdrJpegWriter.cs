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
using System.Text;
using SkiaSharp;

namespace XerahS.Common.Helpers;

/// <summary>
/// Writes an <b>Ultra HDR JPEG</b> (ISO 21496-1) from an fp16 scRGB capture.
///
/// A 16-bit PNG physically cannot carry values above display white, so "SDR+HDR" output as PNG is
/// display-referred and loses its highlights. This container can: it stores an ordinary SDR JPEG
/// plus a grayscale gain map, and Windows Photos, Chrome and Android render the HDR range from it.
///
/// File layout produced:
///   base SDR JPEG (display-referred, clamped at SDR white)
///   + APP1 XMP with hdrgm:* gain-map metadata
///   + APP2 MPF with two entries (primary = base, secondary = gain map)
///   + appended grayscale gain-map JPEG
///
/// The gain map is <c>log2(HDR/SDR)</c> normalized to <c>GainMapMax = log2(peak/white)</c>, which is
/// the interpretation the hdrgm metadata declares (linear gamma, base rendition is SDR).
/// </summary>
public static class UltraHdrJpegWriter
{
    /// <summary>
    /// Saves <paramref name="hdrBitmap"/> (RgbaF16, linear scRGB where 1.0 == SDR white) as an
    /// Ultra HDR JPEG. Returns false for non-fp16 input or when encoding fails (nothing is written
    /// in that case).
    /// </summary>
    /// <param name="sdrWhite">SDR white level in scRGB units; detected from the content when null.</param>
    public static bool TrySave(
        SKBitmap? hdrBitmap,
        string filePath,
        int baseQuality = 92,
        int gainMapQuality = 90,
        float? sdrWhite = null)
    {
        if (hdrBitmap is null || hdrBitmap.ColorType != SKColorType.RgbaF16 || string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        if (hdrBitmap.Width <= 0 || hdrBitmap.Height <= 0)
        {
            return false;
        }

        try
        {
            float white = sdrWhite ?? HdrContentAnalyzer.Analyze(hdrBitmap).WhiteLevel;
            if (!float.IsFinite(white) || white <= 0f)
            {
                white = 1f;
            }

            using SKBitmap baseBmp = RenderClampedSdr(hdrBitmap, white);
            var (gainMap, gainMapMax) = BuildGainMap(hdrBitmap, white);

            byte[] baseJpeg = EncodeJpeg(baseBmp, baseQuality);
            byte[] gainJpeg = EncodeGainMapJpeg(gainMap, hdrBitmap.Width, hdrBitmap.Height, gainMapQuality);
            byte[] xmp = BuildXmpPacket(gainMapMax);

            int sos = FindSosOffset(baseJpeg);
            if (sos <= 0)
            {
                DebugHelper.WriteLine("UltraHdrJpegWriter: base JPEG has no SOS marker.");
                return false;
            }

            int mpfSegLen = BuildMpfSegment(0, (uint)gainJpeg.Length, 0).Length;
            int suffixLen = baseJpeg.Length - sos;

            // Primary image size spans everything up to the gain-map SOI.
            uint primarySize = (uint)(sos + xmp.Length + mpfSegLen + suffixLen);
            // MPF offsets are relative to the TIFF header start (8 bytes into the APP2 segment:
            // marker 2 + length 2 + "MPF\0" 4).
            uint tiffAbs = (uint)(sos + xmp.Length + 8);
            uint secondaryOffset = primarySize - tiffAbs;

            byte[] mpf = BuildMpfSegment(primarySize, (uint)gainJpeg.Length, secondaryOffset);

            FileHelpers.CreateDirectoryFromFilePath(filePath);
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(baseJpeg, 0, sos);
                stream.Write(xmp, 0, xmp.Length);
                stream.Write(mpf, 0, mpf.Length);
                stream.Write(baseJpeg, sos, suffixLen);
                stream.Write(gainJpeg, 0, gainJpeg.Length);
            }

            DebugHelper.WriteLine(
                $"[HDR] Ultra HDR JPEG written: {hdrBitmap.Width}x{hdrBitmap.Height}, " +
                $"sdrWhite={white:F3}, gainMapMax={gainMapMax:F3} (max boost {MathF.Pow(2f, gainMapMax):F1}x): {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"UltraHdrJpegWriter failed: {ex.Message}");
            DebugHelper.WriteException(ex);
            return false;
        }
    }

    /// <summary>Display-referred SDR base: clamp at SDR white, then apply the sRGB OETF.</summary>
    private static SKBitmap RenderClampedSdr(SKBitmap hdr, float white)
    {
        int w = hdr.Width, h = hdr.Height;
        var result = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque, SKColorSpace.CreateSrgb()));
        ReadOnlySpan<byte> src = hdr.GetPixelSpan();
        Span<byte> dst = result.GetPixelSpan();
        int srcStride = hdr.RowBytes, dstStride = result.RowBytes;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int so = y * srcStride + x * 8;
                int doff = y * dstStride + x * 4;
                float r = MathF.Min(ReadHalf(src, so) / white, 1f);
                float g = MathF.Min(ReadHalf(src, so + 2) / white, 1f);
                float b = MathF.Min(ReadHalf(src, so + 4) / white, 1f);
                dst[doff] = ToByte(SrgbOetf(b));
                dst[doff + 1] = ToByte(SrgbOetf(g));
                dst[doff + 2] = ToByte(SrgbOetf(r));
                dst[doff + 3] = 255;
            }
        }

        return result;
    }

    /// <summary>Grayscale gain map + the GainMapMax the XMP declares.</summary>
    private static (byte[] Pixels, float GainMapMax) BuildGainMap(SKBitmap hdr, float white)
    {
        int w = hdr.Width, h = hdr.Height;
        ReadOnlySpan<byte> src = hdr.GetPixelSpan();
        int stride = hdr.RowBytes;

        // GainMapMax = log2(peak / white), clamped to a sane range.
        float peak = 0f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int so = y * stride + x * 8;
                float lum = MathF.Max(ReadHalf(src, so), MathF.Max(ReadHalf(src, so + 2), ReadHalf(src, so + 4)));
                if (lum > peak) peak = lum;
            }
        }

        float gainMapMax = MathF.Max(MathF.Log2(MathF.Max(peak / MathF.Max(white, 1e-3f), 1.0001f)), 0.01f);
        var pixels = new byte[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int so = y * stride + x * 8;
                float r = ReadHalf(src, so);
                float g = ReadHalf(src, so + 2);
                float b = ReadHalf(src, so + 4);
                float hdrLum = MathF.Max(r, MathF.Max(g, b));
                float sdrLum = MathF.Min(hdrLum / white, 1f);

                float logGain = 0f;
                if (sdrLum > 1e-4f && hdrLum > 0f)
                {
                    logGain = MathF.Log2(hdrLum / (sdrLum * white));
                }

                float normalized = Math.Clamp(logGain / gainMapMax, 0f, 1f);
                pixels[y * w + x] = (byte)MathF.Round(normalized * 255f);
            }
        }

        return (pixels, gainMapMax);
    }

    private static byte[] EncodeGainMapJpeg(byte[] pixels, int w, int h, int quality)
    {
        var gray = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque));
        try
        {
            Span<byte> dst = gray.GetPixelSpan();
            int stride = gray.RowBytes;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    byte v = pixels[y * w + x];
                    int o = y * stride + x * 4;
                    dst[o] = v;
                    dst[o + 1] = v;
                    dst[o + 2] = v;
                    dst[o + 3] = 255;
                }
            }

            using var data = gray.Encode(SKEncodedImageFormat.Jpeg, quality);
            return data!.ToArray();
        }
        finally
        {
            gray.Dispose();
        }
    }

    private static byte[] EncodeJpeg(SKBitmap bitmap, int quality)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, quality);
        return data!.ToArray();
    }

    private static byte[] BuildXmpPacket(float gainMapMax)
    {
        string gm = gainMapMax.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
        string xmp =
            "<?xpacket begin=\"\uFEFF\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>" +
            "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">" +
            "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">" +
            "<rdf:Description rdf:about=\"\" " +
            "xmlns:hdrgm=\"http://ns.adobe.com/hdr-gain-map/1.0/\" " +
            "hdrgm:Version=\"1.0\" " +
            $"hdrgm:GainMapMin=\"0.0000\" hdrgm:GainMapMax=\"{gm}\" hdrgm:Gamma=\"1.0000\" " +
            "hdrgm:OffsetSDR=\"0.015625\" hdrgm:OffsetHDR=\"0.015625\" " +
            $"hdrgm:HDRCapacityMin=\"0.0000\" hdrgm:HDRCapacityMax=\"{gm}\" " +
            "hdrgm:BaseRenditionIsHDR=\"False\"/>" +
            "</rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";

        byte[] xmpBytes = Encoding.UTF8.GetBytes(xmp);
        byte[] prefix = Encoding.ASCII.GetBytes("http://ns.adobe.com/xap/1.0/\0");
        int payload = prefix.Length + xmpBytes.Length;
        var segment = new byte[2 + 2 + payload];
        segment[0] = 0xFF;
        segment[1] = 0xE1;
        segment[2] = (byte)((payload + 2) >> 8);
        segment[3] = (byte)((payload + 2) & 0xFF);
        prefix.CopyTo(segment, 4);
        xmpBytes.CopyTo(segment, 4 + prefix.Length);
        return segment;
    }

    /// <summary>APP2 MPF segment (big-endian TIFF, layout verified against libultrahdr).</summary>
    private static byte[] BuildMpfSegment(uint primarySize, uint secondarySize, uint secondaryOffset)
    {
        const int payloadLen = 86; // "MPF\0" (4) + TIFF (82)
        var segment = new byte[2 + 2 + payloadLen];
        segment[0] = 0xFF;
        segment[1] = 0xE2;
        segment[2] = (byte)((payloadLen + 2) >> 8);
        segment[3] = (byte)((payloadLen + 2) & 0xFF);

        int p = 4;
        "MPF\0"u8.CopyTo(segment.AsSpan(p)); p += 4;

        // TIFF header (big-endian).
        segment[p++] = (byte)'M';
        segment[p++] = (byte)'M';
        WriteU16Be(segment, ref p, 0x002A);
        WriteU32Be(segment, ref p, 8);          // index IFD offset
        WriteU16Be(segment, ref p, 3);          // tag count

        // 0xB000 MPFVersion (UNDEFINED, 4) = "0100"
        WriteU16Be(segment, ref p, 0xB000);
        WriteU16Be(segment, ref p, 7);
        WriteU32Be(segment, ref p, 4);
        segment[p++] = (byte)'0';
        segment[p++] = (byte)'1';
        segment[p++] = (byte)'0';
        segment[p++] = (byte)'0';

        // 0xB001 NumberOfImages (LONG, 1) = 2
        WriteU16Be(segment, ref p, 0xB001);
        WriteU16Be(segment, ref p, 4);
        WriteU32Be(segment, ref p, 1);
        WriteU32Be(segment, ref p, 2);

        // 0xB002 MPEntry (UNDEFINED, 32) at offset 50
        WriteU16Be(segment, ref p, 0xB002);
        WriteU16Be(segment, ref p, 7);
        WriteU32Be(segment, ref p, 32);
        WriteU32Be(segment, ref p, 50);

        // Attribute IFD offset (0 = none).
        WriteU32Be(segment, ref p, 0);

        // Primary entry.
        WriteU32Be(segment, ref p, 0x00030000);  // JPEG format + primary type
        WriteU32Be(segment, ref p, primarySize);
        WriteU32Be(segment, ref p, 0);
        WriteU16Be(segment, ref p, 0);
        WriteU16Be(segment, ref p, 0);

        // Secondary (gain map) entry.
        WriteU32Be(segment, ref p, 0);           // JPEG format
        WriteU32Be(segment, ref p, secondarySize);
        WriteU32Be(segment, ref p, secondaryOffset);
        WriteU16Be(segment, ref p, 0);
        WriteU16Be(segment, ref p, 0);

        return segment;
    }

    private static int FindSosOffset(byte[] jpeg)
    {
        int pos = 2; // after SOI
        while (pos + 4 <= jpeg.Length)
        {
            if (jpeg[pos] != 0xFF)
            {
                pos++;
                continue;
            }

            byte marker = jpeg[pos + 1];
            if (marker == 0xDA)
            {
                return pos;
            }

            if (marker == 0xFF)
            {
                pos++;
                continue;
            }

            if (marker is >= 0xD0 and <= 0xD9 || marker == 0x01)
            {
                pos += 2;
                continue;
            }

            int len = (jpeg[pos + 2] << 8) | jpeg[pos + 3];
            pos += 2 + len;
        }

        return -1;
    }

    private static void WriteU16Be(byte[] buffer, ref int pos, ushort value)
    {
        buffer[pos++] = (byte)(value >> 8);
        buffer[pos++] = (byte)value;
    }

    private static void WriteU32Be(byte[] buffer, ref int pos, uint value)
    {
        buffer[pos++] = (byte)(value >> 24);
        buffer[pos++] = (byte)(value >> 16);
        buffer[pos++] = (byte)(value >> 8);
        buffer[pos++] = (byte)value;
    }

    private static float ReadHalf(ReadOnlySpan<byte> span, int offset)
    {
        ushort bits = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2));
        float v = (float)BitConverter.UInt16BitsToHalf(bits);
        return float.IsFinite(v) ? MathF.Max(v, 0f) : 0f;
    }

    private static float SrgbOetf(float linear)
    {
        if (linear <= 0.0031308f) return 12.92f * linear;
        return 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;
    }

    private static byte ToByte(float v) => (byte)MathF.Round(Math.Clamp(v, 0f, 1f) * 255f);
}
