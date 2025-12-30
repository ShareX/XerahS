using System;
using System.Drawing;
using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions;

namespace ShareX.Avalonia.UI.Services
{
    public class ScreenCaptureService : IScreenCaptureService
    {
        public async Task<Image?> CaptureRegionAsync()
        {
            // For now, capture fullscreen until region selection UI is implemented
            return await Task.Run(() =>
            {
                try
                {
                    // Get screen bounds from PlatformServices if available, otherwise use fallback
                    var bounds = PlatformServices.IsInitialized 
                        ? PlatformServices.Screen.GetPrimaryScreenBounds()
                        : new Rectangle(0, 0, 1920, 1080);
                    
                    var bitmap = new Bitmap(bounds.Width, bounds.Height);
                    
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
                    }
                    
                    return (Image)bitmap;
                }
                catch (Exception)
                {
                    // Capture failed - return null
                    return null;
                }
            });
        }
    }
}
