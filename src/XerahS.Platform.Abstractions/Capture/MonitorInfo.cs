using System;

namespace ShareX.Avalonia.Platform.Abstractions.Capture;

/// <summary>
/// Immutable monitor information with explicit DPI scaling.
/// All coordinates are in physical pixels relative to the virtual desktop origin.
/// </summary>
public sealed record MonitorInfo
{
    /// <summary>
    /// Platform-specific monitor identifier.
    /// Examples: Windows: "\\.\DISPLAY1", macOS: "37D8832A-2D66-02CA-B9F7-8F30A301B230", Linux: "69"
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable monitor name.
    /// Examples: "Dell U2720Q", "Built-in Retina Display", "Samsung S24"
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether this is the primary/main monitor.
    /// The primary monitor typically has its origin at (0, 0).
    /// </summary>
    public required bool IsPrimary { get; init; }

    /// <summary>
    /// Physical bounds in pixels, relative to virtual desktop origin.
    /// May have negative X/Y for monitors positioned left/above the primary monitor.
    /// </summary>
    public required PhysicalRectangle Bounds { get; init; }

    /// <summary>
    /// Working area excluding system UI (taskbar, dock, menu bar) in physical pixels.
    /// Relative to virtual desktop origin.
    /// </summary>
    public required PhysicalRectangle WorkingArea { get; init; }

    /// <summary>
    /// DPI scale factor relative to 96 DPI.
    /// Examples: 1.0 = 100%, 1.25 = 125%, 1.5 = 150%, 2.0 = 200%
    /// Logical pixels = Physical pixels / ScaleFactor
    /// </summary>
    public required double ScaleFactor { get; init; }

    /// <summary>
    /// Physical DPI (dots per inch).
    /// Calculated as: 96 * ScaleFactor
    /// </summary>
    public double PhysicalDpi => 96.0 * ScaleFactor;

    /// <summary>
    /// Display rotation in degrees (0, 90, 180, 270).
    /// Default is 0 (landscape).
    /// </summary>
    public int Rotation { get; init; } = 0;

    /// <summary>
    /// Refresh rate in Hz.
    /// 0 if unknown or not applicable.
    /// </summary>
    public int RefreshRate { get; init; } = 0;

    /// <summary>
    /// Bits per pixel (color depth).
    /// Common values: 24, 32 (32-bit BGRA is most common).
    /// </summary>
    public int BitsPerPixel { get; init; } = 32;

    /// <summary>
    /// Additional platform-specific metadata.
    /// </summary>
    public string? DevicePath { get; init; }

    public override string ToString()
    {
        var primary = IsPrimary ? " (Primary)" : "";
        return $"{Name}{primary}: {Bounds.Width}×{Bounds.Height} @ {ScaleFactor:F2}x ({PhysicalDpi:F0} DPI)";
    }
}
