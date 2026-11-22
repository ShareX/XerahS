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
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using XerahS.Common;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture.Services;
using XerahS.UI.ViewModels;
using DrawingPoint = System.Drawing.Point;
using DrawingPointF = System.Drawing.PointF;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;
using RegionMonitorInfo = XerahS.RegionCapture.Models.MonitorInfo;
using RegionPixelRect = XerahS.RegionCapture.Models.PixelRect;

namespace XerahS.UI.Diagnostics;

internal sealed class DisplayDiagnosticsMonitor
{
    public int Number { get; init; }
    public string DeviceName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool Primary { get; init; }
    public DrawingRectangle Bounds { get; init; }
    public DrawingRectangle WorkingArea { get; init; }
    public int ScalePercent { get; init; }
}

internal sealed class DisplayDiagnosticsResult
{
    public IReadOnlyList<DisplayDiagnosticsMonitor> Monitors { get; init; } = [];
    public IReadOnlyList<string> Findings { get; init; } = [];
    public DrawingPoint? CursorPosition { get; init; }
    public string Text { get; init; } = string.Empty;
}

internal readonly record struct DisplayDiagnosticsWindowContext(
    DrawingRectangle? NativeBounds,
    uint? NativeDpi,
    DrawingSize ClientSize,
    double RenderScaling,
    string WindowingBackend,
    bool? IsWaylandLayerShellSupported);

internal readonly record struct DisplayDiagnosticsAppMetadata(
    string VersionText,
    string AvaloniaText,
    string WindowingBackend,
    string SkiaSharpVersion,
    string DotNetRuntime);

internal readonly record struct DisplayDiagnosticsAvaloniaScreen(
    string? DisplayName,
    DrawingRectangle Bounds,
    DrawingRectangle WorkingArea,
    double Scaling,
    bool IsPrimary);

/// <summary>
/// Collects platform, compositor, Avalonia, and region-capture overlay measurements into a
/// plain-English diagnostic report for multi-monitor, DPI, and scaling issues.
/// </summary>
internal static class DisplayDiagnosticsCollector
{
    private const int LabelWidth = 30;

    private sealed class MonitorData
    {
        public int Number;
        public DisplayMonitorInfo Native = null!;
        public DisplayDiagnosticsAvaloniaScreen? AvaloniaScreen;
        public RegionMonitorInfo? RegionMonitor;
        public DrawingPoint? EffectiveDpi;
        public DrawingPoint? AngularDpi;
        public DrawingPoint? RawDpi;
        public int? WindowsScale;
        public DisplayModeInfo? Mode;
        public DisplayDeviceInfo? Adapter;
        public DisplayDeviceInfo? MonitorDevice;
        public DisplayTargetInfo? Target;

        public int ScalePercent
        {
            get
            {
                if (EffectiveDpi is DrawingPoint dpi)
                {
                    return (int)Math.Round(dpi.X * 100.0 / 96.0);
                }

                if (Native.ScaleFactor is double nativeScale && nativeScale > 0)
                {
                    return (int)Math.Round(nativeScale * 100.0);
                }

                if (RegionMonitor != null && RegionMonitor.ScaleFactor > 0)
                {
                    return (int)Math.Round(RegionMonitor.ScaleFactor * 100.0);
                }

                if (AvaloniaScreen is DisplayDiagnosticsAvaloniaScreen avScreen && avScreen.Scaling > 0)
                {
                    return (int)Math.Round(avScreen.Scaling * 100.0);
                }

                return 100;
            }
        }

        public string Name
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Target?.FriendlyName)) return Target!.FriendlyName;
                if (!string.IsNullOrWhiteSpace(Native.FriendlyName)) return Native.FriendlyName;
                if (!string.IsNullOrWhiteSpace(AvaloniaScreen?.DisplayName)) return AvaloniaScreen!.Value.DisplayName!;
                if (!string.IsNullOrWhiteSpace(MonitorDevice?.DeviceString)) return MonitorDevice!.DeviceString;
                return !string.IsNullOrWhiteSpace(Native.DeviceName) ? Native.DeviceName : "Unknown monitor";
            }
        }
    }

    public static DisplayDiagnosticsResult Collect(Window? owner)
    {
        IDisplayDiagnosticsService native = PlatformServices.IsInitialized
            ? PlatformServices.DisplayDiagnostics
            : DisplayDiagnosticsUnsupported.Instance;

        var topLevelScreens = owner?.Screens.All ?? [];
        var avaloniaScreens = topLevelScreens
            .Select(s => new DisplayDiagnosticsAvaloniaScreen(
                s.DisplayName,
                new DrawingRectangle(s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height),
                new DrawingRectangle(s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height),
                s.Scaling,
                s.IsPrimary))
            .ToArray();

        IReadOnlyList<RegionMonitorInfo> regionMonitors = MonitorEnumerationService.GetAllMonitors();
        IReadOnlyList<OverlayWindowGeometryEntry> overlayEntries = OverlayWindowGeometryLog.Entries;

        DrawingRectangle? platformVirtualBounds = PlatformServices.IsInitialized
            ? PlatformServices.Screen.GetVirtualScreenBounds()
            : null;

        var about = new AboutViewModel();
        var appMetadata = new DisplayDiagnosticsAppMetadata(
            VersionText: AppResources.ProductNameWithVersion,
            AvaloniaText: about.AvaloniaDisplayText,
            WindowingBackend: about.WindowingBackend,
            SkiaSharpVersion: about.SkiaSharpVersion,
            DotNetRuntime: about.DotNetRuntime);

        DisplayDiagnosticsWindowContext? windowContext = null;
        if (owner != null)
        {
            IntPtr handle = owner.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            DrawingRectangle? nativeWindowBounds = handle != IntPtr.Zero && native.IsSupported
                ? native.GetWindowBounds(handle)
                : null;
            uint? windowDpi = handle != IntPtr.Zero && native.IsSupported
                ? native.GetDpiForWindow(handle)
                : null;
            bool? layerShellSupported = OperatingSystem.IsLinux() && WaylandProperties.IsWaylandBackend(owner)
                ? WaylandProperties.IsLayerShellSupported(owner)
                : null;

            windowContext = new DisplayDiagnosticsWindowContext(
                NativeBounds: nativeWindowBounds,
                NativeDpi: windowDpi,
                ClientSize: new DrawingSize((int)Math.Round(owner.ClientSize.Width), (int)Math.Round(owner.ClientSize.Height)),
                RenderScaling: owner.RenderScaling,
                WindowingBackend: about.WindowingBackend,
                IsWaylandLayerShellSupported: layerShellSupported);
        }

        TaskSettings? defaultTaskSettings = null;
        try
        {
            defaultTaskSettings = SettingsManager.DefaultTaskSettings;
        }
        catch
        {
            // Ignore when settings are not loaded in tests.
        }

        return CollectCore(
            native: native,
            avaloniaScreens: avaloniaScreens,
            regionMonitors: regionMonitors,
            overlayEntries: overlayEntries,
            platformVirtualBounds: platformVirtualBounds,
            appMetadata: appMetadata,
            windowContext: windowContext,
            taskSettings: defaultTaskSettings,
            linuxDesktopProfileDescription: PlatformServices.DesktopProfile?.Describe());
    }

    internal static DisplayDiagnosticsResult CollectCore(
        IDisplayDiagnosticsService native,
        IReadOnlyList<DisplayDiagnosticsAvaloniaScreen> avaloniaScreens,
        IReadOnlyList<RegionMonitorInfo> regionMonitors,
        IReadOnlyList<OverlayWindowGeometryEntry> overlayEntries,
        DrawingRectangle? platformVirtualBounds,
        DisplayDiagnosticsAppMetadata appMetadata,
        DisplayDiagnosticsWindowContext? windowContext,
        TaskSettings? taskSettings,
        string? linuxDesktopProfileDescription)
    {
        if (!native.IsSupported)
        {
            return CollectAvaloniaOnly(
                avaloniaScreens,
                regionMonitors,
                overlayEntries,
                appMetadata,
                windowContext,
                taskSettings);
        }

        List<MonitorData> monitors = CollectMonitors(native, avaloniaScreens, regionMonitors);
        DrawingPoint? rawCursor = native.GetCursorPosition();
        DrawingPoint? physicalCursor = native.IsWindowsNative
            ? native.GetPhysicalCursorPosition() ?? rawCursor
            : ConvertLinuxCursorToPhysical(rawCursor, monitors);

        StringBuilder report = new();
        List<string> findings = [];

        AppendHeader(report);
        int findingsInsertIndex = report.Length;

        AppendApplication(report, appMetadata);
        if (native.IsWindowsNative)
        {
            AppendWindows(report, native);
            AppendDpi(report, findings, native);
        }
        else
        {
            AppendOperatingSystem(report, native.ProviderName, linuxDesktopProfileDescription, windowContext);
        }

        AppendVirtualScreen(report, findings, native, avaloniaScreens, regionMonitors, platformVirtualBounds, monitors);
        AppendMonitors(report, findings, native, avaloniaScreens, monitors);
        AppendArrangement(report, findings, monitors);
        if (native.IsWindowsNative)
        {
            AppendWindowsTaskbars(report, findings, native, monitors);
        }
        else
        {
            AppendCompositorReservedAreas(report, findings, monitors);
        }

        AppendGraphics(report, native);
        AppendCursor(report, findings, native, monitors, rawCursor, physicalCursor);
        if (windowContext is DisplayDiagnosticsWindowContext winCtx)
        {
            AppendThisWindow(report, winCtx, monitors);
        }

        AppendOverlayWindows(report, findings, overlayEntries);
        AppendCaptureSettings(report, taskSettings);

        StringBuilder findingsText = new();
        findingsText.AppendLine("Findings");
        if (findings.Count == 0)
        {
            findingsText.AppendLine("  - Nothing unusual detected.");
        }
        else
        {
            foreach (string finding in findings)
            {
                findingsText.Append("  - ").AppendLine(finding);
            }
        }
        findingsText.AppendLine();
        report.Insert(findingsInsertIndex, findingsText.ToString());

        return new DisplayDiagnosticsResult
        {
            Monitors = monitors.Select(x => new DisplayDiagnosticsMonitor
            {
                Number = x.Number,
                DeviceName = x.Native.DeviceName,
                Name = x.Name,
                Primary = x.Native.Primary,
                Bounds = x.Native.Bounds,
                WorkingArea = x.Native.WorkingArea,
                ScalePercent = x.ScalePercent
            }).ToArray(),
            Findings = findings,
            CursorPosition = physicalCursor,
            Text = report.ToString().TrimEnd() + Environment.NewLine
        };
    }

    private static List<MonitorData> CollectMonitors(
        IDisplayDiagnosticsService native,
        IReadOnlyList<DisplayDiagnosticsAvaloniaScreen> avaloniaScreens,
        IReadOnlyList<RegionMonitorInfo> regionMonitors)
    {
        IReadOnlyList<DisplayTargetInfo> targets = native.IsWindowsNative ? native.GetDisplayTargets() : [];
        IReadOnlyList<DisplayMonitorInfo> enumerated = native.EnumerateMonitors();
        List<MonitorData> monitors = new(enumerated.Count);
        int sequentialNumber = 1;

        foreach (DisplayMonitorInfo info in enumerated)
        {
            (DisplayDeviceInfo? adapter, DisplayDeviceInfo? monitorDevice) =
                native.IsWindowsNative ? native.GetDisplayDevices(info.DeviceName) : (null, null);

            int number = GetDisplayNumber(info.DeviceName) ?? sequentialNumber;
            sequentialNumber++;

            DisplayDiagnosticsAvaloniaScreen? avScreen = avaloniaScreens
                .Select(s => (Screen: s, Match: string.Equals(s.DisplayName, info.DeviceName, StringComparison.OrdinalIgnoreCase)))
                .Where(x => x.Match)
                .Select(x => (DisplayDiagnosticsAvaloniaScreen?)x.Screen)
                .FirstOrDefault();

            if (avScreen == null && avaloniaScreens.Count == 1 && enumerated.Count == 1)
            {
                avScreen = avaloniaScreens[0];
            }

            RegionMonitorInfo? regionMatch = regionMonitors.FirstOrDefault(m =>
                string.Equals(m.DeviceName, info.DeviceName, StringComparison.OrdinalIgnoreCase));
            if (regionMatch == null && regionMonitors.Count == 1 && enumerated.Count == 1)
            {
                regionMatch = regionMonitors[0];
            }

            monitors.Add(new MonitorData
            {
                Number = number,
                Native = info,
                AvaloniaScreen = avScreen,
                RegionMonitor = regionMatch,
                EffectiveDpi = native.IsWindowsNative ? native.GetDpiForMonitor(info.Handle, DisplayMetric.EffectiveDpi) : null,
                AngularDpi = native.IsWindowsNative ? native.GetDpiForMonitor(info.Handle, DisplayMetric.AngularDpi) : null,
                RawDpi = native.IsWindowsNative ? native.GetDpiForMonitor(info.Handle, DisplayMetric.RawDpi) : null,
                WindowsScale = native.IsWindowsNative ? native.GetScaleFactorForMonitor(info.Handle) : null,
                Mode = native.GetCurrentDisplayMode(info.DeviceName),
                Adapter = adapter,
                MonitorDevice = monitorDevice,
                Target = targets.FirstOrDefault(x =>
                    string.Equals(x.SourceDeviceName, info.DeviceName, StringComparison.OrdinalIgnoreCase))
            });
        }

        return native.IsWindowsNative ? monitors.OrderBy(x => x.Number).ToList() : monitors;
    }

    private static int? GetDisplayNumber(string deviceName)
    {
        const string prefix = @"\\.\DISPLAY";
        return deviceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(deviceName.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            ? number
            : null;
    }

    private static DrawingPoint? ConvertLinuxCursorToPhysical(DrawingPoint? logicalCursor, List<MonitorData> monitors)
    {
        if (logicalCursor is not DrawingPoint cursor || monitors.Count == 0)
        {
            return logicalCursor;
        }

        MonitorData? host = monitors.FirstOrDefault(m =>
            (m.Native.LogicalBounds ?? m.Native.Bounds).Contains(cursor))
            ?? monitors[0];

        DrawingRectangle logical = host.Native.LogicalBounds ?? host.Native.Bounds;
        double scale = host.Native.ScaleFactor is double s && s > 0 ? s : 1.0;

        int physX = host.Native.Bounds.X + (int)Math.Round((cursor.X - logical.X) * scale);
        int physY = host.Native.Bounds.Y + (int)Math.Round((cursor.Y - logical.Y) * scale);
        return new DrawingPoint(physX, physY);
    }

    private static void AppendHeader(StringBuilder report)
    {
        report.AppendLine("XerahS display diagnostics");
        report.AppendLine(new string('=', 26));
        Line(report, "Generated", DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture));
        report.AppendLine();
    }

    private static void AppendApplication(StringBuilder report, DisplayDiagnosticsAppMetadata app)
    {
        Section(report, "XerahS");
        Line(report, "Version", app.VersionText);
        Line(report, "Avalonia", app.AvaloniaText);
        Line(report, "Windowing backend", app.WindowingBackend);
        Line(report, "SkiaSharp", app.SkiaSharpVersion);
        Line(report, ".NET", app.DotNetRuntime);
        Line(report, "Process architecture", RuntimeInformation.ProcessArchitecture.ToString());
        Line(report, "Language", CultureInfo.CurrentUICulture.Name);
        report.AppendLine();
    }

    private static void AppendWindows(StringBuilder report, IDisplayDiagnosticsService native)
    {
        Section(report, "Windows");

        WindowsProductInfo? product = native.ReadWindowsProduct();
        string productName = Environment.OSVersion.VersionString;
        string build = Environment.OSVersion.Version.Build.ToString(CultureInfo.InvariantCulture);
        string? version = null;
        string? ubr = null;
        if (product is WindowsProductInfo info)
        {
            if (!string.IsNullOrEmpty(info.ProductName)) productName = info.ProductName;
            if (!string.IsNullOrEmpty(info.Build)) build = info.Build;
            version = info.DisplayVersion ?? info.ReleaseId;
            ubr = string.IsNullOrEmpty(info.UpdateBuildRevision) ? null : info.UpdateBuildRevision;
        }

        if (int.TryParse(build, NumberStyles.None, CultureInfo.InvariantCulture, out int buildNumber) && buildNumber >= 22000)
        {
            productName = productName.Replace("Windows 10", "Windows 11", StringComparison.Ordinal);
        }

        Line(report, "Edition", productName);
        Line(report, "Version", version ?? "Unknown");
        Line(report, "Build", ubr != null ? $"{build}.{ubr}" : build);
        Line(report, "OS architecture", RuntimeInformation.OSArchitecture.ToString());
        report.AppendLine();
    }

    private static void AppendOperatingSystem(
        StringBuilder report,
        string providerName,
        string? desktopProfileDescription,
        DisplayDiagnosticsWindowContext? windowContext)
    {
        Section(report, "Operating system & session");
        Line(report, "Description", RuntimeInformation.OSDescription);
        Line(report, "OS architecture", RuntimeInformation.OSArchitecture.ToString());
        Line(report, "XDG_SESSION_TYPE", Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "Unset");
        Line(report, "XDG_CURRENT_DESKTOP", Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "Unset");
        Line(report, "WAYLAND_DISPLAY", Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") ?? "Unset");
        Line(report, "DISPLAY", Environment.GetEnvironmentVariable("DISPLAY") ?? "Unset");
        Line(report, "Display provider", providerName);
        if (windowContext?.IsWaylandLayerShellSupported is bool layerShell)
        {
            Line(report, "zwlr_layer_shell_v1", YesNo(layerShell));
        }
        if (!string.IsNullOrWhiteSpace(desktopProfileDescription))
        {
            Line(report, "Desktop profile", desktopProfileDescription);
        }
        report.AppendLine();
    }

    private static void AppendDpi(StringBuilder report, List<string> findings, IDisplayDiagnosticsService native)
    {
        Section(report, "DPI awareness");

        string awareness = native.GetProcessDpiAwareness();
        Line(report, "Thread DPI awareness", awareness);
        if (!awareness.StartsWith("Per-monitor v2", StringComparison.Ordinal))
        {
            findings.Add($"XerahS is not running per-monitor v2 DPI aware ({awareness}). Capture coordinates can be scaled by Windows.");
        }

        uint? systemDpi = native.GetDpiForSystem();
        Line(report, "System DPI", systemDpi is uint dpi ? $"{dpi} ({Percent(dpi)})" : "Unknown");

        string? userLayers = native.ReadCompatibilityLayers(machineWide: false);
        string? machineLayers = native.ReadCompatibilityLayers(machineWide: true);
        Line(report, "Compatibility (user)", string.IsNullOrWhiteSpace(userLayers) ? "None" : userLayers);
        Line(report, "Compatibility (all users)", string.IsNullOrWhiteSpace(machineLayers) ? "None" : machineLayers);

        string layers = $"{userLayers} {machineLayers}";
        if (layers.Contains("DPI", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add("A Windows compatibility setting overrides high DPI scaling for XerahS (Properties > Compatibility > " +
                "Change high DPI settings). Turn it off and test again.");
        }

        report.AppendLine();
    }

    private static void AppendVirtualScreen(
        StringBuilder report,
        List<string> findings,
        IDisplayDiagnosticsService native,
        IReadOnlyList<DisplayDiagnosticsAvaloniaScreen> avaloniaScreens,
        IReadOnlyList<RegionMonitorInfo> regionMonitors,
        DrawingRectangle? platformVirtualBounds,
        List<MonitorData> monitors)
    {
        Section(report, "Virtual screen");

        DrawingRectangle nativeUnion = Combine(monitors.Select(x => x.Native.Bounds));
        DrawingRectangle avaloniaUnion = Combine(avaloniaScreens.Select(x => x.Bounds));
        DrawingRectangle rcPhysicalUnion = Combine(regionMonitors.Select(m => ToDrawingRect(m.PhysicalBounds)));
        DrawingRectangle rcOverlayUnion = Combine(regionMonitors.Select(m => ToDrawingRect(m.OverlayBounds)));

        if (native.IsWindowsNative)
        {
            DrawingRectangle gdi = new(
                native.GetSystemMetric(DisplayMetric.VirtualScreenX),
                native.GetSystemMetric(DisplayMetric.VirtualScreenY),
                native.GetSystemMetric(DisplayMetric.VirtualScreenWidth),
                native.GetSystemMetric(DisplayMetric.VirtualScreenHeight));

            Line(report, "GDI virtual screen", Format(gdi));
            Line(report, "Monitor bounds combined", Format(nativeUnion));
            Line(report, "Avalonia screens combined", Format(avaloniaUnion));
            Line(report, "RegionCapture physical", Format(rcPhysicalUnion));
            if (platformVirtualBounds is DrawingRectangle pvb)
            {
                Line(report, "Platform screen service", Format(pvb));
            }
            Line(report, "Monitor count", $"GDI {native.GetSystemMetric(DisplayMetric.MonitorCount)}, " +
                $"enumerated {monitors.Count}, Avalonia {avaloniaScreens.Count}");
            Line(report, "Same display format", YesNo(native.GetSystemMetric(DisplayMetric.SameDisplayFormat) != 0));

            if (gdi != nativeUnion || avaloniaUnion != nativeUnion || (!rcPhysicalUnion.IsEmpty && rcPhysicalUnion != nativeUnion))
            {
                findings.Add($"The desktop bounds disagree: GDI {Format(gdi)}, monitors {Format(nativeUnion)}, " +
                    $"Avalonia {Format(avaloniaUnion)}, RegionCapture {Format(rcPhysicalUnion)}.");
            }
        }
        else
        {
            DrawingRectangle nativeLogicalUnion = Combine(monitors.Select(x => x.Native.LogicalBounds ?? x.Native.Bounds));

            Line(report, "Native physical combined", Format(nativeUnion));
            Line(report, "Native logical combined", Format(nativeLogicalUnion));
            Line(report, "Avalonia screens combined", Format(avaloniaUnion));
            if (platformVirtualBounds is DrawingRectangle pvb)
            {
                Line(report, "Platform screen service", Format(pvb));
            }
            Line(report, "RegionCapture physical", Format(rcPhysicalUnion));
            Line(report, "RegionCapture overlay", Format(rcOverlayUnion));
            Line(report, "Monitor count", $"Compositor {monitors.Count}, Avalonia {avaloniaScreens.Count}, RegionCapture {regionMonitors.Count}");

            if (!rcPhysicalUnion.IsEmpty && rcPhysicalUnion != nativeUnion)
            {
                findings.Add($"RegionCapture physical virtual bounds ({Format(rcPhysicalUnion)}) differ from compositor physical bounds ({Format(nativeUnion)}).");
            }
        }

        if (avaloniaScreens.Count != monitors.Count)
        {
            findings.Add($"Avalonia sees {avaloniaScreens.Count} screen(s) but {native.ProviderName} lists {monitors.Count} monitor(s).");
        }

        if (nativeUnion.X < 0 || nativeUnion.Y < 0)
        {
            findings.Add($"The desktop extends left of or above the origin ({nativeUnion.X}, {nativeUnion.Y}).");
        }

        report.AppendLine();
    }

    private static void AppendMonitors(
        StringBuilder report,
        List<string> findings,
        IDisplayDiagnosticsService native,
        IReadOnlyList<DisplayDiagnosticsAvaloniaScreen> avaloniaScreens,
        List<MonitorData> monitors)
    {
        foreach (MonitorData monitor in monitors)
        {
            DisplayMonitorInfo info = monitor.Native;
            Section(report, $"Monitor {monitor.Number}{(info.Primary ? " (primary)" : "")}");

            Line(report, "Name", monitor.Name);
            Line(report, "Device", string.IsNullOrWhiteSpace(info.DeviceName) ? "Unknown" : info.DeviceName);
            if (!string.IsNullOrWhiteSpace(info.Source))
            {
                Line(report, "Source", info.Source);
            }
            if (monitor.Target != null)
            {
                Line(report, "Connection", monitor.Target.OutputTechnology);
            }
            if (native.IsWindowsNative)
            {
                Line(report, "Graphics adapter", monitor.Adapter?.DeviceString ?? "Unknown");
            }

            Line(report, "Physical bounds", Format(info.Bounds));
            Line(report, "Physical working area", $"{Format(info.WorkingArea)}  insets {FormatInsets(info.Bounds, info.WorkingArea)}");

            if (info.LogicalBounds is DrawingRectangle logBounds)
            {
                DrawingRectangle logWork = info.LogicalWorkingArea ?? logBounds;
                Line(report, "Logical bounds", Format(logBounds));
                Line(report, "Logical working area", $"{Format(logWork)}  insets {FormatInsets(logBounds, logWork)}");
            }

            if (monitor.Mode is DisplayModeInfo mode)
            {
                Line(report, "Display mode", $"{mode.Width} x {mode.Height} at {mode.Position.X}, {mode.Position.Y}, " +
                    $"{(mode.Frequency > 0 ? $"{mode.Frequency} Hz, " : "")}{mode.BitsPerPixel}-bit, {GetOrientationName(mode.Orientation)}");

                if (native.IsWindowsNative && (mode.Width != info.Bounds.Width || mode.Height != info.Bounds.Height))
                {
                    findings.Add($"Monitor {monitor.Number} reports {info.Bounds.Width} x {info.Bounds.Height} to XerahS but runs at " +
                        $"{mode.Width} x {mode.Height}, so coordinates are being virtualized by Windows.");
                }

                if (mode.Orientation != 0)
                {
                    findings.Add($"Monitor {monitor.Number} is rotated ({GetOrientationName(mode.Orientation)}).");
                }
            }

            int scale = monitor.ScalePercent;
            Line(report, "Scale", $"{scale}% ({(scale / 100.0).ToString("0.###", CultureInfo.InvariantCulture)}x)");

            if (native.IsWindowsNative)
            {
                Line(report, "Effective DPI", FormatDpi(monitor.EffectiveDpi));
                Line(report, "Angular DPI", FormatDpi(monitor.AngularDpi));
                Line(report, "Raw DPI", FormatDpi(monitor.RawDpi));
                Line(report, "Windows scale factor", monitor.WindowsScale is int windowsScale ? $"{windowsScale}%" : "Unknown");
            }
            else if (info.RefreshRateHz is double hz && hz > 0)
            {
                Line(report, "Refresh rate", $"{hz.ToString("0.##", CultureInfo.InvariantCulture)} Hz");
            }

            Line(report, "Size in logical pixels", $"{Math.Round(info.Bounds.Width * 100.0 / scale)} x " +
                $"{Math.Round(info.Bounds.Height * 100.0 / scale)}");

            if (monitor.AvaloniaScreen is DisplayDiagnosticsAvaloniaScreen screen)
            {
                Line(report, "Avalonia Screen", $"scaling {screen.Scaling.ToString("0.###", CultureInfo.InvariantCulture)}, " +
                    $"bounds {Format(screen.Bounds)}, working area {Format(screen.WorkingArea)}");

                if (native.IsWindowsNative && Math.Abs(screen.Scaling * 100 - scale) >= 1)
                {
                    findings.Add($"Avalonia scales monitor {monitor.Number} by {screen.Scaling * 100:0}% but Windows uses {scale}%.");
                }
                else if (!native.IsWindowsNative && scale != 100 && Math.Abs(screen.Scaling - 1.0) <= 0.001)
                {
                    // Expected for Avalonia.Wayland SnapshotScreensImpl (Screen.Scaling=1 while per-window scaling is scale%).
                    Line(report, "Note", $"Avalonia.Wayland reports Screen.Scaling=1 by design; compositor scale is {scale}%.");
                }

                if (native.IsWindowsNative && Format(screen.Bounds) != Format(info.Bounds))
                {
                    findings.Add($"Avalonia places monitor {monitor.Number} at {Format(screen.Bounds)} but Windows has it at {Format(info.Bounds)}.");
                }
            }
            else if (avaloniaScreens.Count > 0)
            {
                Line(report, "Avalonia Screen", "Not matched to a screen");
            }

            if (monitor.RegionMonitor is RegionMonitorInfo rcMon)
            {
                Line(report, "RegionCapture MonitorInfo",
                    $"scale {rcMon.ScaleFactor.ToString("0.###", CultureInfo.InvariantCulture)} ({Math.Round(rcMon.ScaleFactor * 100):0}%), " +
                    $"physical {Format(ToDrawingRect(rcMon.PhysicalBounds))}, overlay {Format(ToDrawingRect(rcMon.OverlayBounds))}");

                if (Math.Abs(rcMon.ScaleFactor * 100.0 - scale) >= 1.0)
                {
                    findings.Add($"Monitor {monitor.Number}: RegionCapture uses scale {rcMon.ScaleFactor:0.###} ({Math.Round(rcMon.ScaleFactor * 100):0}%) while {native.ProviderName} reports {scale}%. Region overlays or captures will scale incorrectly.");
                }

                DrawingRectangle rcPhys = ToDrawingRect(rcMon.PhysicalBounds);
                if (rcPhys != info.Bounds)
                {
                    findings.Add($"Monitor {monitor.Number}: RegionCapture physical bounds ({Format(rcPhys)}) differ from {native.ProviderName} ({Format(info.Bounds)}).");
                }
            }

            if (monitor.Target is DisplayTargetInfo target)
            {
                if (target.RefreshRate > 0)
                {
                    Line(report, "Refresh rate", $"{target.RefreshRate.ToString("0.##", CultureInfo.InvariantCulture)} Hz");
                }

                string hdr = !target.AdvancedColorSupported ? "Not supported" :
                    target.AdvancedColorEnabled ? "On" : "Off";
                if (target.AdvancedColorEnabled && target.SdrWhiteLevelNits > 0)
                {
                    hdr += $", SDR content brightness {target.SdrWhiteLevelNits} nits";
                }
                if (target.BitsPerColorChannel > 0)
                {
                    hdr += $", {target.BitsPerColorChannel} bits per channel";
                }
                Line(report, "HDR", hdr);
            }

            report.AppendLine();
        }

        List<int> scales = monitors.Select(x => x.ScalePercent).Distinct().OrderBy(x => x).ToList();
        if (scales.Count > 1)
        {
            findings.Insert(0, $"Monitors use different scaling ({string.Join(", ", scales.Select(x => x + "%"))}). " +
                "Most region capture offset reports come from mixed scaling.");
        }
    }

    private static void AppendArrangement(StringBuilder report, List<string> findings, List<MonitorData> monitors)
    {
        Section(report, "Arrangement");

        if (monitors.Count < 2)
        {
            report.AppendLine("  Single monitor.");
            report.AppendLine();
            return;
        }

        foreach (MonitorData monitor in monitors)
        {
            DrawingRectangle a = monitor.Native.Bounds;
            bool touchesAny = false;

            foreach (MonitorData other in monitors.Where(x => x.Number > monitor.Number))
            {
                DrawingRectangle b = other.Native.Bounds;
                string? relation = null;
                int startOffset = 0, endOffset = 0;

                if (a.Right == b.Left || b.Right == a.Left)
                {
                    if (Math.Min(a.Bottom, b.Bottom) > Math.Max(a.Top, b.Top))
                    {
                        relation = a.Right == b.Left ? "left of" : "right of";
                        startOffset = b.Top - a.Top;
                        endOffset = b.Bottom - a.Bottom;
                    }
                }
                else if (a.Bottom == b.Top || b.Bottom == a.Top)
                {
                    if (Math.Min(a.Right, b.Right) > Math.Max(a.Left, b.Left))
                    {
                        relation = a.Bottom == b.Top ? "above" : "below";
                        startOffset = b.Left - a.Left;
                        endOffset = b.Right - a.Right;
                    }
                }

                if (relation == null)
                {
                    if (a.IntersectsWith(b))
                    {
                        report.AppendLine($"  Monitor {monitor.Number} overlaps monitor {other.Number}.");
                        findings.Add($"Monitors {monitor.Number} and {other.Number} overlap ({Format(DrawingRectangle.Intersect(a, b))}).");
                        touchesAny = true;
                    }

                    continue;
                }

                touchesAny = true;
                bool vertical = relation is "left of" or "right of";
                string edges = startOffset == 0 && endOffset == 0
                    ? "edges aligned"
                    : vertical
                        ? $"top edges differ by {startOffset} px, bottom edges by {endOffset} px"
                        : $"left edges differ by {startOffset} px, right edges by {endOffset} px";
                report.AppendLine($"  Monitor {monitor.Number} is {relation} monitor {other.Number}: {edges}.");

                if ((startOffset != 0 || endOffset != 0) && monitor.ScalePercent != other.ScalePercent)
                {
                    findings.Add($"Monitors {monitor.Number} ({monitor.ScalePercent}%) and {other.Number} ({other.ScalePercent}%) " +
                        "have different scaling and are not aligned, so one overhangs the other. This arrangement is linked to region capture offsets.");
                }
            }

            if (!touchesAny && monitors.Where(x => x != monitor).All(x => !Touches(a, x.Native.Bounds)))
            {
                report.AppendLine($"  Monitor {monitor.Number} does not touch any other monitor.");
                findings.Add($"Monitor {monitor.Number} does not touch any other monitor, leaving a gap in the desktop.");
            }
        }

        report.AppendLine();
    }

    private static bool Touches(DrawingRectangle a, DrawingRectangle b)
    {
        DrawingRectangle inflated = DrawingRectangle.Inflate(a, 1, 1);
        return inflated.IntersectsWith(b);
    }

    private static void AppendWindowsTaskbars(
        StringBuilder report,
        List<string> findings,
        IDisplayDiagnosticsService native,
        List<MonitorData> monitors)
    {
        Section(report, "Taskbars and app bars");

        List<DrawingRectangle> taskbars = [];

        if (native.GetTaskbar() is var (edge, bounds, autoHide))
        {
            taskbars.Add(bounds);
            Line(report, "Main taskbar", $"{GetEdgeName(edge)}, {Format(bounds)}{(autoHide ? ", auto-hide" : "")}" +
                $", on monitor {FindMonitorNumber(monitors, bounds)}");
            if (edge != 3)
            {
                findings.Add($"The taskbar is on the {GetEdgeName(edge).ToLowerInvariant()} edge. Side and top taskbars have caused overlay offsets.");
            }
        }
        else
        {
            Line(report, "Main taskbar", "Not found");
        }

        foreach (DrawingRectangle secondary in native.GetSecondaryTaskbars())
        {
            taskbars.Add(secondary);
            Line(report, "Secondary taskbar", $"{Format(secondary)}, on monitor {FindMonitorNumber(monitors, secondary)}");
        }

        foreach (MonitorData monitor in monitors)
        {
            DrawingRectangle area = monitor.Native.Bounds;
            DrawingRectangle work = monitor.Native.WorkingArea;
            (string Edge, int Size)[] insets =
            [
                ("left", work.Left - area.Left),
                ("top", work.Top - area.Top),
                ("right", area.Right - work.Right),
                ("bottom", area.Bottom - work.Bottom)
            ];

            foreach ((string insetEdge, int size) in insets.Where(x => x.Size > 0))
            {
                DrawingRectangle strip = insetEdge switch
                {
                    "left" => new DrawingRectangle(area.Left, area.Top, size, area.Height),
                    "top" => new DrawingRectangle(area.Left, area.Top, area.Width, size),
                    "right" => new DrawingRectangle(area.Right - size, area.Top, size, area.Height),
                    _ => new DrawingRectangle(area.Left, area.Bottom - size, area.Width, size)
                };

                if (!taskbars.Any(x => x.IntersectsWith(strip)))
                {
                    Line(report, "Other app bar", $"monitor {monitor.Number}, {insetEdge} edge, {size} px");
                    findings.Add($"Something other than the taskbar reserves {size} px at the {insetEdge} of monitor {monitor.Number}. " +
                        "App bars have shifted capture overlays.");
                }
            }
        }

        report.AppendLine();
    }

    private static void AppendCompositorReservedAreas(StringBuilder report, List<string> findings, List<MonitorData> monitors)
    {
        Section(report, "Panels and reserved areas");

        bool anyReserved = false;
        foreach (MonitorData monitor in monitors)
        {
            DisplayMonitorInfo info = monitor.Native;
            if (info.ReservedLeft > 0 || info.ReservedTop > 0 || info.ReservedRight > 0 || info.ReservedBottom > 0)
            {
                anyReserved = true;
                Line(report, $"Monitor {monitor.Number} reserved (logical)",
                    $"L={info.ReservedLeft} T={info.ReservedTop} R={info.ReservedRight} B={info.ReservedBottom}");
            }
        }

        if (!anyReserved)
        {
            report.AppendLine("  None reported by compositor.");
        }

        report.AppendLine();
    }

    private static void AppendGraphics(StringBuilder report, IDisplayDiagnosticsService native)
    {
        Section(report, "Graphics adapters");

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        bool any = false;
        foreach (DisplayDeviceInfo adapter in native.GetAttachedAdapters())
        {
            string key = adapter.DeviceString + "|" + adapter.DeviceKey + "|" + adapter.DeviceName;
            if (!seen.Add(key))
            {
                continue;
            }

            any = true;
            string driver = native.IsWindowsNative ? native.ReadDriverVersion(adapter.DeviceKey) : string.Empty;
            Line(report, adapter.DeviceName, string.IsNullOrEmpty(driver) ? adapter.DeviceString : $"{adapter.DeviceString}, driver {driver}");
        }

        if (!any)
        {
            report.AppendLine("  None detected.");
        }

        report.AppendLine();
    }

    private static void AppendCursor(
        StringBuilder report,
        List<string> findings,
        IDisplayDiagnosticsService native,
        List<MonitorData> monitors,
        DrawingPoint? rawCursor,
        DrawingPoint? physicalCursor)
    {
        Section(report, "Cursor");

        if (native.IsWindowsNative)
        {
            Line(report, "Position", rawCursor is DrawingPoint position ? $"{position.X}, {position.Y}" : "Unknown");
            Line(report, "Physical position", physicalCursor is DrawingPoint physicalPosition ? $"{physicalPosition.X}, {physicalPosition.Y}" : "Unknown");

            if (rawCursor is DrawingPoint point)
            {
                IntPtr handle = native.MonitorFromPoint(point);
                MonitorData? monitor = monitors.FirstOrDefault(x => x.Native.Handle == handle);
                Line(report, "On monitor", monitor != null ? monitor.Number.ToString(CultureInfo.InvariantCulture) : "None");

                if (physicalCursor is DrawingPoint physPt && physPt != point)
                {
                    findings.Add($"The cursor position differs between logical ({point.X}, {point.Y}) and physical ({physPt.X}, {physPt.Y}) coordinates.");
                }
            }
        }
        else
        {
            Line(report, "Compositor cursor (logical)", rawCursor is DrawingPoint raw ? $"{raw.X}, {raw.Y}" : "Unavailable on this compositor");
            Line(report, "Virtual physical cursor", physicalCursor is DrawingPoint phys ? $"{phys.X}, {phys.Y}" : "Unknown");
            if (physicalCursor is DrawingPoint pt)
            {
                MonitorData? monitor = monitors.FirstOrDefault(m => m.Native.Bounds.Contains(pt));
                Line(report, "On monitor", monitor != null ? monitor.Number.ToString(CultureInfo.InvariantCulture) : "None");
            }
        }

        report.AppendLine();
    }

    private static void AppendThisWindow(StringBuilder report, DisplayDiagnosticsWindowContext windowContext, List<MonitorData> monitors)
    {
        Section(report, "This window");

        Line(report, "Native bounds", windowContext.NativeBounds is DrawingRectangle rect ? Format(rect) : "Unavailable");
        if (windowContext.NativeDpi is uint dpi)
        {
            Line(report, "Window DPI", $"{dpi} ({Percent(dpi)})");
        }
        Line(report, "Client size (logical)", $"{windowContext.ClientSize.Width} x {windowContext.ClientSize.Height}");
        Line(report, "Avalonia render scaling", $"{windowContext.RenderScaling.ToString("0.###", CultureInfo.InvariantCulture)} ({Math.Round(windowContext.RenderScaling * 100):0}%)");
        Line(report, "Windowing backend", windowContext.WindowingBackend);
        if (windowContext.NativeBounds is DrawingRectangle windowBounds)
        {
            Line(report, "On monitor", FindMonitorNumber(monitors, windowBounds));
        }

        report.AppendLine();
    }

    private static void AppendOverlayWindows(
        StringBuilder report,
        List<string> findings,
        IReadOnlyList<OverlayWindowGeometryEntry> entries)
    {
        Section(report, "Recent capture windows");

        if (entries.Count == 0)
        {
            report.AppendLine("  None yet. Start a region capture, close it, then press Refresh to add its measurements.");
            report.AppendLine();
            return;
        }

        foreach (OverlayWindowGeometryEntry entry in entries.Reverse())
        {
            report.AppendLine($"  {entry.Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture)} {entry.Source} ({entry.Backend})");
            Line(report, "  Monitor", string.IsNullOrWhiteSpace(entry.MonitorDeviceName) ? "Unknown" : entry.MonitorDeviceName);
            Line(report, "  Expected physical", Format(entry.ExpectedPhysicalBounds));
            Line(report, "  Expected overlay", Format(entry.ExpectedOverlayBounds));
            Line(report, "  Actual window", Format(entry.ActualBounds));
            if (entry.HasClientOrigin)
            {
                Line(report, "  Client origin", $"{entry.ClientOrigin.X}, {entry.ClientOrigin.Y}");
            }
            if (entry.WindowDpi > 0)
            {
                Line(report, "  Window DPI", $"{entry.WindowDpi} ({Percent(entry.WindowDpi)})");
            }
            Line(report, "  MonitorInfo scale", $"{entry.MonitorScaleFactor.ToString("0.###", CultureInfo.InvariantCulture)} ({Math.Round(entry.MonitorScaleFactor * 100):0}%)");
            Line(report, "  Avalonia render scaling", $"{entry.RenderScaling.ToString("0.###", CultureInfo.InvariantCulture)} ({Math.Round(entry.RenderScaling * 100):0}%)");
            Line(report, "  Client size (logical)", $"{Math.Round(entry.ClientSize.Width)} x {Math.Round(entry.ClientSize.Height)}");
            Line(report, "  Client size x scaling", $"{Math.Round(entry.ClientSize.Width * entry.RenderScaling)} x {Math.Round(entry.ClientSize.Height * entry.RenderScaling)}");
            if (!entry.ImageSize.IsEmpty)
            {
                Line(report, "  Screenshot size", $"{entry.ImageSize.Width} x {entry.ImageSize.Height}");
            }

            if (entry.WindowDpi > 0 && Math.Abs(entry.WindowDpi / 96.0 - entry.RenderScaling) > 0.01)
            {
                findings.Add($"The {entry.Source.ToLowerInvariant()} window runs at {Percent(entry.WindowDpi)} in Windows but Avalonia renders it " +
                    $"at {entry.RenderScaling * 100:0}%. Toolbar, magnifier and drawing positions can be scaled twice or not at all.");
            }

            if (Math.Abs(entry.MonitorScaleFactor - entry.RenderScaling) > 0.01)
            {
                findings.Add($"The {entry.Source.ToLowerInvariant()} window rendered at {entry.RenderScaling * 100:0}% while MonitorInfo used " +
                    $"{entry.MonitorScaleFactor * 100:0}%, causing background cropping or cursor mapping drift.");
            }

            if (entry.CursorScreen is DrawingPoint cursorScreen && entry.CursorAvaloniaClientPixels is DrawingPointF avaloniaCursor)
            {
                DrawingPoint expectedCursor = new(cursorScreen.X - entry.ClientOrigin.X, cursorScreen.Y - entry.ClientOrigin.Y);
                Line(report, "  Cursor in window", $"System {expectedCursor.X}, {expectedCursor.Y}; " +
                    $"Avalonia {Math.Round(avaloniaCursor.X)}, {Math.Round(avaloniaCursor.Y)}");

                if (Math.Abs(avaloniaCursor.X - expectedCursor.X) > 2 || Math.Abs(avaloniaCursor.Y - expectedCursor.Y) > 2)
                {
                    findings.Add($"In the {entry.Source.ToLowerInvariant()} window Avalonia places the cursor at " +
                        $"{Math.Round(avaloniaCursor.X)}, {Math.Round(avaloniaCursor.Y)} but Windows has it at {expectedCursor.X}, {expectedCursor.Y}, " +
                        "so the magnifier and selections use the wrong pixels.");
                }
            }

            if (!entry.ImageSize.IsEmpty &&
                (Math.Abs(entry.ClientSize.Width * entry.RenderScaling - entry.ImageSize.Width) > 2 ||
                 Math.Abs(entry.ClientSize.Height * entry.RenderScaling - entry.ImageSize.Height) > 2))
            {
                findings.Add($"The {entry.Source.ToLowerInvariant()} window covers " +
                    $"{Math.Round(entry.ClientSize.Width * entry.RenderScaling)} x {Math.Round(entry.ClientSize.Height * entry.RenderScaling)} " +
                    $"pixels while the pre-captured screenshot is {entry.ImageSize.Width} x {entry.ImageSize.Height}.");
            }
        }

        report.AppendLine();
    }

    private static void AppendCaptureSettings(StringBuilder report, TaskSettings? taskSettings)
    {
        Section(report, "Capture settings");

        if (taskSettings?.CaptureSettings == null)
        {
            report.AppendLine("  Not loaded.");
            return;
        }

        TaskSettingsCapture capture = taskSettings.CaptureSettings;
        Line(report, "Show cursor", YesNo(capture.ShowCursor));
        Line(report, "Capture transparent", YesNo(capture.CaptureTransparent));
        Line(report, "Use modern capture", YesNo(capture.UseModernCapture));
        Line(report, "HDR color correction", YesNo(capture.HDRScreenshotColorCorrection));
        if (OperatingSystem.IsLinux())
        {
            Line(report, "Linux region selector", capture.LinuxRegionSelectorPreference.ToString());
        }
        else if (OperatingSystem.IsMacOS())
        {
            Line(report, "macOS region selector", capture.MacOSRegionSelectorPreference.ToString());
        }
        Line(report, "Restrict to active monitor", YesNo(capture.RegionCaptureOptions.ActiveMonitorMode));
        Line(report, "Detect windows", YesNo(capture.RegionCaptureOptions.DetectWindows));
        Line(report, "Detect controls", YesNo(capture.RegionCaptureOptions.DetectControls));
        Line(report, "Show magnifier", YesNo(capture.RegionCaptureOptions.ShowMagnifier));
        Line(report, "Show screen crosshair", YesNo(capture.RegionCaptureOptions.ShowScreenCrosshair));
        Line(report, "Show info", YesNo(capture.RegionCaptureOptions.ShowInfo));
    }

    private static DisplayDiagnosticsResult CollectAvaloniaOnly(
        IReadOnlyList<DisplayDiagnosticsAvaloniaScreen> avaloniaScreens,
        IReadOnlyList<RegionMonitorInfo> regionMonitors,
        IReadOnlyList<OverlayWindowGeometryEntry> overlayEntries,
        DisplayDiagnosticsAppMetadata appMetadata,
        DisplayDiagnosticsWindowContext? windowContext,
        TaskSettings? taskSettings)
    {
        StringBuilder report = new();
        List<string> findings = [];

        AppendHeader(report);
        AppendApplication(report, appMetadata);

        Section(report, "Operating system");
        Line(report, "Description", RuntimeInformation.OSDescription);
        Line(report, "Session", Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "Unknown");
        Line(report, "Desktop", Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "Unknown");
        report.AppendLine();

        List<DisplayDiagnosticsMonitor> monitors = [];
        int number = 1;
        foreach (DisplayDiagnosticsAvaloniaScreen screen in avaloniaScreens)
        {
            int scale = (int)Math.Round(screen.Scaling * 100);
            monitors.Add(new DisplayDiagnosticsMonitor
            {
                Number = number,
                DeviceName = screen.DisplayName ?? $"Display {number}",
                Name = screen.DisplayName ?? $"Display {number}",
                Primary = screen.IsPrimary,
                Bounds = screen.Bounds,
                WorkingArea = screen.WorkingArea,
                ScalePercent = scale
            });

            Section(report, $"Monitor {number}{(screen.IsPrimary ? " (primary)" : "")}");
            Line(report, "Name", screen.DisplayName ?? "Unknown");
            Line(report, "Bounds", Format(screen.Bounds));
            Line(report, "Working area", Format(screen.WorkingArea));
            Line(report, "Scale", $"{scale}%");
            report.AppendLine();
            number++;
        }

        if (regionMonitors.Count > 0)
        {
            Section(report, "RegionCapture MonitorInfo");
            foreach (RegionMonitorInfo rc in regionMonitors)
            {
                Line(report, rc.DeviceName, $"scale {rc.ScaleFactor:0.###}, physical {Format(ToDrawingRect(rc.PhysicalBounds))}, overlay {Format(ToDrawingRect(rc.OverlayBounds))}");
            }
            report.AppendLine();
        }

        if (windowContext is DisplayDiagnosticsWindowContext winCtx)
        {
            AppendThisWindow(report, winCtx, []);
        }

        AppendOverlayWindows(report, findings, overlayEntries);
        AppendCaptureSettings(report, taskSettings);

        return new DisplayDiagnosticsResult
        {
            Monitors = monitors,
            Findings = findings,
            Text = report.ToString().TrimEnd() + Environment.NewLine
        };
    }

    private static string FindMonitorNumber(List<MonitorData> monitors, DrawingRectangle bounds)
    {
        MonitorData? best = monitors
            .Select(x => (Monitor: x, Area: Area(DrawingRectangle.Intersect(x.Native.Bounds, bounds))))
            .Where(x => x.Area > 0)
            .OrderByDescending(x => x.Area)
            .Select(x => x.Monitor)
            .FirstOrDefault();
        return best?.Number.ToString(CultureInfo.InvariantCulture) ?? "none";
    }

    private static long Area(DrawingRectangle rectangle) => (long)rectangle.Width * rectangle.Height;

    private static DrawingRectangle ToDrawingRect(RegionPixelRect rect) => new(
        (int)Math.Round(rect.X),
        (int)Math.Round(rect.Y),
        (int)Math.Round(rect.Width),
        (int)Math.Round(rect.Height));

    private static DrawingRectangle Combine(IEnumerable<DrawingRectangle> rectangles)
    {
        DrawingRectangle? result = null;
        foreach (DrawingRectangle rectangle in rectangles)
        {
            if (rectangle.Width <= 0 || rectangle.Height <= 0)
            {
                continue;
            }

            result = result is DrawingRectangle current ? DrawingRectangle.Union(current, rectangle) : rectangle;
        }

        return result ?? DrawingRectangle.Empty;
    }

    private static void Section(StringBuilder report, string title) => report.AppendLine(title);

    private static void Line(StringBuilder report, string label, string value) =>
        report.Append("  ").Append((label + ":").PadRight(LabelWidth)).AppendLine(value);

    private static string Format(DrawingRectangle rectangle) =>
        $"X={rectangle.X} Y={rectangle.Y} W={rectangle.Width} H={rectangle.Height}";

    private static string FormatInsets(DrawingRectangle bounds, DrawingRectangle workingArea) =>
        $"L={workingArea.Left - bounds.Left} T={workingArea.Top - bounds.Top} " +
        $"R={bounds.Right - workingArea.Right} B={bounds.Bottom - workingArea.Bottom}";

    private static string FormatDpi(DrawingPoint? dpi) =>
        dpi is DrawingPoint value ? value.X == value.Y ? $"{value.X}" : $"{value.X} x {value.Y}" : "Unknown";

    private static string Percent(uint dpi) => $"{Math.Round(dpi * 100.0 / 96.0)}%";

    private static string YesNo(bool value) => value ? "Yes" : "No";

    private static string GetOrientationName(int orientation) => orientation switch
    {
        1 => "portrait (90°)",
        2 => "landscape flipped (180°)",
        3 => "portrait flipped (270°)",
        _ => "landscape"
    };

    private static string GetEdgeName(int edge) => edge switch
    {
        0 => "Left",
        1 => "Top",
        2 => "Right",
        _ => "Bottom"
    };
}
