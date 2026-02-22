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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using XerahS.RegionCapture.Models;
using PixelRect = XerahS.RegionCapture.Models.PixelRect;

namespace XerahS.RegionCapture.Services;

/// <summary>
/// Service for enumerating physical monitors using Avalonia's cross-platform API.
/// On Windows, enhanced with native DPI detection for sub-pixel precision.
/// </summary>
public static class MonitorEnumerationService
{
    /// <summary>
    /// Gets information about all connected monitors.
    /// </summary>
    public static IReadOnlyList<MonitorInfo> GetAllMonitors()
    {
#if WINDOWS
        return GetWindowsMonitors();
#else
        return GetAvaloniaMonitors();
#endif
    }

    private static IReadOnlyList<MonitorInfo> GetAvaloniaMonitors()
    {
        var screens = Application.Current?.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop
                => desktop.MainWindow?.Screens,
            _ => null
        };

        if (screens is null)
            return [];

        var monitors = new List<MonitorInfo>();
        var allScreens = screens.All;

        for (int i = 0; i < allScreens.Count; i++)
        {
            var screen = allScreens[i];
            var scaleFactor = screen.Scaling;

            var physicalBounds = new PixelRect(
                screen.Bounds.X,
                screen.Bounds.Y,
                screen.Bounds.Width,
                screen.Bounds.Height);

            var workArea = new PixelRect(
                screen.WorkingArea.X,
                screen.WorkingArea.Y,
                screen.WorkingArea.Width,
                screen.WorkingArea.Height);

            monitors.Add(new MonitorInfo(
                DeviceName: $"Display {i + 1}",
                PhysicalBounds: physicalBounds,
                WorkArea: workArea,
                ScaleFactor: scaleFactor,
                IsPrimary: screen.IsPrimary));
        }

        return monitors;
    }

#if WINDOWS
    private static IReadOnlyList<MonitorInfo> GetWindowsMonitors()
    {
        return Platform.Windows.NativeMonitorService.EnumerateMonitors();
    }
#endif
}
