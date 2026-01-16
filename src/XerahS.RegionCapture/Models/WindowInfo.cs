namespace XerahS.RegionCapture.Models;

/// <summary>
/// Represents information about a visible window for snapping.
/// </summary>
public sealed record WindowInfo(
    nint Handle,
    string Title,
    string ClassName,
    PixelRect Bounds,
    PixelRect VisualBounds,
    bool IsMinimized,
    int ZOrder)
{
    /// <summary>
    /// The visual bounds (excluding shadow/DWM frame) for accurate snapping.
    /// </summary>
    public PixelRect SnapBounds => VisualBounds.IsEmpty ? Bounds : VisualBounds;
}
