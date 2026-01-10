using System;
using System.Threading.Tasks;

namespace ShareX.Avalonia.Platform.Abstractions.Capture;

/// <summary>
/// Platform-specific backend for region capture operations.
/// Implementations handle DPI scaling, coordinate conversion, and bitmap capture
/// using native platform APIs.
/// </summary>
public interface IRegionCaptureBackend : IDisposable
{
    /// <summary>
    /// Enumerate all connected monitors with DPI information.
    /// </summary>
    /// <returns>Array of monitors with physical bounds and scale factors.</returns>
    MonitorInfo[] GetMonitors();

    /// <summary>
    /// Capture a rectangular region in physical pixel coordinates.
    /// </summary>
    /// <param name="physicalRegion">Region to capture in physical pixels.</param>
    /// <param name="options">Capture options (quality, format, etc.).</param>
    /// <returns>Captured bitmap in requested format.</returns>
    /// <exception cref="ArgumentException">Invalid region (zero/negative size).</exception>
    /// <exception cref="InvalidOperationException">Capture failed.</exception>
    Task<CapturedBitmap> CaptureRegionAsync(
        PhysicalRectangle physicalRegion,
        RegionCaptureOptions options);

    /// <summary>
    /// Query backend capabilities for feature detection.
    /// </summary>
    BackendCapabilities GetCapabilities();

    /// <summary>
    /// Detect monitor configuration changes (hotplug, resolution change, DPI change).
    /// </summary>
    event EventHandler<MonitorConfigurationChangedEventArgs>? ConfigurationChanged;
}

/// <summary>
/// Event arguments for monitor configuration changes.
/// </summary>
public sealed class MonitorConfigurationChangedEventArgs : EventArgs
{
    /// <summary>
    /// The type of configuration change.
    /// </summary>
    public MonitorChangeType ChangeType { get; }

    /// <summary>
    /// The updated monitor information after the change.
    /// </summary>
    public MonitorInfo[] Monitors { get; }

    public MonitorConfigurationChangedEventArgs(MonitorChangeType changeType, MonitorInfo[] monitors)
    {
        ChangeType = changeType;
        Monitors = monitors;
    }
}

/// <summary>
/// Types of monitor configuration changes.
/// </summary>
public enum MonitorChangeType
{
    /// <summary>A monitor was added.</summary>
    Added,

    /// <summary>A monitor was removed.</summary>
    Removed,

    /// <summary>Monitor resolution or position changed.</summary>
    LayoutChanged,

    /// <summary>Monitor DPI/scaling changed.</summary>
    DpiChanged,

    /// <summary>Unknown or multiple changes occurred.</summary>
    Unknown
}
