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

using XerahS.Platform.Linux.Capture.Detection;

namespace XerahS.Platform.Linux.Services;

internal sealed class LinuxRuntimeEnvironment
{
    private LinuxRuntimeEnvironment(
        string? appId,
        LinuxSandboxKind sandboxKind,
        string? sessionType,
        string? waylandDisplay,
        string? display,
        string? desktop,
        string? container)
    {
        AppId = appId;
        SandboxKind = sandboxKind;
        SessionType = sessionType;
        WaylandDisplay = waylandDisplay;
        Display = display;
        Desktop = desktop;
        Container = container;
    }

    public string? AppId { get; }

    public LinuxSandboxKind SandboxKind { get; }

    public string? SessionType { get; }

    public string? WaylandDisplay { get; }

    public string? Display { get; }

    public string? Desktop { get; }

    public string? Container { get; }

    public bool IsFlatpak => SandboxKind == LinuxSandboxKind.Flatpak;

    public bool IsSnap => SandboxKind == LinuxSandboxKind.Snap;

    public bool IsSandboxed => SandboxKind != LinuxSandboxKind.None;

    public bool IsWayland =>
        string.Equals(SessionType, "wayland", StringComparison.OrdinalIgnoreCase) ||
        !string.IsNullOrWhiteSpace(WaylandDisplay);

    public bool IsX11 =>
        string.Equals(SessionType, "x11", StringComparison.OrdinalIgnoreCase) ||
        (!IsWayland && !string.IsNullOrWhiteSpace(Display));

    /// <summary>
    /// True when desktop access must go through XDG portals (Wayland sessions and sandboxes).
    /// </summary>
    public bool RequiresPortalServices => IsWayland || IsSandboxed;

    public bool ShouldUsePortalServices(bool usePortalServices)
    {
        return usePortalServices && RequiresPortalServices;
    }

    public static LinuxRuntimeEnvironment Detect()
    {
        return Detect(Environment.GetEnvironmentVariable, File.Exists);
    }

    internal static LinuxRuntimeEnvironment Detect(
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> fileExists)
    {
        string? flatpakId = getEnvironmentVariable("FLATPAK_ID");
        string? snap = getEnvironmentVariable("SNAP");
        string? container = getEnvironmentVariable("container");
        string? appId = flatpakId;
        LinuxSandboxKind sandboxKind = LinuxSandboxKind.None;

        if (!string.IsNullOrWhiteSpace(flatpakId) ||
            string.Equals(container, "flatpak", StringComparison.OrdinalIgnoreCase) ||
            SafeFileExists(fileExists, "/.flatpak-info"))
        {
            sandboxKind = LinuxSandboxKind.Flatpak;
            appId = NormalizeAppId(flatpakId, "flatpak");
        }
        else if (!string.IsNullOrWhiteSpace(snap) ||
                 string.Equals(container, "snap", StringComparison.OrdinalIgnoreCase))
        {
            sandboxKind = LinuxSandboxKind.Snap;
            appId = NormalizeAppId(getEnvironmentVariable("SNAP_NAME"), NormalizeAppId(snap, "snap"));
        }
        else if (!string.IsNullOrWhiteSpace(container))
        {
            sandboxKind = LinuxSandboxKind.Container;
        }

        return new LinuxRuntimeEnvironment(
            appId,
            sandboxKind,
            getEnvironmentVariable("XDG_SESSION_TYPE"),
            getEnvironmentVariable("WAYLAND_DISPLAY"),
            getEnvironmentVariable("DISPLAY"),
            DesktopEnvironmentDetector.Detect(getEnvironmentVariable),
            container);
    }

    internal static string NormalizeAppId(string? appId, string fallback)
    {
        return string.IsNullOrWhiteSpace(appId) ? fallback : appId.Trim();
    }

    private static bool SafeFileExists(Func<string, bool> fileExists, string path)
    {
        try
        {
            return fileExists(path);
        }
        catch
        {
            return false;
        }
    }

    public string ToDiagnosticString()
    {
        return $"sandbox={SandboxKind}, appId={AppId ?? "<none>"}, session={SessionType ?? "<unset>"}, " +
               $"waylandDisplay={(string.IsNullOrWhiteSpace(WaylandDisplay) ? "<unset>" : "<set>")}, " +
               $"display={(string.IsNullOrWhiteSpace(Display) ? "<unset>" : "<set>")}, desktop={Desktop ?? "<unknown>"}";
    }
}
