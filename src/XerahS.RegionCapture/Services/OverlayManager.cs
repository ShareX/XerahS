using XerahS.RegionCapture.Models;
using XerahS.RegionCapture.UI;

namespace XerahS.RegionCapture.Services;

/// <summary>
/// Manages the lifecycle and coordination of per-monitor overlay windows.
/// This implements the "Per-Monitor Overlay" pattern (Strategy B) to bypass
/// mixed-DPI scaling artifacts common in single-span windows.
/// </summary>
public sealed class OverlayManager : IDisposable
{
    private readonly List<OverlayWindow> _overlays = [];
    private readonly TaskCompletionSource<PixelRect?> _completionSource;
    private readonly CoordinateTranslationService _coordinateService;
    private bool _disposed;

    public OverlayManager()
    {
        _completionSource = new TaskCompletionSource<PixelRect?>();
        _coordinateService = new CoordinateTranslationService();
    }

    /// <summary>
    /// Gets all active overlay windows.
    /// </summary>
    public IReadOnlyList<OverlayWindow> Overlays => _overlays;

    /// <summary>
    /// Gets the coordinate translation service for cross-monitor calculations.
    /// </summary>
    public CoordinateTranslationService CoordinateService => _coordinateService;

    /// <summary>
    /// Creates and shows overlay windows for all monitors.
    /// </summary>
    public async Task<PixelRect?> ShowOverlaysAsync(Action<PixelRect>? onSelectionChanged = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var monitors = _coordinateService.Monitors;

        if (monitors.Count == 0)
            return null;

        try
        {
            // Create one overlay per monitor
            foreach (var monitor in monitors)
            {
                var overlay = new OverlayWindow(monitor, _completionSource, onSelectionChanged);
                _overlays.Add(overlay);
            }

            // Show all overlays simultaneously
            foreach (var overlay in _overlays)
            {
                overlay.Show();
                overlay.Activate();
            }

            // Focus the primary monitor's overlay
            var primaryOverlay = _overlays.FirstOrDefault(o =>
                monitors.FirstOrDefault(m => m.IsPrimary)?.PhysicalBounds == GetOverlayMonitorBounds(o));

            primaryOverlay?.Focus();

            // Wait for result
            return await _completionSource.Task;
        }
        finally
        {
            CloseAllOverlays();
        }
    }

    private static PixelRect GetOverlayMonitorBounds(OverlayWindow overlay)
    {
        return new PixelRect(overlay.Position.X, overlay.Position.Y, overlay.Width, overlay.Height);
    }

    private void CloseAllOverlays()
    {
        foreach (var overlay in _overlays)
        {
            try
            {
                overlay.Close();
            }
            catch
            {
                // Ignore close errors
            }
        }

        _overlays.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        CloseAllOverlays();
        _completionSource.TrySetCanceled();
    }
}
