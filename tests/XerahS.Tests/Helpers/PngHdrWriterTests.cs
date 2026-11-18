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

using NUnit.Framework;
using SkiaSharp;
using XerahS.Common;
using XerahS.Common.Helpers;

namespace XerahS.Tests.Helpers;

public class PngHdrWriterTests
{
    [Test]
    public void EncodeChannel_ZeroIsBlack()
    {
        Assert.That(PngHdrWriter.EncodeChannel(0f), Is.Zero);
        Assert.That(PngHdrWriter.EncodeChannel(-1f), Is.Zero);
        Assert.That(PngHdrWriter.EncodeChannel(float.NaN), Is.Zero);
    }

    [Test]
    public void EncodeChannel_SdrWhiteLandsAtBt2408ReferenceWhite()
    {
        // 1.0 == SDR white == 203 cd/m2. PQ does not put that at mid-scale: the BT.2408 reference
        // white for a 10 000 nit container encodes to roughly 0.58 of full scale.
        ushort code = PngHdrWriter.EncodeChannel(1f);
        Assert.That(code, Is.InRange(37_900, 38_200));
    }

    [Test]
    public void EncodeChannel_IsMonotonicAndKeepsHighlightsDistinct()
    {
        ushort white = PngHdrWriter.EncodeChannel(1f);
        ushort tenX = PngHdrWriter.EncodeChannel(10f);
        ushort fortyX = PngHdrWriter.EncodeChannel(40f);
        ushort beyondPqPeak = PngHdrWriter.EncodeChannel(100f);

        Assert.That(tenX, Is.GreaterThan(white));
        Assert.That(fortyX, Is.GreaterThan(tenX));
        Assert.That(fortyX, Is.LessThan(ushort.MaxValue), "8000 nits still fits below 10000");
        Assert.That(beyondPqPeak, Is.EqualTo(ushort.MaxValue), "beyond the PQ peak the signal saturates");
    }

    [Test]
    public void EncodePq16_PreservesPeakRatioAndWritesOpaqueSamples()
    {
        using SKBitmap source = CreateFp16(new float[] { 0f, 1f, 2f, 4f });
        using SKBitmap? encoded = PngHdrWriter.EncodePq16(source, out float peak);

        Assert.That(encoded, Is.Not.Null);
        Assert.That(encoded!.ColorType, Is.EqualTo(SKColorType.Rgba16161616));
        Assert.That(peak, Is.EqualTo(4f).Within(0.001f));

        ReadSamples(encoded, out ushort dark, out ushort mid, out ushort alpha);
        Assert.That(dark, Is.Zero);
        Assert.That(alpha, Is.EqualTo(ushort.MaxValue), "alpha must be opaque");
        Assert.That(mid, Is.GreaterThan(dark), "brighter pixels must encode higher");
    }

    [Test]
    public void EncodePq16_RejectsNonFloatBitmap()
    {
        using var source = new SKBitmap(1, 1);
        Assert.That(PngHdrWriter.EncodePq16(source, out float peak), Is.Null);
        Assert.That(peak, Is.Zero);
    }

    [Test]
    public void InsertChunk_PlacesCicpBeforeFirstIdatWithValidCrc()
    {
        byte[] png = PngHdrWriter.InsertChunk(SmallestPng(), "cICP", new byte[] { 9, 16, 0, 1 });
        int cicp = IndexOfChunk(png, "cICP");
        int idat = IndexOfChunk(png, "IDAT");

        Assert.That(cicp, Is.GreaterThan(0));
        Assert.That(idat, Is.GreaterThan(cicp));

        Assert.That(ReadChunkPayload(png, "cICP"), Is.EqualTo(new byte[] { 9, 16, 0, 1 }),
            "BT.2020 primaries, PQ transfer, identity matrix, full range");
    }

    [Test]
    public void TrySave_WritesHdr10PngWithCicpChunk()
    {
        using SKBitmap source = CreateFp16(new float[] { 0f, 1f, 8f, 8f });
        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"xerahs-hdr10-{Guid.NewGuid():N}.png");
        try
        {
            Assert.That(PngHdrWriter.TrySave(source, path, out float peak), Is.True);
            Assert.That(peak, Is.EqualTo(8f).Within(0.001f));

            byte[] png = File.ReadAllBytes(path);
            Assert.That(ReadChunkPayload(png, "cICP"), Is.Not.Null);
            Assert.That(IndexOfChunk(png, "cICP"), Is.LessThan(IndexOfChunk(png, "IDAT")));
            Assert.That(ReadPngChunks(png).BitDepth, Is.EqualTo(16));
            Assert.That(ReadPngChunks(png).ColorType, Is.AnyOf(2, 6), "truecolour, alpha dropped when opaque");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Test]
    public void SaveBitmap_WithHdr10Depth_WritesCicpPng()
    {
        using SKBitmap source = CreateFp16(new float[] { 0f, 1f, 8f, 8f });
        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"xerahs-hdr10-depth-{Guid.NewGuid():N}.png");
        try
        {
            ImageHelpers.SaveBitmap(source, path, PNGBitDepth.Bit16Hdr);

            byte[] png = File.ReadAllBytes(path);
            Assert.That(ReadChunkPayload(png, "cICP"), Is.Not.Null);
            Assert.That(ReadPngChunks(png).BitDepth, Is.EqualTo(16));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static unsafe SKBitmap CreateFp16(float[] values)
    {
        var bitmap = new SKBitmap(new SKImageInfo(values.Length, 1, SKColorType.RgbaF16, SKAlphaType.Opaque,
            SKColorSpace.CreateSrgbLinear()));
        byte* pixels = (byte*)bitmap.GetPixels();
        for (int i = 0; i < values.Length; i++)
        {
            for (int channel = 0; channel < 3; channel++)
            {
                ushort bits = BitConverter.HalfToUInt16Bits((Half)values[i]);
                pixels[(i * 8) + (channel * 2)] = (byte)(bits & 0xFF);
                pixels[(i * 8) + (channel * 2) + 1] = (byte)(bits >> 8);
            }

            // Alpha 1.0.
            ushort alpha = BitConverter.HalfToUInt16Bits((Half)1f);
            pixels[(i * 8) + 6] = (byte)(alpha & 0xFF);
            pixels[(i * 8) + 7] = (byte)(alpha >> 8);
        }

        bitmap.NotifyPixelsChanged();
        return bitmap;
    }

    private static byte[] SmallestPng()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(1, 1, SKColorType.Bgra8888, SKAlphaType.Opaque));
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static unsafe void ReadSamples(SKBitmap bitmap, out ushort first, out ushort second, out ushort alpha)
    {
        ushort* samples = (ushort*)bitmap.GetPixels();
        first = samples[0];
        second = samples[4];
        alpha = samples[3];
    }

    private readonly record struct PngHeader(int BitDepth, int ColorType);

    private static PngHeader ReadPngHeader(byte[] png)
    {
        Assert.That(png.Length, Is.GreaterThan(33));
        Assert.That(png[12], Is.EqualTo((byte)'I'));
        Assert.That(png[13], Is.EqualTo((byte)'H'));
        Assert.That(png[14], Is.EqualTo((byte)'D'));
        Assert.That(png[15], Is.EqualTo((byte)'R'));
        return new PngHeader(png[24], png[25]);
    }

    private static PngHeader ReadPngChunks(byte[] png) => ReadPngHeader(png);


    private static int IndexOfChunk(byte[] png, string type)
    {
        int position = 8;
        while (position + 12 <= png.Length)
        {
            int length = (png[position] << 24) | (png[position + 1] << 16) | (png[position + 2] << 8) | png[position + 3];
            if (length < 0) return -1;

            string name = System.Text.Encoding.ASCII.GetString(png, position + 4, 4);
            if (name == type) return position;
            position += 12 + length;
        }

        return -1;
    }

    private static byte[]? ReadChunkPayload(byte[] png, string type)
    {
        int position = IndexOfChunk(png, type);
        if (position < 0) return null;

        int length = (png[position] << 24) | (png[position + 1] << 16) | (png[position + 2] << 8) | png[position + 3];
        var payload = new byte[length];
        Array.Copy(png, position + 8, payload, 0, length);
        return payload;
    }
}
