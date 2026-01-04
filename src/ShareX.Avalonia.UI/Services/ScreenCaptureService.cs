using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using ShareX.Ava.Platform.Abstractions;
using ShareX.Ava.UI.Views.RegionCapture;
using SkiaSharp;
// REMOVED: System.Drawing (except for temporary conversion if needed, but strict replacement preferred if possible)

namespace ShareX.Ava.UI.Services
{
    public class ScreenCaptureService : IScreenCaptureService
    {
        private readonly IScreenCaptureService _platformImpl;

        public ScreenCaptureService(IScreenCaptureService platformImpl)
        {
            _platformImpl = platformImpl;
        }

        public Task<SKBitmap?> CaptureRectAsync(SKRect rect)
        {
            return _platformImpl.CaptureRectAsync(rect);
        }

        public Task<SKBitmap?> CaptureFullScreenAsync()
        {
            return _platformImpl.CaptureFullScreenAsync();
        }

        public Task<SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService)
        {
            return _platformImpl.CaptureActiveWindowAsync(windowService);
        }

        public async Task<SKBitmap?> CaptureRegionAsync()
        {
            SKRectI selection = SKRectI.Empty;

            // Show UI window on UI thread
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var window = new RegionCaptureWindow();
                
                // Window will handle background capture in OnOpened
                window.Show();
                selection = await window.GetResultAsync();
            });

            if (selection.IsEmpty || selection.Width <= 0 || selection.Height <= 0)
            {
                return null;
            }

            // Small delay to allow window to close fully
            await Task.Delay(200);

            // Delegate capture to platform implementation
            var skRect = new SKRect(selection.Left, selection.Top, selection.Right, selection.Bottom);
            return await _platformImpl.CaptureRectAsync(skRect);
        }
    }
}
