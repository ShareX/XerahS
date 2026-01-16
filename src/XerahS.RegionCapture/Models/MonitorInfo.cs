namespace XerahS.RegionCapture.Models;

/// <summary>
/// Represents information about a physical monitor including DPI scaling.
/// </summary>
public sealed record MonitorInfo(
    string DeviceName,
    PixelRect PhysicalBounds,
    PixelRect WorkArea,
    double ScaleFactor,
    bool IsPrimary)
{
    /// <summary>
    /// Gets the DPI value for this monitor (96 * ScaleFactor).
    /// </summary>
    public double Dpi => 96.0 * ScaleFactor;

    /// <summary>
    /// Converts physical pixels to logical (DIPs) for this monitor.
    /// </summary>
    public double PhysicalToLogical(double physical) => physical / ScaleFactor;

    /// <summary>
    /// Converts logical (DIPs) to physical pixels for this monitor.
    /// </summary>
    public double LogicalToPhysical(double logical) => logical * ScaleFactor;

    /// <summary>
    /// Converts a physical point to logical coordinates relative to this monitor.
    /// </summary>
    public PixelPoint PhysicalToLogical(PixelPoint physical) =>
        new((physical.X - PhysicalBounds.X) / ScaleFactor,
            (physical.Y - PhysicalBounds.Y) / ScaleFactor);

    /// <summary>
    /// Converts a logical point (relative to this monitor) to physical coordinates.
    /// </summary>
    public PixelPoint LogicalToPhysical(PixelPoint logical) =>
        new(logical.X * ScaleFactor + PhysicalBounds.X,
            logical.Y * ScaleFactor + PhysicalBounds.Y);
}
