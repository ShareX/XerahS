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
using XerahS.Platform.Linux.Capture.Cli;
using XerahS.Platform.Linux.Capture.Contracts;
using XerahS.Platform.Linux.Capture.Detection;
using XerahS.Platform.Linux.Capture.Orchestration;
using XerahS.Platform.Linux.Capture.Providers;
using XerahS.Platform.Linux.Capture.Gnome;
using XerahS.Platform.Linux.Capture.Kde;
using XerahS.Platform.Linux.Capture.OmaSnap;
using XerahS.Platform.Linux.Capture.Portal;
using XerahS.Platform.Linux.Capture.Wayland;
using XerahS.Platform.Linux.Capture.X11;
using SkiaSharp;

namespace XerahS.Platform.Linux
{
    /// <summary>
    /// Linux screen capture service with multiple fallback methods.
    /// Supports gnome-screenshot, spectacle (KDE), scrot, and import (ImageMagick).
    /// </summary>
    public class LinuxScreenCaptureService : IScreenCaptureService, ILinuxCaptureRuntime, ILinuxRegionCaptureCapabilityProvider, ILinuxRegionSelectorDiagnosticsProvider
    {
        private readonly LinuxCaptureCoordinator _captureCoordinator;
        private readonly Lock _linuxRegionSelectorDecisionLock = new();
        private LinuxRegionSelectorRuntimeDecision? _lastLinuxRegionSelectorDecision;

        public LinuxScreenCaptureService()
            : this(null)
        {
        }

        public LinuxScreenCaptureService(IHostedCaptureEngine? hostedCaptureEngine)
        {
            IHostedCaptureEngine engine = hostedCaptureEngine ??
                PlatformServices.HostedCaptureEngine ??
                new OmaSnapCaptureEngine(Services.LinuxDesktopProfile.Current);

            _captureCoordinator = new LinuxCaptureCoordinator(
                new ILinuxCaptureProvider[]
                {
                    new OmaSnapCaptureStrategy(engine),
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

        public LinuxRegionCaptureCapability GetLinuxRegionCaptureCapability(CaptureOptions? options = null)
        {
            var context = LinuxRuntimeContextDetector.Detect();
            var support = LinuxRegionCaptureCapabilityDetector.ProbeSupportSnapshot(context);
            return LinuxRegionCaptureCapabilityDetector.Detect(context, support);
        }

        public LinuxRegionSelectorDiagnostics? GetLinuxRegionSelectorDiagnostics()
        {
            var diagnostics = LinuxRegionSelectorDiagnosticsDetector.Detect();
            return diagnostics with
            {
                LastDecision = GetLastLinuxRegionSelectorDecision()
            };
        }

        public async Task<SKRectI> SelectRegionAsync(CaptureOptions? options = null)
        {
            var requestedPreference = options?.LinuxRegionSelectorPreference ?? LinuxInteractiveRegionSelectorPreference.Automatic;
            if (IsWayland && WaylandCliCapture.IsSlurpAvailable())
            {
                try
                {
                    var region = await WaylandCliCapture.SelectRegionWithSlurpAsync().ConfigureAwait(false);
                    RecordLinuxRegionSelectorDecision(CreateRuntimeDecision(
                        operation: "Region selection",
                        providerId: "wlroots",
                        requestedPreference: requestedPreference,
                        outcome: region.IsEmpty ? "Cancelled" : "Succeeded"));
                    return region;
                }
                catch (OperationCanceledException)
                {
                    RecordLinuxRegionSelectorDecision(CreateRuntimeDecision(
                        operation: "Region selection",
                        providerId: "wlroots",
                        requestedPreference: requestedPreference,
                        outcome: "Cancelled"));
                    throw;
                }
            }

            if (IsWayland)
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: SelectRegionAsync requested on Wayland but slurp is not available.");
                RecordLinuxRegionSelectorDecision(CreateRuntimeDecision(
                    operation: "Region selection",
                    providerId: "wlroots",
                    requestedPreference: requestedPreference,
                    outcome: "Failed"));
            }

            return SKRectI.Empty;
        }

        uint ILinuxCaptureRuntime.PortalCancelledResponseCode => PortalScreenCapture.PortalResponseCancelled;

        Task<(SKBitmap? bitmap, uint response)> ILinuxCaptureRuntime.TryPortalCaptureAsync(LinuxCaptureKind kind, CaptureOptions? options)
        {
            if (kind == LinuxCaptureKind.FullScreen &&
                ShouldSkipPortalAfterOverlaySelection(options, LinuxRuntimeContextDetector.Detect()))
            {
                DebugHelper.WriteLine(
                    "LinuxScreenCaptureService: skipping portal full-screen capture because the XerahS overlay follow-up on GNOME Wayland must not reopen portal UI after region selection.");
                return Task.FromResult<(SKBitmap? bitmap, uint response)>((null, PortalScreenCapture.PortalResponseFailed));
            }

            bool forceInteractive = kind != LinuxCaptureKind.FullScreen || ShouldForceInteractivePortalFullScreen(options);
            // Do not enable allowInteractiveFallback: on GNOME, the portal can emit response=2 while the
            // capture path is still acceptable; a silent-then-interactive retry then prompts twice or
            // misreports failure. Prefer a single interactive request when needed (e.g. overlay path).
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

        Task<SKBitmap?> ILinuxCaptureRuntime.TryCliCaptureAsync(
            LinuxCaptureKind kind,
            string? desktop,
            IWindowService? windowService,
            CaptureOptions? options)
        {
            return CliCaptureExecutor.TryCaptureAsync(kind, desktop, windowService, IsWayland);
        }

        public async Task<SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null)
        {
            var context = LinuxRuntimeContextDetector.Detect();
            var preference = options?.LinuxRegionSelectorPreference ?? LinuxInteractiveRegionSelectorPreference.Automatic;
            DebugHelper.WriteLine($"LinuxScreenCaptureService: Region capture requested with selector preference '{preference}'.");
            var request = new LinuxCaptureRequest(LinuxCaptureKind.Region, options);
            var execution = await _captureCoordinator.CaptureWithTraceAsync(request, context).ConfigureAwait(false);
            var result = execution.Result;
            LogCaptureDecisionTrace("Region", execution.Trace);
            if (result.IsCancelled)
            {
                RecordLinuxRegionSelectorDecision(CreateRuntimeDecision(
                    operation: "Region capture",
                    providerId: execution.Trace.FinalProviderId ?? result.ProviderId,
                    requestedPreference: preference,
                    outcome: "Cancelled"));
                DebugHelper.WriteLine($"LinuxScreenCaptureService: Region capture cancelled by provider '{result.ProviderId}'.");
                throw new OperationCanceledException($"Region capture cancelled by provider '{result.ProviderId}'.");
            }

            if (result.Bitmap != null)
            {
                RecordLinuxRegionSelectorDecision(CreateRuntimeDecision(
                    operation: "Region capture",
                    providerId: execution.Trace.FinalProviderId ?? result.ProviderId,
                    requestedPreference: preference,
                    outcome: "Succeeded"));
                DebugHelper.WriteLine($"LinuxScreenCaptureService: Region capture succeeded with provider '{result.ProviderId}'.");
            }
            else
            {
                RecordLinuxRegionSelectorDecision(CreateRuntimeDecision(
                    operation: "Region capture",
                    providerId: execution.Trace.FinalProviderId ?? result.ProviderId,
                    requestedPreference: preference,
                    outcome: "Failed"));
            }

            return result.Bitmap;
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
            DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureRectAsync: Input rect (from UI): Left={rect.Left}, Top={rect.Top}, Right={rect.Right}, Bottom={rect.Bottom}, Size={rect.Right - rect.Left:F0}x{rect.Bottom - rect.Top:F0}");

            var context = LinuxRuntimeContextDetector.Detect();
            var fullScreenFallbackOptions = options;
            if (ShouldUseDirectGnomeAreaCapture(options, context))
            {
                var areaRect = CreateDirectAreaCaptureRect(rect);
                DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureRectAsync: Using GNOME direct area capture for logical rect L={areaRect.Left}, T={areaRect.Top}, R={areaRect.Right}, B={areaRect.Bottom}, Size={areaRect.Width}x{areaRect.Height}");

                if (areaRect.Width > 0 && areaRect.Height > 0)
                {
                    var directAreaBitmap = await GnomeDbusScreenCapture.CaptureAreaAsync(areaRect).ConfigureAwait(false);
                    if (directAreaBitmap != null)
                    {
                        DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureRectAsync: GNOME direct area capture succeeded: {directAreaBitmap.Width}x{directAreaBitmap.Height}");
                        return directAreaBitmap;
                    }

                    DebugHelper.WriteLine("LinuxScreenCaptureService: CaptureRectAsync: GNOME direct area capture returned null; falling back to full-screen crop path.");
                    fullScreenFallbackOptions = CreateFullScreenFallbackOptionsAfterDirectAreaFailure(options, context);
                }
            }

            // Capture full screen with the same options and crop.
            var fullBitmap = await CaptureFullScreenAsync(fullScreenFallbackOptions);
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

                // Fast path: portal returned a physical-resolution bitmap (e.g. KDE Plasma portal).
                // Detect by comparing bitmap size to PhysicalVirtualScreenBoundsForCrop (within 2% tolerance).
                // If matched, use PhysicalRectForCrop directly — no per-monitor scale needed.
                bool usedPhysicalPath = false;
                var physicalVirtualBounds = options?.PhysicalVirtualScreenBoundsForCrop;
                var physicalRect = options?.PhysicalRectForCrop;
                if (physicalVirtualBounds.HasValue && physicalRect.HasValue)
                {
                    var pv = physicalVirtualBounds.Value;
                    const double tolerance = 0.02;
                    bool widthMatch = Math.Abs(fullBitmap.Width - pv.Width) <= Math.Max(1, pv.Width * tolerance);
                    bool heightMatch = Math.Abs(fullBitmap.Height - pv.Height) <= Math.Max(1, pv.Height * tolerance);
                    DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureRectAsync: Physical bitmap check: bitmap={fullBitmap.Width}x{fullBitmap.Height}, physVirtual={pv.Width}x{pv.Height}, widthMatch={widthMatch}, heightMatch={heightMatch}");
                    if (widthMatch && heightMatch)
                    {
                        var pr = physicalRect.Value;
                        cropRect.Left = pr.Left - pv.Left;
                        cropRect.Top = pr.Top - pv.Top;
                        cropRect.Right = pr.Right - pv.Left;
                        cropRect.Bottom = pr.Bottom - pv.Top;
                        DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureRectAsync: Physical path: crop at physical offset ({pv.Left},{pv.Top}): L={cropRect.Left}, T={cropRect.Top}, R={cropRect.Right}, B={cropRect.Bottom}");
                        usedPhysicalPath = true;
                    }
                }

                // When portal/capture returns a different size than app virtual screen (e.g. 2560x2790 vs physical layout),
                // transform the selection rect from virtual screen coordinates to capture bitmap coordinates.
                var virtualBounds = options?.VirtualScreenBoundsForCrop;
                if (!usedPhysicalPath && virtualBounds.HasValue)
                {
                    var v = virtualBounds.Value;
                    DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureRectAsync: VirtualScreenBoundsForCrop: X={v.X}, Y={v.Y}, Width={v.Width}, Height={v.Height}");
                    if (v.Width > 0 && v.Height > 0 &&
                        (v.Width != fullBitmap.Width || v.Height != fullBitmap.Height))
                    {
                        double scaleX = (double)fullBitmap.Width / v.Width;
                        double scaleY = (double)fullBitmap.Height / v.Height;
                        DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureRectAsync: Applying mapping: scaleX={scaleX:F4}, scaleY={scaleY:F4} (capture size {fullBitmap.Width}x{fullBitmap.Height} vs virtual {v.Width}x{v.Height})");
                        cropRect.Left = (int)Math.Round((rect.Left - v.X) * scaleX);
                        cropRect.Top = (int)Math.Round((rect.Top - v.Y) * scaleY);
                        cropRect.Right = (int)Math.Round((rect.Right - v.X) * scaleX);
                        cropRect.Bottom = (int)Math.Round((rect.Bottom - v.Y) * scaleY);
                        DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureRectAsync: Mapped crop rect: L={cropRect.Left}, T={cropRect.Top}, R={cropRect.Right}, B={cropRect.Bottom}, Size={cropRect.Width}x{cropRect.Height}");
                    }
                    else
                    {
                        DebugHelper.WriteLine("LinuxScreenCaptureService: CaptureRectAsync: Virtual size matches capture; using rect as direct crop (no scale).");
                    }
                }
                else if (!usedPhysicalPath)
                {
                    DebugHelper.WriteLine("LinuxScreenCaptureService: CaptureRectAsync: VirtualScreenBoundsForCrop not set; using rect as direct crop.");
                }

                DebugHelper.WriteLine($"LinuxScreenCaptureService: CaptureRectAsync: Crop rect before clamp: L={cropRect.Left}, T={cropRect.Top}, R={cropRect.Right}, B={cropRect.Bottom}");

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
                canvas.DrawBitmap(fullBitmap, cropRect, new SKRect(0, 0, cropRect.Width, cropRect.Height), SKSamplingOptions.Default);
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

        internal static LinuxRegionSelectorRuntimeDecision CreateRuntimeDecision(
            string operation,
            string providerId,
            LinuxInteractiveRegionSelectorPreference requestedPreference,
            string outcome,
            DateTimeOffset? timestampUtc = null)
        {
            return new LinuxRegionSelectorRuntimeDecision(
                Operation: operation,
                ProviderId: providerId,
                ProviderDisplayName: GetProviderDisplayName(providerId),
                RequestedPreference: requestedPreference,
                EffectivePreference: GetEffectivePreference(providerId, requestedPreference),
                Outcome: outcome,
                TimestampUtc: timestampUtc ?? DateTimeOffset.UtcNow);
        }

        private LinuxRegionSelectorRuntimeDecision? GetLastLinuxRegionSelectorDecision()
        {
            lock (_linuxRegionSelectorDecisionLock)
            {
                return _lastLinuxRegionSelectorDecision;
            }
        }

        private void RecordLinuxRegionSelectorDecision(LinuxRegionSelectorRuntimeDecision decision)
        {
            lock (_linuxRegionSelectorDecisionLock)
            {
                _lastLinuxRegionSelectorDecision = decision;
            }
        }

        private static LinuxInteractiveRegionSelectorPreference GetEffectivePreference(
            string providerId,
            LinuxInteractiveRegionSelectorPreference requestedPreference)
        {
            return providerId switch
            {
                "portal" => LinuxInteractiveRegionSelectorPreference.PortalDialog,
                "kde-dbus" or "gnome-dbus" => LinuxInteractiveRegionSelectorPreference.DesktopNative,
                "wlroots" => LinuxInteractiveRegionSelectorPreference.Slurp,
                "xerahs-overlay" => LinuxInteractiveRegionSelectorPreference.XerahSOverlay,
                OmaSnapCaptureEngine.Id => LinuxInteractiveRegionSelectorPreference.OmaSnap,
                _ => requestedPreference
            };
        }

        private static string GetProviderDisplayName(string providerId)
        {
            return providerId switch
            {
                "portal" => "XDG portal dialog",
                "kde-dbus" => "KDE desktop selector",
                "gnome-dbus" => "GNOME desktop selector",
                "wlroots" => "slurp",
                "xerahs-overlay" => "XerahS overlay crosshair",
                "x11" => "X11 native capture",
                "cli-tools" => "CLI capture tools",
                OmaSnapCaptureEngine.Id => "OmaSnap native Hyprland overlay",
                "none" => "No provider",
                _ => providerId
            };
        }

        internal static bool ShouldUseDirectGnomeAreaCapture(CaptureOptions? options, ILinuxCaptureContext context)
        {
            return context.IsWayland &&
                string.Equals(context.Desktop, "GNOME", StringComparison.Ordinal) &&
                (options?.UseTransparentOverlay == true ||
                 options?.LinuxDisallowPortalAfterOverlaySelection == true);
        }

        internal static bool ShouldSkipPortalAfterOverlaySelection(CaptureOptions? options, ILinuxCaptureContext context)
        {
            return context.IsWayland &&
                options?.LinuxDisallowPortalAfterOverlaySelection == true &&
                string.Equals(context.Desktop, "GNOME", StringComparison.Ordinal);
        }

        internal static SKRectI CreateDirectAreaCaptureRect(SKRect rect)
        {
            return new SKRectI(
                (int)Math.Floor(rect.Left),
                (int)Math.Floor(rect.Top),
                (int)Math.Ceiling(rect.Right),
                (int)Math.Ceiling(rect.Bottom));
        }

        internal static CaptureOptions? CreateFullScreenFallbackOptionsAfterDirectAreaFailure(
            CaptureOptions? options,
            ILinuxCaptureContext context)
        {
            if (!ShouldSkipPortalAfterOverlaySelection(options, context))
            {
                return options;
            }

            DebugHelper.WriteLine(
                "LinuxScreenCaptureService: GNOME direct area capture failed; restoring v0.20.12-style full-screen fallback for the follow-up crop.");

            return new CaptureOptions
            {
                UseModernCapture = options?.UseModernCapture ?? true,
                LinuxRegionSelectorPreference = options?.LinuxRegionSelectorPreference ?? LinuxInteractiveRegionSelectorPreference.Automatic,
                MacOSRegionSelectorPreference = options?.MacOSRegionSelectorPreference ?? MacOSInteractiveRegionSelectorPreference.Automatic,
                MacOSPlayCaptureSound = options?.MacOSPlayCaptureSound ?? true,
                LinuxForceLegacyCapturePath = options?.LinuxForceLegacyCapturePath ?? false,
                LinuxDisallowPortalAfterOverlaySelection = false,
                LinuxSkipHostedCaptureEngine = options?.LinuxSkipHostedCaptureEngine ?? false,
                ShowCursor = options?.ShowCursor ?? true,
                CaptureTransparent = options?.CaptureTransparent ?? false,
                // Match the pre-direct-area fallback path: once the fast transparent-overlay path
                // has already failed, fall back to a normal full-screen capture request/crop flow.
                UseTransparentOverlay = false,
                CaptureShadow = options?.CaptureShadow ?? true,
                CaptureClientArea = options?.CaptureClientArea ?? false,
                WorkflowId = options?.WorkflowId,
                WorkflowCategory = options?.WorkflowCategory,
                CaptureStartDelaySeconds = options?.CaptureStartDelaySeconds ?? 0,
                CaptureStartDelayCancellationToken = options?.CaptureStartDelayCancellationToken ?? default,
                VirtualScreenBoundsForCrop = options?.VirtualScreenBoundsForCrop,
                PhysicalVirtualScreenBoundsForCrop = options?.PhysicalVirtualScreenBoundsForCrop,
                PhysicalRectForCrop = options?.PhysicalRectForCrop
            };
        }

        private static bool ShouldForceInteractivePortalFullScreen(CaptureOptions? options)
        {
            if (!IsWayland || options?.UseTransparentOverlay != true)
            {
                return false;
            }

            string[] desktopHints =
            {
                Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? string.Empty,
                Environment.GetEnvironmentVariable("XDG_SESSION_DESKTOP") ?? string.Empty,
                Environment.GetEnvironmentVariable("DESKTOP_SESSION") ?? string.Empty
            };

            foreach (string hint in desktopHints)
            {
                foreach (string token in hint.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    string normalized = token.ToUpperInvariant();
                    if (normalized.Contains("GNOME", StringComparison.Ordinal) ||
                        normalized.Contains("UBUNTU", StringComparison.Ordinal) ||
                        normalized.Contains("UNITY", StringComparison.Ordinal) ||
                        normalized.Contains("BUDGIE", StringComparison.Ordinal) ||
                        normalized.Contains("PANTHEON", StringComparison.Ordinal))
                    {
                        DebugHelper.WriteLine(
                            "LinuxScreenCaptureService: forcing interactive portal full-screen request for transparent-overlay region capture on GNOME Wayland.");
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
