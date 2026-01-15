using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions.Capture;

namespace ShareX.Avalonia.Platform.Windows.Capture;

/// <summary>
/// Internal interface for platform-specific capture strategies.
/// Each strategy implements a different capture API with varying support levels.
/// </summary>
internal interface ICaptureStrategy
{
    /// <summary>
    /// Check if this strategy is supported on the current system.
    /// </summary>
    static abstract bool IsSupported();

    /// <summary>
    /// Get the name of this capture strategy for diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Enumerate all monitors with DPI information.
    /// </summary>
    MonitorInfo[] GetMonitors();

    /// <summary>
    /// Capture a region in physical pixel coordinates.
    /// </summary>
    Task<CapturedBitmap> CaptureRegionAsync(
        PhysicalRectangle physicalRegion,
        RegionCaptureOptions options);

    /// <summary>
    /// Get the capabilities of this strategy.
    /// </summary>
    BackendCapabilities GetCapabilities();

    /// <summary>
    /// Cleanup resources used by this strategy.
    /// </summary>
    void Dispose();
}
