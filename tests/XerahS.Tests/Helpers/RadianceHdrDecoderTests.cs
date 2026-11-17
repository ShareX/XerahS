using System.Buffers.Binary;
using System.Text;
using NUnit.Framework;
using SkiaSharp;
using XerahS.Common.Helpers;

namespace XerahS.Tests.Helpers;

/// <summary>
/// The bundled settings preview loads a Radiance .hdr file; these tests cover both scanline
/// encodings and the linear (not sRGB) conversion, since a decode mistake would silently shift
/// the tone-curve comparison the panel exists to show.
/// </summary>
public class RadianceHdrDecoderTests
{
    // 128 * 2^(128-136) = 0.5, 64 * 2^-8 = 0.25, 32 * 2^-8 = 0.125
    private static readonly byte[] RgbePixel = { 128, 64, 32, 128 };

    [Test]
    public void TryDecode_ReadsFlatScanlinesAsLinearScRgb()
    {
        using SKBitmap? bitmap = RadianceHdrDecoder.TryDecode(new MemoryStream(BuildRadiance(4, 2, useRle: false)));

        Assert.That(bitmap, Is.Not.Null);
        Assert.That(bitmap!.Width, Is.EqualTo(4));
        Assert.That(bitmap.Height, Is.EqualTo(2));
        Assert.That(bitmap.ColorType, Is.EqualTo(SKColorType.RgbaF16));

        for (int i = 0; i < 4 * 2; i++)
        {
            Assert.That(ReadChannel(bitmap, i, 0), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(ReadChannel(bitmap, i, 1), Is.EqualTo(0.25f).Within(1e-4f));
            Assert.That(ReadChannel(bitmap, i, 2), Is.EqualTo(0.125f).Within(1e-4f));
            Assert.That(ReadChannel(bitmap, i, 3), Is.EqualTo(1f).Within(1e-4f));
        }
    }

    [Test]
    public void TryDecode_ReadsAdaptiveRleScanlines()
    {
        using SKBitmap? bitmap = RadianceHdrDecoder.TryDecode(new MemoryStream(BuildRadiance(8, 1, useRle: true)));

        Assert.That(bitmap, Is.Not.Null);
        Assert.That(bitmap!.Width, Is.EqualTo(8));

        for (int i = 0; i < 8; i++)
        {
            Assert.That(ReadChannel(bitmap, i, 0), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(ReadChannel(bitmap, i, 2), Is.EqualTo(0.125f).Within(1e-4f));
        }
    }

    [Test]
    public void TryDecode_KeepsHdrValuesAboveDisplayWhite()
    {
        // RGBE mantissa 128 with exponent 130 is 128 * 2^(130-136) = 2.0 in scRGB, i.e. 2x SDR white.
        byte[] data = BuildRadiance(4, 1, useRle: false, new byte[] { 128, 128, 128, 130 });
        using SKBitmap? bitmap = RadianceHdrDecoder.TryDecode(new MemoryStream(data));

        Assert.That(bitmap, Is.Not.Null);
        Assert.That(ReadChannel(bitmap!, 0, 0), Is.EqualTo(2f).Within(1e-4f));
    }

    [Test]
    public void TryDecode_ReturnsNullForNonRadianceData()
    {
        Assert.That(RadianceHdrDecoder.TryDecode(new MemoryStream(Encoding.ASCII.GetBytes("not a radiance file"))), Is.Null);
    }

    private static float ReadChannel(SKBitmap bitmap, int pixelIndex, int channel)
    {
        Span<byte> pixels = bitmap.GetPixelSpan();
        int offset = (pixelIndex / bitmap.Width) * bitmap.RowBytes + (pixelIndex % bitmap.Width) * 8 + channel * 2;
        return (float)BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(pixels.Slice(offset, 2)));
    }

    private static byte[] BuildRadiance(int width, int height, bool useRle, byte[]? pixel = null)
    {
        pixel ??= RgbePixel;
        var stream = new MemoryStream();

        void Text(string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value + "\n");
            stream.Write(bytes, 0, bytes.Length);
        }

        Text("#?RADIANCE");
        Text("FORMAT=32-bit_rle_rgbe");
        Text(string.Empty);
        Text("-Y " + height + " +X " + width);

        if (useRle)
        {
            Assert.That(width, Is.InRange(8, 32767));
            for (int y = 0; y < height; y++)
            {
                stream.WriteByte(2);
                stream.WriteByte(2);
                stream.WriteByte((byte)(width >> 8));
                stream.WriteByte((byte)(width & 0xFF));

                for (int channel = 0; channel < 4; channel++)
                {
                    stream.WriteByte((byte)(128 + width)); // run of width identical bytes
                    stream.WriteByte(pixel[channel]);
                }
            }
        }
        else
        {
            for (int i = 0; i < width * height; i++)
            {
                stream.Write(pixel, 0, 4);
            }
        }

        return stream.ToArray();
    }
}
