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
using System.IO;
using System.Text.Json;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// Collects live compositor/X11 monitor geometry, scaling, reserved areas, DRM GPU devices,
/// and cursor position on Linux for Display Diagnostics.
/// </summary>
public sealed class LinuxDisplayDiagnosticsService : IDisplayDiagnosticsService
{
    private readonly Lock _snapshotLock = new();
    private IReadOnlyList<LinuxMonitorSnapshot> _lastSnapshots = [];
    private string _providerName = "Linux";

    public bool IsSupported => true;

    public bool IsWindowsNative => false;

    public string ProviderName
    {
        get
        {
            lock (_snapshotLock)
            {
                return _providerName;
            }
        }
    }

    public IReadOnlyList<DisplayMonitorInfo> EnumerateMonitors()
    {
        var snapshots = LinuxScreenService.QueryDetailedMonitors();

        if (PlatformServices.IsInitialized && PlatformServices.Screen is LinuxScreenService linuxScreenService)
        {
            linuxScreenService.Refresh();
        }

        string source = snapshots.FirstOrDefault()?.Source ?? string.Empty;
        string provider = FormatProviderName(source);

        lock (_snapshotLock)
        {
            _lastSnapshots = snapshots;
            _providerName = provider;
        }

        var result = new List<DisplayMonitorInfo>(snapshots.Count);
        for (int i = 0; i < snapshots.Count; i++)
        {
            LinuxMonitorSnapshot s = snapshots[i];
            string friendly = !string.IsNullOrWhiteSpace(s.Description)
                ? s.Description
                : string.Join(" ", new[] { s.Make, s.Model }.Where(v => !string.IsNullOrWhiteSpace(v)));

            result.Add(new DisplayMonitorInfo
            {
                Handle = (IntPtr)(i + 1),
                DeviceName = s.Name,
                FriendlyName = friendly,
                Bounds = s.PhysicalBounds,
                WorkingArea = s.WorkingAreaPhysical,
                LogicalBounds = s.LogicalBounds,
                LogicalWorkingArea = s.WorkingAreaLogical,
                ScaleFactor = s.ScaleFactor,
                RefreshRateHz = s.RefreshRateHz > 0 ? s.RefreshRateHz : null,
                Orientation = s.Transform,
                ReservedLeft = s.ReservedLeft,
                ReservedTop = s.ReservedTop,
                ReservedRight = s.ReservedRight,
                ReservedBottom = s.ReservedBottom,
                Primary = s.IsPrimary,
                Source = s.Source
            });
        }

        return result;
    }

    public DisplayModeInfo? GetCurrentDisplayMode(string deviceName)
    {
        LinuxMonitorSnapshot? match;
        lock (_snapshotLock)
        {
            match = _lastSnapshots.FirstOrDefault(s =>
                string.Equals(s.Name, deviceName, StringComparison.OrdinalIgnoreCase));
        }

        if (match == null)
        {
            return null;
        }

        return new DisplayModeInfo
        {
            Width = match.PhysicalBounds.Width,
            Height = match.PhysicalBounds.Height,
            Position = new Point(match.LogicalBounds.X, match.LogicalBounds.Y),
            BitsPerPixel = 24,
            Frequency = (int)Math.Round(match.RefreshRateHz),
            Orientation = match.Transform
        };
    }

    public IReadOnlyList<DisplayDeviceInfo> GetAttachedAdapters()
    {
        var adapters = new List<DisplayDeviceInfo>();

        try
        {
            const string drmDir = "/sys/class/drm";
            if (!Directory.Exists(drmDir))
            {
                return adapters;
            }

            foreach (string cardDir in Directory.EnumerateDirectories(drmDir, "card*"))
            {
                string cardName = Path.GetFileName(cardDir);
                if (!IsCardDirectoryName(cardName))
                {
                    continue;
                }

                string ueventPath = Path.Combine(cardDir, "device", "uevent");
                if (!File.Exists(ueventPath))
                {
                    continue;
                }

                string driver = string.Empty;
                string pciId = string.Empty;
                foreach (string line in File.ReadLines(ueventPath))
                {
                    if (line.StartsWith("DRIVER=", StringComparison.Ordinal))
                    {
                        driver = line["DRIVER=".Length..].Trim();
                    }
                    else if (line.StartsWith("PCI_ID=", StringComparison.Ordinal))
                    {
                        pciId = line["PCI_ID=".Length..].Trim();
                    }
                }

                if (string.IsNullOrEmpty(driver) && string.IsNullOrEmpty(pciId))
                {
                    continue;
                }

                adapters.Add(new DisplayDeviceInfo
                {
                    DeviceName = cardName,
                    DeviceString = string.IsNullOrEmpty(pciId) ? driver : $"{driver} ({pciId})",
                    DeviceId = pciId
                });
            }
        }
        catch
        {
            // Ignore sysfs read errors in restricted sandboxes.
        }

        return adapters;
    }

    public Point? GetCursorPosition()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE")))
        {
            var (output, exitCode) = LinuxScreenService.RunXrandrCapture("hyprctl", "cursorpos -j", 500);
            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(output);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                        doc.RootElement.TryGetProperty("x", out JsonElement xEl) &&
                        doc.RootElement.TryGetProperty("y", out JsonElement yEl) &&
                        xEl.TryGetDouble(out double x) &&
                        yEl.TryGetDouble(out double y))
                    {
                        return new Point((int)Math.Round(x), (int)Math.Round(y));
                    }
                }
                catch (JsonException)
                {
                    // Ignore malformed output.
                }
            }
        }

        bool isWayland = string.Equals(
            Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"),
            "wayland",
            StringComparison.OrdinalIgnoreCase);

        if (!isWayland && PlatformServices.IsInitialized)
        {
            return PlatformServices.Input.GetCursorPosition();
        }

        return null;
    }

    private static bool IsCardDirectoryName(string name)
    {
        if (!name.StartsWith("card", StringComparison.Ordinal) || name.Length <= 4)
        {
            return false;
        }

        for (int i = 4; i < name.Length; i++)
        {
            if (!char.IsAsciiDigit(name[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static string FormatProviderName(string source) => source switch
    {
        "hyprctl" => "Hyprland (hyprctl)",
        "swaymsg" => "Sway (swaymsg)",
        "kscreen-doctor" => "KDE KWin (kscreen-doctor)",
        "wlr-randr" => "wlroots (wlr-randr)",
        "xrandr" => "X11 (xrandr)",
        _ => "Linux fallback"
    };
}
