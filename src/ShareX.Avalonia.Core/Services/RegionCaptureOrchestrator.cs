using System;
using System.Linq;
using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions.Capture;
using SkiaSharp;

namespace ShareX.Avalonia.Core.Services;

/// <summary>
/// Platform-agnostic orchestrator for region capture workflow.
/// Coordinates between UI layer, coordinate transforms, and platform backend.
/// </summary>
public sealed class RegionCaptureOrchestrator : IDisposable
{
    private readonly IRegionCaptureBackend _backend;
    private CoordinateTransform _transform;
    private bool _disposed;

    public RegionCaptureOrchestrator(IRegionCaptureBackend backend)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));

        // Initialize coordinate transform with current monitor config
        var monitors = _backend.GetMonitors();
        _transform = new CoordinateTransform(monitors);

        // Subscribe to monitor configuration changes
        _backend.ConfigurationChanged += OnMonitorConfigChanged;
    }

    /// <summary>
    /// Get all monitors for overlay spanning.
    /// Returns monitors sorted by position (primary first, then left-to-right, top-to-bottom).
    /// </summary>
    public MonitorInfo[] GetMonitorsForOverlay()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var monitors = _backend.GetMonitors();
        return monitors
            .OrderByDescending(m => m.IsPrimary)
            .ThenBy(m => m.Bounds.X)
            .ThenBy(m => m.Bounds.Y)
            .ToArray();
    }

    /// <summary>
    /// Get the virtual desktop bounds (union of all monitors).
    /// </summary>
    public PhysicalRectangle GetVirtualDesktopBounds()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transform.VirtualDesktopBounds;
    }

    /// <summary>
    /// Get the virtual desktop bounds in logical coordinates.
    /// </summary>
    public LogicalRectangle GetVirtualDesktopBoundsLogical()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var physical = _transform.VirtualDesktopBounds;
        return _transform.PhysicalToLogical(physical);
    }

    /// <summary>
    /// Capture a region selected by the user in logical coordinates.
    /// Handles coordinate conversion and multi-monitor stitching automatically.
    /// </summary>
    public async Task<SKBitmap> CaptureRegionAsync(
        LogicalRectangle logicalRegion,
        RegionCaptureOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        options ??= RegionCaptureOptions.Default;

        // Convert logical region to physical pixels
        var physicalRegion = _transform.LogicalToPhysical(logicalRegion);

        // Validate region
        _transform.ValidateCaptureRegion(physicalRegion);

        // Check if region spans multiple monitors
        var intersectedMonitors = _transform.GetMonitorsIntersecting(physicalRegion);

        if (intersectedMonitors.Length == 0)
            throw new InvalidOperationException("Region does not intersect any monitor");

        if (intersectedMonitors.Length == 1)
        {
            // Simple case: region within single monitor
            return await CaptureSingleMonitorAsync(physicalRegion, options);
        }
        else
        {
            // Complex case: region spans multiple monitors
            return await CaptureMultiMonitorAsync(physicalRegion, intersectedMonitors, options);
        }
    }

    /// <summary>
    /// Convert logical coordinates to physical for a given point.
    /// </summary>
    public PhysicalPoint LogicalToPhysical(LogicalPoint logical)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transform.LogicalToPhysical(logical);
    }

    /// <summary>
    /// Convert physical coordinates to logical for a given point.
    /// </summary>
    public LogicalPoint PhysicalToLogical(PhysicalPoint physical)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transform.PhysicalToLogical(physical);
    }

    /// <summary>
    /// Convert logical rectangle to physical for overlay rendering.
    /// </summary>
    public PhysicalRectangle LogicalToPhysical(LogicalRectangle logical)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transform.LogicalToPhysical(logical);
    }

    /// <summary>
    /// Convert physical rectangle to logical for capture processing.
    /// </summary>
    public LogicalRectangle PhysicalToLogical(PhysicalRectangle physical)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transform.PhysicalToLogical(physical);
    }

    /// <summary>
    /// Get backend capabilities for feature detection.
    /// </summary>
    public BackendCapabilities GetCapabilities()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _backend.GetCapabilities();
    }

    private async Task<SKBitmap> CaptureSingleMonitorAsync(
        PhysicalRectangle region,
        RegionCaptureOptions options)
    {
        var captured = await _backend.CaptureRegionAsync(region, options);
        return captured.Bitmap;
    }

    private async Task<SKBitmap> CaptureMultiMonitorAsync(
        PhysicalRectangle region,
        MonitorInfo[] monitors,
        RegionCaptureOptions options)
    {
        // Capture each monitor's portion in parallel
        var captureTasks = monitors.Select(async monitor =>
        {
            var intersection = region.Intersect(monitor.Bounds);
            if (intersection == null || intersection.Value.IsEmpty)
                return ((MonitorInfo, CapturedBitmap, PhysicalRectangle)?)null;

            var captured = await _backend.CaptureRegionAsync(intersection.Value, options);
            return ((MonitorInfo, CapturedBitmap, PhysicalRectangle)?)(monitor, captured, intersection: intersection.Value);
        }).ToArray();

        var captures = await Task.WhenAll(captureTasks);
        var validCaptures = captures.Where(c => c != null).Select(c => c!.Value).ToArray();

        if (validCaptures.Length == 0)
            throw new InvalidOperationException("No valid captures from any monitor");

        // Stitch captures together
        return StitchCaptures(validCaptures, region);
    }

    private SKBitmap StitchCaptures(
        (MonitorInfo monitor, CapturedBitmap captured, PhysicalRectangle intersection)[] captures,
        PhysicalRectangle targetRegion)
    {
        // Create final bitmap
        var result = new SKBitmap(
            targetRegion.Width,
            targetRegion.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);

        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Black);

        // Draw each captured region at the correct position
        foreach (var (monitor, captured, intersection) in captures)
        {
            var destX = intersection.X - targetRegion.X;
            var destY = intersection.Y - targetRegion.Y;

            canvas.DrawBitmap(
                captured.Bitmap,
                new SKRect(0, 0, captured.Width, captured.Height),
                new SKRect(destX, destY, destX + captured.Width, destY + captured.Height));

            // Dispose individual capture
            captured.Dispose();
        }

        return result;
    }

    private void OnMonitorConfigChanged(object? sender, MonitorConfigurationChangedEventArgs e)
    {
        // Rebuild coordinate transform with new monitor configuration
        _transform = new CoordinateTransform(e.Monitors);

        // Notify listeners (if needed in the future)
        MonitorConfigurationChanged?.Invoke(this, e);
    }

    /// <summary>
    /// Fired when monitor configuration changes (hotplug, resolution change, DPI change).
    /// </summary>
    public event EventHandler<MonitorConfigurationChangedEventArgs>? MonitorConfigurationChanged;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _backend.ConfigurationChanged -= OnMonitorConfigChanged;
        _backend?.Dispose();
    }
}
