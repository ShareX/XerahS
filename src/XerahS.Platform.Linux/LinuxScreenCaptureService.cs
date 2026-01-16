#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

using XerahS.Common;
using XerahS.Platform.Abstractions;
using SkiaSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XerahS.Platform.Linux
{
    /// <summary>
    /// Linux screen capture service with multiple fallback methods.
    /// Supports gnome-screenshot, spectacle (KDE), scrot, and import (ImageMagick).
    /// </summary>
    public class LinuxScreenCaptureService : IScreenCaptureService
    {
        /// <summary>
        /// Check if running on Wayland (where X11 APIs don't work)
        /// </summary>
        public static bool IsWayland =>
            Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")?.Equals("wayland", StringComparison.OrdinalIgnoreCase) == true;

        public Task<SKRectI> SelectRegionAsync(CaptureOptions? options = null)
        {
            // This method should only be called from the UI layer wrapper
            return Task.FromResult(SKRectI.Empty);
        }

        public async Task<SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null)
        {
            // Fullscreen capture - region selection is handled at UI level
            return await CaptureFullScreenAsync(options);
        }

        public async Task<SKBitmap?> CaptureRectAsync(SKRect rect, CaptureOptions? options = null)
        {
            // Capture full screen and crop
            var fullBitmap = await CaptureFullScreenAsync();
            if (fullBitmap == null) return null;

            try
            {
                var cropRect = new SKRectI(
                    (int)rect.Left,
                    (int)rect.Top,
                    (int)rect.Right,
                    (int)rect.Bottom
                );

                // Clamp to image bounds
                cropRect.Left = Math.Max(0, cropRect.Left);
                cropRect.Top = Math.Max(0, cropRect.Top);
                cropRect.Right = Math.Min(fullBitmap.Width, cropRect.Right);
                cropRect.Bottom = Math.Min(fullBitmap.Height, cropRect.Bottom);

                if (cropRect.Width <= 0 || cropRect.Height <= 0)
                {
                    fullBitmap.Dispose();
                    return null;
                }

                var cropped = new SKBitmap(cropRect.Width, cropRect.Height);
                using var canvas = new SKCanvas(cropped);
                canvas.DrawBitmap(fullBitmap, cropRect, new SKRect(0, 0, cropRect.Width, cropRect.Height));
                fullBitmap.Dispose();
                return cropped;
            }
            catch
            {
                fullBitmap?.Dispose();
                return null;
            }
        }

        public async Task<SKBitmap?> CaptureFullScreenAsync(CaptureOptions? options = null)
        {
            DebugHelper.WriteLine("LinuxScreenCaptureService: Attempting screenshot capture...");

            var nativeCapture = await CaptureWithX11Async();
            if (nativeCapture != null)
            {
                return nativeCapture;
            }

            // Try multiple screenshot tools in order of preference
            var result = await CaptureWithGnomeScreenshotAsync();
            if (result != null) return result;

            result = await CaptureWithSpectacleAsync();
            if (result != null) return result;

            result = await CaptureWithScrotAsync();
            if (result != null) return result;

            result = await CaptureWithImportAsync();
            if (result != null) return result;

            DebugHelper.WriteLine("LinuxScreenCaptureService: All capture methods failed");
            return null;
        }

        private async Task<SKBitmap?> CaptureWithX11Async()
        {
            if (IsWayland)
            {
                return null;
            }

            return await Task.Run(() => CaptureWithX11());
        }

        private SKBitmap? CaptureWithX11()
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

        public async Task<SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService, CaptureOptions? options = null)
        {
            // Most Linux tools support window capture with --window flag
            // For now, fall back to fullscreen
            return await CaptureFullScreenAsync();
        }

        public async Task<SKBitmap?> CaptureWindowAsync(IntPtr windowHandle, IWindowService windowService, CaptureOptions? options = null)
        {
            // TODO: Implement Linux window capture by handle
            // For now, get bounds and capture rect
            if (windowHandle == IntPtr.Zero) return null;
            var bounds = windowService.GetWindowBounds(windowHandle);
            if (bounds.Width <= 0 || bounds.Height <= 0) return null;
            return await CaptureRectAsync(new SKRect(bounds.X, bounds.Y, bounds.X + bounds.Width, bounds.Y + bounds.Height), options);
        }

        /// <summary>
        /// Capture using gnome-screenshot (GNOME desktop)
        /// </summary>
        private async Task<SKBitmap?> CaptureWithGnomeScreenshotAsync()
        {
            return await CaptureWithToolAsync("gnome-screenshot", "-f");
        }

        /// <summary>
        /// Capture using spectacle (KDE desktop)
        /// </summary>
        private async Task<SKBitmap?> CaptureWithSpectacleAsync()
        {
            return await CaptureWithToolAsync("spectacle", "-b -n -o");
        }

        /// <summary>
        /// Capture using scrot (lightweight, widely available)
        /// </summary>
        private async Task<SKBitmap?> CaptureWithScrotAsync()
        {
            return await CaptureWithToolAsync("scrot", "");
        }

        /// <summary>
        /// Capture using import from ImageMagick (fallback)
        /// </summary>
        private async Task<SKBitmap?> CaptureWithImportAsync()
        {
            return await CaptureWithToolAsync("import", "-window root");
        }

        /// <summary>
        /// Generic helper to run a screenshot tool and load the result
        /// </summary>
        private async Task<SKBitmap?> CaptureWithToolAsync(string toolName, string argsPrefix)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");

            try
            {
                var arguments = string.IsNullOrEmpty(argsPrefix)
                    ? $"\"{tempFile}\""
                    : $"{argsPrefix} \"{tempFile}\"";

                var startInfo = new ProcessStartInfo
                {
                    FileName = toolName,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return null;

                // Wait up to 10 seconds for the screenshot
                var completed = await Task.Run(() => process.WaitForExit(10000));
                if (!completed)
                {
                    try { process.Kill(); } catch { }
                    return null;
                }

                if (process.ExitCode != 0 || !File.Exists(tempFile))
                {
                    return null;
                }

                DebugHelper.WriteLine($"LinuxScreenCaptureService: Screenshot captured with {toolName}");

                using var stream = File.OpenRead(tempFile);
                return SKBitmap.Decode(stream);
            }
            catch
            {
                // Tool not available or failed - silently continue to next fallback
                return null;
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }
    }
}
