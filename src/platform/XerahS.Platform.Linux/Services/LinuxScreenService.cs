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
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services;

internal sealed record LinuxMonitorSnapshot(
    string Name,
    string Description,
    string Make,
    string Model,
    string Serial,
    Rectangle PhysicalBounds,
    Rectangle LogicalBounds,
    Rectangle WorkingAreaPhysical,
    Rectangle WorkingAreaLogical,
    int ReservedLeft,
    int ReservedTop,
    int ReservedRight,
    int ReservedBottom,
    double ScaleFactor,
    double RefreshRateHz,
    int Transform,
    bool IsPrimary,
    string Source);

/// <summary>
/// Screen enumeration using native Wayland compositor queries (Hyprland, Sway, KDE, wlr-randr)
/// with xrandr fallback. Provides accurate scale factors, physical pixel bounds, logical bounds,
/// and reserved panel areas for multi-monitor setups.
/// </summary>
public sealed partial class LinuxScreenService : IScreenService
{
    [GeneratedRegex(@"^(?<name>\S+)\s+connected\s+(?<primary>primary\s+)?(?<width>\d+)x(?<height>\d+)\+(?<x>-?\d+)\+(?<y>-?\d+)", RegexOptions.Multiline)]
    private static partial Regex ParseScreensRegex();

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(2);

    private readonly Lock _screensLock = new();
    private List<ScreenInfo> _screens = [];
    private DateTime _lastRefreshUtc = DateTime.MinValue;

    public LinuxScreenService()
    {
        Refresh();
    }

    public bool UsePerScreenScalingForRegionCaptureLayout => false;
    public bool UseWindowPositionForRegionCaptureFallback => false;
    public bool UseLogicalCoordinatesForRegionCapture => false;

    internal void Refresh()
    {
        var snapshots = QueryDetailedMonitors();
        var updated = snapshots.Select(ToScreenInfo).ToList();

        lock (_screensLock)
        {
            _screens = updated;
            _lastRefreshUtc = DateTime.UtcNow;
        }
    }

    private List<ScreenInfo> GetCurrentScreens()
    {
        lock (_screensLock)
        {
            if (_screens.Count > 0 && (DateTime.UtcNow - _lastRefreshUtc) < CacheLifetime)
            {
                return _screens;
            }
        }

        Refresh();

        lock (_screensLock)
        {
            return _screens;
        }
    }

    public Rectangle GetVirtualScreenBounds()
    {
        var screens = GetCurrentScreens();
        if (screens.Count == 0) return Rectangle.Empty;

        var left = screens.Min(s => s.Bounds.Left);
        var top = screens.Min(s => s.Bounds.Top);
        var right = screens.Max(s => s.Bounds.Right);
        var bottom = screens.Max(s => s.Bounds.Bottom);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    public Rectangle GetWorkingArea()
    {
        var screens = GetCurrentScreens();
        if (screens.Count == 0) return Rectangle.Empty;

        var left = screens.Min(s => s.WorkingArea.Left);
        var top = screens.Min(s => s.WorkingArea.Top);
        var right = screens.Max(s => s.WorkingArea.Right);
        var bottom = screens.Max(s => s.WorkingArea.Bottom);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    public Rectangle GetActiveScreenBounds()
    {
        var cursor = new LinuxInputService().GetCursorPosition();
        var screen = GetScreenFromPoint(cursor);
        return screen.Bounds;
    }

    public Rectangle GetActiveScreenWorkingArea()
    {
        var cursor = new LinuxInputService().GetCursorPosition();
        var screen = GetScreenFromPoint(cursor);
        return screen.WorkingArea;
    }

    public Rectangle GetPrimaryScreenBounds()
    {
        var screens = GetCurrentScreens();
        return (screens.FirstOrDefault(s => s.IsPrimary) ?? screens.FirstOrDefault())?.Bounds ?? Rectangle.Empty;
    }

    public Rectangle GetPrimaryScreenWorkingArea()
    {
        var screens = GetCurrentScreens();
        return (screens.FirstOrDefault(s => s.IsPrimary) ?? screens.FirstOrDefault())?.WorkingArea ?? Rectangle.Empty;
    }

    public ScreenInfo[] GetAllScreens() => GetCurrentScreens().ToArray();

    public ScreenInfo GetScreenFromPoint(Point point)
    {
        var screens = GetCurrentScreens();
        return screens.FirstOrDefault(s => s.Bounds.Contains(point)) ?? screens.FirstOrDefault() ?? new ScreenInfo();
    }

    public ScreenInfo GetScreenFromRectangle(Rectangle rectangle)
    {
        var screens = GetCurrentScreens();
        return screens.FirstOrDefault(s => s.Bounds.IntersectsWith(rectangle)) ?? screens.FirstOrDefault() ?? new ScreenInfo();
    }

    private static ScreenInfo ToScreenInfo(LinuxMonitorSnapshot snapshot) => new()
    {
        DeviceName = snapshot.Name,
        Bounds = snapshot.PhysicalBounds,
        WorkingArea = snapshot.WorkingAreaPhysical,
        IsPrimary = snapshot.IsPrimary,
        BitsPerPixel = 24,
        ScaleFactor = snapshot.ScaleFactor
    };

    internal static IReadOnlyList<LinuxMonitorSnapshot> QueryDetailedMonitors()
    {
        bool isWayland = string.Equals(
            Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"),
            "wayland",
            StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE")))
        {
            var (output, exitCode) = RunXrandrCapture("hyprctl", "monitors -j", 1000);
            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var parsed = ParseHyprlandMonitorsJson(output);
                if (parsed.Count > 0)
                {
                    return parsed;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SWAYSOCK")))
        {
            var (output, exitCode) = RunXrandrCapture("swaymsg", "-t get_outputs -r", 1000);
            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var parsed = ParseSwayOutputsJson(output);
                if (parsed.Count > 0)
                {
                    return parsed;
                }
            }
        }

        string? desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        if (!string.IsNullOrWhiteSpace(desktop) && desktop.Contains("KDE", StringComparison.OrdinalIgnoreCase))
        {
            var (output, exitCode) = RunXrandrCapture("kscreen-doctor", "-j", 1000);
            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var parsed = ParseKScreenDoctorJson(output);
                if (parsed.Count > 0)
                {
                    return parsed;
                }
            }
        }

        if (isWayland)
        {
            var (output, exitCode) = RunXrandrCapture("wlr-randr", "--json", 1000);
            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var parsed = ParseWlrRandrJson(output);
                if (parsed.Count > 0)
                {
                    return parsed;
                }
            }
        }

        var xrandrScreens = ParseXrandrScreens();
        if (xrandrScreens.Count > 0)
        {
            return xrandrScreens;
        }

        return
        [
            new LinuxMonitorSnapshot(
                Name: "Virtual",
                Description: "Virtual fallback display",
                Make: string.Empty,
                Model: string.Empty,
                Serial: string.Empty,
                PhysicalBounds: new Rectangle(0, 0, 1920, 1080),
                LogicalBounds: new Rectangle(0, 0, 1920, 1080),
                WorkingAreaPhysical: new Rectangle(0, 0, 1920, 1080),
                WorkingAreaLogical: new Rectangle(0, 0, 1920, 1080),
                ReservedLeft: 0,
                ReservedTop: 0,
                ReservedRight: 0,
                ReservedBottom: 0,
                ScaleFactor: 1.0,
                RefreshRateHz: 0,
                Transform: 0,
                IsPrimary: true,
                Source: "fallback")
        ];
    }

    internal static IReadOnlyList<LinuxMonitorSnapshot> ParseHyprlandMonitorsJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var rawList = new List<RawCompositorMonitor>();
            int primaryCandidateIndex = -1;
            int idZeroIndex = -1;

            foreach (JsonElement item in doc.RootElement.EnumerateArray())
            {
                if (GetBool(item, "disabled") == true)
                {
                    continue;
                }

                int modeWidth = GetInt(item, "width");
                int modeHeight = GetInt(item, "height");
                if (modeWidth <= 0 || modeHeight <= 0)
                {
                    continue;
                }

                string name = GetString(item, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = $"Display {rawList.Count + 1}";
                }

                int transform = GetInt(item, "transform");
                bool rotated = (transform & 1) != 0;
                int physW = rotated ? modeHeight : modeWidth;
                int physH = rotated ? modeWidth : modeHeight;

                double scale = GetSafeScale(GetDouble(item, "scale"));
                int logX = GetInt(item, "x");
                int logY = GetInt(item, "y");
                int logW = Math.Max(1, (int)Math.Round(physW / scale));
                int logH = Math.Max(1, (int)Math.Round(physH / scale));

                var (resL, resT, resR, resB) = ReadHyprlandReserved(item);
                bool focused = GetBool(item, "focused") == true;
                int id = GetInt(item, "id", -1);

                if (focused && primaryCandidateIndex < 0)
                {
                    primaryCandidateIndex = rawList.Count;
                }
                if (id == 0 && idZeroIndex < 0)
                {
                    idZeroIndex = rawList.Count;
                }

                rawList.Add(new RawCompositorMonitor(
                    Name: name,
                    Description: GetString(item, "description"),
                    Make: GetString(item, "make"),
                    Model: GetString(item, "model"),
                    Serial: GetString(item, "serial"),
                    LogicalBounds: new Rectangle(logX, logY, logW, logH),
                    PhysicalWidth: physW,
                    PhysicalHeight: physH,
                    ReservedLeft: resL,
                    ReservedTop: resT,
                    ReservedRight: resR,
                    ReservedBottom: resB,
                    ScaleFactor: scale,
                    RefreshRateHz: NormalizeRefreshHz(GetDouble(item, "refreshRate")),
                    Transform: NormalizeRotationSteps(transform),
                    IsPrimary: false,
                    Source: "hyprctl"));
            }

            if (rawList.Count == 0)
            {
                return [];
            }

            int primaryIdx = primaryCandidateIndex >= 0
                ? primaryCandidateIndex
                : (idZeroIndex >= 0 ? idZeroIndex : 0);

            rawList[primaryIdx] = rawList[primaryIdx] with { IsPrimary = true };
            return NormalizePhysicalLayout(rawList);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    internal static IReadOnlyList<LinuxMonitorSnapshot> ParseSwayOutputsJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var rawList = new List<RawCompositorMonitor>();
            int primaryIdx = -1;
            int focusedIdx = -1;

            foreach (JsonElement item in doc.RootElement.EnumerateArray())
            {
                if (GetBool(item, "active") == false)
                {
                    continue;
                }

                double scale = GetSafeScale(GetDouble(item, "scale"));
                int transform = ParseTransformName(GetString(item, "transform"));
                bool rotated = (transform & 1) != 0;

                int logX = 0, logY = 0, logW = 0, logH = 0;
                if (item.TryGetProperty("rect", out JsonElement rectEl) && rectEl.ValueKind == JsonValueKind.Object)
                {
                    logX = GetInt(rectEl, "x");
                    logY = GetInt(rectEl, "y");
                    logW = GetInt(rectEl, "width");
                    logH = GetInt(rectEl, "height");
                }

                int modeW = 0, modeH = 0;
                double refresh = 0;
                if (item.TryGetProperty("current_mode", out JsonElement modeEl) && modeEl.ValueKind == JsonValueKind.Object)
                {
                    modeW = GetInt(modeEl, "width");
                    modeH = GetInt(modeEl, "height");
                    refresh = NormalizeRefreshHz(GetDouble(modeEl, "refresh"));
                }

                int physW = modeW > 0 && modeH > 0 ? (rotated ? modeH : modeW) : Math.Max(1, (int)Math.Round(logW * scale));
                int physH = modeW > 0 && modeH > 0 ? (rotated ? modeW : modeH) : Math.Max(1, (int)Math.Round(logH * scale));
                if (physW <= 0 || physH <= 0)
                {
                    continue;
                }

                if (logW <= 0 || logH <= 0)
                {
                    logW = Math.Max(1, (int)Math.Round(physW / scale));
                    logH = Math.Max(1, (int)Math.Round(physH / scale));
                }

                string name = GetString(item, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = $"Display {rawList.Count + 1}";
                }

                if (GetBool(item, "primary") == true && primaryIdx < 0)
                {
                    primaryIdx = rawList.Count;
                }
                if (GetBool(item, "focused") == true && focusedIdx < 0)
                {
                    focusedIdx = rawList.Count;
                }

                string make = GetString(item, "make");
                string model = GetString(item, "model");
                string description = string.Join(" ", new[] { make, model }.Where(s => !string.IsNullOrWhiteSpace(s)));

                rawList.Add(new RawCompositorMonitor(
                    Name: name,
                    Description: description,
                    Make: make,
                    Model: model,
                    Serial: GetString(item, "serial"),
                    LogicalBounds: new Rectangle(logX, logY, logW, logH),
                    PhysicalWidth: physW,
                    PhysicalHeight: physH,
                    ReservedLeft: 0,
                    ReservedTop: 0,
                    ReservedRight: 0,
                    ReservedBottom: 0,
                    ScaleFactor: scale,
                    RefreshRateHz: refresh,
                    Transform: transform,
                    IsPrimary: false,
                    Source: "swaymsg"));
            }

            if (rawList.Count == 0)
            {
                return [];
            }

            int chosenPrimary = primaryIdx >= 0 ? primaryIdx : (focusedIdx >= 0 ? focusedIdx : 0);
            rawList[chosenPrimary] = rawList[chosenPrimary] with { IsPrimary = true };
            return NormalizePhysicalLayout(rawList);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    internal static IReadOnlyList<LinuxMonitorSnapshot> ParseKScreenDoctorJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("outputs", out JsonElement outputsEl) ||
                outputsEl.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var rawList = new List<RawCompositorMonitor>();
            int primaryIdx = -1;

            foreach (JsonElement item in outputsEl.EnumerateArray())
            {
                if (GetBool(item, "enabled") == false || GetBool(item, "connected") == false)
                {
                    continue;
                }

                double scale = GetSafeScale(GetDouble(item, "scale"));
                int rotationValue = GetInt(item, "rotation", 1);
                int transform = rotationValue switch
                {
                    2 => 1,
                    4 => 2,
                    8 => 3,
                    _ => 0
                };
                bool rotated = (transform & 1) != 0;

                int logX = 0, logY = 0;
                if (item.TryGetProperty("pos", out JsonElement posEl) && posEl.ValueKind == JsonValueKind.Object)
                {
                    logX = GetInt(posEl, "x");
                    logY = GetInt(posEl, "y");
                }

                int sizeW = 0, sizeH = 0;
                if (item.TryGetProperty("size", out JsonElement sizeEl) && sizeEl.ValueKind == JsonValueKind.Object)
                {
                    sizeW = GetInt(sizeEl, "width");
                    sizeH = GetInt(sizeEl, "height");
                }

                string? currentModeId = item.TryGetProperty("currentModeId", out JsonElement cmId)
                    ? (cmId.ValueKind == JsonValueKind.String ? cmId.GetString() : cmId.ToString())
                    : null;

                int modeW = 0, modeH = 0;
                double refresh = 0;
                if (item.TryGetProperty("modes", out JsonElement modesEl) && modesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement mode in modesEl.EnumerateArray())
                    {
                        string? modeId = mode.TryGetProperty("id", out JsonElement idEl)
                            ? (idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : idEl.ToString())
                            : null;

                        if (currentModeId != null && string.Equals(modeId, currentModeId, StringComparison.Ordinal))
                        {
                            if (mode.TryGetProperty("size", out JsonElement ms) && ms.ValueKind == JsonValueKind.Object)
                            {
                                modeW = GetInt(ms, "width");
                                modeH = GetInt(ms, "height");
                            }
                            refresh = NormalizeRefreshHz(GetDouble(mode, "refreshRate"));
                            break;
                        }
                    }
                }

                int physW = modeW > 0 && modeH > 0 ? (rotated ? modeH : modeW) : sizeW;
                int physH = modeW > 0 && modeH > 0 ? (rotated ? modeW : modeH) : sizeH;
                if (physW <= 0 || physH <= 0)
                {
                    continue;
                }

                int logW = sizeW > 0 && sizeW != physW
                    ? sizeW
                    : Math.Max(1, (int)Math.Round(physW / scale));
                int logH = sizeH > 0 && sizeH != physH
                    ? sizeH
                    : Math.Max(1, (int)Math.Round(physH / scale));

                if (GetInt(item, "priority") == 1 && primaryIdx < 0)
                {
                    primaryIdx = rawList.Count;
                }

                string name = GetString(item, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = $"Display {rawList.Count + 1}";
                }

                rawList.Add(new RawCompositorMonitor(
                    Name: name,
                    Description: GetString(item, "name"),
                    Make: string.Empty,
                    Model: string.Empty,
                    Serial: string.Empty,
                    LogicalBounds: new Rectangle(logX, logY, logW, logH),
                    PhysicalWidth: physW,
                    PhysicalHeight: physH,
                    ReservedLeft: 0,
                    ReservedTop: 0,
                    ReservedRight: 0,
                    ReservedBottom: 0,
                    ScaleFactor: scale,
                    RefreshRateHz: refresh,
                    Transform: transform,
                    IsPrimary: false,
                    Source: "kscreen-doctor"));
            }

            if (rawList.Count == 0)
            {
                return [];
            }

            rawList[primaryIdx >= 0 ? primaryIdx : 0] = rawList[primaryIdx >= 0 ? primaryIdx : 0] with { IsPrimary = true };
            return NormalizePhysicalLayout(rawList);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    internal static IReadOnlyList<LinuxMonitorSnapshot> ParseWlrRandrJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var rawList = new List<RawCompositorMonitor>();

            foreach (JsonElement item in doc.RootElement.EnumerateArray())
            {
                if (GetBool(item, "enabled") == false)
                {
                    continue;
                }

                double scale = GetSafeScale(GetDouble(item, "scale"));
                int transform = ParseTransformName(GetString(item, "transform"));
                bool rotated = (transform & 1) != 0;

                int logX = 0, logY = 0;
                if (item.TryGetProperty("position", out JsonElement posEl) && posEl.ValueKind == JsonValueKind.Object)
                {
                    logX = GetInt(posEl, "x");
                    logY = GetInt(posEl, "y");
                }

                int modeW = 0, modeH = 0;
                double refresh = 0;
                if (item.TryGetProperty("modes", out JsonElement modesEl) && modesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement mode in modesEl.EnumerateArray())
                    {
                        if (GetBool(mode, "current") == true)
                        {
                            modeW = GetInt(mode, "width");
                            modeH = GetInt(mode, "height");
                            refresh = NormalizeRefreshHz(GetDouble(mode, "refresh"));
                            break;
                        }
                    }
                }

                int physW = rotated ? modeH : modeW;
                int physH = rotated ? modeW : modeH;
                if (physW <= 0 || physH <= 0)
                {
                    continue;
                }

                int logW = Math.Max(1, (int)Math.Round(physW / scale));
                int logH = Math.Max(1, (int)Math.Round(physH / scale));

                string name = GetString(item, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = $"Display {rawList.Count + 1}";
                }

                rawList.Add(new RawCompositorMonitor(
                    Name: name,
                    Description: GetString(item, "description"),
                    Make: GetString(item, "make"),
                    Model: GetString(item, "model"),
                    Serial: GetString(item, "serial"),
                    LogicalBounds: new Rectangle(logX, logY, logW, logH),
                    PhysicalWidth: physW,
                    PhysicalHeight: physH,
                    ReservedLeft: 0,
                    ReservedTop: 0,
                    ReservedRight: 0,
                    ReservedBottom: 0,
                    ScaleFactor: scale,
                    RefreshRateHz: refresh,
                    Transform: transform,
                    IsPrimary: false,
                    Source: "wlr-randr"));
            }

            if (rawList.Count == 0)
            {
                return [];
            }

            rawList[0] = rawList[0] with { IsPrimary = true };
            return NormalizePhysicalLayout(rawList);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<LinuxMonitorSnapshot> ParseXrandrScreens()
    {
        var list = new List<LinuxMonitorSnapshot>();

        try
        {
            var (output, _) = RunXrandrCapture("xrandr", "--current", 1000);
            if (string.IsNullOrEmpty(output))
            {
                return list;
            }

            var regex = ParseScreensRegex();
            foreach (Match match in regex.Matches(output))
            {
                string name = match.Groups["name"].Value;
                int width = int.Parse(match.Groups["width"].Value, CultureInfo.InvariantCulture);
                int height = int.Parse(match.Groups["height"].Value, CultureInfo.InvariantCulture);
                int x = int.Parse(match.Groups["x"].Value, CultureInfo.InvariantCulture);
                int y = int.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture);
                bool isPrimary = !string.IsNullOrEmpty(match.Groups["primary"].Value);

                var bounds = new Rectangle(x, y, width, height);
                list.Add(new LinuxMonitorSnapshot(
                    Name: name,
                    Description: name,
                    Make: string.Empty,
                    Model: string.Empty,
                    Serial: string.Empty,
                    PhysicalBounds: bounds,
                    LogicalBounds: bounds,
                    WorkingAreaPhysical: bounds,
                    WorkingAreaLogical: bounds,
                    ReservedLeft: 0,
                    ReservedTop: 0,
                    ReservedRight: 0,
                    ReservedBottom: 0,
                    ScaleFactor: 1.0,
                    RefreshRateHz: 0,
                    Transform: 0,
                    IsPrimary: isPrimary,
                    Source: "xrandr"));
            }

            if (list.Count > 0 && !list.Any(m => m.IsPrimary))
            {
                list[0] = list[0] with { IsPrimary = true };
            }
        }
        catch
        {
            // Ignore parsing failures; caller handles fallback.
        }

        return list;
    }

    private static IReadOnlyList<LinuxMonitorSnapshot> NormalizePhysicalLayout(List<RawCompositorMonitor> rawMonitors)
    {
        var physXByLogicalCol = BuildCumulativeOrigins(
            rawMonitors,
            m => m.LogicalBounds.X,
            m => m.PhysicalWidth);
        var physYByLogicalRow = BuildCumulativeOrigins(
            rawMonitors,
            m => m.LogicalBounds.Y,
            m => m.PhysicalHeight);

        var result = new List<LinuxMonitorSnapshot>(rawMonitors.Count);
        foreach (RawCompositorMonitor raw in rawMonitors)
        {
            int physX = physXByLogicalCol[raw.LogicalBounds.X];
            int physY = physYByLogicalRow[raw.LogicalBounds.Y];
            var physicalBounds = new Rectangle(physX, physY, raw.PhysicalWidth, raw.PhysicalHeight);

            var logicalWork = new Rectangle(
                raw.LogicalBounds.X + raw.ReservedLeft,
                raw.LogicalBounds.Y + raw.ReservedTop,
                Math.Max(1, raw.LogicalBounds.Width - raw.ReservedLeft - raw.ReservedRight),
                Math.Max(1, raw.LogicalBounds.Height - raw.ReservedTop - raw.ReservedBottom));

            int physResL = (int)Math.Round(raw.ReservedLeft * raw.ScaleFactor);
            int physResT = (int)Math.Round(raw.ReservedTop * raw.ScaleFactor);
            int physResR = (int)Math.Round(raw.ReservedRight * raw.ScaleFactor);
            int physResB = (int)Math.Round(raw.ReservedBottom * raw.ScaleFactor);

            var physicalWork = new Rectangle(
                physX + physResL,
                physY + physResT,
                Math.Max(1, raw.PhysicalWidth - physResL - physResR),
                Math.Max(1, raw.PhysicalHeight - physResT - physResB));

            result.Add(new LinuxMonitorSnapshot(
                Name: raw.Name,
                Description: raw.Description,
                Make: raw.Make,
                Model: raw.Model,
                Serial: raw.Serial,
                PhysicalBounds: physicalBounds,
                LogicalBounds: raw.LogicalBounds,
                WorkingAreaPhysical: physicalWork,
                WorkingAreaLogical: logicalWork,
                ReservedLeft: raw.ReservedLeft,
                ReservedTop: raw.ReservedTop,
                ReservedRight: raw.ReservedRight,
                ReservedBottom: raw.ReservedBottom,
                ScaleFactor: raw.ScaleFactor,
                RefreshRateHz: raw.RefreshRateHz,
                Transform: raw.Transform,
                IsPrimary: raw.IsPrimary,
                Source: raw.Source));
        }

        return result;
    }

    private static Dictionary<int, int> BuildCumulativeOrigins<T>(
        IReadOnlyList<T> items,
        Func<T, int> logicalCoordinateSelector,
        Func<T, int> physicalExtentSelector)
    {
        var groups = items
            .GroupBy(logicalCoordinateSelector)
            .OrderBy(group => group.Key)
            .ToArray();

        var origins = new Dictionary<int, int>(groups.Length);
        int currentPhysicalOrigin = 0;

        foreach (var group in groups)
        {
            origins[group.Key] = currentPhysicalOrigin;
            currentPhysicalOrigin += group.Max(physicalExtentSelector);
        }

        return origins;
    }

    private static (int Left, int Top, int Right, int Bottom) ReadHyprlandReserved(JsonElement element)
    {
        if (!element.TryGetProperty("reserved", out JsonElement resEl) || resEl.ValueKind != JsonValueKind.Array)
        {
            return (0, 0, 0, 0);
        }

        int[] values = [0, 0, 0, 0];
        int i = 0;
        foreach (JsonElement v in resEl.EnumerateArray())
        {
            if (i >= 4) break;
            if (v.TryGetInt32(out int val))
            {
                values[i] = Math.Max(0, val);
            }
            i++;
        }

        return (values[0], values[1], values[2], values[3]);
    }

    private static int ParseTransformName(string? transform)
    {
        if (string.IsNullOrWhiteSpace(transform))
        {
            return 0;
        }

        if (transform.Contains("90", StringComparison.Ordinal))
        {
            return 1;
        }

        if (transform.Contains("180", StringComparison.Ordinal))
        {
            return 2;
        }

        if (transform.Contains("270", StringComparison.Ordinal))
        {
            return 3;
        }

        return 0;
    }

    private static int NormalizeRotationSteps(int transform) => ((transform % 4) + 4) % 4;

    private static double GetSafeScale(double value) =>
        double.IsFinite(value) && value > 0 ? Math.Round(value, 4) : 1.0;

    private static double NormalizeRefreshHz(double raw)
    {
        if (!double.IsFinite(raw) || raw <= 0)
        {
            return 0;
        }

        return raw > 1000 ? raw / 1000.0 : raw;
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? string.Empty
            : string.Empty;

    private static bool? GetBool(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out JsonElement val))
        {
            return null;
        }

        return val.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static int GetInt(JsonElement element, string property, int defaultValue = 0) =>
        element.TryGetProperty(property, out JsonElement val) && val.TryGetInt32(out int num)
            ? num
            : defaultValue;

    private static double GetDouble(JsonElement element, string property, double defaultValue = 0) =>
        element.TryGetProperty(property, out JsonElement val) && val.TryGetDouble(out double num)
            ? num
            : defaultValue;

    private readonly record struct RawCompositorMonitor(
        string Name,
        string Description,
        string Make,
        string Model,
        string Serial,
        Rectangle LogicalBounds,
        int PhysicalWidth,
        int PhysicalHeight,
        int ReservedLeft,
        int ReservedTop,
        int ReservedRight,
        int ReservedBottom,
        double ScaleFactor,
        double RefreshRateHz,
        int Transform,
        bool IsPrimary,
        string Source);

    /// <summary>
    /// Runs an xrandr-style synchronous CLI tool and returns its captured stdout
    /// plus exit code. Drains stderr asynchronously so a noisy child process
    /// cannot deadlock on a full OS pipe buffer (the same anti-pattern
    /// previously fixed in <see cref="XerahS.Platform.Linux.Capture.Helpers.LinuxCliToolRunner"/>
    /// for capture helpers). Reads stdout asynchronously too, so a child that
    /// sleeps without producing output (e.g. <c>sleep 5</c>) cannot stretch a
    /// 1-second timeout into 5 seconds waiting for the child to close its
    /// stdout pipe.
    /// </summary>
    /// <param name="fileName">Executable to run (e.g. "xrandr").</param>
    /// <param name="arguments">Command-line arguments to pass.</param>
    /// <param name="timeoutMs">Maximum time to wait for the process to exit.</param>
    /// <returns>Tuple of (stdout, exitCode). stdout may be empty; exitCode is null on timeout.</returns>
    internal static (string output, int? exitCode) RunXrandrCapture(string fileName, string arguments, int timeoutMs)
    {
        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process = Process.Start(startInfo);
            if (process == null)
                return (string.Empty, null);

            // Drain stderr asynchronously so a chatty child process cannot
            // block writing to a full 64KB OS pipe buffer.
            var stderrDrain = process.StandardError.ReadToEndAsync().ContinueWith(
                _ => { },
                TaskScheduler.Default);

            // Read stdout asynchronously too so a child that does not close
            // its stdout pipe within the timeout does not stretch the call.
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var timeoutTask = Task.Delay(timeoutMs);

            var completed = Task.WaitAny(stdoutTask, timeoutTask);
            if (completed != 0)
            {
                try { process.Kill(); } catch { /* best effort */ }
                try
                {
                    Task.WaitAll(new Task[] { stdoutTask, stderrDrain }, 1000);
                }
                catch
                {
                    // Drainer may have faulted on a closed stream; ignore.
                }
                return (string.Empty, null);
            }

            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { /* best effort */ }
                process.WaitForExit(1000);
                return (stdoutTask.Result, null);
            }

            try
            {
                Task.WaitAll(new Task[] { stderrDrain }, 1000);
            }
            catch
            {
                // Drainer may have faulted on a closed stream; ignore.
            }

            return (stdoutTask.Result, process.ExitCode);
        }
        catch
        {
            return (string.Empty, null);
        }
        finally
        {
            process?.Dispose();
        }
    }

    /// <summary>
    /// Exposes <see cref="RunXrandrCapture"/> for regression tests so the
    /// test assembly can drive the run helper with synthetic commands
    /// (e.g. /bin/sh -c "...large stderr...") without needing a real
    /// xrandr binary on the test machine.
    /// </summary>
    internal static class TestAccessor
    {
        public static (string output, int? exitCode) RunXrandrCapture(string fileName, string arguments, int timeoutMs)
        {
            return LinuxScreenService.RunXrandrCapture(fileName, arguments, timeoutMs);
        }
    }
}
