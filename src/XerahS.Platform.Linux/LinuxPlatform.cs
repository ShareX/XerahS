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
using XerahS.Platform.Linux.Recording;
using XerahS.Platform.Linux.Services;
using XerahS.RegionCapture.ScreenRecording;
namespace XerahS.Platform.Linux
{
    public static class LinuxPlatform
    {
        public static void Initialize(IScreenCaptureService? screenCaptureService = null, bool useModernCapture = true)
        {
            // Use LinuxScreenCaptureService if none provided
            if (screenCaptureService == null)
            {
                screenCaptureService = new LinuxScreenCaptureService();
                DebugHelper.WriteLine(LinuxScreenCaptureService.IsWayland
                    ? "Linux: Running on Wayland. Using LinuxScreenCaptureService with XDG Portal support."
                    : "Linux: Running on X11. Using LinuxScreenCaptureService with CLI fallbacks.");
            }

            bool isWayland = LinuxScreenCaptureService.IsWayland;

            // When UseModernCapture is disabled, skip all XDG Portal services to avoid
            // EIS connection errors and unnecessary D-Bus connections
            bool usePortalServices = useModernCapture && isWayland;
            if (!useModernCapture && isWayland)
            {
                DebugHelper.WriteLine("Linux: UseModernCapture is disabled. Skipping XDG Portal services (input capture, hotkeys, system). " +
                    "Using fallback services instead. Re-enable and restart to use portal services.");
            }

            bool hasGlobalShortcuts = usePortalServices && PortalInterfaceChecker.HasInterface("org.freedesktop.portal.GlobalShortcuts");
            bool hasInputCapture = usePortalServices && PortalInterfaceChecker.HasInterface("org.freedesktop.portal.InputCapture");
            bool hasOpenUri = usePortalServices && PortalInterfaceChecker.HasInterface("org.freedesktop.portal.OpenURI");

            IHotkeyService hotkeyService = hasGlobalShortcuts
                ? new WaylandPortalHotkeyService()
                : new LinuxHotkeyService();

            IInputService inputService = hasInputCapture
                ? new WaylandPortalInputService()
                : new LinuxInputService();

            ISystemService systemService = hasOpenUri
                ? new WaylandPortalSystemService()
                : new LinuxSystemService();

            PlatformServices.Initialize(
                platformInfo: new LinuxPlatformInfo(),
                screenService: new LinuxScreenService(),
                clipboardService: new LinuxClipboardService(),
                windowService: new LinuxWindowService(),
                screenCaptureService: screenCaptureService,
                hotkeyService: hotkeyService,
                inputService: inputService,
                fontService: new LinuxFontService(),
                startupService: new LinuxStartupService(),
                systemService: systemService,
                notificationService: new LinuxNotificationService(),
                diagnosticService: new Services.LinuxDiagnosticService()
            );

            // Register OCR service stub (Tesseract integration planned)
            PlatformServices.Ocr = new LinuxOcrService();

            // Initialize theme service for dark mode detection
            PlatformServices.Theme = new LinuxThemeService();
            DebugHelper.WriteLine($"Linux: Theme service initialized. Dark mode preferred: {PlatformServices.Theme.IsDarkModePreferred}");
        }

        /// <summary>
        /// Initialize screen recording for Linux using FFmpeg-based recording
        /// Stage 7: Cross-platform recording support
        ///
        /// Note: This uses FFmpegRecordingService as the primary recording method.
        /// Future enhancement: Implement native PipeWire/XDG Portal capture source
        /// with GStreamer or FFmpeg pipe encoder for better performance.
        /// </summary>
        public static void InitializeRecording()
        {
            DebugHelper.WriteLine("LinuxPlatform.InitializeRecording() called");
            try
            {
                bool isWayland = LinuxScreenCaptureService.IsWayland;
                DebugHelper.WriteLine($"LinuxPlatform: isWayland={isWayland}");

                bool hasScreenCastPortal = false;
                if (isWayland)
                {
                    hasScreenCastPortal = PortalInterfaceChecker.HasInterface("org.freedesktop.portal.ScreenCast");
                    DebugHelper.WriteLine($"LinuxPlatform: hasScreenCastPortal={hasScreenCastPortal}");
                }

                if (hasScreenCastPortal)
                {
                    DebugHelper.WriteLine("Linux: ScreenCast portal detected. Using Wayland portal recording.");
                    ScreenRecorderService.NativeRecordingServiceFactory = () => new WaylandPortalRecordingService();
                    DebugHelper.WriteLine($"LinuxPlatform: NativeRecordingServiceFactory set = {ScreenRecorderService.NativeRecordingServiceFactory != null}");
                }
                else
                {
                    DebugHelper.WriteLine("Linux: ScreenCast portal NOT detected.");
                    if (isWayland)
                    {
                        DebugHelper.WriteLine("WARNING: On Wayland without ScreenCast portal, screen recording may not work properly.");
                        DebugHelper.WriteLine("  - Install xdg-desktop-portal with ScreenCast support for your desktop environment");
                        DebugHelper.WriteLine("  - Ensure PipeWire is running");
                        DebugHelper.WriteLine("  - Common portal backends: xdg-desktop-portal-gnome, xdg-desktop-portal-kde, xdg-desktop-portal-wlr, xdg-desktop-portal-hyprland");
                    }
                    else
                    {
                        DebugHelper.WriteLine("Linux X11: Using FFmpeg x11grab backend.");
                    }
                }

                // FFmpeg fallback only works reliably on X11
                // On Wayland without portal, this will likely fail
                ScreenRecorderService.FallbackServiceFactory = () => new FFmpegRecordingService();

                DebugHelper.WriteLine("Linux: Screen recording initialized successfully");
                DebugHelper.WriteLine(hasScreenCastPortal
                    ? "  - Recording backend: XDG ScreenCast Portal (PipeWire) with FFmpeg encoding"
                    : "  - Recording backend: FFmpeg (x11grab/Wayland)");
                DebugHelper.WriteLine("  - Supported modes: Screen, Window, Region");
                DebugHelper.WriteLine("  - Codecs: H.264, HEVC, VP9, AV1 (depends on FFmpeg build)");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to initialize Linux screen recording");
            }
        }
    }

}
