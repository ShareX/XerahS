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

using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Capture;
using SkiaSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Tmds.DBus;

namespace XerahS.Platform.Linux
{
    /// <summary>
    /// Linux screen capture service with multiple fallback methods.
    /// Supports gnome-screenshot, spectacle (KDE), scrot, and import (ImageMagick).
    /// </summary>
    public class LinuxScreenCaptureService : IScreenCaptureService
    {
        private const uint PortalResponseSuccess = 0;
        private const uint PortalResponseCancelled = 1;
        private const uint PortalResponseFailed = 2;
        private static int _portalDiagnosticsLogged;

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

        /// <summary>
        /// Detect the current desktop environment
        /// </summary>
        private static string? GetCurrentDesktop()
        {
            // XDG_CURRENT_DESKTOP can contain multiple values separated by colon
            var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
            if (!string.IsNullOrEmpty(desktop))
            {
                var desktops = desktop.Split(':');
                foreach (var d in desktops)
                {
                    var normalized = d.Trim().ToUpperInvariant();
                    if (normalized.Contains("GNOME")) return "GNOME";
                    if (normalized.Contains("KDE") || normalized.Contains("PLASMA")) return "KDE";
                    if (normalized.Contains("HYPRLAND")) return "HYPRLAND";
                    if (normalized.Contains("SWAY")) return "SWAY";
                    if (normalized.Contains("XFCE")) return "XFCE";
                    if (normalized.Contains("MATE")) return "MATE";
                    if (normalized.Contains("CINNAMON")) return "CINNAMON";
                    if (normalized.Contains("LXQT")) return "LXQT";
                    if (normalized.Contains("LXDE")) return "LXDE";
                }
            }

            // Fallback: check DESKTOP_SESSION
            var session = Environment.GetEnvironmentVariable("DESKTOP_SESSION");
            if (!string.IsNullOrEmpty(session))
            {
                var normalized = session.ToUpperInvariant();
                if (normalized.Contains("GNOME")) return "GNOME";
                if (normalized.Contains("PLASMA") || normalized.Contains("KDE")) return "KDE";
                if (normalized.Contains("HYPRLAND")) return "HYPRLAND";
                if (normalized.Contains("SWAY")) return "SWAY";
            }

            return null;
        }

        public async Task<SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null)
        {
            DebugHelper.WriteLine("LinuxScreenCaptureService: CaptureRegionAsync - using interactive region selection");

            SKBitmap? result;
            var currentDesktop = GetCurrentDesktop();
            DebugHelper.WriteLine($"LinuxScreenCaptureService: Detected desktop environment: {currentDesktop ?? "unknown"}, Wayland: {IsWayland}");

            if (IsWayland)
            {
                // On Wayland, try XDG Portal first - it's the standard cross-DE method
                DebugHelper.WriteLine("LinuxScreenCaptureService: Trying XDG Portal for interactive region capture");
                var (portalResult, portalResponse) = await CaptureWithPortalDetailedAsync(forceInteractive: true).ConfigureAwait(false);
                result = portalResult;
                if (result != null)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Region captured with XDG Portal");
                    return result;
                }

                if (portalResponse == PortalResponseCancelled)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: XDG Portal region capture cancelled by user. Skipping tool fallback.");
                    return null;
                }
            }

            // Try DE-native tools first based on detected desktop
            result = await TryCaptureWithDesktopNativeToolAsync(currentDesktop);
            if (result != null)
            {
                return result;
            }

            // On Wayland (especially wlroots-based like Hyprland, Sway), try grim+slurp
            if (IsWayland)
            {
                // grim + slurp (works on all wlroots compositors: Hyprland, Sway, River, etc.)
                result = await CaptureWithGrimSlurpAsync();
                if (result != null)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Region captured with grim+slurp");
                    return result;
                }

                // grimblast (Hyprland convenience wrapper)
                result = await CaptureWithGrimblastRegionAsync();
                if (result != null)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Region captured with grimblast");
                    return result;
                }

                // hyprshot (alternative Hyprland tool)
                result = await CaptureWithHyprshotRegionAsync();
                if (result != null)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Region captured with hyprshot");
                    return result;
                }
            }

            // Try remaining generic tools that weren't already tried
            result = await TryCaptureWithGenericToolsAsync(currentDesktop);
            if (result != null)
            {
                return result;
            }

            DebugHelper.WriteLine("LinuxScreenCaptureService: No region capture tool available, falling back to fullscreen");
            return await CaptureFullScreenAsync(options);
        }

        /// <summary>
        /// Try the native screenshot tool for the detected desktop environment
        /// </summary>
        private async Task<SKBitmap?> TryCaptureWithDesktopNativeToolAsync(string? desktop)
        {
            SKBitmap? result;

            switch (desktop)
            {
                case "GNOME":
                case "CINNAMON":
                case "MATE":
                    // GNOME and GNOME-based: gnome-screenshot
                    result = await CaptureWithToolInteractiveAsync("gnome-screenshot", "-a -f");
                    if (result != null)
                    {
                        DebugHelper.WriteLine("LinuxScreenCaptureService: Region captured with gnome-screenshot");
                        return result;
                    }
                    break;

                case "KDE":
                case "LXQT":
                    // KDE Plasma: spectacle
                    result = await CaptureWithToolInteractiveAsync("spectacle", "-b -n -r -o");
                    if (result != null)
                    {
                        DebugHelper.WriteLine("LinuxScreenCaptureService: Region captured with spectacle");
                        return result;
                    }
                    break;

                case "XFCE":
                    // XFCE: xfce4-screenshooter
                    result = await CaptureWithToolInteractiveAsync("xfce4-screenshooter", "-r -s");
                    if (result != null)
                    {
                        DebugHelper.WriteLine("LinuxScreenCaptureService: Region captured with xfce4-screenshooter");
                        return result;
                    }
                    break;

                case "HYPRLAND":
                case "SWAY":
                    // wlroots-based: prefer grim+slurp (handled in main method)
                    break;
            }

            return null;
        }

        /// <summary>
        /// Try generic screenshot tools that work across multiple DEs
        /// </summary>
        private async Task<SKBitmap?> TryCaptureWithGenericToolsAsync(string? alreadyTriedDesktop)
        {
            SKBitmap? result;

            // Try tools that weren't already tried based on desktop
            if (alreadyTriedDesktop != "GNOME" && alreadyTriedDesktop != "CINNAMON" && alreadyTriedDesktop != "MATE")
            {
                result = await CaptureWithToolInteractiveAsync("gnome-screenshot", "-a -f");
                if (result != null)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Region captured with gnome-screenshot");
                    return result;
                }
            }

            if (alreadyTriedDesktop != "KDE" && alreadyTriedDesktop != "LXQT")
            {
                result = await CaptureWithToolInteractiveAsync("spectacle", "-b -n -r -o");
                if (result != null)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Region captured with spectacle");
                    return result;
                }
            }

            if (alreadyTriedDesktop != "XFCE")
            {
                result = await CaptureWithToolInteractiveAsync("xfce4-screenshooter", "-r -s");
                if (result != null)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Region captured with xfce4-screenshooter");
                    return result;
                }
            }

            // X11-only tools (won't work on pure Wayland but worth trying)
            if (!IsWayland)
            {
                // scrot -s: select mode (X11 only)
                result = await CaptureWithToolInteractiveAsync("scrot", "-s");
                if (result != null)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Region captured with scrot");
                    return result;
                }

                // import (ImageMagick): interactive selection (X11 only)
                result = await CaptureWithToolInteractiveAsync("import", "");
                if (result != null)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Region captured with import");
                    return result;
                }
            }

            return null;
        }

        public async Task<SKBitmap?> CaptureRectAsync(SKRect rect, CaptureOptions? options = null)
        {
            Console.WriteLine($"CaptureRectAsync: Capturing rect - Left={rect.Left}, Top={rect.Top}, Right={rect.Right}, Bottom={rect.Bottom}");

            // Capture full screen and crop
            var fullBitmap = await CaptureFullScreenAsync();
            if (fullBitmap == null)
            {
                Console.WriteLine("ERROR: CaptureFullScreenAsync returned null");
                return null;
            }

            Console.WriteLine($"Full screen captured: {fullBitmap.Width}x{fullBitmap.Height}");

            try
            {
                var cropRect = new SKRectI(
                    (int)rect.Left,
                    (int)rect.Top,
                    (int)rect.Right,
                    (int)rect.Bottom
                );

                Console.WriteLine($"Initial crop rect: Left={cropRect.Left}, Top={cropRect.Top}, Right={cropRect.Right}, Bottom={cropRect.Bottom}");

                // Clamp to image bounds
                cropRect.Left = Math.Max(0, cropRect.Left);
                cropRect.Top = Math.Max(0, cropRect.Top);
                cropRect.Right = Math.Min(fullBitmap.Width, cropRect.Right);
                cropRect.Bottom = Math.Min(fullBitmap.Height, cropRect.Bottom);

                Console.WriteLine($"Clamped crop rect: Left={cropRect.Left}, Top={cropRect.Top}, Right={cropRect.Right}, Bottom={cropRect.Bottom}");
                Console.WriteLine($"Crop dimensions: Width={cropRect.Width}, Height={cropRect.Height}");

                if (cropRect.Width <= 0 || cropRect.Height <= 0)
                {
                    Console.WriteLine($"ERROR: Invalid crop dimensions (Width={cropRect.Width}, Height={cropRect.Height})");
                    fullBitmap.Dispose();
                    return null;
                }

                var cropped = new SKBitmap(cropRect.Width, cropRect.Height);
                using var canvas = new SKCanvas(cropped);
                canvas.DrawBitmap(fullBitmap, cropRect, new SKRect(0, 0, cropRect.Width, cropRect.Height));
                fullBitmap.Dispose();

                Console.WriteLine($"Successfully cropped bitmap: {cropped.Width}x{cropped.Height}");

                // Sample some pixels to check if the image is blank
                if (cropped.Width > 0 && cropped.Height > 0)
                {
                    int sampleX = Math.Min(10, cropped.Width / 2);
                    int sampleY = Math.Min(10, cropped.Height / 2);
                    var samplePixel = cropped.GetPixel(sampleX, sampleY);
                    Console.WriteLine($"Sample pixel at ({sampleX},{sampleY}): R={samplePixel.Red}, G={samplePixel.Green}, B={samplePixel.Blue}, A={samplePixel.Alpha}");

                    // Check if image appears to be all black or all white
                    bool mightBeBlank = true;
                    int checkPoints = Math.Min(5, Math.Min(cropped.Width, cropped.Height) / 10);
                    for (int i = 0; i < checkPoints; i++)
                    {
                        int x = (i + 1) * cropped.Width / (checkPoints + 1);
                        int y = (i + 1) * cropped.Height / (checkPoints + 1);
                        var pixel = cropped.GetPixel(x, y);
                        if (pixel.Red > 10 || pixel.Green > 10 || pixel.Blue > 10)
                        {
                            mightBeBlank = false;
                            break;
                        }
                    }
                    if (mightBeBlank)
                    {
                        Console.WriteLine("WARNING: Captured image appears to be blank/black!");
                    }
                }

                return cropped;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in CaptureRectAsync: {ex.Message}");
                fullBitmap?.Dispose();
                return null;
            }
        }

        public async Task<SKBitmap?> CaptureFullScreenAsync(CaptureOptions? options = null)
        {
            bool useModern = options?.UseModernCapture ?? true;

            DebugHelper.WriteLine($"LinuxScreenCaptureService: Attempting capture (Modern={useModern})...");

            // If Modern Capture is disabled, force X11 immediately (legacy mode)
            if (!useModern)
            {
                 DebugHelper.WriteLine("LinuxScreenCaptureService: Legacy mode enabled. Forcing X11 capture.");
                 return await CaptureWithX11Async();
            }

            // Modern Mode: Try XDG Portal first on Wayland, then tools, then X11

            // 1. On Wayland, try XDG Portal Screenshot API first (most reliable method)
            if (IsWayland)
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: Wayland detected, trying XDG Portal...");
                var portalResult = await CaptureWithPortalAsync(forceInteractive: false);
                if (portalResult != null)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: XDG Portal capture succeeded.");
                    return portalResult;
                }
                DebugHelper.WriteLine("LinuxScreenCaptureService: XDG Portal capture failed, trying Wayland-native tools...");

                // Try grim (standard Wayland screenshot tool, works with Hyprland, Sway, etc.)
                var grimResult = await CaptureWithGrimAsync();
                if (grimResult != null)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with grim.");
                    return grimResult;
                }
            }

            // 2. Try generic tools (gnome-screenshot, spectacle, etc) which work on both Wayland and X11
            var result = await CaptureWithGnomeScreenshotAsync();
            if (result != null) return result;

            result = await CaptureWithSpectacleAsync();
            if (result != null) return result;

            result = await CaptureWithScrotAsync();
            if (result != null) return result;

            result = await CaptureWithImportAsync();
            if (result != null) return result;

            // 3. Fallback to X11 if generic tools failed
            DebugHelper.WriteLine("LinuxScreenCaptureService: external tools failed. Falling back to X11.");
            return await CaptureWithX11Async();
        }

        private async Task<SKBitmap?> CaptureWithX11Async()
        {
            if (IsWayland)
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: X11 capture skipped (Wayland active).");
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
            Console.WriteLine("=== ACTIVE WINDOW CAPTURE DEBUG START ===");
            Console.WriteLine("LinuxScreenCaptureService: CaptureActiveWindowAsync started");
            DebugHelper.WriteLine("LinuxScreenCaptureService: CaptureActiveWindowAsync started");

            if (IsWayland)
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: Wayland active - using portal interactive capture for active window.");
                var (portalResult, portalResponse) = await CaptureWithPortalDetailedAsync(forceInteractive: true).ConfigureAwait(false);
                if (portalResult != null)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Portal capture successful.");
                    return portalResult;
                }

                if (portalResponse == PortalResponseCancelled)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Portal active-window capture cancelled by user. Skipping tool fallback.");
                    return null;
                }

                DebugHelper.WriteLine("LinuxScreenCaptureService: Portal capture failed, trying generic tools for window capture...");
                
                // Fallback to generic tools
                var result = await CaptureWindowWithGnomeScreenshotAsync();
                if (result != null) return result;

                result = await CaptureWindowWithSpectacleAsync();
                if (result != null) return result;

                result = await CaptureWindowWithScrotAsync();
                if (result != null) return result;
                
                DebugHelper.WriteLine("LinuxScreenCaptureService: All window capture methods failed.");
                return null;
            }

            var handle = windowService.GetForegroundWindow();
            Console.WriteLine($"GetForegroundWindow returned handle: {handle} (0x{handle:X})");
            DebugHelper.WriteLine($"LinuxScreenCaptureService: GetForegroundWindow returned handle: {handle}");

            if (handle == IntPtr.Zero)
            {
                Console.WriteLine("ERROR: No foreground window found, falling back to fullscreen");
                DebugHelper.WriteLine("LinuxScreenCaptureService: No foreground window found, falling back to fullscreen");
                return await CaptureFullScreenAsync(options);
            }

            // Get window details for debugging
            var windowTitle = windowService.GetWindowText(handle);
            var windowClass = windowService.GetWindowClassName(handle);
            var isVisible = windowService.IsWindowVisible(handle);

            Console.WriteLine($"Window Details:");
            Console.WriteLine($"  - Title: '{windowTitle}'");
            Console.WriteLine($"  - Class: '{windowClass}'");
            Console.WriteLine($"  - Visible: {isVisible}");

            Console.WriteLine($"Proceeding to capture window with handle: {handle}");
            return await CaptureWindowAsync(handle, windowService, options);
        }

        public async Task<SKBitmap?> CaptureWindowAsync(IntPtr windowHandle, IWindowService windowService, CaptureOptions? options = null)
        {
            Console.WriteLine($"CaptureWindowAsync: Started for handle {windowHandle} (0x{windowHandle:X})");
            DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureWindowAsync started for handle {windowHandle}");

            if (windowHandle == IntPtr.Zero)
            {
                Console.WriteLine("ERROR: CaptureWindowAsync called with Zero handle");
                DebugHelper.WriteLine("LinuxScreenCaptureService: CaptureWindowAsync called with Zero handle");
                return null;
            }

            var bounds = windowService.GetWindowBounds(windowHandle);
            Console.WriteLine($"GetWindowBounds returned: X={bounds.X}, Y={bounds.Y}, Width={bounds.Width}, Height={bounds.Height}");
            DebugHelper.WriteLine($"LinuxScreenCaptureService: Capturing window {windowHandle} bounds: {bounds} (X={bounds.X}, Y={bounds.Y}, W={bounds.Width}, H={bounds.Height})");

            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                Console.WriteLine($"ERROR: Invalid window bounds (Width={bounds.Width}, Height={bounds.Height})");
                DebugHelper.WriteLine("LinuxScreenCaptureService: Invalid window bounds");
                return null;
            }

            // Capture the specific rectangle
            // Note: X11 window coordinates are relative to the screen
            var rect = new SKRect(bounds.X, bounds.Y, bounds.X + bounds.Width, bounds.Y + bounds.Height);
            Console.WriteLine($"Calculated SKRect: Left={rect.Left}, Top={rect.Top}, Right={rect.Right}, Bottom={rect.Bottom}");
            Console.WriteLine($"SKRect dimensions: Width={rect.Width}, Height={rect.Height}");
            DebugHelper.WriteLine($"LinuxScreenCaptureService: Calling CaptureRectAsync with rect: {rect}");

            var result = await CaptureRectAsync(rect, options);
            Console.WriteLine($"CaptureRectAsync returned bitmap: {(result != null ? $"{result.Width}x{result.Height}" : "NULL")}");
            Console.WriteLine("=== ACTIVE WINDOW CAPTURE DEBUG END ===");

            return result;
        }

        public Task<CursorInfo?> CaptureCursorAsync()
        {
            return Task.FromResult<CursorInfo?>(null);
        }

        private async Task<SKBitmap?> CaptureWindowWithGnomeScreenshotAsync()
        {
            // -w = window, -b = include border
            return await CaptureWithToolAsync("gnome-screenshot", "-w -b");
        }

        private async Task<SKBitmap?> CaptureWindowWithSpectacleAsync()
        {
            // -a = active window, -b = background/decorations, -n = non-notify
            return await CaptureWithToolAsync("spectacle", "-a -b -n -o");
        }

        private async Task<SKBitmap?> CaptureWindowWithScrotAsync()
        {
            // -u = currently focused window, -b = border
            return await CaptureWithToolAsync("scrot", "-u -b");
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

        #region Wayland-native capture tools (grim, slurp, grimblast, hyprshot)

        /// <summary>
        /// Capture region using grimblast (Hyprland's grim+slurp wrapper).
        /// This is the preferred tool for Hyprland users.
        /// </summary>
        private async Task<SKBitmap?> CaptureWithGrimblastRegionAsync()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "grimblast",
                    Arguments = $"save area \"{tempFile}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return null;

                var completed = await Task.Run(() => process.WaitForExit(60000));
                if (!completed)
                {
                    try { process.Kill(); } catch { }
                    return null;
                }

                if (process.ExitCode != 0 || !File.Exists(tempFile))
                {
                    return null;
                }

                DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with grimblast");
                using var stream = File.OpenRead(tempFile);
                return SKBitmap.Decode(stream);
            }
            catch
            {
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

        /// <summary>
        /// Capture region using grim + slurp (standard Wayland screenshot tools).
        /// slurp provides interactive region selection, grim captures the region.
        /// </summary>
        private async Task<SKBitmap?> CaptureWithGrimSlurpAsync()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");

            try
            {
                // First, use slurp to get the region selection
                var slurpStartInfo = new ProcessStartInfo
                {
                    FileName = "slurp",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                string? geometry;
                using (var slurpProcess = Process.Start(slurpStartInfo))
                {
                    if (slurpProcess == null) return null;

                    var completed = await Task.Run(() => slurpProcess.WaitForExit(60000));
                    if (!completed)
                    {
                        try { slurpProcess.Kill(); } catch { }
                        return null;
                    }

                    if (slurpProcess.ExitCode != 0)
                    {
                        return null;
                    }

                    geometry = (await slurpProcess.StandardOutput.ReadToEndAsync()).Trim();
                    if (string.IsNullOrEmpty(geometry))
                    {
                        return null;
                    }
                }

                // Now use grim to capture the selected region
                var grimStartInfo = new ProcessStartInfo
                {
                    FileName = "grim",
                    Arguments = $"-g \"{geometry}\" \"{tempFile}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var grimProcess = Process.Start(grimStartInfo);
                if (grimProcess == null) return null;

                var grimCompleted = await Task.Run(() => grimProcess.WaitForExit(10000));
                if (!grimCompleted)
                {
                    try { grimProcess.Kill(); } catch { }
                    return null;
                }

                if (grimProcess.ExitCode != 0 || !File.Exists(tempFile))
                {
                    return null;
                }

                DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with grim+slurp");
                using var stream = File.OpenRead(tempFile);
                return SKBitmap.Decode(stream);
            }
            catch
            {
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

        /// <summary>
        /// Capture region using hyprshot (alternative Hyprland screenshot tool).
        /// </summary>
        private async Task<SKBitmap?> CaptureWithHyprshotRegionAsync()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "hyprshot",
                    Arguments = $"-m region -o \"{Path.GetDirectoryName(tempFile)}\" -f \"{Path.GetFileName(tempFile)}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return null;

                var completed = await Task.Run(() => process.WaitForExit(60000));
                if (!completed)
                {
                    try { process.Kill(); } catch { }
                    return null;
                }

                if (process.ExitCode != 0 || !File.Exists(tempFile))
                {
                    return null;
                }

                DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with hyprshot");
                using var stream = File.OpenRead(tempFile);
                return SKBitmap.Decode(stream);
            }
            catch
            {
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

        /// <summary>
        /// Capture full screen using grim (standard Wayland screenshot tool).
        /// </summary>
        private async Task<SKBitmap?> CaptureWithGrimAsync()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "grim",
                    Arguments = $"\"{tempFile}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return null;

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

                DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with grim");
                using var stream = File.OpenRead(tempFile);
                return SKBitmap.Decode(stream);
            }
            catch
            {
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

        #endregion

        /// <summary>
        /// Helper for interactive region selection with extended timeout (60 seconds).
        /// Used for tools like gnome-screenshot -a, spectacle -r, scrot -s.
        /// </summary>
        private async Task<SKBitmap?> CaptureWithToolInteractiveAsync(string toolName, string argsPrefix)
        {
            return await CaptureWithToolAsync(toolName, argsPrefix, timeoutMs: 60000);
        }

        /// <summary>
        /// Generic helper to run a screenshot tool and load the result
        /// </summary>
        private async Task<SKBitmap?> CaptureWithToolAsync(string toolName, string argsPrefix, int timeoutMs = 10000)
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

                // Wait for the screenshot (default 10s, interactive 60s)
                var completed = await Task.Run(() => process.WaitForExit(timeoutMs));
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

        #region XDG Portal Screenshot

        private const string PortalBusName = "org.freedesktop.portal.Desktop";
        private static readonly ObjectPath PortalObjectPath = new("/org/freedesktop/portal/desktop");

        /// <summary>
        /// Capture screenshot using XDG Desktop Portal Screenshot API.
        /// This is the standard way to capture on Wayland and works across desktop environments.
        /// </summary>
        private async Task<SKBitmap?> CaptureWithPortalAsync(bool forceInteractive)
        {
            var (bitmap, _) = await CaptureWithPortalDetailedAsync(forceInteractive).ConfigureAwait(false);
            return bitmap;
        }

        private async Task<(SKBitmap? bitmap, uint response)> CaptureWithPortalDetailedAsync(bool forceInteractive)
        {
            try
            {
                LogPortalDiagnosticsOnce();

                using var connection = new Connection(Address.Session);
                await connection.ConnectAsync();

                var portal = connection.CreateProxy<IScreenshotPortal>(PortalBusName, PortalObjectPath);

                var options = new Dictionary<string, object>
                {
                    ["modal"] = false,
                    ["interactive"] = forceInteractive,
                    ["handle_token"] = $"xerahs_{Guid.NewGuid():N}"
                };

                var (bitmap, response) = await TryPortalScreenshotAsync(connection, portal, options).ConfigureAwait(false);
                if (bitmap != null)
                {
                    return (bitmap, PortalResponseSuccess);
                }

                // Response 1 = user cancelled, 2 = error. Retry interactively only on error.
                if (!forceInteractive && response == PortalResponseFailed)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Portal non-interactive capture failed; retrying interactive.");
                    options["interactive"] = true;
                    options["modal"] = true;
                    var (interactiveBitmap, interactiveResponse) = await TryPortalScreenshotAsync(connection, portal, options).ConfigureAwait(false);
                    if (interactiveBitmap != null)
                    {
                        return (interactiveBitmap, PortalResponseSuccess);
                    }

                    response = interactiveResponse;
                }

                DebugHelper.WriteLine($"LinuxScreenCaptureService: Portal screenshot cancelled or failed (response={response})");
                return (null, response);
            }
            catch (DBusException ex)
            {
                DebugHelper.WriteException(ex, "LinuxScreenCaptureService: Portal D-Bus error");
                return (null, PortalResponseFailed);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "LinuxScreenCaptureService: Portal capture failed");
                return (null, PortalResponseFailed);
            }
        }

        private static async Task<(SKBitmap? bitmap, uint response)> TryPortalScreenshotAsync(Connection connection, IScreenshotPortal portal, IDictionary<string, object> options)
        {
            var requestStartUtc = DateTime.UtcNow;
            using var monitor = PortalBusMonitor.TryStart("LinuxScreenCaptureService");
            var requestPath = await portal.ScreenshotAsync(string.Empty, options).ConfigureAwait(false);
            var request = connection.CreateProxy<IPortalRequest>(PortalBusName, requestPath);
            var (response, results) = await request.WaitForResponseAsync().ConfigureAwait(false);
            string? uriStr = null;

            // Start checking for results
            if (results != null)
            {
                if (results.TryGetResult("uri", out uriStr) && !string.IsNullOrWhiteSpace(uriStr))
                {
                    var previewUri = new Uri(uriStr);
                    if (previewUri.IsFile && !string.IsNullOrEmpty(previewUri.LocalPath) && File.Exists(previewUri.LocalPath))
                    {
                        using var previewStream = File.OpenRead(previewUri.LocalPath);
                        var previewBitmap = SKBitmap.Decode(previewStream);
                        try { File.Delete(previewUri.LocalPath); } catch { }
                        return (previewBitmap, 0); // Return success 0 since we got the file
                    }
                }
            }

            if (response != 0)
            {
                var fallbackBitmap = await PortalScreenshotFallback
                    .TryFindScreenshotAsync(requestStartUtc, TimeSpan.FromSeconds(2), "LinuxScreenCaptureService")
                    .ConfigureAwait(false);
                if (fallbackBitmap != null)
                {
                    return (fallbackBitmap, 0);
                }

                LogPortalEnvironment();
                DebugHelper.WriteLine("LinuxScreenCaptureService: Portal request options:");
                DebugHelper.WriteLine($"  - interactive: {(options.TryGetValue("interactive", out var interactive) ? interactive : "unset")}");
                DebugHelper.WriteLine($"  - modal: {(options.TryGetValue("modal", out var modal) ? modal : "unset")}");
                DebugHelper.WriteLine($"  - handle_token: {(options.TryGetValue("handle_token", out var token) ? token : "unset")}");
                DebugHelper.WriteLine($"LinuxScreenCaptureService: Portal request failed with response {response}");
                if (results != null)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Portal response results:");
                    foreach (var kvp in results)
                    {
                        var valueStr = "null";
                        try
                        {
                            if (kvp.Value != null)
                            {
                                // Handle potential Tmds.DBus.Protocol.Variant wrapping
                                var unwrapped = UnwrapVariant(kvp.Value);
                                valueStr = unwrapped?.ToString() ?? "null";
                            }
                        }
                        catch (Exception ex)
                        {
                            valueStr = $"Error reading value: {ex.Message}";
                        }
                        DebugHelper.WriteLine($"  - {kvp.Key}: {valueStr}");
                    }
                }
                return (null, response);
            }

            if (results == null || !results.TryGetResult("uri", out uriStr) || string.IsNullOrWhiteSpace(uriStr))
            {
                var fallbackBitmap = await PortalScreenshotFallback
                    .TryFindScreenshotAsync(requestStartUtc, TimeSpan.FromSeconds(2), "LinuxScreenCaptureService")
                    .ConfigureAwait(false);
                if (fallbackBitmap != null)
                {
                    return (fallbackBitmap, 0);
                }

                DebugHelper.WriteLine("LinuxScreenCaptureService: Portal screenshot missing URI in response");
                return (null, response);
            }

            var uri = new Uri(uriStr);
            if (!uri.IsFile || string.IsNullOrEmpty(uri.LocalPath) || !File.Exists(uri.LocalPath))
            {
                DebugHelper.WriteLine($"LinuxScreenCaptureService: Portal screenshot file not found: {uriStr}");
                return (null, response);
            }

            using var stream = File.OpenRead(uri.LocalPath);
            var bitmap = SKBitmap.Decode(stream);

            try { File.Delete(uri.LocalPath); } catch { }

            return (bitmap, response);
        }

        private static void LogPortalEnvironment()
        {
            DebugHelper.WriteLine("LinuxScreenCaptureService: Portal environment:");
            DebugHelper.WriteLine($"  - XDG_SESSION_TYPE: {Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "unset"}");
            DebugHelper.WriteLine($"  - XDG_CURRENT_DESKTOP: {Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "unset"}");
            DebugHelper.WriteLine($"  - XDG_SESSION_DESKTOP: {Environment.GetEnvironmentVariable("XDG_SESSION_DESKTOP") ?? "unset"}");
        }

        private static void LogPortalDiagnosticsOnce()
        {
            if (Interlocked.Exchange(ref _portalDiagnosticsLogged, 1) != 0)
            {
                return;
            }

            var runningBackends = GetRunningPortalBackends();
            var routingHint = GetPortalRoutingHint();
            var portalsConfigSummary = GetPortalsConfigSummary();

            DebugHelper.WriteLine("LinuxScreenCaptureService: XDG portal backend diagnostics:");
            DebugHelper.WriteLine($"  - Running backends: {runningBackends}");
            DebugHelper.WriteLine($"  - Routing hint (from desktop session): {routingHint}");
            DebugHelper.WriteLine($"  - portals.conf: {portalsConfigSummary}");
            DebugHelper.WriteLine("  - Note: Portal UI is provided by the selected backend and can differ across desktop environments.");
        }

        private static string GetRunningPortalBackends()
        {
            var running = new List<string>();

            TryAddRunningBackend(running, "xdg-desktop-portal-kde", "kde");
            TryAddRunningBackend(running, "xdg-desktop-portal-gnome", "gnome");
            TryAddRunningBackend(running, "xdg-desktop-portal-gtk", "gtk");
            TryAddRunningBackend(running, "xdg-desktop-portal-wlr", "wlr");
            TryAddRunningBackend(running, "xdg-desktop-portal-hyprland", "hyprland");
            TryAddRunningBackend(running, "xdg-desktop-portal-lxqt", "lxqt");

            return running.Count > 0 ? string.Join(", ", running) : "none detected";
        }

        private static void TryAddRunningBackend(List<string> running, string processName, string label)
        {
            try
            {
                if (Process.GetProcessesByName(processName).Length > 0)
                {
                    running.Add(label);
                }
            }
            catch
            {
                // Best-effort diagnostics only.
            }
        }

        private static string GetPortalRoutingHint()
        {
            var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ??
                          Environment.GetEnvironmentVariable("XDG_SESSION_DESKTOP") ??
                          string.Empty;

            var normalized = desktop.ToUpperInvariant();
            if (normalized.Contains("KDE") || normalized.Contains("PLASMA"))
            {
                return "kde";
            }

            if (normalized.Contains("GNOME"))
            {
                return "gnome";
            }

            if (normalized.Contains("HYPRLAND"))
            {
                return "hyprland/wlr";
            }

            if (normalized.Contains("SWAY"))
            {
                return "wlr";
            }

            return "unknown";
        }

        private static string GetPortalsConfigSummary()
        {
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrWhiteSpace(configHome))
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                configHome = string.IsNullOrWhiteSpace(userProfile) ? null : Path.Combine(userProfile, ".config");
            }

            var userConfigPath = string.IsNullOrWhiteSpace(configHome)
                ? string.Empty
                : Path.Combine(configHome, "xdg-desktop-portal", "portals.conf");
            var systemConfigPath = "/etc/xdg-desktop-portal/portals.conf";

            var userConfigState = string.IsNullOrWhiteSpace(userConfigPath)
                ? "user=unresolved"
                : $"user={(File.Exists(userConfigPath) ? "present" : "missing")}";
            var systemConfigState = $"system={(File.Exists(systemConfigPath) ? "present" : "missing")}";

            return $"{userConfigState}, {systemConfigState}";
        }

        private static object UnwrapVariant(object value)
        {
            var current = value;
            while (current != null)
            {
                var type = current.GetType();
                var typeName = type.FullName;
                if (typeName != "Tmds.DBus.Protocol.Variant" &&
                    typeName != "Tmds.DBus.Protocol.VariantValue" &&
                    typeName != "Tmds.DBus.Variant")
                {
                    break;
                }

                var valueProp = type.GetProperty("Value");
                var unwrapped = valueProp?.GetValue(current);
                if (unwrapped == null)
                {
                    break;
                }

                current = unwrapped;
            }

            return current ?? value;
        }

        #endregion
    }
}
