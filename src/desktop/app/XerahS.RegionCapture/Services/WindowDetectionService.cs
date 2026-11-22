#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)
using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using XerahS.RegionCapture.Models;
using PlatformLogicalWindowPointQueryService = XerahS.Platform.Abstractions.ILogicalWindowPointQueryService;
using PlatformServices = XerahS.Platform.Abstractions.PlatformServices;
using PlatformWindowPointQueryCapability = XerahS.Platform.Abstractions.WindowPointQueryCapability;
using PlatformWindowPointQuerySupportLevel = XerahS.Platform.Abstractions.WindowPointQuerySupportLevel;
using PlatformWindowTitles = XerahS.Platform.Abstractions.PlatformWindowTitles;
using PlatformWindowInfo = XerahS.Platform.Abstractions.WindowInfo;

namespace XerahS.RegionCapture.Services;

internal enum WindowPreselectionSupportLevel
{
    Full,
    Partial,
    Unsupported
}

internal readonly record struct WindowPreselectionCapability(
    WindowPreselectionSupportLevel Level,
    string? UserMessage)
{
    public bool IsEnabled => Level != WindowPreselectionSupportLevel.Unsupported;
}

internal readonly record struct WindowPointQueryResult(
    bool Handled,
    WindowInfo? Window);

/// <summary>
/// High-performance window detection service using spatial indexing.
/// Provides instant window detection under the cursor for smart snapping.
/// </summary>
public sealed class WindowDetectionService
{
    private static readonly Lock ExcludedHandlesLock = new();
    private static readonly HashSet<nint> ExcludedHandles = [];
    private IReadOnlyList<WindowInfo> _windows = [];
    private readonly Lock _lock = new();
    private DateTime _lastRefresh = DateTime.MinValue;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan DirectQueryRefreshInterval = TimeSpan.FromMilliseconds(75);
    private readonly Func<IReadOnlyList<WindowInfo>> _enumerateVisibleWindows;
    private readonly Func<PixelPoint, WindowPointQueryResult> _tryGetWindowAtPointDirect;

    /// <summary>
    /// Gets the list of visible windows, refreshing if stale.
    /// </summary>
    private volatile bool _isRefreshing;
    private readonly object _refreshLock = new();
    private readonly Lock _directQueryLock = new();
    private DateTime _lastDirectQueryAt = DateTime.MinValue;
    private WindowInfo? _lastDirectQueryWindow;
    private PixelPoint _lastDirectQueryPoint;
    private IReadOnlyList<MonitorInfo>? _cachedDirectQueryMonitors;

    public WindowDetectionService(bool detectControls = true)
    {
        _enumerateVisibleWindows = () => EnumerateVisibleWindows(detectControls);
        _tryGetWindowAtPointDirect = TryGetDirectWindowAtPointCached;
    }

    internal WindowDetectionService(
        Func<IReadOnlyList<WindowInfo>> enumerateVisibleWindows,
        Func<PixelPoint, WindowPointQueryResult>? tryGetWindowAtPointDirect = null)
    {
        _enumerateVisibleWindows = enumerateVisibleWindows ?? throw new ArgumentNullException(nameof(enumerateVisibleWindows));
        _tryGetWindowAtPointDirect = tryGetWindowAtPointDirect ?? (_ => default);
    }

    /// <summary>
    /// Registers a native overlay handle so hover detection ignores the overlay itself.
    /// </summary>
    public static void ExcludeHandle(nint handle)
    {
        if (handle == 0)
            return;

        lock (ExcludedHandlesLock)
        {
            ExcludedHandles.Add(handle);
        }
    }

    /// <summary>
    /// Removes a native overlay handle from the exclusion list.
    /// </summary>
    public static void RemoveExcludedHandle(nint handle)
    {
        if (handle == 0)
            return;

        lock (ExcludedHandlesLock)
        {
            ExcludedHandles.Remove(handle);
        }
    }

    /// <summary>
    /// Gets the list of visible windows, refreshing if stale.
    /// </summary>
    public IReadOnlyList<WindowInfo> Windows
    {
        get
        {
            var timeSinceLastRefresh = DateTime.UtcNow - _lastRefresh;
            if (timeSinceLastRefresh > RefreshInterval && !_isRefreshing)
            {
                // Refresh asynchronously to not block the UI thread
                RefreshWindowsAsync();
            }
            return _windows;
        }
    }

    private void RefreshWindowsAsync()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;

        Task.Run(() =>
        {
            try
            {
                var windows = _enumerateVisibleWindows();
                lock (_lock)
                {
                    _windows = windows;
                    _lastRefresh = DateTime.UtcNow;
                }
            }
            catch
            {
                // Ignore errors during enumeration
            }
            finally
            {
                _isRefreshing = false;
            }
        });
    }

    /// <summary>
    /// Forces a refresh of the window list (synchronous).
    /// </summary>
    public void RefreshWindows()
    {
        lock (_lock)
        {
            _windows = _enumerateVisibleWindows();
            _lastRefresh = DateTime.UtcNow;
        }
    }

    internal static WindowPreselectionCapability GetWindowPreselectionCapability()
    {
        if (!OperatingSystem.IsLinux())
        {
            return new(WindowPreselectionSupportLevel.Full, null);
        }

        bool isWaylandSession = MonitorEnumerationService.IsWaylandSession();
        bool hasX11Display = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"));
        string? compositor = DetectLinuxWaylandCompositor();
        PlatformWindowPointQueryCapability? directCapability = null;

        try
        {
            directCapability = PlatformServices.Window is PlatformLogicalWindowPointQueryService logicalPointQueryService
                ? logicalPointQueryService.GetLogicalWindowPointQueryCapability()
                : null;
        }
        catch (InvalidOperationException)
        {
            // Platform services are not always initialized in design-time/headless smoke tests.
            // Fall back to compositor/env-based capability detection instead of crashing UI construction.
        }

        return GetLinuxWindowPreselectionCapability(isWaylandSession, hasX11Display, compositor, IsLinuxCommandAvailable, directCapability);
    }

    internal static WindowPreselectionCapability GetLinuxWindowPreselectionCapability(
        bool isWaylandSession,
        bool hasX11Display)
    {
        return GetLinuxWindowPreselectionCapability(
            isWaylandSession,
            hasX11Display,
            compositor: null,
            IsLinuxCommandAvailable);
    }

    internal static WindowPreselectionCapability GetLinuxWindowPreselectionCapability(
        bool isWaylandSession,
        bool hasX11Display,
        string? compositor,
        Func<string, bool>? isCommandAvailable,
        PlatformWindowPointQueryCapability? directCapability = null)
    {
        if (!isWaylandSession)
        {
            return new(WindowPreselectionSupportLevel.Full, null);
        }

        if (directCapability is { IsEnabled: true })
        {
            return directCapability.Value.Level switch
            {
                PlatformWindowPointQuerySupportLevel.Partial => new(
                    WindowPreselectionSupportLevel.Partial,
                    directCapability.Value.UserMessage),
                _ => new(WindowPreselectionSupportLevel.Full, directCapability.Value.UserMessage)
            };
        }

        var helperProvider = DetectWaylandHelperProvider(compositor, isCommandAvailable ?? IsLinuxCommandAvailable);
        if (helperProvider is not null)
        {
            return new(WindowPreselectionSupportLevel.Full, null);
        }

        if (hasX11Display)
        {
            return new(
                WindowPreselectionSupportLevel.Partial,
                directCapability?.UserMessage is { Length: > 0 } message
                    ? $"{message} Only X11/XWayland windows can be snapped."
                    : "Wayland session: native window snapping helper is unavailable; only X11/XWayland windows can be snapped.");
        }

        return new(
            WindowPreselectionSupportLevel.Unsupported,
            directCapability?.UserMessage ?? "Wayland session: native window snapping helper is unavailable on this compositor.");
    }

    internal static IReadOnlyList<WindowInfo> EnumerateVisibleWindows(bool includeControls = true)
    {
#if WINDOWS
        return FilterExcludedWindows(Platform.Windows.NativeWindowService.EnumerateVisibleWindows(includeControls));
#else
        _ = includeControls;
        try
        {
            return ConvertPlatformWindows(XerahS.Platform.Abstractions.PlatformServices.Window.GetAllWindows());
        }
        catch (InvalidOperationException)
        {
            return [];
        }
#endif
    }

    internal static IReadOnlyList<WindowInfo> ConvertPlatformWindows(IEnumerable<PlatformWindowInfo> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);

        var visibleWindows = new List<WindowInfo>();
        int zOrder = 0;

        foreach (var window in windows)
        {
            if (!ShouldIncludePlatformWindow(window))
                continue;

            var bounds = ToPixelRect(window.Bounds);
            visibleWindows.Add(new WindowInfo(
                Handle: window.Handle,
                Title: window.Title,
                ClassName: window.ClassName,
                Bounds: bounds,
                VisualBounds: bounds,
                IsMinimized: window.IsMinimized,
                ZOrder: zOrder++));
        }

        return visibleWindows;
    }

    internal static bool ShouldIncludePlatformWindow(PlatformWindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.Handle == IntPtr.Zero || IsExcludedHandle(window.Handle))
            return false;

        if (!window.IsVisible || window.IsMinimized)
            return false;

        if (string.IsNullOrWhiteSpace(window.Title))
            return false;

        return window.Bounds.Width > 1 && window.Bounds.Height > 1;
    }

    private static IReadOnlyList<WindowInfo> FilterExcludedWindows(IEnumerable<WindowInfo> windows)
    {
        return windows.Where(window => !IsExcludedHandle(window.Handle)).ToArray();
    }

    private static bool IsExcludedHandle(nint handle)
    {
        if (handle == 0)
            return false;

        lock (ExcludedHandlesLock)
        {
            return ExcludedHandles.Contains(handle);
        }
    }

    private static string? DetectLinuxWaylandCompositor()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE")))
        {
            return "HYPRLAND";
        }

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SWAYSOCK")))
        {
            return "SWAY";
        }

        foreach (string hint in EnumerateDesktopHints())
        {
            foreach (string token in hint.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string normalized = token.ToUpperInvariant();

                if (normalized.Contains("HYPRLAND", StringComparison.Ordinal))
                {
                    return "HYPRLAND";
                }

                if (normalized.Contains("SWAY", StringComparison.Ordinal))
                {
                    return "SWAY";
                }

                if (normalized.Contains("KDE", StringComparison.Ordinal) ||
                    normalized.Contains("PLASMA", StringComparison.Ordinal))
                {
                    return "KDE";
                }

                if (normalized.Contains("GNOME", StringComparison.Ordinal) ||
                    normalized.Contains("UBUNTU", StringComparison.Ordinal) ||
                    normalized.Contains("UNITY", StringComparison.Ordinal) ||
                    normalized.Contains("BUDGIE", StringComparison.Ordinal) ||
                    normalized.Contains("PANTHEON", StringComparison.Ordinal))
                {
                    return "GNOME";
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            return "WAYLAND";
        }

        return null;
    }

    private static IEnumerable<string> EnumerateDesktopHints()
    {
        string? currentDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        if (!string.IsNullOrWhiteSpace(currentDesktop))
        {
            yield return currentDesktop;
        }

        string? sessionDesktop = Environment.GetEnvironmentVariable("XDG_SESSION_DESKTOP");
        if (!string.IsNullOrWhiteSpace(sessionDesktop))
        {
            yield return sessionDesktop;
        }

        string? desktopSession = Environment.GetEnvironmentVariable("DESKTOP_SESSION");
        if (!string.IsNullOrWhiteSpace(desktopSession))
        {
            yield return desktopSession;
        }
    }

    private static string? DetectWaylandHelperProvider(
        string? compositor,
        Func<string, bool> isCommandAvailable)
    {
        ArgumentNullException.ThrowIfNull(isCommandAvailable);

        return compositor?.ToUpperInvariant() switch
        {
            "HYPRLAND" when isCommandAvailable("hyprctl") => "hyprland",
            "SWAY" when isCommandAvailable("swaymsg") => "sway",
            _ => null
        };
    }

    private static bool IsLinuxCommandAvailable(string commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return false;
        }

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate = Path.Combine(directory, commandName);
            if (File.Exists(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private WindowPointQueryResult TryGetDirectWindowAtPointCached(PixelPoint physicalPoint)
    {
        if (!OperatingSystem.IsLinux() || !MonitorEnumerationService.IsAvaloniaWaylandBackend())
            return default;

        if (!PlatformServices.IsWindowServiceInitialized ||
            PlatformServices.Window is not PlatformLogicalWindowPointQueryService logicalPointQueryService)
            return default;

        if (!logicalPointQueryService.GetLogicalWindowPointQueryCapability().IsEnabled)
            return default;

        _cachedDirectQueryMonitors ??= MonitorEnumerationService.GetAllMonitors();
        return TryGetDirectWindowAtPointWithMonitors(physicalPoint, _cachedDirectQueryMonitors, logicalPointQueryService);
    }

    internal static WindowPointQueryResult TryGetDirectWindowAtPoint(PixelPoint physicalPoint)
    {
        if (!OperatingSystem.IsLinux() || !MonitorEnumerationService.IsAvaloniaWaylandBackend())
            return default;

        // Pointer tracking runs per mouse move; skip the query instead of throwing when the
        // window service is not registered (headless hosts and UI smoke tests).
        if (!PlatformServices.IsWindowServiceInitialized ||
            PlatformServices.Window is not PlatformLogicalWindowPointQueryService logicalPointQueryService)
            return default;

        if (!logicalPointQueryService.GetLogicalWindowPointQueryCapability().IsEnabled)
            return default;

        return TryGetDirectWindowAtPointWithMonitors(
            physicalPoint,
            MonitorEnumerationService.GetAllMonitors(),
            logicalPointQueryService);
    }

    internal static WindowPointQueryResult TryGetDirectWindowAtPoint(
        PixelPoint physicalPoint,
        IReadOnlyList<MonitorInfo> monitors)
    {
        if (!OperatingSystem.IsLinux() || !MonitorEnumerationService.IsAvaloniaWaylandBackend())
            return default;

        if (!PlatformServices.IsWindowServiceInitialized ||
            PlatformServices.Window is not PlatformLogicalWindowPointQueryService logicalPointQueryService)
            return default;

        if (!logicalPointQueryService.GetLogicalWindowPointQueryCapability().IsEnabled)
            return default;

        return TryGetDirectWindowAtPointWithMonitors(physicalPoint, monitors, logicalPointQueryService);
    }

    private static WindowPointQueryResult TryGetDirectWindowAtPointWithMonitors(
        PixelPoint physicalPoint,
        IReadOnlyList<MonitorInfo> monitors,
        PlatformLogicalWindowPointQueryService logicalPointQueryService)
    {
        if (!TryConvertPhysicalToLogicalPoint(physicalPoint, monitors, out Point logicalPoint))
            return new WindowPointQueryResult(Handled: true, Window: null);

        PlatformWindowInfo? platformWindow = logicalPointQueryService.GetWindowAtLogicalPoint(logicalPoint);
        if (platformWindow == null)
            return new WindowPointQueryResult(Handled: true, Window: null);

        return new WindowPointQueryResult(
            Handled: true,
            Window: ConvertLogicalPlatformWindow(platformWindow, monitors));
    }

    internal static bool TryConvertPhysicalToLogicalPoint(
        PixelPoint physicalPoint,
        IReadOnlyList<MonitorInfo> monitors,
        out Point logicalPoint)
    {
        logicalPoint = Point.Empty;
        MonitorInfo? monitor = FindMonitorForPhysicalPoint(monitors, physicalPoint.X, physicalPoint.Y, inclusive: true);
        if (monitor == null || monitor.ScaleFactor <= 0)
            return false;

        logicalPoint = new Point(
            (int)Math.Round(monitor.OverlayBounds.X + ((physicalPoint.X - monitor.PhysicalBounds.X) / monitor.ScaleFactor)),
            (int)Math.Round(monitor.OverlayBounds.Y + ((physicalPoint.Y - monitor.PhysicalBounds.Y) / monitor.ScaleFactor)));
        return true;
    }

    internal static WindowInfo? ConvertLogicalPlatformWindow(
        PlatformWindowInfo window,
        IReadOnlyList<MonitorInfo> monitors)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.Handle == IntPtr.Zero || IsExcludedHandle(window.Handle))
            return null;

        if (!window.IsVisible || window.IsMinimized)
            return null;

        if (window.Bounds.Width <= 1 || window.Bounds.Height <= 1)
            return null;

        var bounds = ConvertLogicalRectToPhysical(window.Bounds, monitors);
        if (bounds.IsEmpty)
            return null;

        string title = string.IsNullOrWhiteSpace(window.Title) ? window.ClassName : window.Title;
        if (string.Equals(title, PlatformWindowTitles.RegionCaptureOverlay, StringComparison.Ordinal))
            return null;

        return new WindowInfo(
            Handle: window.Handle,
            Title: title,
            ClassName: window.ClassName,
            Bounds: bounds,
            VisualBounds: bounds,
            IsMinimized: window.IsMinimized,
            ZOrder: 0);
    }

    internal static PixelRect ConvertLogicalRectToPhysical(
        Rectangle logicalBounds,
        IReadOnlyList<MonitorInfo> monitors)
    {
        if (!TryConvertLogicalToPhysicalPoint(
                new PixelPoint(logicalBounds.Left, logicalBounds.Top),
                monitors,
                inclusive: true,
                out PixelPoint topLeft))
        {
            return PixelRect.Empty;
        }

        if (!TryConvertLogicalToPhysicalPoint(
                new PixelPoint(logicalBounds.Right, logicalBounds.Bottom),
                monitors,
                inclusive: true,
                out PixelPoint bottomRight))
        {
            return PixelRect.Empty;
        }

        return new PixelRect(
            topLeft.X,
            topLeft.Y,
            Math.Max(0, bottomRight.X - topLeft.X),
            Math.Max(0, bottomRight.Y - topLeft.Y));
    }

    private static bool TryConvertLogicalToPhysicalPoint(
        PixelPoint logicalPoint,
        IReadOnlyList<MonitorInfo> monitors,
        bool inclusive,
        out PixelPoint physicalPoint)
    {
        physicalPoint = default;
        MonitorInfo? monitor = FindMonitorForLogicalPoint(monitors, logicalPoint.X, logicalPoint.Y, inclusive);
        if (monitor == null || monitor.ScaleFactor <= 0)
            return false;

        physicalPoint = new PixelPoint(
            monitor.PhysicalBounds.X + ((logicalPoint.X - monitor.OverlayBounds.X) * monitor.ScaleFactor),
            monitor.PhysicalBounds.Y + ((logicalPoint.Y - monitor.OverlayBounds.Y) * monitor.ScaleFactor));
        return true;
    }

    private static MonitorInfo? FindMonitorForPhysicalPoint(
        IReadOnlyList<MonitorInfo> monitors,
        double x,
        double y,
        bool inclusive)
    {
        return monitors.FirstOrDefault(monitor => ContainsPoint(monitor.PhysicalBounds, x, y, inclusive))
            ?? monitors.FirstOrDefault();
    }

    private static MonitorInfo? FindMonitorForLogicalPoint(
        IReadOnlyList<MonitorInfo> monitors,
        double x,
        double y,
        bool inclusive)
    {
        return monitors.FirstOrDefault(monitor => ContainsPoint(monitor.OverlayBounds, x, y, inclusive))
            ?? monitors.FirstOrDefault();
    }

    private static bool ContainsPoint(PixelRect bounds, double x, double y, bool inclusive)
    {
        if (inclusive)
        {
            return x >= bounds.Left && x <= bounds.Right &&
                   y >= bounds.Top && y <= bounds.Bottom;
        }

        return bounds.Contains(x, y);
    }

    private static PixelRect ToPixelRect(System.Drawing.Rectangle bounds)
    {
        return new PixelRect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    /// <summary>
    /// Gets the topmost window at the specified physical point.
    /// Uses Z-order to determine which window is visually on top.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WindowInfo? GetWindowAtPoint(PixelPoint physicalPoint)
    {
        var directQueryResult = GetCachedDirectWindowAtPoint(physicalPoint);
        if (directQueryResult.Handled)
        {
            return directQueryResult.Window;
        }

        // Direct lookup - sorted by Z-order (topmost first)
        foreach (var window in Windows)
        {
            if (IsExcludedHandle(window.Handle))
                continue;

            if (window.SnapBounds.Contains(physicalPoint))
            {
                return window;
            }
        }

        return null;
    }

    private WindowPointQueryResult GetCachedDirectWindowAtPoint(PixelPoint physicalPoint)
    {
        lock (_directQueryLock)
        {
            if (_lastDirectQueryAt != DateTime.MinValue &&
                DateTime.UtcNow - _lastDirectQueryAt <= DirectQueryRefreshInterval)
            {
                if (_lastDirectQueryWindow?.SnapBounds.Contains(physicalPoint) == true && !IsExcludedHandle(_lastDirectQueryWindow.Handle))
                {
                    return new WindowPointQueryResult(Handled: true, Window: _lastDirectQueryWindow);
                }

                if (_lastDirectQueryWindow == null &&
                    Math.Abs(_lastDirectQueryPoint.X - physicalPoint.X) < 2 &&
                    Math.Abs(_lastDirectQueryPoint.Y - physicalPoint.Y) < 2)
                {
                    return new WindowPointQueryResult(Handled: true, Window: null);
                }
            }
        }

        var result = _tryGetWindowAtPointDirect(physicalPoint);
        if (!result.Handled)
            return default;

        lock (_directQueryLock)
        {
            _lastDirectQueryAt = DateTime.UtcNow;
            _lastDirectQueryWindow = result.Window;
            _lastDirectQueryPoint = physicalPoint;
        }

        return result;
    }

    /// <summary>
    /// Gets all windows that intersect with the specified region.
    /// Useful for determining which windows a selection covers.
    /// </summary>
    public IEnumerable<WindowInfo> GetWindowsInRegion(PixelRect region)
    {
        foreach (var window in Windows)
        {
            if (IsExcludedHandle(window.Handle))
                continue;

            if (window.SnapBounds.IntersectsWith(region))
            {
                yield return window;
            }
        }
    }

    /// <summary>
    /// Finds windows near a point within a specified radius.
    /// Useful for snap-to-edge functionality.
    /// </summary>
    public IEnumerable<(WindowInfo Window, double Distance)> GetWindowsNearPoint(PixelPoint point, double radius)
    {
        var radiusSquared = radius * radius;

        foreach (var window in Windows)
        {
            if (IsExcludedHandle(window.Handle))
                continue;

            var bounds = window.SnapBounds;

            // Calculate distance to nearest edge
            var nearestX = Math.Clamp(point.X, bounds.Left, bounds.Right);
            var nearestY = Math.Clamp(point.Y, bounds.Top, bounds.Bottom);

            var dx = point.X - nearestX;
            var dy = point.Y - nearestY;
            var distanceSquared = dx * dx + dy * dy;

            if (distanceSquared <= radiusSquared)
            {
                yield return (window, Math.Sqrt(distanceSquared));
            }
        }
    }

    /// <summary>
    /// Gets snap edges from nearby windows for edge snapping behavior.
    /// </summary>
    public SnapEdges GetSnapEdges(PixelPoint cursorPosition, double snapDistance)
    {
        var edges = new SnapEdges();

        foreach (var window in Windows)
        {
            if (IsExcludedHandle(window.Handle))
                continue;

            var bounds = window.SnapBounds;

            // Check left edge
            if (Math.Abs(cursorPosition.X - bounds.Left) <= snapDistance)
                edges.VerticalEdges.Add(bounds.Left);

            // Check right edge
            if (Math.Abs(cursorPosition.X - bounds.Right) <= snapDistance)
                edges.VerticalEdges.Add(bounds.Right);

            // Check top edge
            if (Math.Abs(cursorPosition.Y - bounds.Top) <= snapDistance)
                edges.HorizontalEdges.Add(bounds.Top);

            // Check bottom edge
            if (Math.Abs(cursorPosition.Y - bounds.Bottom) <= snapDistance)
                edges.HorizontalEdges.Add(bounds.Bottom);
        }

        return edges;
    }
}

/// <summary>
/// Contains horizontal and vertical snap edges from nearby windows.
/// </summary>
public sealed class SnapEdges
{
    public List<double> HorizontalEdges { get; } = [];
    public List<double> VerticalEdges { get; } = [];

    /// <summary>
    /// Gets the nearest horizontal edge to the specified Y coordinate.
    /// </summary>
    public double? GetNearestHorizontal(double y, double maxDistance)
    {
        double? nearest = null;
        double minDist = maxDistance;

        foreach (var edge in HorizontalEdges)
        {
            var dist = Math.Abs(edge - y);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = edge;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Gets the nearest vertical edge to the specified X coordinate.
    /// </summary>
    public double? GetNearestVertical(double x, double maxDistance)
    {
        double? nearest = null;
        double minDist = maxDistance;

        foreach (var edge in VerticalEdges)
        {
            var dist = Math.Abs(edge - x);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = edge;
            }
        }

        return nearest;
    }
}
