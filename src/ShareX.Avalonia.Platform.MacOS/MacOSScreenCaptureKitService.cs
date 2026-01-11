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
using XerahS.Platform.MacOS.Native;
using SkiaSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XerahS.Platform.MacOS
{
    /// <summary>
    /// macOS screen capture implementation using the native ScreenCaptureKit framework.
    /// Requires macOS 12.3+ and libscreencapturekit_bridge.dylib.
    /// Falls back to CLI-based MacOSScreenshotService if native library is unavailable.
    /// </summary>
    public class MacOSScreenCaptureKitService : IScreenCaptureService
    {
        private readonly MacOSScreenshotService _fallbackService;
        private readonly bool _nativeAvailable;

        public MacOSScreenCaptureKitService()
        {
            _fallbackService = new MacOSScreenshotService();
            _nativeAvailable = CheckNativeAvailability();

            if (_nativeAvailable)
            {
                DebugHelper.WriteLine("[ScreenCaptureKit] Native library loaded successfully");
            }
            else
            {
                DebugHelper.WriteLine("[ScreenCaptureKit] Native library not available, using CLI fallback");
            }
        }

        private static bool CheckNativeAvailability()
        {
            try
            {
                return ScreenCaptureKitInterop.TryLoad() && ScreenCaptureKitInterop.IsAvailable() == 1;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "ScreenCaptureKit availability check failed");
                return false;
            }
        }

        public Task<SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null)
        {
            // Region capture requires interactive selection - delegate to CLI
            // ScreenCaptureKit doesn't have built-in interactive selection
            return _fallbackService.CaptureRegionAsync(options);
        }

        public Task<SKBitmap?> CaptureRectAsync(SKRect rect, CaptureOptions? options = null)
        {
            if (!_nativeAvailable)
            {
                return _fallbackService.CaptureRectAsync(rect, options);
            }

            return Task.Run(() => CaptureRectNative(rect));
        }

        public Task<SKBitmap?> CaptureFullScreenAsync(CaptureOptions? options = null)
        {
            if (!_nativeAvailable)
            {
                return _fallbackService.CaptureFullScreenAsync(options);
            }

            return Task.Run(CaptureFullscreenNative);
        }

        public Task<SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService, CaptureOptions? options = null)
        {
            if (!_nativeAvailable)
            {
                return _fallbackService.CaptureActiveWindowAsync(windowService, options);
            }

            // For window capture, we need the window ID from the window service
            // Fallback to CLI for now as getting window ID requires additional interface support
            return _fallbackService.CaptureActiveWindowAsync(windowService, options);
        }

        public Task<SKBitmap?> CaptureWindowAsync(IntPtr windowHandle, IWindowService windowService, CaptureOptions? options = null)
        {
            return _fallbackService.CaptureWindowAsync(windowHandle, windowService, options);
        }

        private SKBitmap? CaptureFullscreenNative()
        {
            var stopwatch = Stopwatch.StartNew();
            IntPtr dataPtr = IntPtr.Zero;

            try
            {
                DebugHelper.WriteLine("[ScreenCaptureKit] Starting fullscreen capture");

                int result = ScreenCaptureKitInterop.CaptureFullscreen(out dataPtr, out int length);

                if (result != ScreenCaptureKitInterop.SUCCESS)
                {
                    DebugHelper.WriteLine($"[ScreenCaptureKit] Capture failed: {ScreenCaptureKitInterop.GetErrorMessage(result)}");

                    // Fall back to CLI if permission denied
                    if (result == ScreenCaptureKitInterop.ERROR_PERMISSION_DENIED)
                    {
                        DebugHelper.WriteLine("[ScreenCaptureKit] Falling back to CLI due to permission issue");
                        return _fallbackService.CaptureFullScreenAsync().GetAwaiter().GetResult();
                    }

                    return null;
                }

                if (dataPtr == IntPtr.Zero || length <= 0)
                {
                    DebugHelper.WriteLine("[ScreenCaptureKit] No data returned from capture");
                    return null;
                }

                var bitmap = DecodePngFromPointer(dataPtr, length);
                stopwatch.Stop();
                DebugHelper.WriteLine($"[ScreenCaptureKit] Fullscreen capture completed in {stopwatch.ElapsedMilliseconds}ms, size={bitmap?.Width}x{bitmap?.Height}");

                return bitmap;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "ScreenCaptureKit fullscreen capture failed");
                return _fallbackService.CaptureFullScreenAsync().GetAwaiter().GetResult();
            }
            finally
            {
                if (dataPtr != IntPtr.Zero)
                {
                    ScreenCaptureKitInterop.FreeBuffer(dataPtr);
                }
            }
        }

        private SKBitmap? CaptureRectNative(SKRect rect)
        {
            var stopwatch = Stopwatch.StartNew();
            IntPtr dataPtr = IntPtr.Zero;

            try
            {
                DebugHelper.WriteLine($"[ScreenCaptureKit] Starting rect capture: {rect.Left},{rect.Top},{rect.Width}x{rect.Height}");

                int result = ScreenCaptureKitInterop.CaptureRect(
                    rect.Left, rect.Top, rect.Width, rect.Height,
                    out dataPtr, out int length);

                if (result != ScreenCaptureKitInterop.SUCCESS)
                {
                    DebugHelper.WriteLine($"[ScreenCaptureKit] Capture failed: {ScreenCaptureKitInterop.GetErrorMessage(result)}");
                    return _fallbackService.CaptureRectAsync(rect).GetAwaiter().GetResult();
                }

                if (dataPtr == IntPtr.Zero || length <= 0)
                {
                    DebugHelper.WriteLine("[ScreenCaptureKit] No data returned from capture");
                    return null;
                }

                var bitmap = DecodePngFromPointer(dataPtr, length);
                stopwatch.Stop();
                DebugHelper.WriteLine($"[ScreenCaptureKit] Rect capture completed in {stopwatch.ElapsedMilliseconds}ms, size={bitmap?.Width}x{bitmap?.Height}");

                return bitmap;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "ScreenCaptureKit rect capture failed");
                return _fallbackService.CaptureRectAsync(rect).GetAwaiter().GetResult();
            }
            finally
            {
                if (dataPtr != IntPtr.Zero)
                {
                    ScreenCaptureKitInterop.FreeBuffer(dataPtr);
                }
            }
        }

        private static SKBitmap? DecodePngFromPointer(IntPtr dataPtr, int length)
        {
            try
            {
                var bytes = new byte[length];
                Marshal.Copy(dataPtr, bytes, 0, length);

                using var stream = new System.IO.MemoryStream(bytes);
                return SKBitmap.Decode(stream);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to decode PNG data");
                return null;
            }
        }
    }
}
