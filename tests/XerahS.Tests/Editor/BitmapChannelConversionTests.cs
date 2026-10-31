using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.NUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NUnit.Framework;
using ShareX.ImageEditor.Presentation.Rendering;
using SkiaSharp;

namespace XerahS.Tests.Editor;

[TestFixture]
public class BitmapChannelConversionTests
{
    [AvaloniaTest]
    [TestCase(false, false, AlphaFormat.Premul)]
    [TestCase(false, true, AlphaFormat.Premul)]
    [TestCase(true, false, AlphaFormat.Premul)]
    [TestCase(true, true, AlphaFormat.Premul)]
    [TestCase(false, false, AlphaFormat.Unpremul)]
    [TestCase(false, true, AlphaFormat.Unpremul)]
    [TestCase(true, false, AlphaFormat.Unpremul)]
    [TestCase(true, true, AlphaFormat.Unpremul)]
    [TestCase(false, false, AlphaFormat.Opaque)]
    [TestCase(true, true, AlphaFormat.Opaque)]
    public void Conversion_PreservesRedBlueAndAlpha(bool rgba, bool writable, AlphaFormat alpha)
    {
        var format = rgba ? PixelFormat.Rgba8888 : PixelFormat.Bgra8888;
        byte a = alpha == AlphaFormat.Opaque ? (byte)255 : (byte)128;
        byte r = alpha == AlphaFormat.Premul ? (byte)100 : (byte)200;
        byte g = alpha == AlphaFormat.Premul ? (byte)10 : (byte)20;
        byte b = alpha == AlphaFormat.Premul ? (byte)20 : (byte)40;
        // Three pixels per row, with padding in the immutable input.
        byte[] data = new byte[32];
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 3; x++)
            {
                int i = y * 16 + x * 4;
                data[i] = rgba ? r : b; data[i + 1] = g; data[i + 2] = rgba ? b : r; data[i + 3] = a;
            }
        Bitmap input;
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            input = writable
                ? new WriteableBitmap(format, alpha, handle.AddrOfPinnedObject(), new PixelSize(3, 2), new Vector(96, 96), 16)
                : new Bitmap(format, alpha, handle.AddrOfPinnedObject(), new PixelSize(3, 2), new Vector(96, 96), 16);
        }
        finally { handle.Free(); }
        using (input)
        using (var sk = BitmapConversionHelpers.ToSKBitmap(input))
        using (var output = BitmapConversionHelpers.ToAvaloniBitmap(sk))
        using (var roundTrip = BitmapConversionHelpers.ToSKBitmap(output))
        {
            foreach (var bitmap in new[] { sk, roundTrip })
                for (int y = 0; y < 2; y++)
                    for (int x = 0; x < 3; x++)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        Assert.Multiple(() =>
                        {
                            Assert.That(pixel.Red, Is.EqualTo(200).Within(2));
                            Assert.That(pixel.Green, Is.EqualTo(20).Within(2));
                            Assert.That(pixel.Blue, Is.EqualTo(40).Within(2));
                            Assert.That(pixel.Alpha, Is.EqualTo(a));
                        });
                    }
        }
    }
}
