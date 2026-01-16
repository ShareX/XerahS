using SkiaSharp;

namespace XerahS.Platform.Abstractions
{
    public interface IScreenCaptureService
    {
        /// <summary>
        /// Shows the region selector UI and returns the selected rectangle without capturing.
        /// Used by screen recording to get the capture area before starting recording.
        /// </summary>
        /// <returns>SKRectI of the selected region, or SKRectI.Empty if cancelled.</returns>
        Task<SKRectI> SelectRegionAsync(CaptureOptions? options = null);

        /// <summary>
        /// Captures a region of the screen.
        /// </summary>
        /// <returns>SkiaSharp.SKBitmap if successful, null otherwise.</returns>
        Task<SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null);

        /// <summary>
        /// Captures a specific region of the screen
        /// </summary>
        Task<SKBitmap?> CaptureRectAsync(SKRect rect, CaptureOptions? options = null);

        /// <summary>
        /// Captures the full screen
        /// </summary>
        Task<SKBitmap?> CaptureFullScreenAsync(CaptureOptions? options = null);

        /// <summary>
        /// Captures the active window
        /// </summary>
        Task<SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService, CaptureOptions? options = null);

        /// <summary>
        /// Captures a specific window by its handle
        /// </summary>
        /// <param name="windowHandle">The native window handle to capture</param>
        /// <param name="windowService">Window service for bounds/state queries</param>
        /// <param name="options">Capture options</param>
        /// <returns>SKBitmap of the window, or null on failure</returns>
        Task<SKBitmap?> CaptureWindowAsync(IntPtr windowHandle, IWindowService windowService, CaptureOptions? options = null);
    }
}
