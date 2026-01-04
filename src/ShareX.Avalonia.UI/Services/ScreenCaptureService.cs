using System;
using System.Drawing;
using System.Threading.Tasks;
using Avalonia.Threading;
using ShareX.Ava.Platform.Abstractions;
using ShareX.Ava.UI.Views.RegionCapture;

namespace ShareX.Ava.UI.Services
{
    public class ScreenCaptureService : IScreenCaptureService
    {
        private readonly IScreenCaptureService _platformImpl;

        public ScreenCaptureService(IScreenCaptureService platformImpl)
        {
            _platformImpl = platformImpl;
        }

        public Task<Image?> CaptureRectAsync(Rectangle rect)
        {
            return _platformImpl.CaptureRectAsync(rect);
        }

        public Task<Image?> CaptureFullScreenAsync()
        {
            return _platformImpl.CaptureFullScreenAsync();
        }

        public Task<Image?> CaptureActiveWindowAsync(IWindowService windowService)
        {
            return _platformImpl.CaptureActiveWindowAsync(windowService);
        }

        public async Task<Image?> CaptureRegionAsync()
        {
            System.Drawing.Rectangle selection = System.Drawing.Rectangle.Empty;

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
            return await _platformImpl.CaptureRectAsync(selection);
        }
    }
}
