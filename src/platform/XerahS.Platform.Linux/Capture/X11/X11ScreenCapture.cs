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
using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Linux;

namespace XerahS.Platform.Linux.Capture.X11;

/// <summary>
/// Full-screen capture using X11 XGetImage. Use only when not on Wayland.
/// </summary>
internal static class X11ScreenCapture
{
    /// <summary>
    /// Captures the root window (full screen) via XGetImage. Returns null on Wayland or failure.
    /// </summary>
    public static async Task<SKBitmap?> CaptureFullScreenAsync(bool isWayland)
    {
        if (isWayland)
        {
            DebugHelper.WriteLine("LinuxScreenCaptureService: X11 capture skipped (Wayland active).");
            return null;
        }

        return await Task.Run(() => CaptureFullScreen()).ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronous full-screen capture. Call only when not on Wayland.
    /// </summary>
    public static SKBitmap? CaptureFullScreen()
    {
        IntPtr display = NativeMethods.XOpenDisplay(null);
        if (display == IntPtr.Zero)
        {
            DebugHelper.WriteLine("LinuxScreenCaptureService: XOpenDisplay failed.");
            return null;
        }

        try
        {
            var screen = 0;
            var root = NativeMethods.XDefaultRootWindow(display);
            int width = NativeMethods.XDisplayWidth(display, screen);
            int height = NativeMethods.XDisplayHeight(display, screen);
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            IntPtr imagePtr = NativeMethods.XGetImage(display, root, 0, 0, (uint)width, (uint)height, ulong.MaxValue, NativeMethods.ZPixmap);
            if (imagePtr == IntPtr.Zero)
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: XGetImage returned null.");
                return null;
            }

            try
            {
                var ximage = Marshal.PtrToStructure<XImage>(imagePtr);
                return ConvertZPixmap(ximage.data, width, height, ximage.bytes_per_line, ximage.bits_per_pixel,
                    ximage.byte_order, ximage.red_mask, ximage.green_mask, ximage.blue_mask);
            }
            finally
            {
                NativeMethods.XDestroyImage(imagePtr);
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "LinuxScreenCaptureService: X11 capture failed.");
            return null;
        }
        finally
        {
            NativeMethods.XCloseDisplay(display);
        }
    }

    /// <summary>
    /// Converts XGetImage ZPixmap data to an opaque Bgra8888 bitmap.
    /// Returns null when there is no data or fewer than 24 bits per pixel.
    /// </summary>
    internal static SKBitmap? ConvertZPixmap(IntPtr data, int width, int height, int bytesPerLine, int bitsPerPixel,
        int byteOrder, ulong redMask, ulong greenMask, ulong blueMask)
    {
        if (data == IntPtr.Zero || bitsPerPixel < 24)
        {
            return null;
        }

        int bytesPerPixel = Math.Max(1, bitsPerPixel / 8);
        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);

        int redShift = GetTrailingZeroCount(redMask);
        int greenShift = GetTrailingZeroCount(greenMask);
        int blueShift = GetTrailingZeroCount(blueMask);

        int redBits = GetContinuousOnes(redMask >> redShift);
        int greenBits = GetContinuousOnes(greenMask >> greenShift);
        int blueBits = GetContinuousOnes(blueMask >> blueShift);

        IntPtr dstBase = bitmap.GetPixels();
        int dstStride = bitmap.RowBytes;

        // Build each row in a managed buffer and copy it into the bitmap in one call.
        // SKBitmap.SetPixel per pixel costs a native call each: ~4 s at 2560x1600.
        // Bgra8888 in little-endian memory is 0xAARRGGBB, the same layout as a
        // 32 bpp LSBFirst ZPixmap with masks 0xFF0000/0xFF00/0xFF, so that common
        // case is a straight row copy with the alpha byte forced to 0xFF.
        bool isBgrx32 = bytesPerPixel == 4 &&
            byteOrder == 0 &&
            redMask == 0xFF0000 &&
            greenMask == 0xFF00 &&
            blueMask == 0xFF;

        var row = new int[width];
        for (int y = 0; y < height; y++)
        {
            var rowStart = IntPtr.Add(data, y * bytesPerLine);
            if (isBgrx32)
            {
                Marshal.Copy(rowStart, row, 0, width);
                for (int x = 0; x < width; x++)
                {
                    row[x] |= unchecked((int)0xFF000000);
                }
            }
            else
            {
                for (int x = 0; x < width; x++)
                {
                    var pixelPtr = IntPtr.Add(rowStart, x * bytesPerPixel);
                    uint pixelValue = ReadPixel(pixelPtr, bytesPerPixel);

                    uint r = NormalizeChannel((pixelValue & (uint)redMask) >> redShift, redBits);
                    uint g = NormalizeChannel((pixelValue & (uint)greenMask) >> greenShift, greenBits);
                    uint b = NormalizeChannel((pixelValue & (uint)blueMask) >> blueShift, blueBits);

                    row[x] = unchecked((int)(0xFF000000u | (r << 16) | (g << 8) | b));
                }
            }

            Marshal.Copy(row, 0, IntPtr.Add(dstBase, y * dstStride), width);
        }

        return bitmap;
    }

    private static uint ReadPixel(IntPtr ptr, int bytesPerPixel)
    {
        if (bytesPerPixel >= 4)
        {
            return (uint)Marshal.ReadInt32(ptr);
        }

        uint value = 0;
        for (int i = 0; i < bytesPerPixel; i++)
        {
            value |= (uint)Marshal.ReadByte(ptr, i) << (8 * i);
        }

        return value;
    }

    private static byte NormalizeChannel(uint value, int bits)
    {
        if (bits <= 0)
        {
            return 0;
        }

        if (bits >= 8)
        {
            return (byte)(value >> (bits - 8));
        }

        uint max = (1u << bits) - 1;
        if (max == 0)
        {
            return 0;
        }

        return (byte)((value * 255u) / max);
    }

    private static int GetTrailingZeroCount(ulong mask)
    {
        int shift = 0;
        while (shift < 64 && (mask & 1) == 0)
        {
            mask >>= 1;
            shift++;
        }

        return shift;
    }

    private static int GetContinuousOnes(ulong mask)
    {
        int count = 0;
        while ((mask & 1) == 1)
        {
            count++;
            mask >>= 1;
        }

        return count;
    }
}
