namespace XerahS.Platform.Abstractions;

/// <summary>
/// Presents an HDR image in its own window backed by an HDR swapchain (scRGB on Windows).
/// Unlike the normal UI, which renders into an SDR surface and therefore has to tone map or clip
/// everything above display white, this keeps the original values so an HDR display shows the
/// real highlights.
/// </summary>
public interface IHdrPreviewService
{
    /// <summary>True when this platform can present HDR pixels in a window.</summary>
    bool IsSupported { get; }

    /// <summary>
    /// Opens a preview window for 16-bit float RGBA pixels in linear scRGB (1.0 == SDR white).
    /// Returns false when the window or its HDR swapchain could not be created, so callers can
    /// fall back to an external viewer.
    /// </summary>
    bool TryShow(ReadOnlySpan<byte> rgbaFloat16, int width, int height, int rowBytes, string title);
}
