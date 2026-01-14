using System;
using System.Linq;
using ShareX.Avalonia.Platform.Abstractions.Capture;

namespace ShareX.Avalonia.Core.Services;

/// <summary>
/// Handles coordinate conversions between physical and logical spaces.
/// Thread-safe and immutable after construction.
/// </summary>
public sealed class CoordinateTransform
{
    private readonly MonitorInfo[] _monitors;
    private readonly PhysicalRectangle _virtualDesktopBounds;
    private readonly MonitorInfo _primaryMonitor;

    public CoordinateTransform(MonitorInfo[] monitors)
    {
        if (monitors == null || monitors.Length == 0)
            throw new ArgumentException("At least one monitor required", nameof(monitors));

        _monitors = monitors.ToArray(); // Defensive copy
        _primaryMonitor = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
        _virtualDesktopBounds = CalculateVirtualDesktopBounds(monitors);
    }

    public MonitorInfo[] Monitors => _monitors.ToArray();
    public PhysicalRectangle VirtualDesktopBounds => _virtualDesktopBounds;
    public MonitorInfo PrimaryMonitor => _primaryMonitor;

    #region Physical ↔ Logical Conversions (Points)

    /// <summary>
    /// Convert logical point to physical pixels.
    /// Automatically detects which monitor contains the point.
    /// </summary>
    /// <exception cref="ArgumentException">Point outside all monitors and no fallback available.</exception>
    public PhysicalPoint LogicalToPhysical(LogicalPoint logical)
    {
        // Find monitor containing this logical point
        var monitor = FindMonitorContainingLogical(logical);
        if (monitor == null)
        {
            // Fallback: use primary monitor's scale
            monitor = _primaryMonitor;
        }

        // Get logical origin of this monitor
        var monitorLogicalOrigin = GetMonitorLogicalOrigin(monitor);

        // Convert to monitor-local logical coordinates
        var monitorLocalLogical = new LogicalPoint(
            logical.X - monitorLogicalOrigin.X,
            logical.Y - monitorLogicalOrigin.Y);

        // Apply monitor's scale factor
        var monitorLocalPhysical = new PhysicalPoint(
            (int)Math.Round(monitorLocalLogical.X * monitor.ScaleFactor),
            (int)Math.Round(monitorLocalLogical.Y * monitor.ScaleFactor));

        // Convert to virtual desktop coordinates
        return new PhysicalPoint(
            monitorLocalPhysical.X + monitor.Bounds.X,
            monitorLocalPhysical.Y + monitor.Bounds.Y);
    }

    /// <summary>
    /// Convert physical point to logical coordinates.
    /// </summary>
    /// <exception cref="ArgumentException">Point outside all monitors and no fallback available.</exception>
    public LogicalPoint PhysicalToLogical(PhysicalPoint physical)
    {
        // Find monitor containing this physical point
        var monitor = FindMonitorContainingPhysical(physical);
        if (monitor == null)
        {
            // Fallback: use primary monitor's scale
            monitor = _primaryMonitor;
        }

        // Convert to monitor-local coordinates
        var monitorLocalPhysical = new PhysicalPoint(
            physical.X - monitor.Bounds.X,
            physical.Y - monitor.Bounds.Y);

        // Apply inverse scale factor
        var monitorLocalLogical = new LogicalPoint(
            monitorLocalPhysical.X / monitor.ScaleFactor,
            monitorLocalPhysical.Y / monitor.ScaleFactor);

        // Convert to virtual desktop logical coordinates
        var monitorLogicalOrigin = GetMonitorLogicalOrigin(monitor);

        return new LogicalPoint(
            monitorLocalLogical.X + monitorLogicalOrigin.X,
            monitorLocalLogical.Y + monitorLogicalOrigin.Y);
    }

    #endregion

    #region Physical ↔ Logical Conversions (Rectangles)

    /// <summary>
    /// Convert logical rectangle to physical pixels.
    /// For regions spanning multiple monitors, converts corner points individually.
    /// </summary>
    public PhysicalRectangle LogicalToPhysical(LogicalRectangle logical)
    {
        // Convert corner points (each may be on different monitor)
        var topLeft = LogicalToPhysical(logical.TopLeft);
        var bottomRight = LogicalToPhysical(logical.BottomRight);

        return PhysicalRectangle.FromCorners(topLeft, bottomRight);
    }

    /// <summary>
    /// Convert physical rectangle to logical coordinates.
    /// </summary>
    public LogicalRectangle PhysicalToLogical(PhysicalRectangle physical)
    {
        // Convert corner points
        var topLeft = PhysicalToLogical(physical.TopLeft);
        var bottomRight = PhysicalToLogical(physical.BottomRight);

        return LogicalRectangle.FromCorners(topLeft, bottomRight);
    }

    #endregion

    #region Monitor Detection

    /// <summary>
    /// Find which monitor contains the given physical point.
    /// Returns null if point is outside all monitors.
    /// </summary>
    public MonitorInfo? FindMonitorContainingPhysical(PhysicalPoint point)
    {
        foreach (var monitor in _monitors)
        {
            if (monitor.Bounds.Contains(point))
                return monitor;
        }

        return null; // Outside all monitors
    }

    /// <summary>
    /// Find which monitor contains the given logical point.
    /// Returns null if point is outside all monitors.
    /// </summary>
    public MonitorInfo? FindMonitorContainingLogical(LogicalPoint point)
    {
        foreach (var monitor in _monitors)
        {
            var logicalBounds = GetMonitorLogicalBounds(monitor);
            if (logicalBounds.Contains(point))
                return monitor;
        }

        return null;
    }

    /// <summary>
    /// Get all monitors that intersect with the given physical rectangle.
    /// Returns monitors sorted by intersection area (largest first).
    /// </summary>
    public MonitorInfo[] GetMonitorsIntersecting(PhysicalRectangle region)
    {
        var intersections = new System.Collections.Generic.List<(MonitorInfo monitor, int area)>();

        foreach (var monitor in _monitors)
        {
            var intersection = region.Intersect(monitor.Bounds);
            if (intersection.HasValue)
            {
                intersections.Add((monitor, intersection.Value.Area));
            }
        }

        return intersections
            .OrderByDescending(x => x.area)
            .Select(x => x.monitor)
            .ToArray();
    }

    /// <summary>
    /// Get the monitor with the largest intersection area.
    /// Returns primary monitor if no intersection found.
    /// </summary>
    public MonitorInfo GetPrimaryMonitorForRegion(PhysicalRectangle region)
    {
        var intersecting = GetMonitorsIntersecting(region);
        return intersecting.FirstOrDefault() ?? _primaryMonitor;
    }

    /// <summary>
    /// Find the nearest monitor to a given logical point (for out-of-bounds handling).
    /// </summary>
    public MonitorInfo FindNearestMonitor(LogicalPoint point)
    {
        return _monitors
            .OrderBy(m => DistanceToMonitor(point, m))
            .First();
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Calculate virtual desktop bounds (union of all monitors).
    /// </summary>
    private static PhysicalRectangle CalculateVirtualDesktopBounds(MonitorInfo[] monitors)
    {
        int minX = monitors.Min(m => m.Bounds.X);
        int minY = monitors.Min(m => m.Bounds.Y);
        int maxX = monitors.Max(m => m.Bounds.X + m.Bounds.Width);
        int maxY = monitors.Max(m => m.Bounds.Y + m.Bounds.Height);

        return new PhysicalRectangle(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Get logical origin of a monitor relative to primary monitor.
    /// This handles the complexity of mixed-DPI setups.
    /// </summary>
    private LogicalPoint GetMonitorLogicalOrigin(MonitorInfo monitor)
    {
        if (monitor.IsPrimary)
            return new LogicalPoint(0, 0);

        // Calculate by converting physical offset to logical
        // using the primary monitor's scale factor for the offset conversion
        var physicalOffset = new PhysicalPoint(
            monitor.Bounds.X - _primaryMonitor.Bounds.X,
            monitor.Bounds.Y - _primaryMonitor.Bounds.Y);

        // Use primary monitor's scale for offset conversion
        // This matches Avalonia's coordinate system behavior
        return new LogicalPoint(
            physicalOffset.X / _primaryMonitor.ScaleFactor,
            physicalOffset.Y / _primaryMonitor.ScaleFactor);
    }

    /// <summary>
    /// Get logical bounds of a monitor.
    /// </summary>
    private LogicalRectangle GetMonitorLogicalBounds(MonitorInfo monitor)
    {
        var origin = GetMonitorLogicalOrigin(monitor);

        return new LogicalRectangle(
            origin.X,
            origin.Y,
            monitor.Bounds.Width / monitor.ScaleFactor,
            monitor.Bounds.Height / monitor.ScaleFactor);
    }

    /// <summary>
    /// Calculate distance from a logical point to the nearest edge of a monitor.
    /// Used for finding the nearest monitor when a point is outside all monitors.
    /// </summary>
    private double DistanceToMonitor(LogicalPoint point, MonitorInfo monitor)
    {
        var bounds = GetMonitorLogicalBounds(monitor);

        // Calculate distance to nearest edge
        double dx = Math.Max(bounds.X - point.X,
                            Math.Max(0, point.X - (bounds.X + bounds.Width)));
        double dy = Math.Max(bounds.Y - point.Y,
                            Math.Max(0, point.Y - (bounds.Y + bounds.Height)));

        return Math.Sqrt(dx * dx + dy * dy);
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validate that a physical rectangle is suitable for capture.
    /// </summary>
    /// <exception cref="ArgumentException">Invalid region.</exception>
    public void ValidateCaptureRegion(PhysicalRectangle region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            throw new ArgumentException($"Invalid region size: {region.Width}×{region.Height}");

        if (region.Width > 16384 || region.Height > 16384)
            throw new ArgumentException($"Region too large: {region.Width}×{region.Height} (max 16384)");

        bool intersectsAny = _monitors.Any(m =>
            region.Intersect(m.Bounds) != null);

        if (!intersectsAny)
            throw new ArgumentException($"Region {region} does not intersect any monitor");
    }

    /// <summary>
    /// Test round-trip conversion accuracy for debugging.
    /// Returns the error distance in pixels.
    /// </summary>
    public double TestRoundTripAccuracy(PhysicalPoint original)
    {
        var logical = PhysicalToLogical(original);
        var roundTrip = LogicalToPhysical(logical);

        return Math.Sqrt(
            Math.Pow(original.X - roundTrip.X, 2) +
            Math.Pow(original.Y - roundTrip.Y, 2));
    }

    #endregion
}
