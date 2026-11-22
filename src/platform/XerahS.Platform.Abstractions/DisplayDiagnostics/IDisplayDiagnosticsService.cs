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

using System.Drawing;

namespace XerahS.Platform.Abstractions;

/// <summary>Win32 system metrics and monitor DPI constants queried by display diagnostics.</summary>
public static class DisplayMetric
{
    public const int EffectiveDpi = 0;
    public const int AngularDpi = 1;
    public const int RawDpi = 2;

    public const int VirtualScreenX = 76;
    public const int VirtualScreenY = 77;
    public const int VirtualScreenWidth = 78;
    public const int VirtualScreenHeight = 79;
    public const int MonitorCount = 80;
    public const int SameDisplayFormat = 81;
}

/// <summary>Native/compositor monitor bounds, working area, scaling, and metadata.</summary>
public sealed class DisplayMonitorInfo
{
    public IntPtr Handle { get; init; }
    public string DeviceName { get; init; } = string.Empty;
    public string FriendlyName { get; init; } = string.Empty;
    public Rectangle Bounds { get; init; }
    public Rectangle WorkingArea { get; init; }
    public Rectangle? LogicalBounds { get; init; }
    public Rectangle? LogicalWorkingArea { get; init; }
    public double? ScaleFactor { get; init; }
    public double? RefreshRateHz { get; init; }
    public int? Orientation { get; init; }
    public int ReservedLeft { get; init; }
    public int ReservedTop { get; init; }
    public int ReservedRight { get; init; }
    public int ReservedBottom { get; init; }
    public bool Primary { get; init; }
    public string Source { get; init; } = string.Empty;
}

/// <summary>Current display mode reported by the platform or compositor.</summary>
public sealed class DisplayModeInfo
{
    public int Width { get; init; }
    public int Height { get; init; }
    public Point Position { get; init; }
    public int BitsPerPixel { get; init; }
    public int Frequency { get; init; }

    /// <summary>Rotation as 0 (0°), 1 (90°), 2 (180°), or 3 (270°).</summary>
    public int Orientation { get; init; }
}

/// <summary>Graphics adapter or display device metadata.</summary>
public sealed class DisplayDeviceInfo
{
    public string DeviceName { get; init; } = string.Empty;
    public string DeviceString { get; init; } = string.Empty;
    public string DeviceKey { get; init; } = string.Empty;
    public string DeviceId { get; init; } = string.Empty;
}

/// <summary>One active display path: connection, friendly name, and color characteristics.</summary>
public sealed class DisplayTargetInfo
{
    public string SourceDeviceName { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string OutputTechnology { get; set; } = string.Empty;
    public int Rotation { get; set; }
    public double RefreshRate { get; set; }
    public bool AdvancedColorSupported { get; set; }
    public bool AdvancedColorEnabled { get; set; }
    public int BitsPerColorChannel { get; set; }
    public uint SdrWhiteLevelNits { get; set; }
}

/// <summary>Windows NT current-version values used in display diagnostics.</summary>
public readonly record struct WindowsProductInfo(
    string ProductName,
    string Build,
    string? UpdateBuildRevision,
    string? DisplayVersion,
    string? ReleaseId);

/// <summary>Measured native bounds, client origin, DPI, and cursor for an open window handle.</summary>
public readonly record struct NativeWindowGeometry(
    Rectangle Bounds,
    Point ClientOrigin,
    bool HasClientOrigin,
    uint Dpi,
    string MonitorDeviceName,
    Point Cursor,
    bool HasCursor);

/// <summary>
/// Native display facts for the Display Diagnostics report.
/// </summary>
public interface IDisplayDiagnosticsService
{
    bool IsSupported => false;

    bool IsWindowsNative => false;

    string ProviderName => "None";

    IReadOnlyList<DisplayMonitorInfo> EnumerateMonitors() => [];

    Point? GetDpiForMonitor(IntPtr monitor, int dpiType) => null;

    /// <summary>The scale factor percent reported by the OS (for example, 125 or 150).</summary>
    int? GetScaleFactorForMonitor(IntPtr monitor) => null;

    DisplayModeInfo? GetCurrentDisplayMode(string deviceName) => null;

    (DisplayDeviceInfo? Adapter, DisplayDeviceInfo? Monitor) GetDisplayDevices(string deviceName) => (null, null);

    IReadOnlyList<DisplayDeviceInfo> GetAttachedAdapters() => [];

    string GetProcessDpiAwareness() => "Unknown";

    uint? GetDpiForSystem() => null;

    uint? GetDpiForWindow(IntPtr window) => null;

    Point? GetCursorPosition() => null;

    Point? GetPhysicalCursorPosition() => null;

    IntPtr MonitorFromPoint(Point point) => IntPtr.Zero;

    Rectangle? GetWindowBounds(IntPtr window) => null;

    NativeWindowGeometry? ReadNativeWindowGeometry(IntPtr window) => null;

    /// <summary>The main taskbar: its edge (0 left, 1 top, 2 right, 3 bottom), bounds and auto-hide state.</summary>
    (int Edge, Rectangle Bounds, bool AutoHide)? GetTaskbar() => null;

    IReadOnlyList<Rectangle> GetSecondaryTaskbars() => [];

    int GetSystemMetric(int index) => 0;

    IReadOnlyList<DisplayTargetInfo> GetDisplayTargets() => [];

    WindowsProductInfo? ReadWindowsProduct() => null;

    string? ReadCompatibilityLayers(bool machineWide) => null;

    string ReadDriverVersion(string deviceKey) => string.Empty;
}

/// <summary>Fallback when no native display diagnostics provider is registered.</summary>
public sealed class DisplayDiagnosticsUnsupported : IDisplayDiagnosticsService
{
    public static readonly DisplayDiagnosticsUnsupported Instance = new();

    private DisplayDiagnosticsUnsupported()
    {
    }
}
