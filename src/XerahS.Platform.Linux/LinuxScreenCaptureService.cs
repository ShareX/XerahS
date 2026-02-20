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
using XerahS.Platform.Linux.Capture.Contracts;
using XerahS.Platform.Linux.Capture.Detection;
using XerahS.Platform.Linux.Capture.Helpers;
using XerahS.Platform.Linux.Capture.Orchestration;
using XerahS.Platform.Linux.Capture.Providers;
using XerahS.Platform.Linux.Capture.Gnome;
using XerahS.Platform.Linux.Capture.Kde;
using XerahS.Platform.Linux.Capture.Portal;
using XerahS.Platform.Linux.Capture.Wayland;
using XerahS.Platform.Linux.Capture.X11;
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
    public class LinuxScreenCaptureService : IScreenCaptureService, ILinuxCaptureRuntime
    {
        private readonly LinuxCaptureCoordinator _captureCoordinator;

        public LinuxScreenCaptureService()
        {
            _captureCoordinator = new LinuxCaptureCoordinator(
                new ILinuxCaptureProvider[]
                {
                    new PortalCaptureProvider(this),
                    new KdeDbusCaptureProvider(this),
                    new GnomeDbusCaptureProvider(this),
                    new WlrootsCaptureProvider(this),
                    new X11CaptureProvider(this),
                    new CliCaptureProvider(this)
                },
                new WaterfallCapturePolicy());
        }

        /// <summary>
        /// Check if running on Wayland (where X11 APIs don't work)
        /// </summary>
        public static bool IsWayland =>
            Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")?.Equals("wayland", StringComparison.OrdinalIgnoreCase) == true;

        public Task<SKRectI> SelectRegionAsync(CaptureOptions? options = null)
        {
            if (IsWayland && WaylandCliCapture.IsSlurpAvailable())
            {
                return WaylandCliCapture.SelectRegionWithSlurpAsync();
            }
            if (IsWayland)
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: SelectRegionAsync requested on Wayland but slurp is not available.");
            }
            return Task.FromResult(SKRectI.Empty);
        }

        uint ILinuxCaptureRuntime.PortalCancelledResponseCode => PortalScreenCapture.PortalResponseCancelled;

        Task<(SKBitmap? bitmap, uint response)> ILinuxCaptureRuntime.TryPortalCaptureAsync(LinuxCaptureKind kind, CaptureOptions? options)
        {
            bool forceInteractive = kind != LinuxCaptureKind.FullScreen;
            return PortalScreenCapture.CaptureAsync(forceInteractive, allowInteractiveFallback: false);
        }

        async Task<SKBitmap?> ILinuxCaptureRuntime.TryKdeDbusCaptureAsync(LinuxCaptureKind kind, CaptureOptions? options)
        {
            DebugHelper.WriteLine("LinuxScreenCaptureService: [Stage 2/4] Trying KDE ScreenShot2 D-Bus fallback");
            return await KdeDbusScreenCapture.CaptureAsync(kind, options).ConfigureAwait(false);
        }

        async Task<SKBitmap?> ILinuxCaptureRuntime.TryGnomeDbusCaptureAsync(LinuxCaptureKind kind, CaptureOptions? options)
        {
            DebugHelper.WriteLine("LinuxScreenCaptureService: [Stage 2/4] Trying GNOME Shell D-Bus fallback");
            return kind switch
            {
                LinuxCaptureKind.Region => await GnomeDbusScreenCapture.CaptureRegionAsync().ConfigureAwait(false),
                LinuxCaptureKind.FullScreen => await GnomeDbusScreenCapture.CaptureFullScreenAsync().ConfigureAwait(false),
                LinuxCaptureKind.ActiveWindow => await GnomeDbusScreenCapture.CaptureWindowAsync(options).ConfigureAwait(false),
                _ => null
            };
        }

        Task<SKBitmap?> ILinuxCaptureRuntime.TryWlrootsCaptureAsync(LinuxCaptureKind kind, string? desktop, CaptureOptions? options)
        {
            return WaylandCliCapture.CaptureAsync(kind, desktop);
        }

        async Task<SKBitmap?> ILinuxCaptureRuntime.TryX11NativeCaptureAsync(
            LinuxCaptureKind kind,
            IWindowService? windowService,
            CaptureOptions? options)
        {
            if (IsWayland)
            {
                return null;
            }

            switch (kind)
            {
                case LinuxCaptureKind.FullScreen:
                    return await CaptureWithX11Async(IsWayland).ConfigureAwait(false);
                case LinuxCaptureKind.ActiveWindow:
                    if (windowService == null)
                    {
                        return null;
                    }

                    var handle = windowService.GetForegroundWindow();
                    if (handle == IntPtr.Zero)
                    {
                        return null;
                    }

                    return await CaptureWindowAsync(handle, windowService, options).ConfigureAwait(false);
                default:
                    return null;
            }
        }

        async Task<SKBitmap?> ILinuxCaptureRuntime.TryCliCaptureAsync(
            LinuxCaptureKind kind,
            string? desktop,
            IWindowService? windowService,
            CaptureOptions? options)
        {
            switch (kind)
            {
                case LinuxCaptureKind.Region:
                {
                    var x11Result = await TryCaptureWithDesktopNativeToolAsync(desktop).ConfigureAwait(false);
                    if (x11Result != null)
                    {
                        return x11Result;
                    }

                    return await TryCaptureWithGenericToolsAsync(desktop).ConfigureAwait(false);
                }
                case LinuxCaptureKind.FullScreen:
                {
                    var x11Result = await CaptureWithGnomeScreenshotAsync().ConfigureAwait(false);
                    if (x11Result != null) return x11Result;

                    x11Result = await CaptureWithSpectacleAsync().ConfigureAwait(false);
                    if (x11Result != null) return x11Result;

                    x11Result = await CaptureWithScrotAsync().ConfigureAwait(false);
                    if (x11Result != null) return x11Result;

                    x11Result = await CaptureWithImportAsync().ConfigureAwait(false);
                    return x11Result;
                }
                case LinuxCaptureKind.ActiveWindow:
                {
                    if (windowService == null)
                    {
                        return null;
                    }

                    var handle = windowService.GetForegroundWindow();

                    var x11Result = await CaptureWindowWithGnomeScreenshotAsync().ConfigureAwait(false);
                    if (x11Result != null) return x11Result;

                    x11Result = await CaptureWindowWithSpectacleAsync().ConfigureAwait(false);
                    if (x11Result != null) return x11Result;

                    x11Result = await CaptureWindowWithScrotAsync().ConfigureAwait(false);
                    if (x11Result != null) return x11Result;

                    if (handle == IntPtr.Zero)
                    {
                        x11Result = await CaptureWithGnomeScreenshotAsync().ConfigureAwait(false);
                        if (x11Result != null) return x11Result;

                        x11Result = await CaptureWithSpectacleAsync().ConfigureAwait(false);
                        if (x11Result != null) return x11Result;

                        x11Result = await CaptureWithScrotAsync().ConfigureAwait(false);
                        if (x11Result != null) return x11Result;

                        return await CaptureWithImportAsync().ConfigureAwait(false);
                    }

                    return null;
                }
                default:
                    return null;
            }
        }

        public async Task<SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null)
        {
            var context = LinuxRuntimeContextDetector.Detect();
            var request = new LinuxCaptureRequest(LinuxCaptureKind.Region, options);
            var execution = await _captureCoordinator.CaptureWithTraceAsync(request, context).ConfigureAwait(false);
            var result = execution.Result;
            LogCaptureDecisionTrace("Region", execution.Trace);
            if (result.IsCancelled)
            {
                DebugHelper.WriteLine($"LinuxScreenCaptureService: Region capture cancelled by provider '{result.ProviderId}'.");
                return null;
            }

            if (result.Bitmap != null)
            {
                DebugHelper.WriteLine($"LinuxScreenCaptureService: Region capture succeeded with provider '{result.ProviderId}'.");
            }

            return result.Bitmap;
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

        private static void LogCaptureDecisionTrace(string captureName, CaptureDecisionTrace trace)
        {
            DebugHelper.WriteLine($"LinuxScreenCaptureService: {captureName} decision trace (final={trace.FinalOutcome}, provider={trace.FinalProviderId ?? "none"})");

            foreach (var step in trace.Steps)
            {
                if (string.IsNullOrWhiteSpace(step.Reason))
                {
                    DebugHelper.WriteLine($"  - stage={step.Stage}, provider={step.ProviderId}, outcome={step.Outcome}");
                    continue;
                }

                DebugHelper.WriteLine($"  - stage={step.Stage}, provider={step.ProviderId}, outcome={step.Outcome}, reason={step.Reason}");
            }
        }

        public async Task<SKBitmap?> CaptureRectAsync(SKRect rect, CaptureOptions? options = null)
        {
            DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureRectAsync: Capturing rect - Left={rect.Left}, Top={rect.Top}, Right={rect.Right}, Bottom={rect.Bottom}");

            // Capture full screen with the same options and crop.
            var fullBitmap = await CaptureFullScreenAsync(options);
            if (fullBitmap == null)
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: CaptureRectAsync: CaptureFullScreenAsync returned null");
                return null;
            }

            DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureRectAsync: Full screen captured: {fullBitmap.Width}x{fullBitmap.Height}");

            try
            {
                var cropRect = new SKRectI(
                    (int)rect.Left,
                    (int)rect.Top,
                    (int)rect.Right,
                    (int)rect.Bottom
                );

                DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureRectAsync: Initial crop rect: Left={cropRect.Left}, Top={cropRect.Top}, Right={cropRect.Right}, Bottom={cropRect.Bottom}");

                // Clamp to image bounds
                cropRect.Left = Math.Max(0, cropRect.Left);
                cropRect.Top = Math.Max(0, cropRect.Top);
                cropRect.Right = Math.Min(fullBitmap.Width, cropRect.Right);
                cropRect.Bottom = Math.Min(fullBitmap.Height, cropRect.Bottom);

                DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureRectAsync: Clamped crop rect: Left={cropRect.Left}, Top={cropRect.Top}, Right={cropRect.Right}, Bottom={cropRect.Bottom}, Width={cropRect.Width}, Height={cropRect.Height}");

                if (cropRect.Width <= 0 || cropRect.Height <= 0)
                {
                    DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureRectAsync: Invalid crop dimensions (Width={cropRect.Width}, Height={cropRect.Height})");
                    fullBitmap.Dispose();
                    return null;
                }

                var cropped = new SKBitmap(cropRect.Width, cropRect.Height);
                using var canvas = new SKCanvas(cropped);
                canvas.DrawBitmap(fullBitmap, cropRect, new SKRect(0, 0, cropRect.Width, cropRect.Height));
                fullBitmap.Dispose();

                DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureRectAsync: Successfully cropped bitmap: {cropped.Width}x{cropped.Height}");

                // Sample some pixels to check if the image is blank
                if (cropped.Width > 0 && cropped.Height > 0)
                {
                    int sampleX = Math.Min(10, cropped.Width / 2);
                    int sampleY = Math.Min(10, cropped.Height / 2);
                    var samplePixel = cropped.GetPixel(sampleX, sampleY);
                    DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureRectAsync: Sample pixel at ({sampleX},{sampleY}): R={samplePixel.Red}, G={samplePixel.Green}, B={samplePixel.Blue}, A={samplePixel.Alpha}");

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
                        DebugHelper.WriteLine("LinuxScreenCaptureService: CaptureRectAsync: WARNING: Captured image appears to be blank/black!");
                    }
                }

                return cropped;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureRectAsync: Exception: {ex.Message}");
                fullBitmap?.Dispose();
                return null;
            }
        }

        public async Task<SKBitmap?> CaptureFullScreenAsync(CaptureOptions? options = null)
        {
            var context = LinuxRuntimeContextDetector.Detect();
            var request = new LinuxCaptureRequest(LinuxCaptureKind.FullScreen, options);
            var execution = await _captureCoordinator.CaptureWithTraceAsync(request, context).ConfigureAwait(false);
            var result = execution.Result;
            LogCaptureDecisionTrace("FullScreen", execution.Trace);
            if (result.IsCancelled)
            {
                DebugHelper.WriteLine($"LinuxScreenCaptureService: Full-screen capture cancelled by provider '{result.ProviderId}'.");
                return null;
            }

            if (result.Bitmap != null)
            {
                DebugHelper.WriteLine($"LinuxScreenCaptureService: Full-screen capture succeeded with provider '{result.ProviderId}'.");
            }

            return result.Bitmap;
        }

        private static Task<SKBitmap?> CaptureWithX11Async(bool isWayland)
        {
            return X11ScreenCapture.CaptureFullScreenAsync(isWayland);
        }

        public async Task<SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService, CaptureOptions? options = null)
        {
            DebugHelper.WriteLine("LinuxScreenCaptureService: CaptureActiveWindowAsync started");
            var context = LinuxRuntimeContextDetector.Detect();
            var request = new LinuxCaptureRequest(LinuxCaptureKind.ActiveWindow, options, windowService);
            var execution = await _captureCoordinator.CaptureWithTraceAsync(request, context).ConfigureAwait(false);
            var result = execution.Result;
            LogCaptureDecisionTrace("ActiveWindow", execution.Trace);
            if (result.IsCancelled)
            {
                DebugHelper.WriteLine($"LinuxScreenCaptureService: Active-window capture cancelled by provider '{result.ProviderId}'.");
                return null;
            }

            if (result.Bitmap != null)
            {
                DebugHelper.WriteLine($"LinuxScreenCaptureService: Active-window capture succeeded with provider '{result.ProviderId}'.");
            }

            return result.Bitmap;
        }

        public async Task<SKBitmap?> CaptureWindowAsync(IntPtr windowHandle, IWindowService windowService, CaptureOptions? options = null)
        {
            DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureWindowAsync started for handle {windowHandle}");

            if (windowHandle == IntPtr.Zero)
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: CaptureWindowAsync called with Zero handle");
                return null;
            }

            var bounds = windowService.GetWindowBounds(windowHandle);
            DebugHelper.WriteLine($"LinuxScreenCaptureService: Capturing window {windowHandle} bounds: {bounds} (X={bounds.X}, Y={bounds.Y}, W={bounds.Width}, H={bounds.Height})");

            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: Invalid window bounds");
                return null;
            }

            // Capture the specific rectangle
            // Note: X11 window coordinates are relative to the screen
            var rect = new SKRect(bounds.X, bounds.Y, bounds.X + bounds.Width, bounds.Y + bounds.Height);
            DebugHelper.WriteLine($"LinuxScreenCaptureService: Calling CaptureRectAsync with rect: {rect}");

            return await CaptureRectAsync(rect, options);
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

        /// <summary>
        /// Helper for interactive region selection with extended timeout (60 seconds).
        /// Used for tools like gnome-screenshot -a, spectacle -r, scrot -s.
        /// </summary>
        private static Task<SKBitmap?> CaptureWithToolInteractiveAsync(string toolName, string argsPrefix)
        {
            return LinuxCliToolRunner.RunAsync(toolName, argsPrefix, LinuxCliToolRunner.InteractiveTimeoutMs);
        }

        /// <summary>
        /// Generic helper to run a screenshot tool and load the result.
        /// </summary>
        private static Task<SKBitmap?> CaptureWithToolAsync(string toolName, string argsPrefix, int timeoutMs = 10000)
        {
            return LinuxCliToolRunner.RunAsync(toolName, argsPrefix, timeoutMs);
        }

    }
}
