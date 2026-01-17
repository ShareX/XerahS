using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Services;
using XerahS.RegionCapture.ScreenRecording;
namespace XerahS.Platform.Linux
{
    public static class LinuxPlatform
    {
        public static void Initialize(IScreenCaptureService? screenCaptureService = null)
        {
            // Use LinuxScreenCaptureService if none provided
            if (screenCaptureService == null)
            {
                screenCaptureService = new LinuxScreenCaptureService();
                DebugHelper.WriteLine(LinuxScreenCaptureService.IsWayland
                    ? "Linux: Running on Wayland. Using LinuxScreenCaptureService with XDG Portal support."
                    : "Linux: Running on X11. Using LinuxScreenCaptureService with CLI fallbacks.");
            }

            var hotkeyService = LinuxScreenCaptureService.IsWayland
                ? new WaylandPortalHotkeyService()
                : new LinuxHotkeyService();

            var inputService = LinuxScreenCaptureService.IsWayland
                ? new WaylandPortalInputService()
                : new LinuxInputService();

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
                systemService: new Services.LinuxSystemService(),
                notificationService: new LinuxNotificationService(),
                diagnosticService: new Services.LinuxDiagnosticService()
            );
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
            try
            {
                // Linux uses FFmpegRecordingService as the primary recording method
                // FFmpeg supports x11grab (X11) and various Wayland capture methods
                DebugHelper.WriteLine("Linux: Initializing screen recording with FFmpeg backend");

                // Register FFmpegRecordingService factory
                // Note: FFmpegRecordingService is a complete recording service (not just capture/encoder)
                // so we don't use CaptureSourceFactory/EncoderFactory pattern here
                ScreenRecorderService.FallbackServiceFactory = () => new FFmpegRecordingService();

                DebugHelper.WriteLine("Linux: Screen recording initialized successfully");
                DebugHelper.WriteLine("  - Recording backend: FFmpeg (x11grab/Wayland)");
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
