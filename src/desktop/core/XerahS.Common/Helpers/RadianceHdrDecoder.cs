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
/// Decodes Radiance RGBE (<c>.hdr</c>) images into fp16 linear scRGB bitmaps.
/// Used for the bundled CC0 HDR photograph shown in the settings preview and for HDR tooling.
/// Supports both adaptive-RLE and flat scanline encodings.
/// </summary>
public static class RadianceHdrDecoder
{
    /// <summary>Decodes a .hdr file. Returns null when the data is not a valid Radiance image.</summary>
    public static SKBitmap? TryDecode(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return TryDecode(stream);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"RadianceHdrDecoder: {path} failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Decodes Radiance data from a stream. Returns null when the header is not Radiance.</summary>
    public static SKBitmap? TryDecode(Stream stream)
    {
        try
        {
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            // The header is a list of text lines terminated by an empty line.
            string? line;
            while (!string.IsNullOrEmpty(line = ReadLine(reader)))
            {
            }

            string? resolution = ReadLine(reader);
            if (resolution is null)
            {
                return null;
            }

            string[] parts = resolution.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
            {
                return null;
            }

            bool yFirst = parts[0].StartsWith("-Y", StringComparison.Ordinal);
            int height = int.Parse(parts[1]);
            int width = int.Parse(parts[3]);
            if (!yFirst)
            {
                (height, width) = (width, height);
            }

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            var rgbe = new byte[width * height * 4];
            ReadPixels(reader, rgbe, width, height);

            var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.RgbaF16, SKAlphaType.Premul, SKColorSpace.CreateSrgbLinear()));
            Span<byte> dst = bitmap.GetPixelSpan();
            int stride = bitmap.RowBytes;

            for (int i = 0; i < width * height; i++)
            {
                int o = i * 4;
                byte exponent = rgbe[o + 3];
                float scale = exponent == 0 ? 0f : MathF.Pow(2f, exponent - 136f); // mantissa * 2^(E-128-8)
                int d = (i / width) * stride + (i % width) * 8;
                WriteHalf(dst, d, rgbe[o] * scale);
                WriteHalf(dst, d + 2, rgbe[o + 1] * scale);
                WriteHalf(dst, d + 4, rgbe[o + 2] * scale);
                WriteHalf(dst, d + 6, 1f);
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"RadianceHdrDecoder: decode failed: {ex.Message}");
            return null;
        }
    }

    private static void ReadPixels(BinaryReader reader, byte[] rgbe, int width, int height)
    {
        if (width >= 8 && width < 32768)
        {
            for (int y = 0; y < height; y++)
            {
                byte b0 = reader.ReadByte(), b1 = reader.ReadByte(), b2 = reader.ReadByte(), b3 = reader.ReadByte();
                if (b0 == 2 && b1 == 2 && (b2 & 0x80) == 0)
                {
                    int scanlineWidth = (b2 << 8) | b3;
                    if (scanlineWidth != width)
                    {
                        throw new FormatException($"RLE scanline length {scanlineWidth} != width {width}");
                    }

                    for (int channel = 0; channel < 4; channel++)
                    {
                        int x = 0;
                        while (x < width)
                        {
                            int count = reader.ReadByte();
                            if (count > 128)
                            {
                                int run = count - 128;
                                byte value = reader.ReadByte();
                                for (int i = 0; i < run && x < width; i++)
                                {
                                    rgbe[(y * width + x) * 4 + channel] = value;
                                    x++;
                                }
                            }
                            else
                            {
                                for (int i = 0; i < count && x < width; i++)
                                {
                                    rgbe[(y * width + x) * 4 + channel] = reader.ReadByte();
                                    x++;
                                }
                            }
                        }
                    }
                }
                else
                {
                    rgbe[(y * width) * 4] = b0;
                    rgbe[(y * width) * 4 + 1] = b1;
                    rgbe[(y * width) * 4 + 2] = b2;
                    rgbe[(y * width) * 4 + 3] = b3;
                    for (int x = 1; x < width; x++)
                    {
                        int o = (y * width + x) * 4;
                        rgbe[o] = reader.ReadByte();
                        rgbe[o + 1] = reader.ReadByte();
                        rgbe[o + 2] = reader.ReadByte();
                        rgbe[o + 3] = reader.ReadByte();
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < width * height; i++)
            {
                int o = i * 4;
                rgbe[o] = reader.ReadByte();
                rgbe[o + 1] = reader.ReadByte();
                rgbe[o + 2] = reader.ReadByte();
                rgbe[o + 3] = reader.ReadByte();
            }
        }
    }

    private static void WriteHalf(Span<byte> span, int offset, float value)
    {
        float clamped = float.IsFinite(value) ? MathF.Min(MathF.Max(value, 0f), 60000f) : 0f;
        ushort bits = BitConverter.HalfToUInt16Bits((Half)clamped);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset, 2), bits);
    }

    private static string? ReadLine(BinaryReader reader)
    {
        var builder = new StringBuilder();
        int ch;
        while ((ch = reader.Read()) != -1)
        {
            if (ch == '\n')
            {
                return builder.ToString();
            }

            if (ch != '\r')
            {
                builder.Append((char)ch);
            }
        }

        return builder.Length > 0 ? builder.ToString() : null;
    }
}
