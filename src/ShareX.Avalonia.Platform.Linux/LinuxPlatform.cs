using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.ScreenCapture.ScreenRecording;
using Rectangle = System.Drawing.Rectangle;
using SysPoint = System.Drawing.Point;

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

            PlatformServices.Initialize(
                platformInfo: new LinuxPlatformInfo(),
                screenService: new StubScreenService(),
                clipboardService: new StubClipboardService(),
                windowService: new LinuxWindowService(),
                screenCaptureService: screenCaptureService,
                hotkeyService: new StubHotkeyService(),
                inputService: new StubInputService(),
                fontService: new StubFontService(),
                systemService: new Services.LinuxSystemService()
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

    internal class StubScreenService : IScreenService
    {
        public bool UsePerScreenScalingForRegionCaptureLayout => false;
        public bool UseWindowPositionForRegionCaptureFallback => false;
        public bool UseLogicalCoordinatesForRegionCapture => false;
        public Rectangle GetVirtualScreenBounds() => Rectangle.Empty;
        public Rectangle GetWorkingArea() => Rectangle.Empty;
        public Rectangle GetActiveScreenBounds() => Rectangle.Empty;
        public Rectangle GetActiveScreenWorkingArea() => Rectangle.Empty;
        public Rectangle GetPrimaryScreenBounds() => Rectangle.Empty;
        public Rectangle GetPrimaryScreenWorkingArea() => Rectangle.Empty;
        public ScreenInfo[] GetAllScreens() => Array.Empty<ScreenInfo>();
        public ScreenInfo GetScreenFromPoint(SysPoint point) => new ScreenInfo();
        public ScreenInfo GetScreenFromRectangle(Rectangle rectangle) => new ScreenInfo();
    }

    internal class StubClipboardService : IClipboardService
    {
        public void SetText(string text) { }
        public string? GetText() => string.Empty;
        public void SetImage(SkiaSharp.SKBitmap image) { }
        public SkiaSharp.SKBitmap? GetImage() => null;
        public void SetFileDropList(string[] files) { }
        public string[]? GetFileDropList() => Array.Empty<string>();
        public void Clear() { }
        public bool ContainsText() => false;
        public bool ContainsImage() => false;
        public bool ContainsFileDropList() => false;
        public object? GetData(string format) => null;
        public void SetData(string format, object data) { }
        public bool ContainsData(string format) => false;
        public System.Threading.Tasks.Task<string?> GetTextAsync() => System.Threading.Tasks.Task.FromResult<string?>(string.Empty);
        public System.Threading.Tasks.Task SetTextAsync(string text) => System.Threading.Tasks.Task.CompletedTask;
    }

    internal class StubScreenCaptureService : IScreenCaptureService
    {
        public System.Threading.Tasks.Task<SkiaSharp.SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null) => System.Threading.Tasks.Task.FromResult<SkiaSharp.SKBitmap?>(null);
        public System.Threading.Tasks.Task<SkiaSharp.SKBitmap?> CaptureRectAsync(SkiaSharp.SKRect rect, CaptureOptions? options = null) => System.Threading.Tasks.Task.FromResult<SkiaSharp.SKBitmap?>(null);
        public System.Threading.Tasks.Task<SkiaSharp.SKBitmap?> CaptureFullScreenAsync(CaptureOptions? options = null) => System.Threading.Tasks.Task.FromResult<SkiaSharp.SKBitmap?>(null);
        public System.Threading.Tasks.Task<SkiaSharp.SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService, CaptureOptions? options = null) => System.Threading.Tasks.Task.FromResult<SkiaSharp.SKBitmap?>(null);
        public System.Threading.Tasks.Task<SkiaSharp.SKBitmap?> CaptureWindowAsync(System.IntPtr windowHandle, IWindowService windowService, CaptureOptions? options = null) => System.Threading.Tasks.Task.FromResult<SkiaSharp.SKBitmap?>(null);
    }

    internal class StubHotkeyService : IHotkeyService
    {
        public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered { add { } remove { } }
        public bool RegisterHotkey(HotkeyInfo hotkeyInfo) => false;
        public bool UnregisterHotkey(HotkeyInfo hotkeyInfo) => false;
        public void UnregisterAll() { }
        public bool IsRegistered(HotkeyInfo hotkeyInfo) => false;
        public bool IsSuspended { get; set; }
        public void Dispose() { }
    }

    internal class StubInputService : IInputService
    {
        public SysPoint GetCursorPosition() => SysPoint.Empty;
    }

    internal class StubFontService : IFontService
    {
        public FontSpec GetDefaultMenuFont() => new FontSpec();
        public FontSpec GetDefaultContextMenuFont() => new FontSpec();
    }
}
