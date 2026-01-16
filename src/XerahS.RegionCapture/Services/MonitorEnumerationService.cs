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
