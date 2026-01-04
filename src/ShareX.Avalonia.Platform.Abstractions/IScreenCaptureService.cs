using System.Threading.Tasks;
using SkiaSharp;
using ShareX.Ava.Platform.Abstractions;

namespace ShareX.Ava.Platform.Abstractions
{
    public interface IScreenCaptureService
    {
        /// <summary>
        /// Captures a region of the screen.
        /// </summary>
        /// <returns>SkiaSharp.SKBitmap if successful, null otherwise.</returns>
        Task<SKBitmap?> CaptureRegionAsync();

        /// <summary>
        /// Captures a specific region of the screen
        /// </summary>
        Task<SKBitmap?> CaptureRectAsync(SKRect rect);
        
        /// <summary>
        /// Captures the full screen
        /// </summary>
        Task<SKBitmap?> CaptureFullScreenAsync();
        
        /// <summary>
        /// Captures the active window
        /// </summary>
        Task<SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService);
    }
}
