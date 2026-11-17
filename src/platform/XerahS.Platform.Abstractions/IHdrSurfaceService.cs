namespace XerahS.Platform.Abstractions;

/// <summary>
/// Creates native HDR surfaces that can be embedded inside a host window. The normal UI renders
/// into an SDR surface, so an embedded HDR surface is the only way to show values above display
/// white next to tone-mapped variants in the same window.
/// </summary>
public interface IHdrSurfaceService
{
    /// <summary>True when this platform can embed an HDR surface.</summary>
    bool IsSupported { get; }

    /// <summary>
    /// Creates a child surface for 16-bit float RGBA pixels in linear scRGB (1.0 == SDR white).
    /// Returns null when the surface or its HDR swapchain could not be created.
    /// </summary>
    IHdrSurface? Create(nint parentWindowHandle, ReadOnlySpan<byte> rgbaFloat16, int width, int height, int rowBytes);
}

/// <summary>A native child window presenting HDR pixels, positioned in the host's client area.</summary>
public interface IHdrSurface : IDisposable
{
    /// <summary>Places the surface using host client coordinates and physical pixels.</summary>
    void SetBounds(int x, int y, int width, int height);

    void SetVisible(bool visible);
}
