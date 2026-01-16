using XerahS.RegionCapture.Models;
using XerahS.RegionCapture.Services;

namespace XerahS.RegionCapture;

/// <summary>
/// High-level service for initiating region capture operations.
/// Provides industry-leading mixed-DPI handling using per-monitor overlays.
/// </summary>
public sealed class RegionCaptureService
{
    /// <summary>
    /// Configuration options for region capture.
    /// </summary>
    public RegionCaptureOptions Options { get; init; } = new();

    /// <summary>
    /// Initiates a region capture operation and returns the selected region in physical pixels.
    /// </summary>
    /// <returns>The captured region, or null if cancelled.</returns>
    public async Task<PixelRect?> CaptureRegionAsync()
    {
        using var manager = new OverlayManager();
        return await manager.ShowOverlaysAsync();
    }

    /// <summary>
    /// Initiates a region capture with a callback for real-time selection updates.
    /// </summary>
    public async Task<PixelRect?> CaptureRegionAsync(Action<PixelRect>? onSelectionChanged)
    {
        using var manager = new OverlayManager();
        // TODO: Wire up selection change callback
        return await manager.ShowOverlaysAsync();
    }
}

/// <summary>
/// Configuration options for region capture behavior.
/// </summary>
public sealed record RegionCaptureOptions
{
    /// <summary>
    /// Enable window snapping on hover. Default: true
    /// </summary>
    public bool EnableWindowSnapping { get; init; } = true;

    /// <summary>
    /// Enable magnifier for pixel-perfect precision. Default: true
    /// </summary>
    public bool EnableMagnifier { get; init; } = true;

    /// <summary>
    /// Magnifier zoom level. Default: 4x
    /// </summary>
    public int MagnifierZoom { get; init; } = 4;

    /// <summary>
    /// Enable keyboard arrow nudging of selection. Default: true
    /// </summary>
    public bool EnableKeyboardNudge { get; init; } = true;

    /// <summary>
    /// Dim overlay opacity (0.0-1.0). Default: 0.7
    /// </summary>
    public double DimOpacity { get; init; } = 0.7;

    /// <summary>
    /// Color of the selection border.
    /// </summary>
    public uint SelectionBorderColor { get; init; } = 0xFFFFFFFF; // White

    /// <summary>
    /// Color of the window snap highlight.
    /// </summary>
    public uint WindowSnapColor { get; init; } = 0xFF00AEFF; // Blue
}
