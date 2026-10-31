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
        public static void Initialize(IScreenCaptureService? screenCaptureService = null, bool useWaylandPortalServices = true)
        {
            var environment = LinuxRuntimeEnvironment.Detect();
            DebugHelper.WriteLine($"Linux: Runtime environment detected: {environment.ToDiagnosticString()}");

            var clipboardService = new LinuxClipboardService();
            IClipboardMonitorService clipboardMonitorService = environment.IsSandboxed
                ? new UnsupportedClipboardMonitorService()
                : new LinuxClipboardMonitorService();

            // One source of truth for Omarchy/Hyprland/OmaSnap (XIP0088). The OmaSnap probe runs in the
            // background and only on non-sandboxed Hyprland sessions with an OmaSnap binary present.
            var desktopProfile = LinuxDesktopProfile.Current;
            var hostedCaptureEngine = new Capture.OmaSnap.OmaSnapCaptureEngine(desktopProfile);
            PlatformServices.DesktopProfile = desktopProfile;
            PlatformServices.HostedCaptureEngine = hostedCaptureEngine;
            PlatformServices.CompositorKeybindings = new HyprlandKeybindingService(desktopProfile);
            desktopProfile.StartBackgroundProbe();

            // Use LinuxScreenCaptureService if none provided
            if (screenCaptureService == null)
            {
                screenCaptureService = new LinuxScreenCaptureService(hostedCaptureEngine);
                DebugHelper.WriteLine(environment.IsWayland || environment.IsSandboxed
                    ? "Linux: Using LinuxScreenCaptureService with portal-aware capture routing."
                    : "Linux: Using LinuxScreenCaptureService with native X11/CLI fallbacks.");
            }

            bool isWayland = environment.IsWayland;

            // When Wayland portal services are disabled, skip portal-backed hotkeys/input/system
            // and fall back to simpler desktop services.
            // This setting is separate from Linux screenshot/recording preferences.
            // It controls GlobalShortcuts/InputCapture/OpenURI integration only.
            // On native X11, this flag has no effect.
            // On Wayland or sandboxed Linux, disabling it may reduce integration but also avoids portal-specific issues.
            // Keeping this separate avoids overloading the old UseModernCapture toggle.
            //
            // Note: Screen capture and recording continue to make their own backend decisions.
            // EIS connection errors and unnecessary D-Bus connections
            bool usePortalServices = environment.ShouldUsePortalServices(useWaylandPortalServices);
            if (!useWaylandPortalServices && (isWayland || environment.IsSandboxed))
            {
                DebugHelper.WriteLine("Linux: Portal services are disabled. Skipping portal hotkeys/input/system integration. " +
                    "Using fallback services instead. Re-enable and restart to use portal-backed integration.");
            }

            bool hasGlobalShortcuts = usePortalServices && PortalInterfaceChecker.HasInterface("org.freedesktop.portal.GlobalShortcuts");
            bool hasInputCapture = usePortalServices && PortalInterfaceChecker.HasInterface("org.freedesktop.portal.InputCapture");

            var hotkeyService = new SwitchableHotkeyService(CreateHotkeyService(environment, hasGlobalShortcuts));

            // Quick Setup needs /dev/input, which Flatpak and Snap sandboxes do not expose.
            PlatformServices.HotkeyAccessSetup = environment.IsSandboxed
                ? null
                : new Services.QuickSetup.LinuxInputQuickSetupService(hotkeyService, backendForced: IsHotkeyBackendForced());

            IInputService inputService = hasInputCapture
                ? new WaylandPortalInputService()
                : new LinuxInputService();

            ISystemService systemService = usePortalServices
                ? new WaylandPortalSystemService(allowNativeFallback: !environment.IsSandboxed)
                : new LinuxSystemService();

            IStartupService startupService = CreateStartupService(environment);
            IShellIntegrationService shellIntegrationService = environment.IsSandboxed
                ? new UnsupportedShellIntegrationService()
                : new LinuxShellIntegrationService();
            var notificationService = CreateNotificationService(environment, usePortalServices);

            PlatformServices.Initialize(
                platformInfo: new LinuxPlatformInfo(),
                screenService: new LinuxScreenService(),
                clipboardService: clipboardService,
                windowService: new LinuxWindowService(),
                screenCaptureService: screenCaptureService,
                hotkeyService: hotkeyService,
                inputService: inputService,
                fontService: new LinuxFontService(),
                startupService: startupService,
                systemService: systemService,
                shellIntegrationService: shellIntegrationService,
                notificationService: notificationService,
                diagnosticService: new Services.LinuxDiagnosticService(),
                watchFolderDaemonService: new LinuxWatchFolderDaemonService(),
                clipboardMonitorService: clipboardMonitorService
            );

            // Register OCR service stub (Tesseract integration planned)
            PlatformServices.Ocr = new LinuxOcrService();

            // Initialize theme service for dark mode detection
            PlatformServices.Theme = new LinuxThemeService();
            DebugHelper.WriteLine($"Linux: Theme service initialized. Dark mode preferred: {PlatformServices.Theme.IsDarkModePreferred}");
        }

        /// <summary>
        /// Selects the global hotkey provider for the current session.
        ///
        /// Preference order:
        /// 1. Direct evdev listener (works on every Wayland compositor and X11) when at least
        ///    one keyboard device is readable. See XIP0080.
        /// 2. XDG GlobalShortcuts portal as a legacy fallback on Wayland sessions that expose it.
        /// 3. X11 key grabs (<see cref="LinuxHotkeyService"/>) as the final fallback.
        ///
        /// The backend can be forced with the XERAHS_LINUX_HOTKEY_BACKEND environment variable
        /// (values: evdev, portal, x11) for diagnostics and troubleshooting.
        /// </summary>
        private static string? GetForcedHotkeyBackend() =>
            Environment.GetEnvironmentVariable("XERAHS_LINUX_HOTKEY_BACKEND")?.Trim().ToLowerInvariant();

        private static bool IsHotkeyBackendForced() => !string.IsNullOrEmpty(GetForcedHotkeyBackend());

        private static IHotkeyService CreateHotkeyService(LinuxRuntimeEnvironment environment, bool hasGlobalShortcuts)
        {
            string? forced = GetForcedHotkeyBackend();

            if (forced == "portal")
            {
                if (hasGlobalShortcuts)
                {
                    DebugHelper.WriteLine("Linux hotkeys: Forced portal backend via XERAHS_LINUX_HOTKEY_BACKEND.");
                    return new WaylandPortalHotkeyService();
                }

                DebugHelper.WriteLine("Linux hotkeys: portal backend forced but GlobalShortcuts portal is unavailable; falling back to X11.");
                return new LinuxHotkeyService();
            }

            if (forced == "x11")
            {
                DebugHelper.WriteLine("Linux hotkeys: Forced X11 backend via XERAHS_LINUX_HOTKEY_BACKEND.");
                return new LinuxHotkeyService();
            }

            bool evdevForced = forced == "evdev";
            bool evdevAvailable = EvdevGlobalHotkeyService.IsAvailable();

            if (evdevForced || evdevAvailable)
            {
                if (evdevAvailable)
                {
                    DebugHelper.WriteLine("Linux hotkeys: Using direct evdev listener (XIP0080).");
                    return new EvdevGlobalHotkeyService();
                }

                DebugHelper.WriteLine("Linux hotkeys: evdev backend requested but no readable keyboard devices found. " +
                    "Use Settings > Advanced > Global Hotkeys > Grant keyboard access, join the input group, or run 'xerahs doctor --linux-input'.");

                if (evdevForced)
                {
                    // Honor the forced choice even if currently unusable; the service self-reports failures.
                    return new EvdevGlobalHotkeyService();
                }
            }

            if (hasGlobalShortcuts)
            {
                DebugHelper.WriteLine("Linux hotkeys: evdev unavailable; using XDG GlobalShortcuts portal (legacy fallback).");
                return new WaylandPortalHotkeyService();
            }

            DebugHelper.WriteLine("Linux hotkeys: Using X11 key grabs.");
            return new LinuxHotkeyService();
        }

        private static IStartupService CreateStartupService(LinuxRuntimeEnvironment environment)
        {
            if (!environment.IsSandboxed)
            {
                return new LinuxStartupService();
            }

            if (environment.IsFlatpak &&
                PortalInterfaceChecker.HasInterface("org.freedesktop.portal.Background"))
            {
                return new FlatpakPortalStartupService(environment.AppId ?? "io.github.ShareX.XerahS");
            }

            DebugHelper.WriteLine("Linux: Startup integration is unavailable in this sandbox.");
            return new UnsupportedStartupService();
        }

        private static XerahS.Services.Abstractions.INotificationService CreateNotificationService(
            LinuxRuntimeEnvironment environment,
            bool usePortalServices)
        {
            if (usePortalServices &&
                PortalInterfaceChecker.HasInterface("org.freedesktop.portal.Notification"))
            {
                return new PortalNotificationService(allowNativeFallback: !environment.IsSandboxed);
            }

            return new LinuxNotificationService();
        }

        /// <summary>
        /// Initialize screen recording for Linux.
        /// Uses the Wayland ScreenCast portal path when available and falls back to FFmpeg x11grab on X11.
        /// </summary>
        public static void InitializeRecording()
        {
            DebugHelper.WriteLine("LinuxPlatform.InitializeRecording() called");
            try
            {
                var environment = LinuxRuntimeEnvironment.Detect();
                bool isWayland = environment.IsWayland;
                DebugHelper.WriteLine($"LinuxPlatform: isWayland={isWayland}");
                DebugHelper.WriteLine($"LinuxPlatform: recording environment={environment.ToDiagnosticString()}");

                bool hasScreenCastPortal = false;
                if (isWayland || environment.IsSandboxed)
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
                    if (isWayland || environment.IsSandboxed)
                    {
                        DebugHelper.WriteLine("WARNING: On Wayland/sandboxed Linux without ScreenCast portal, screen recording may not work properly.");
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
                // On Wayland or in Flatpak without portal, this will likely fail
                ScreenRecorderService.FallbackServiceFactory = environment.IsSandboxed
                    ? null
                    : () => new FFmpegRecordingService();

                DebugHelper.WriteLine("Linux: Screen recording initialized successfully");
                DebugHelper.WriteLine(hasScreenCastPortal
                    ? "  - Recording backend: XDG ScreenCast Portal with wf-recorder, FFmpeg(pipewire), or GStreamer(pipewiresrc)"
                    : "  - Recording backend: FFmpeg x11grab fallback");
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
