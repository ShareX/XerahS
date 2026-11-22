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
using XerahS.Common;
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

        var allScreens = screens.All;

        if (OperatingSystem.IsMacOS())
        {
            return GetMacOSMonitors(allScreens);
        }

        // Check whether Avalonia is actually using the Wayland backend.
        // XDG_SESSION_TYPE=wayland alone is not sufficient: the app may be running via XWayland
        // (Avalonia X11 backend, platform handle=XID). In that case Screen.Bounds are in physical
        // X11 pixels and must be divided by scale just like a pure X11 session.
        bool isAvaloniaWayland = IsAvaloniaWaylandBackend();
        DebugHelper.WriteLine($"[MonitorEnum] isAvaloniaWayland={isAvaloniaWayland} (XDG_SESSION_TYPE={Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "unset"})");

        var logicalScreens = new List<AvaloniaScreenLayout>(allScreens.Count);

        for (int i = 0; i < allScreens.Count; i++)
        {
            var screen = allScreens[i];
            var scaleFactor = screen.Scaling > 0 ? screen.Scaling : 1.0;

            DebugHelper.WriteLine($"[MonitorEnum] Screen[{i}]: Bounds=({screen.Bounds.X},{screen.Bounds.Y},{screen.Bounds.Width},{screen.Bounds.Height}) WorkingArea=({screen.WorkingArea.X},{screen.WorkingArea.Y},{screen.WorkingArea.Width},{screen.WorkingArea.Height}) Scaling={scaleFactor:F4} IsPrimary={screen.IsPrimary}");

            // Wayland native backend: Avalonia reports bounds in logical coordinates
            // (compositor space) — keep as-is.
            // X11 backend (including XWayland): Avalonia reports bounds in device (physical) pixels;
            // convert to logical so the normalizer produces correct OverlayBounds and PhysicalBounds
            // for mixed-DPI setups.
            PixelRect bounds;
            PixelRect workArea;
            bool useLogicalAsIs = isAvaloniaWayland;
            if (useLogicalAsIs)
            {
                bounds = new PixelRect(
                    screen.Bounds.X,
                    screen.Bounds.Y,
                    screen.Bounds.Width,
                    screen.Bounds.Height);
                workArea = new PixelRect(
                    screen.WorkingArea.X,
                    screen.WorkingArea.Y,
                    screen.WorkingArea.Width,
                    screen.WorkingArea.Height);
            }
            else
            {
                bounds = PhysicalToLogicalRect(ToPixelRect(screen.Bounds), scaleFactor);
                workArea = PhysicalToLogicalRect(ToPixelRect(screen.WorkingArea), scaleFactor);
            }

            DebugHelper.WriteLine($"[MonitorEnum] Screen[{i}]: DisplayName={screen.DisplayName ?? "<null>"} useLogicalAsIs={useLogicalAsIs} logicalBounds=({bounds.X:F1},{bounds.Y:F1},{bounds.Width:F1},{bounds.Height:F1})");
            logicalScreens.Add(new AvaloniaScreenLayout(
                DeviceName: !string.IsNullOrWhiteSpace(screen.DisplayName) ? screen.DisplayName : $"Display {i + 1}",
                ScaleFactor: scaleFactor,
                Bounds: bounds,
                WorkingArea: workArea,
                IsPrimary: screen.IsPrimary));
        }

        // Use the normalizer on Linux/non-macOS so overlay windows get logical bounds
        // (OverlayBoundsOverride) and capture uses physical bounds. This avoids one monitor's
        // overlay appearing squashed in mixed-DPI configurations (X11, Wayland).
        var monitors = WaylandMonitorLayoutNormalizer.Normalize(logicalScreens);
        foreach (var m in monitors)
        {
            DebugHelper.WriteLine($"[MonitorEnum] MonitorInfo: {m.DeviceName} IsPrimary={m.IsPrimary} Scale={m.ScaleFactor:F4} PhysicalBounds=({m.PhysicalBounds.X:F1},{m.PhysicalBounds.Y:F1},{m.PhysicalBounds.Width:F1},{m.PhysicalBounds.Height:F1}) OverlayBounds=({m.OverlayBounds.X:F1},{m.OverlayBounds.Y:F1},{m.OverlayBounds.Width:F1},{m.OverlayBounds.Height:F1})");
        }
        return monitors;
    }

    private static IReadOnlyList<MonitorInfo> GetMacOSMonitors(IReadOnlyList<Screen> allScreens)
    {
        var monitors = new List<MonitorInfo>(allScreens.Count);

        for (int i = 0; i < allScreens.Count; i++)
        {
            var screen = allScreens[i];
            var scaleFactor = screen.Scaling > 0 ? screen.Scaling : 1.0;

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

            DebugHelper.WriteLine($"[MonitorEnum] macOS Screen[{i}]: Bounds=({screen.Bounds.X},{screen.Bounds.Y},{screen.Bounds.Width},{screen.Bounds.Height}) WorkingArea=({screen.WorkingArea.X},{screen.WorkingArea.Y},{screen.WorkingArea.Width},{screen.WorkingArea.Height}) Scaling={scaleFactor:F4} IsPrimary={screen.IsPrimary}");

            monitors.Add(new MonitorInfo(
                DeviceName: $"Display {i + 1}",
                PhysicalBounds: physicalBounds,
                WorkArea: workArea,
                ScaleFactor: scaleFactor,
                IsPrimary: screen.IsPrimary));
        }

        return monitors;
    }

    private static PixelRect ToPixelRect(Avalonia.PixelRect r)
    {
        return new PixelRect(r.X, r.Y, r.Width, r.Height);
    }

    /// <summary>
    /// Converts a rectangle from physical (device) pixels to logical coordinates by dividing by scale.
    /// Used on X11 where Avalonia reports physical bounds; we need logical for overlay sizing.
    /// </summary>
    private static PixelRect PhysicalToLogicalRect(PixelRect physical, double scaleFactor)
    {
        if (scaleFactor <= 0) scaleFactor = 1.0;
        return new PixelRect(
            physical.X / scaleFactor,
            physical.Y / scaleFactor,
            Math.Max(1, physical.Width / scaleFactor),
            Math.Max(1, physical.Height / scaleFactor));
    }

    internal static bool IsWaylandSession()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        return string.Equals(
            Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"),
            "wayland",
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true only when Avalonia is using its native Wayland backend (platform handle = "WAYLAND").
    /// Returns false when Avalonia is using the X11 backend, including when running via XWayland on a
    /// Wayland compositor (where XDG_SESSION_TYPE=wayland but platform handles are XIDs).
    /// Falls back to <see cref="IsWaylandSession"/> if the main window handle is unavailable.
    /// </summary>
    internal static bool IsAvaloniaWaylandBackend()
    {
        if (!OperatingSystem.IsLinux())
            return false;

        var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (mainWindow == null)
            return IsWaylandSession(); // best-effort fallback during startup

        return WaylandProperties.IsWaylandBackend(mainWindow);
    }

#if WINDOWS
    private static IReadOnlyList<MonitorInfo> GetWindowsMonitors()
    {
        return Platform.Windows.NativeMonitorService.EnumerateMonitors();
    }
#endif
}
