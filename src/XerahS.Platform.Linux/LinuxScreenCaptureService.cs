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
using XerahS.Platform.Linux.Services;
using SkiaSharp;
using Microsoft.Win32.SafeHandles;
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
        private const string KdeScreenShotBusName = "org.kde.KWin.ScreenShot2";
        private static readonly ObjectPath KdeScreenShotObjectPath = new("/org/kde/KWin/ScreenShot2");
        private static int _portalDiagnosticsLogged;

        private enum WaterfallCaptureKind
        {
            Region,
            FullScreen,
            ActiveWindow
        }

        private enum KdeCaptureKind
        {
            ActiveWindow,
            Workspace
        }

        /// <summary>
        /// Check if running on Wayland (where X11 APIs don't work)
        /// </summary>
        public static bool IsWayland =>
            Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")?.Equals("wayland", StringComparison.OrdinalIgnoreCase) == true;

        public Task<SKRectI> SelectRegionAsync(CaptureOptions? options = null)
        {
            // Coordinate-only region selection (used by recording).
            // On Wayland we prefer native selector tools; on X11 the UI overlay remains the primary path.
            if (IsWayland)
            {
                if (IsSlurpAvailable())
                {
                    return SelectRegionWithSlurpAsync();
                }

                DebugHelper.WriteLine("LinuxScreenCaptureService: SelectRegionAsync requested on Wayland but slurp is not available.");
            }

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

        private static bool IsSandboxedSession()
        {
            var container = Environment.GetEnvironmentVariable("container");
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLATPAK_ID")) ||
                   !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SNAP")) ||
                   !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPIMAGE")) ||
                   string.Equals(container, "flatpak", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(container, "snap", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldTryPortalStage()
        {
            if (IsWayland || IsSandboxedSession())
            {
                return true;
            }

            return PortalInterfaceChecker.HasInterface("org.freedesktop.portal.Screenshot");
        }

        private static bool IsKdeDesktop(string? desktop)
        {
            return desktop == "KDE" || desktop == "LXQT";
        }

        private static bool IsGnomeDesktop(string? desktop)
        {
            return desktop == "GNOME" || desktop == "MATE" || desktop == "CINNAMON";
        }

        private static bool IsWlrootsDesktop(string? desktop)
        {
            return desktop == "HYPRLAND" || desktop == "SWAY";
        }

        public async Task<SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null)
        {
            DebugHelper.WriteLine("LinuxScreenCaptureService: CaptureRegionAsync - using interactive region selection");

            var currentDesktop = GetCurrentDesktop();
            DebugHelper.WriteLine($"LinuxScreenCaptureService: Detected desktop environment: {currentDesktop ?? "unknown"}, Wayland: {IsWayland}");

            // Stage 1: XDG Portal (modern standard)
            if (ShouldTryPortalStage())
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: [Stage 1/4] Trying XDG Portal for region capture");
                var (portalResult, portalResponse) = await CaptureWithPortalDetailedAsync(forceInteractive: true, allowInteractiveFallback: false).ConfigureAwait(false);
                if (portalResult != null)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Region captured with XDG Portal");
                    return portalResult;
                }

                if (portalResponse == PortalResponseCancelled)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Region capture cancelled by user (portal)");
                    return null;
                }
            }

            // Stage 2: Desktop-specific D-Bus
            var dbusResult = await CaptureWithDesktopDbusFallbackAsync(WaterfallCaptureKind.Region, currentDesktop, options).ConfigureAwait(false);
            if (dbusResult != null)
            {
                return dbusResult;
            }

            // Stage 3: Wayland protocol/tool fallback
            var waylandResult = await CaptureWithWaylandProtocolFallbackAsync(WaterfallCaptureKind.Region, currentDesktop).ConfigureAwait(false);
            if (waylandResult != null)
            {
                return waylandResult;
            }

            // Stage 4: X11/XLib fallback
            if (!IsWayland)
            {
                var x11Result = await TryCaptureWithDesktopNativeToolAsync(currentDesktop);
                if (x11Result != null)
                {
                    return x11Result;
                }

                x11Result = await TryCaptureWithGenericToolsAsync(currentDesktop);
                if (x11Result != null)
                {
                    return x11Result;
                }
            }

            DebugHelper.WriteLine("LinuxScreenCaptureService: Region capture failed after waterfall fallbacks");
            return null;
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

        private async Task<SKBitmap?> CaptureWithDesktopDbusFallbackAsync(WaterfallCaptureKind captureKind, string? desktop, CaptureOptions? options)
        {
            DebugHelper.WriteLine("LinuxScreenCaptureService: [Stage 2/4] Trying desktop-specific D-Bus fallbacks");

            if (IsKdeDesktop(desktop))
            {
                SKBitmap? kdeResult = null;
                if (captureKind == WaterfallCaptureKind.FullScreen)
                {
                    kdeResult = await CaptureWithKdeScreenShot2Async(KdeCaptureKind.Workspace, options).ConfigureAwait(false);
                }
                else if (captureKind == WaterfallCaptureKind.ActiveWindow)
                {
                    kdeResult = await CaptureWithKdeScreenShot2Async(KdeCaptureKind.ActiveWindow, options).ConfigureAwait(false);
                }
                else
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: KDE ScreenShot2 does not provide an interactive free-form region selector.");
                }

                if (kdeResult != null)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Capture succeeded via KDE ScreenShot2 D-Bus");
                    return kdeResult;
                }
            }

            if (IsGnomeDesktop(desktop))
            {
                if (captureKind == WaterfallCaptureKind.FullScreen)
                {
                    var gnomeResult = await CaptureWithGnomeShellDBusAsync().ConfigureAwait(false);
                    if (gnomeResult != null)
                    {
                        DebugHelper.WriteLine("LinuxScreenCaptureService: Capture succeeded via GNOME Shell D-Bus");
                        return gnomeResult;
                    }
                }
                else
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: GNOME Shell D-Bus fallback only supports full-screen capture.");
                }
            }

            return null;
        }

        private async Task<SKBitmap?> CaptureWithWaylandProtocolFallbackAsync(WaterfallCaptureKind captureKind, string? desktop)
        {
            if (!IsWayland)
            {
                return null;
            }

            DebugHelper.WriteLine("LinuxScreenCaptureService: [Stage 3/4] Trying Wayland protocol/tool fallbacks");

            switch (captureKind)
            {
                case WaterfallCaptureKind.Region:
                {
                    if (desktop == "HYPRLAND")
                    {
                        var hyprResult = await CaptureWithGrimblastRegionAsync().ConfigureAwait(false);
                        if (hyprResult != null)
                        {
                            return hyprResult;
                        }

                        hyprResult = await CaptureWithHyprshotRegionAsync().ConfigureAwait(false);
                        if (hyprResult != null)
                        {
                            return hyprResult;
                        }
                    }

                    if (IsWlrootsDesktop(desktop) || desktop == null)
                    {
                        var wlrootsResult = await CaptureWithGrimSlurpAsync().ConfigureAwait(false);
                        if (wlrootsResult != null)
                        {
                            return wlrootsResult;
                        }
                    }

                    return null;
                }
                case WaterfallCaptureKind.FullScreen:
                {
                    return await CaptureWithGrimAsync().ConfigureAwait(false);
                }
                case WaterfallCaptureKind.ActiveWindow:
                {
                    if (desktop == "HYPRLAND")
                    {
                        var hyprWindowResult = await CaptureWithGrimblastActiveWindowAsync().ConfigureAwait(false);
                        if (hyprWindowResult != null)
                        {
                            return hyprWindowResult;
                        }

                        hyprWindowResult = await CaptureWithHyprshotWindowAsync().ConfigureAwait(false);
                        if (hyprWindowResult != null)
                        {
                            return hyprWindowResult;
                        }
                    }

                    if (IsWlrootsDesktop(desktop) || desktop == null)
                    {
                        // wlroots compositors do not expose a universal active-window API.
                        // Interactive region fallback is the closest portable behavior.
                        return await CaptureWithGrimSlurpAsync().ConfigureAwait(false);
                    }

                    return null;
                }
                default:
                    return null;
            }
        }

        public async Task<SKBitmap?> CaptureRectAsync(SKRect rect, CaptureOptions? options = null)
        {
            Console.WriteLine($"CaptureRectAsync: Capturing rect - Left={rect.Left}, Top={rect.Top}, Right={rect.Right}, Bottom={rect.Bottom}");

            // Capture full screen with the same options and crop.
            var fullBitmap = await CaptureFullScreenAsync(options);
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
            var currentDesktop = GetCurrentDesktop();

            DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureFullScreenAsync start (Modern={useModern}, Desktop={currentDesktop ?? "unknown"}, Wayland={IsWayland})");

            // Keep legacy override on X11 when modern capture is disabled.
            if (!useModern && !IsWayland)
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: Legacy mode enabled on X11. Forcing X11 capture.");
                return await CaptureWithX11Async();
            }

            // Stage 1: XDG Portal
            if (useModern && ShouldTryPortalStage())
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: [Stage 1/4] Trying XDG Portal for full-screen capture");
                var (portalResult, portalResponse) = await CaptureWithPortalDetailedAsync(forceInteractive: false, allowInteractiveFallback: false).ConfigureAwait(false);
                if (portalResult != null)
                {
                    return portalResult;
                }

                if (portalResponse == PortalResponseCancelled)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Full-screen capture cancelled by user (portal).");
                    return null;
                }
            }

            // Stage 2: Desktop-specific D-Bus
            var dbusResult = await CaptureWithDesktopDbusFallbackAsync(WaterfallCaptureKind.FullScreen, currentDesktop, options).ConfigureAwait(false);
            if (dbusResult != null)
            {
                return dbusResult;
            }

            // Stage 3: Wayland protocol/tool fallback
            var waylandResult = await CaptureWithWaylandProtocolFallbackAsync(WaterfallCaptureKind.FullScreen, currentDesktop).ConfigureAwait(false);
            if (waylandResult != null)
            {
                return waylandResult;
            }

            // Stage 4: X11/XLib fallback
            var x11Result = await CaptureWithX11Async().ConfigureAwait(false);
            if (x11Result != null)
            {
                return x11Result;
            }

            // Additional legacy X11 tools (best-effort) after direct X11 fallback.
            if (!IsWayland)
            {
                x11Result = await CaptureWithGnomeScreenshotAsync().ConfigureAwait(false);
                if (x11Result != null) return x11Result;

                x11Result = await CaptureWithSpectacleAsync().ConfigureAwait(false);
                if (x11Result != null) return x11Result;

                x11Result = await CaptureWithScrotAsync().ConfigureAwait(false);
                if (x11Result != null) return x11Result;

                x11Result = await CaptureWithImportAsync().ConfigureAwait(false);
                if (x11Result != null) return x11Result;
            }

            DebugHelper.WriteLine("LinuxScreenCaptureService: Full-screen capture failed after waterfall fallbacks.");
            return null;
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
            var currentDesktop = GetCurrentDesktop();

            // Stage 1: XDG Portal
            if (ShouldTryPortalStage())
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: [Stage 1/4] Trying XDG Portal for active window capture");
                var (portalResult, portalResponse) = await CaptureWithPortalDetailedAsync(forceInteractive: true, allowInteractiveFallback: false).ConfigureAwait(false);
                if (portalResult != null)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Active window captured with XDG Portal.");
                    return portalResult;
                }

                if (portalResponse == PortalResponseCancelled)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Active window capture cancelled by user (portal).");
                    return null;
                }
            }

            // Stage 2: Desktop-specific D-Bus
            var dbusResult = await CaptureWithDesktopDbusFallbackAsync(WaterfallCaptureKind.ActiveWindow, currentDesktop, options).ConfigureAwait(false);
            if (dbusResult != null)
            {
                return dbusResult;
            }

            // Stage 3: Wayland protocol/tool fallback
            var waylandResult = await CaptureWithWaylandProtocolFallbackAsync(WaterfallCaptureKind.ActiveWindow, currentDesktop).ConfigureAwait(false);
            if (waylandResult != null)
            {
                return waylandResult;
            }

            // Stage 4: X11/XLib fallback
            if (IsWayland)
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: Active window capture failed after waterfall fallbacks on Wayland.");
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
            var capturedWindow = await CaptureWindowAsync(handle, windowService, options);
            if (capturedWindow != null)
            {
                return capturedWindow;
            }

            var x11ToolResult = await CaptureWindowWithGnomeScreenshotAsync().ConfigureAwait(false);
            if (x11ToolResult != null) return x11ToolResult;

            x11ToolResult = await CaptureWindowWithSpectacleAsync().ConfigureAwait(false);
            if (x11ToolResult != null) return x11ToolResult;

            x11ToolResult = await CaptureWindowWithScrotAsync().ConfigureAwait(false);
            if (x11ToolResult != null) return x11ToolResult;

            DebugHelper.WriteLine("LinuxScreenCaptureService: Active window capture failed after X11 fallback attempts.");
            return null;
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
        /// Get region selection using slurp (Wayland-native region selector).
        /// Returns the selected region without capturing a screenshot.
        /// This is ideal for video recording where we just need coordinates.
        /// </summary>
        /// <returns>Selected region, or empty if cancelled/failed</returns>
        public static async Task<SKRectI> SelectRegionWithSlurpAsync()
        {
            try
            {
                var slurpStartInfo = new ProcessStartInfo
                {
                    FileName = "slurp",
                    Arguments = "-f \"%x %y %w %h\"",  // Output format: x y width height
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var slurpProcess = Process.Start(slurpStartInfo);
                if (slurpProcess == null)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: Failed to start slurp process");
                    return SKRectI.Empty;
                }

                var completed = await Task.Run(() => slurpProcess.WaitForExit(60000));
                if (!completed)
                {
                    try { slurpProcess.Kill(); } catch { }
                    DebugHelper.WriteLine("LinuxScreenCaptureService: slurp timed out");
                    return SKRectI.Empty;
                }

                if (slurpProcess.ExitCode != 0)
                {
                    // Exit code 1 typically means user cancelled (pressed Escape)
                    DebugHelper.WriteLine($"LinuxScreenCaptureService: slurp exited with code {slurpProcess.ExitCode} (likely cancelled)");
                    return SKRectI.Empty;
                }

                string output = (await slurpProcess.StandardOutput.ReadToEndAsync()).Trim();
                DebugHelper.WriteLine($"LinuxScreenCaptureService: slurp output: '{output}'");

                // Parse "x y w h" format
                var parts = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 &&
                    int.TryParse(parts[0], out int x) &&
                    int.TryParse(parts[1], out int y) &&
                    int.TryParse(parts[2], out int w) &&
                    int.TryParse(parts[3], out int h))
                {
                    DebugHelper.WriteLine($"LinuxScreenCaptureService: slurp region selected: x={x}, y={y}, w={w}, h={h}");
                    return new SKRectI(x, y, x + w, y + h);
                }

                DebugHelper.WriteLine($"LinuxScreenCaptureService: Failed to parse slurp output: '{output}'");
                return SKRectI.Empty;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"LinuxScreenCaptureService: slurp exception: {ex.Message}");
                return SKRectI.Empty;
            }
        }

        /// <summary>
        /// Check if slurp is available on the system
        /// </summary>
        public static bool IsSlurpAvailable()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "slurp",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using var process = Process.Start(startInfo);
                process?.WaitForExit(3000);
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }


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
        /// Capture active window using grimblast (Hyprland/Sway wrapper).
        /// </summary>
        private async Task<SKBitmap?> CaptureWithGrimblastActiveWindowAsync()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "grimblast",
                    Arguments = $"save active \"{tempFile}\"",
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

                DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with grimblast (active window)");
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
        /// Capture active window using hyprshot.
        /// </summary>
        private async Task<SKBitmap?> CaptureWithHyprshotWindowAsync()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "hyprshot",
                    Arguments = $"-m window -o \"{Path.GetDirectoryName(tempFile)}\" -f \"{Path.GetFileName(tempFile)}\"",
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

                DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with hyprshot (window)");
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

        private async Task<(SKBitmap? bitmap, uint response)> CaptureWithPortalDetailedAsync(bool forceInteractive, bool allowInteractiveFallback = true)
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
                if (allowInteractiveFallback && !forceInteractive && response == PortalResponseFailed)
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

        private async Task<SKBitmap?> CaptureWithKdeScreenShot2Async(KdeCaptureKind captureKind, CaptureOptions? options)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_kwin_raw_{Guid.NewGuid():N}.bin");

            try
            {
                using var connection = new Connection(Address.Session);
                await connection.ConnectAsync().ConfigureAwait(false);

                var proxy = connection.CreateProxy<IKdeScreenShot2>(KdeScreenShotBusName, KdeScreenShotObjectPath);
                var kdeOptions = BuildKdeScreenShotOptions(captureKind, options);

                IDictionary<string, object> results;
                using (var stream = new FileStream(tempFile, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    results = captureKind switch
                    {
                        KdeCaptureKind.ActiveWindow => await proxy.CaptureActiveWindowAsync(kdeOptions, stream.SafeFileHandle).ConfigureAwait(false),
                        KdeCaptureKind.Workspace => await proxy.CaptureWorkspaceAsync(kdeOptions, stream.SafeFileHandle).ConfigureAwait(false),
                        _ => new Dictionary<string, object>()
                    };
                }

                if (!TryGetStringResult(results, "type", out var type) ||
                    !string.Equals(type, "raw", StringComparison.OrdinalIgnoreCase))
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: KDE ScreenShot2 returned unsupported image type.");
                    return null;
                }

                if (!TryGetUInt32Result(results, "width", out var width) ||
                    !TryGetUInt32Result(results, "height", out var height) ||
                    !TryGetUInt32Result(results, "stride", out var stride) ||
                    !TryGetUInt32Result(results, "format", out var format))
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: KDE ScreenShot2 returned incomplete raw metadata.");
                    return null;
                }

                long expectedBytes = (long)stride * height;
                if (expectedBytes <= 0)
                {
                    return null;
                }

                if (!await WaitForFileLengthAsync(tempFile, expectedBytes, TimeSpan.FromSeconds(3)).ConfigureAwait(false))
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: KDE ScreenShot2 raw output timed out.");
                    return null;
                }

                var rawData = await File.ReadAllBytesAsync(tempFile).ConfigureAwait(false);
                var bitmap = DecodeKdeRawBitmap(rawData, (int)width, (int)height, (int)stride, format);
                if (bitmap != null)
                {
                    DebugHelper.WriteLine($"LinuxScreenCaptureService: KDE ScreenShot2 capture succeeded ({width}x{height}, format={format}).");
                }

                return bitmap;
            }
            catch (DBusException ex)
            {
                if (string.Equals(ex.ErrorName, "org.kde.KWin.ScreenShot2.Error.Cancelled", StringComparison.Ordinal))
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: KDE ScreenShot2 capture cancelled by user.");
                    return null;
                }

                DebugHelper.WriteLine($"LinuxScreenCaptureService: KDE ScreenShot2 D-Bus capture failed: {ex.ErrorName} ({ex.ErrorMessage})");
                return null;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"LinuxScreenCaptureService: KDE ScreenShot2 capture failed: {ex.Message}");
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

        private static IDictionary<string, object> BuildKdeScreenShotOptions(KdeCaptureKind captureKind, CaptureOptions? options)
        {
            var includeCursor = options?.ShowCursor == true;

            var dbusOptions = new Dictionary<string, object>
            {
                ["include-cursor"] = includeCursor,
                ["native-resolution"] = true
            };

            if (captureKind == KdeCaptureKind.ActiveWindow)
            {
                dbusOptions["include-decoration"] = true;
                dbusOptions["include-shadow"] = true;
            }

            return dbusOptions;
        }

        private static async Task<bool> WaitForFileLengthAsync(string path, long minimumLength, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    var info = new FileInfo(path);
                    if (info.Exists && info.Length >= minimumLength)
                    {
                        return true;
                    }
                }
                catch
                {
                    // Best effort wait loop.
                }

                await Task.Delay(25).ConfigureAwait(false);
            }

            return false;
        }

        private static SKBitmap? DecodeKdeRawBitmap(byte[] rawData, int width, int height, int stride, uint format)
        {
            if (width <= 0 || height <= 0 || stride <= 0)
            {
                return null;
            }

            long requiredBytes = (long)stride * height;
            if (rawData.LongLength < requiredBytes)
            {
                return null;
            }

            const uint qImageFormatRgb32 = 4;
            const uint qImageFormatArgb32 = 5;
            const uint qImageFormatArgb32Premultiplied = 6;
            const uint qImageFormatRgbx8888 = 16;
            const uint qImageFormatRgba8888 = 17;
            const uint qImageFormatRgba8888Premultiplied = 18;

            bool isBgraCompatible = format == qImageFormatRgb32 ||
                                    format == qImageFormatArgb32 ||
                                    format == qImageFormatArgb32Premultiplied;
            bool requiresRgbToBgrSwap = format == qImageFormatRgbx8888 ||
                                        format == qImageFormatRgba8888 ||
                                        format == qImageFormatRgba8888Premultiplied;

            if (!isBgraCompatible && !requiresRgbToBgrSwap)
            {
                DebugHelper.WriteLine($"LinuxScreenCaptureService: Unsupported KDE raw image format: {format}");
                return null;
            }

            var alphaType = format switch
            {
                qImageFormatRgb32 => SKAlphaType.Opaque,
                qImageFormatRgbx8888 => SKAlphaType.Opaque,
                qImageFormatArgb32Premultiplied => SKAlphaType.Premul,
                qImageFormatRgba8888Premultiplied => SKAlphaType.Premul,
                _ => SKAlphaType.Unpremul
            };

            var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, alphaType);
            IntPtr bitmapPixels = bitmap.GetPixels();
            if (bitmapPixels == IntPtr.Zero)
            {
                bitmap.Dispose();
                return null;
            }

            int targetStride = bitmap.RowBytes;
            int bytesPerPixel = 4;
            int copyWidthBytes = width * bytesPerPixel;
            var rowBuffer = requiresRgbToBgrSwap ? new byte[copyWidthBytes] : null;

            for (int y = 0; y < height; y++)
            {
                var sourceOffset = y * stride;
                var destinationRow = IntPtr.Add(bitmapPixels, y * targetStride);

                if (isBgraCompatible)
                {
                    Marshal.Copy(rawData, sourceOffset, destinationRow, Math.Min(copyWidthBytes, targetStride));
                    continue;
                }

                Buffer.BlockCopy(rawData, sourceOffset, rowBuffer!, 0, copyWidthBytes);

                for (int x = 0; x < width; x++)
                {
                    int index = x * bytesPerPixel;
                    byte r = rowBuffer![index + 0];
                    byte g = rowBuffer[index + 1];
                    byte b = rowBuffer[index + 2];
                    byte a = rowBuffer[index + 3];

                    rowBuffer[index + 0] = b;
                    rowBuffer[index + 1] = g;
                    rowBuffer[index + 2] = r;
                    rowBuffer[index + 3] = format == qImageFormatRgbx8888 ? (byte)255 : a;
                }

                Marshal.Copy(rowBuffer!, 0, destinationRow, Math.Min(copyWidthBytes, targetStride));
            }

            return bitmap;
        }

        private static bool TryGetUInt32Result(IDictionary<string, object> results, string key, out uint value)
        {
            value = 0;
            if (!results.TryGetValue(key, out var raw) || raw == null)
            {
                return false;
            }

            raw = UnwrapVariant(raw);
            switch (raw)
            {
                case uint uintValue:
                    value = uintValue;
                    return true;
                case int intValue when intValue >= 0:
                    value = (uint)intValue;
                    return true;
                case long longValue when longValue >= 0 && longValue <= uint.MaxValue:
                    value = (uint)longValue;
                    return true;
                case string stringValue when uint.TryParse(stringValue, out var parsed):
                    value = parsed;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryGetStringResult(IDictionary<string, object> results, string key, out string value)
        {
            value = string.Empty;
            if (!results.TryGetValue(key, out var raw) || raw == null)
            {
                return false;
            }

            raw = UnwrapVariant(raw);
            value = raw.ToString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        private async Task<SKBitmap?> CaptureWithGnomeShellDBusAsync()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_gnome_{Guid.NewGuid():N}.png");
            try
            {
                using var connection = new Connection(Address.Session);
                await connection.ConnectAsync();

                var proxy = connection.CreateProxy<IGnomeShellScreenshot>("org.gnome.Shell.Screenshot", "/org/gnome/Shell/Screenshot");
                
                // Screenshot(include_cursor, flash, filename)
                var (success, filenameUsed) = await proxy.ScreenshotAsync(false, false, tempFile);

                if (success && File.Exists(tempFile))
                {
                    DebugHelper.WriteLine($"LinuxScreenCaptureService: GNOME Shell D-Bus capture succeeded: {tempFile}");
                    using var stream = File.OpenRead(tempFile);
                    var bitmap = SKBitmap.Decode(stream);
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"LinuxScreenCaptureService: GNOME Shell D-Bus capture failed: {ex.Message}");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
            return null;
        }

        [DBusInterface("org.kde.KWin.ScreenShot2")]
        public interface IKdeScreenShot2 : IDBusObject
        {
            Task<uint> GetVersionAsync();
            Task<IDictionary<string, object>> CaptureActiveWindowAsync(IDictionary<string, object> options, SafeFileHandle pipe);
            Task<IDictionary<string, object>> CaptureWorkspaceAsync(IDictionary<string, object> options, SafeFileHandle pipe);
        }

        [DBusInterface("org.gnome.Shell.Screenshot")]
        public interface IGnomeShellScreenshot : IDBusObject
        {
            Task<(bool success, string filename)> ScreenshotAsync(bool include_cursor, bool flash, string filename);
        }
    }
}
