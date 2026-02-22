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
using XerahS.RegionCapture.Models;
using XerahS.RegionCapture;
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
    private readonly TaskCompletionSource<RegionSelectionResult?> _completionSource;
    private readonly CoordinateTranslationService _coordinateService;
    private bool _disposed;

    public OverlayManager()
    {
        _completionSource = new TaskCompletionSource<RegionSelectionResult?>();
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
    /// <summary>
    /// Creates and shows overlay windows for all monitors.
    /// </summary>
    public async Task<RegionSelectionResult?> ShowOverlaysAsync(
        Action<PixelRect>? onSelectionChanged = null,
        XerahS.Platform.Abstractions.CursorInfo? initialCursor = null,
        RegionCaptureOptions? options = null)
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
                var overlay = new OverlayWindow(monitor, _completionSource, onSelectionChanged, initialCursor, options);
                _overlays.Add(overlay);
            }

            // Show all overlays simultaneously
            foreach (var overlay in _overlays)
            {
                overlay.Show();
                overlay.Activate();

#if WINDOWS
                if (overlay.TryGetPlatformHandle()?.Handle is { } handle)
                {
                    Platform.Windows.NativeWindowService.ExcludeHandle(handle);
                }
#endif
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
#if WINDOWS
            if (overlay.TryGetPlatformHandle()?.Handle is { } handle)
            {
                Platform.Windows.NativeWindowService.RemoveExcludedHandle(handle);
            }
#endif

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
