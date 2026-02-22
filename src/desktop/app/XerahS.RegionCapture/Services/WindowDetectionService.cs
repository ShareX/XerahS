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
using System.Runtime.CompilerServices;
using XerahS.RegionCapture.Models;

namespace XerahS.RegionCapture.Services;

/// <summary>
/// High-performance window detection service using spatial indexing.
/// Provides instant window detection under the cursor for smart snapping.
/// </summary>
public sealed class WindowDetectionService
{
    private IReadOnlyList<WindowInfo> _windows = [];
    private readonly object _lock = new();
    private DateTime _lastRefresh = DateTime.MinValue;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets the list of visible windows, refreshing if stale.
    /// </summary>
    private volatile bool _isRefreshing;
    private readonly object _refreshLock = new();

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
#if WINDOWS
                var windows = Platform.Windows.NativeWindowService.EnumerateVisibleWindows();
#else
                var windows = new List<WindowInfo>();
#endif
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
#if WINDOWS
            _windows = Platform.Windows.NativeWindowService.EnumerateVisibleWindows();
#else
            _windows = [];
#endif
            _lastRefresh = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Gets the topmost window at the specified physical point.
    /// Uses Z-order to determine which window is visually on top.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WindowInfo? GetWindowAtPoint(PixelPoint physicalPoint)
    {
        // Direct lookup - sorted by Z-order (topmost first)
        foreach (var window in Windows)
        {
            if (window.SnapBounds.Contains(physicalPoint))
            {
                return window;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all windows that intersect with the specified region.
    /// Useful for determining which windows a selection covers.
    /// </summary>
    public IEnumerable<WindowInfo> GetWindowsInRegion(PixelRect region)
    {
        foreach (var window in Windows)
        {
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
