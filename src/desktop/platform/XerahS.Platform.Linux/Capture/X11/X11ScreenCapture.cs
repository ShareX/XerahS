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
                if (ximage.data == IntPtr.Zero || ximage.bits_per_pixel < 24)
                {
                    return null;
                }

                int bytesPerPixel = Math.Max(1, ximage.bits_per_pixel / 8);
                var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);

                int redShift = GetTrailingZeroCount(ximage.red_mask);
                int greenShift = GetTrailingZeroCount(ximage.green_mask);
                int blueShift = GetTrailingZeroCount(ximage.blue_mask);

                int redBits = GetContinuousOnes(ximage.red_mask >> redShift);
                int greenBits = GetContinuousOnes(ximage.green_mask >> greenShift);
                int blueBits = GetContinuousOnes(ximage.blue_mask >> blueShift);

                var stride = ximage.bytes_per_line;
                var baseAddress = ximage.data;
                for (int y = 0; y < height; y++)
                {
                    var rowStart = IntPtr.Add(baseAddress, y * stride);
                    for (int x = 0; x < width; x++)
                    {
                        var pixelPtr = IntPtr.Add(rowStart, x * bytesPerPixel);
                        uint pixelValue = ReadPixel(pixelPtr, bytesPerPixel);

                        byte r = NormalizeChannel((pixelValue & (uint)ximage.red_mask) >> redShift, redBits);
                        byte g = NormalizeChannel((pixelValue & (uint)ximage.green_mask) >> greenShift, greenBits);
                        byte b = NormalizeChannel((pixelValue & (uint)ximage.blue_mask) >> blueShift, blueBits);

                        bitmap.SetPixel(x, y, new SKColor(r, g, b));
                    }
                }

                return bitmap;
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
