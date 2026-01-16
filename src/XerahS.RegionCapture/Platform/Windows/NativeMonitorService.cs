#if WINDOWS
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using XerahS.RegionCapture.Models;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace XerahS.RegionCapture.Platform.Windows;

/// <summary>
/// Native Windows monitor enumeration using Win32 APIs for accurate DPI information.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class NativeMonitorService
{
    public static IReadOnlyList<MonitorInfo> EnumerateMonitors()
    {
        var monitors = new List<MonitorInfo>();

        unsafe
        {
            PInvoke.EnumDisplayMonitors(HDC.Null, null, (hMonitor, hdc, lpRect, lParam) =>
            {
                var info = GetMonitorInfoEx(hMonitor);
                if (info is not null)
                {
                    monitors.Add(info);
                }
                return true;
            }, 0);
        }

        return monitors;
    }

    private static MonitorInfo? GetMonitorInfoEx(HMONITOR hMonitor)
    {
        var monitorInfo = new MONITORINFOEXW
        {
            monitorInfo = new MONITORINFO
            {
                cbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>()
            }
        };

        unsafe
        {
            if (!PInvoke.GetMonitorInfo(hMonitor, (MONITORINFO*)&monitorInfo))
                return null;
        }

        // Get DPI for this monitor
        var dpiResult = PInvoke.GetDpiForMonitor(
            hMonitor,
            MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI,
            out var dpiX,
            out var dpiY);

        double scaleFactor = dpiResult.Succeeded ? dpiX / 96.0 : 1.0;

        var rcMonitor = monitorInfo.monitorInfo.rcMonitor;
        var rcWork = monitorInfo.monitorInfo.rcWork;

        unsafe
        {
            var deviceName = new string(monitorInfo.szDevice.AsSpan()).TrimEnd('\0');

            return new MonitorInfo(
                DeviceName: deviceName,
                PhysicalBounds: new PixelRect(
                    rcMonitor.X,
                    rcMonitor.Y,
                    rcMonitor.Width,
                    rcMonitor.Height),
                WorkArea: new PixelRect(
                    rcWork.X,
                    rcWork.Y,
                    rcWork.Width,
                    rcWork.Height),
                ScaleFactor: scaleFactor,
                IsPrimary: (monitorInfo.monitorInfo.dwFlags & 0x1) != 0); // MONITORINFOF_PRIMARY
        }
    }

    /// <summary>
    /// Gets the physical cursor position (not affected by DPI virtualization).
    /// </summary>
    public static PixelPoint GetPhysicalCursorPosition()
    {
        if (PInvoke.GetPhysicalCursorPos(out var point))
        {
            return new PixelPoint(point.X, point.Y);
        }

        // Fallback to regular cursor position
        if (PInvoke.GetCursorPos(out point))
        {
            return new PixelPoint(point.X, point.Y);
        }

        return PixelPoint.Origin;
    }
}
#endif
