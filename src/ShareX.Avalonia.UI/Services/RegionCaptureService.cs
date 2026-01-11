using System;
using System.Threading.Tasks;
using ShareX.Avalonia.Core.Services;
using ShareX.Avalonia.Platform.Abstractions.Capture;
using SkiaSharp;

namespace ShareX.Avalonia.UI.Services;

/// <summary>
/// UI-layer service wrapper for the new region capture backend.
/// Provides simplified interface for RegionCaptureWindow integration.
/// </summary>
public sealed class RegionCaptureService : IDisposable
{
    private readonly RegionCaptureOrchestrator _orchestrator;
    private bool _disposed;

    public RegionCaptureService(IRegionCaptureBackend backend)
    {
        if (backend == null)
            throw new ArgumentNullException(nameof(backend));

        _orchestrator = new RegionCaptureOrchestrator(backend);

        // Subscribe to monitor configuration changes
        _orchestrator.MonitorConfigurationChanged += OnMonitorConfigurationChanged;
    }

    /// <summary>
    /// Get all monitors for overlay positioning.
    /// Returns monitors sorted by position (primary first, then left-to-right).
    /// </summary>
    public MonitorInfo[] GetMonitors()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _orchestrator.GetMonitorsForOverlay();
    }

    /// <summary>
    /// Get virtual desktop bounds in physical coordinates.
    /// Use this to determine the full capture area across all monitors.
    /// </summary>
    public PhysicalRectangle GetVirtualDesktopBoundsPhysical()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _orchestrator.GetVirtualDesktopBounds();
    }

    /// <summary>
    /// Get virtual desktop bounds in logical coordinates for overlay sizing.
    /// This is what the Avalonia window should use for its dimensions.
    /// </summary>
    public LogicalRectangle GetVirtualDesktopBoundsLogical()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _orchestrator.GetVirtualDesktopBoundsLogical();
    }

    /// <summary>
    /// Convert logical point (from Avalonia mouse input) to physical pixels.
    /// Use this when processing mouse events to get actual screen coordinates.
    /// </summary>
    public PhysicalPoint LogicalToPhysical(LogicalPoint logical)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _orchestrator.LogicalToPhysical(logical);
    }

    /// <summary>
    /// Convert physical point to logical coordinates for UI rendering.
    /// Use this when drawing screen-coordinate elements in the Avalonia canvas.
    /// </summary>
    public LogicalPoint PhysicalToLogical(PhysicalPoint physical)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _orchestrator.PhysicalToLogical(physical);
    }

    /// <summary>
    /// Convert logical rectangle to physical for capture operations.
    /// </summary>
    public PhysicalRectangle LogicalToPhysical(LogicalRectangle logical)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _orchestrator.LogicalToPhysical(logical);
    }

    /// <summary>
    /// Convert physical rectangle to logical for UI rendering.
    /// </summary>
    public LogicalRectangle PhysicalToLogical(PhysicalRectangle physical)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _orchestrator.PhysicalToLogical(physical);
    }

    /// <summary>
    /// Capture a region selected by the user.
    /// The region should be in logical coordinates (as measured in the Avalonia UI).
    /// Returns the captured bitmap in physical pixel resolution.
    /// </summary>
    public async Task<SKBitmap> CaptureRegionAsync(LogicalRectangle region, RegionCaptureOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _orchestrator.CaptureRegionAsync(region, options);
    }

    /// <summary>
    /// Get backend capabilities for feature detection.
    /// </summary>
    public BackendCapabilities GetCapabilities()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _orchestrator.GetCapabilities();
    }

    /// <summary>
    /// Fired when monitor configuration changes (hotplug, resolution, DPI).
    /// The overlay window should rebuild itself when this occurs.
    /// </summary>
    public event EventHandler<MonitorConfigurationChangedEventArgs>? MonitorConfigurationChanged;

    private void OnMonitorConfigurationChanged(object? sender, MonitorConfigurationChangedEventArgs e)
    {
        MonitorConfigurationChanged?.Invoke(this, e);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_orchestrator != null)
        {
            _orchestrator.MonitorConfigurationChanged -= OnMonitorConfigurationChanged;
            _orchestrator.Dispose();
        }
    }
}
