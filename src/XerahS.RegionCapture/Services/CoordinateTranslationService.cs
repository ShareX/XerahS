using System.Runtime.CompilerServices;
using XerahS.RegionCapture.Models;

namespace XerahS.RegionCapture.Services;

/// <summary>
/// Service for translating coordinates between logical and physical pixel spaces
/// across monitors with different DPI scaling.
///
/// This is the "Truth Layer" - all coordinates are normalized to physical pixels
/// to solve the "Logical vs. Physical" pixel mismatch in mixed-DPI environments.
/// </summary>
public sealed class CoordinateTranslationService
{
    private IReadOnlyList<MonitorInfo>? _monitors;

    /// <summary>
    /// Gets the cached list of monitors, refreshing if needed.
    /// </summary>
    public IReadOnlyList<MonitorInfo> Monitors => _monitors ??= MonitorEnumerationService.GetAllMonitors();

    /// <summary>
    /// Refreshes the monitor cache.
    /// </summary>
    public void RefreshMonitors()
    {
        _monitors = MonitorEnumerationService.GetAllMonitors();
    }

    /// <summary>
    /// Gets the current physical cursor position (not affected by DPI virtualization).
    /// This is the key to solving mixed-DPI coordinate issues.
    /// </summary>
    public PixelPoint GetPhysicalCursorPosition()
    {
#if WINDOWS
        return Platform.Windows.NativeMonitorService.GetPhysicalCursorPosition();
#else
        // On non-Windows platforms, we need to use Avalonia's cursor position
        // and manually convert based on the screen the cursor is on
        return GetFallbackCursorPosition();
#endif
    }

    private PixelPoint GetFallbackCursorPosition()
    {
        // This is a fallback for non-Windows platforms
        // In a real implementation, this would use platform-specific APIs
        return PixelPoint.Origin;
    }

    /// <summary>
    /// Finds the monitor containing the specified physical point.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MonitorInfo? GetMonitorAt(PixelPoint physicalPoint)
    {
        foreach (var monitor in Monitors)
        {
            if (monitor.PhysicalBounds.Contains(physicalPoint))
                return monitor;
        }

        // Fallback: find nearest monitor
        return GetNearestMonitor(physicalPoint);
    }

    /// <summary>
    /// Finds the monitor nearest to the specified physical point.
    /// </summary>
    public MonitorInfo? GetNearestMonitor(PixelPoint physicalPoint)
    {
        MonitorInfo? nearest = null;
        double minDistance = double.MaxValue;

        foreach (var monitor in Monitors)
        {
            var center = monitor.PhysicalBounds.Center;
            var distance = physicalPoint.DistanceSquaredTo(center);

            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = monitor;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Gets the scale factor for a physical point (DPI at that location).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetScaleFactorAt(PixelPoint physicalPoint)
    {
        var monitor = GetMonitorAt(physicalPoint);
        return monitor?.ScaleFactor ?? 1.0;
    }

    /// <summary>
    /// Converts a physical point to logical coordinates on the specified monitor.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (double X, double Y) PhysicalToLogical(PixelPoint physical, MonitorInfo monitor)
    {
        return (
            (physical.X - monitor.PhysicalBounds.X) / monitor.ScaleFactor,
            (physical.Y - monitor.PhysicalBounds.Y) / monitor.ScaleFactor);
    }

    /// <summary>
    /// Converts logical coordinates on a monitor to physical coordinates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PixelPoint LogicalToPhysical(double logicalX, double logicalY, MonitorInfo monitor)
    {
        return new PixelPoint(
            logicalX * monitor.ScaleFactor + monitor.PhysicalBounds.X,
            logicalY * monitor.ScaleFactor + monitor.PhysicalBounds.Y);
    }

    /// <summary>
    /// Converts a physical rectangle to logical coordinates on a specific monitor.
    /// </summary>
    public (double X, double Y, double Width, double Height) PhysicalToLogical(PixelRect physical, MonitorInfo monitor)
    {
        return (
            (physical.X - monitor.PhysicalBounds.X) / monitor.ScaleFactor,
            (physical.Y - monitor.PhysicalBounds.Y) / monitor.ScaleFactor,
            physical.Width / monitor.ScaleFactor,
            physical.Height / monitor.ScaleFactor);
    }

    /// <summary>
    /// Converts a logical rectangle on a monitor to physical coordinates.
    /// </summary>
    public PixelRect LogicalToPhysical(double x, double y, double width, double height, MonitorInfo monitor)
    {
        return new PixelRect(
            x * monitor.ScaleFactor + monitor.PhysicalBounds.X,
            y * monitor.ScaleFactor + monitor.PhysicalBounds.Y,
            width * monitor.ScaleFactor,
            height * monitor.ScaleFactor);
    }

    /// <summary>
    /// Maps a physical point from one monitor's context to another.
    /// This is critical for cross-monitor selection operations.
    /// </summary>
    public PixelPoint MapBetweenMonitors(PixelPoint physical, MonitorInfo from, MonitorInfo to)
    {
        // Convert to logical coordinates on source monitor
        var (logX, logY) = PhysicalToLogical(physical, from);

        // Convert back to physical on target monitor
        return LogicalToPhysical(logX, logY, to);
    }

    /// <summary>
    /// Normalizes coordinates to ensure they're within the virtual screen bounds.
    /// </summary>
    public PixelPoint ClampToVirtualScreen(PixelPoint physical)
    {
        var bounds = GetVirtualScreenBounds();
        return new PixelPoint(
            Math.Clamp(physical.X, bounds.Left, bounds.Right - 1),
            Math.Clamp(physical.Y, bounds.Top, bounds.Bottom - 1));
    }

    /// <summary>
    /// Gets the combined virtual screen bounds in physical pixels.
    /// </summary>
    public PixelRect GetVirtualScreenBounds()
    {
        if (Monitors.Count == 0)
            return PixelRect.Empty;

        var result = Monitors[0].PhysicalBounds;

        for (int i = 1; i < Monitors.Count; i++)
        {
            result = result.Union(Monitors[i].PhysicalBounds);
        }

        return result;
    }

    /// <summary>
    /// Finds all monitors that intersect with the given physical rectangle.
    /// Useful for selection operations that span multiple monitors.
    /// </summary>
    public IEnumerable<MonitorInfo> GetIntersectingMonitors(PixelRect physical)
    {
        foreach (var monitor in Monitors)
        {
            if (monitor.PhysicalBounds.IntersectsWith(physical))
                yield return monitor;
        }
    }

    /// <summary>
    /// Gets the portion of a physical rectangle that falls within a specific monitor.
    /// </summary>
    public PixelRect GetMonitorIntersection(PixelRect physical, MonitorInfo monitor)
    {
        return physical.Intersect(monitor.PhysicalBounds);
    }
}
