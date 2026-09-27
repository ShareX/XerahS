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

using System.Runtime.InteropServices;
using NUnit.Framework;
using SkiaSharp;
using XerahS.Platform.Linux.Capture.X11;

namespace XerahS.Tests.Platform.Linux;

[TestFixture]
public class X11ScreenCaptureTests
{
    private const int LsbFirst = 0;
    private const ulong Red8 = 0xFF0000;
    private const ulong Green8 = 0xFF00;
    private const ulong Blue8 = 0xFF;

    [Test]
    public void ConvertZPixmap_Bgrx32_CopiesChannelsAndSetsAlphaOpaque()
    {
        byte[] source = [10, 20, 30, 0, 40, 50, 60, 0x7F];

        byte[] pixels = Convert(source, width: 2, height: 1, bytesPerLine: 8, bitsPerPixel: 32, Red8, Green8, Blue8);

        Assert.That(pixels, Is.EqualTo(new byte[] { 10, 20, 30, 255, 40, 50, 60, 255 }));
    }

    [Test]
    public void ConvertZPixmap_Bgrx32PaddedRows_ReadsEachRowFromItsOwnOffset()
    {
        byte[] source =
        [
            1, 2, 3, 0, 4, 5, 6, 0, 0xEE, 0xEE, 0xEE, 0xEE,
            7, 8, 9, 0, 10, 11, 12, 0, 0xEE, 0xEE, 0xEE, 0xEE
        ];

        byte[] pixels = Convert(source, width: 2, height: 2, bytesPerLine: 12, bitsPerPixel: 32, Red8, Green8, Blue8);

        Assert.That(pixels, Is.EqualTo(new byte[]
        {
            1, 2, 3, 255, 4, 5, 6, 255,
            7, 8, 9, 255, 10, 11, 12, 255
        }));
    }

    [Test]
    public void ConvertZPixmap_Bgrx32_MatchesSourceBytesForRandomImage()
    {
        const int width = 37;
        const int height = 5;
        const int bytesPerLine = width * 4 + 12;
        byte[] source = new byte[bytesPerLine * height];
        new Random(1234).NextBytes(source);

        byte[] pixels = Convert(source, width, height, bytesPerLine, bitsPerPixel: 32, Red8, Green8, Blue8);

        byte[] expected = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int src = y * bytesPerLine + x * 4;
                int dst = (y * width + x) * 4;
                expected[dst] = source[src];
                expected[dst + 1] = source[src + 1];
                expected[dst + 2] = source[src + 2];
                expected[dst + 3] = 255;
            }
        }

        Assert.That(pixels, Is.EqualTo(expected));
    }

    [Test]
    public void ConvertZPixmap_Depth30_KeepsTopEightBitsOfEachChannel()
    {
        // Pixels read back from a depth 30 Xvfb root window (masks 0x3FF00000, 0xFFC00, 0x3FF):
        // 0x3E2460C8 shows RGB 248, 70, 50 and 0x020DAA19 shows RGB 8, 218, 134.
        byte[] source = [0xC8, 0x60, 0x24, 0x3E, 0x19, 0xAA, 0x0D, 0x02];

        byte[] pixels = Convert(source, width: 2, height: 1, bytesPerLine: 8, bitsPerPixel: 32, 0x3FF00000, 0xFFC00, 0x3FF);

        Assert.That(pixels, Is.EqualTo(new byte[] { 50, 70, 248, 255, 134, 218, 8, 255 }));
    }

    [Test]
    public void ConvertZPixmap_Packed24_ReadsThreeBytesPerPixel()
    {
        byte[] source = [10, 20, 30, 40, 50, 60, 0xEE, 0xEE];

        byte[] pixels = Convert(source, width: 2, height: 1, bytesPerLine: 8, bitsPerPixel: 24, Red8, Green8, Blue8);

        Assert.That(pixels, Is.EqualTo(new byte[] { 10, 20, 30, 255, 40, 50, 60, 255 }));
    }

    [Test]
    public void ConvertZPixmap_FewerThan24BitsPerPixel_ReturnsNull()
    {
        IntPtr sourcePtr = Marshal.AllocHGlobal(8);
        try
        {
            using SKBitmap? bitmap = X11ScreenCapture.ConvertZPixmap(sourcePtr, 2, 1, 8, 16, LsbFirst, 0xF800, 0x7E0, 0x1F);
            Assert.That(bitmap, Is.Null);
        }
        finally
        {
            Marshal.FreeHGlobal(sourcePtr);
        }
    }

    [Test]
    public void ConvertZPixmap_NullData_ReturnsNull()
    {
        using SKBitmap? bitmap = X11ScreenCapture.ConvertZPixmap(IntPtr.Zero, 2, 1, 8, 32, LsbFirst, Red8, Green8, Blue8);

        Assert.That(bitmap, Is.Null);
    }

    /// <summary>Converts <paramref name="source"/> and returns the bitmap's Bgra8888 pixel bytes without row padding.</summary>
    private static byte[] Convert(byte[] source, int width, int height, int bytesPerLine, int bitsPerPixel,
        ulong redMask, ulong greenMask, ulong blueMask)
    {
        IntPtr sourcePtr = Marshal.AllocHGlobal(source.Length);
        try
        {
            Marshal.Copy(source, 0, sourcePtr, source.Length);
            using SKBitmap? bitmap = X11ScreenCapture.ConvertZPixmap(
                sourcePtr, width, height, bytesPerLine, bitsPerPixel, LsbFirst, redMask, greenMask, blueMask);

            Assert.That(bitmap, Is.Not.Null);
            Assert.That(bitmap!.ColorType, Is.EqualTo(SKColorType.Bgra8888));

            byte[] pixels = new byte[width * height * 4];
            ReadOnlySpan<byte> bitmapBytes = bitmap.GetPixelSpan();
            for (int y = 0; y < height; y++)
            {
                bitmapBytes.Slice(y * bitmap.RowBytes, width * 4).CopyTo(pixels.AsSpan(y * width * 4));
            }

            return pixels;
        }
        finally
        {
            Marshal.FreeHGlobal(sourcePtr);
        }
    }
}
