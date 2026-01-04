using ShareX.Ava.Platform.Abstractions;
using System;
using System.Drawing;
using System.Collections.Generic;

namespace ShareX.Ava.Platform.Linux
{
    public static class LinuxPlatform
    {
        public static void Initialize(IScreenCaptureService? screenCaptureService = null)
        {
             PlatformServices.Initialize(
                platformInfo: new LinuxPlatformInfo(),
                screenService: new StubScreenService(),
                clipboardService: new StubClipboardService(),
                windowService: new LinuxWindowService(),
                screenCaptureService: screenCaptureService ?? new StubScreenCaptureService(),
                hotkeyService: new StubHotkeyService(),
                inputService: new StubInputService(),
                fontService: new StubFontService()
             );
        }
    }

    internal class StubScreenService : IScreenService
    {
        public Rectangle GetVirtualScreenBounds() => Rectangle.Empty;
        public Rectangle GetWorkingArea() => Rectangle.Empty;
        public Rectangle GetActiveScreenBounds() => Rectangle.Empty;
        public Rectangle GetActiveScreenWorkingArea() => Rectangle.Empty;
        public Rectangle GetPrimaryScreenBounds() => Rectangle.Empty;
        public Rectangle GetPrimaryScreenWorkingArea() => Rectangle.Empty;
        public ScreenInfo[] GetAllScreens() => Array.Empty<ScreenInfo>();
        public ScreenInfo GetScreenFromPoint(Point point) => new ScreenInfo();
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
        public System.Threading.Tasks.Task<SkiaSharp.SKBitmap?> CaptureRegionAsync() => System.Threading.Tasks.Task.FromResult<SkiaSharp.SKBitmap?>(null);
        public System.Threading.Tasks.Task<SkiaSharp.SKBitmap?> CaptureRectAsync(SkiaSharp.SKRect rect) => System.Threading.Tasks.Task.FromResult<SkiaSharp.SKBitmap?>(null);
        public System.Threading.Tasks.Task<SkiaSharp.SKBitmap?> CaptureFullScreenAsync() => System.Threading.Tasks.Task.FromResult<SkiaSharp.SKBitmap?>(null);
        public System.Threading.Tasks.Task<SkiaSharp.SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService) => System.Threading.Tasks.Task.FromResult<SkiaSharp.SKBitmap?>(null);
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
        public Point GetCursorPosition() => Point.Empty;
    }

    internal class StubFontService : IFontService
    {
        public FontSpec GetDefaultMenuFont() => new FontSpec();
        public FontSpec GetDefaultContextMenuFont() => new FontSpec();
    }
}
