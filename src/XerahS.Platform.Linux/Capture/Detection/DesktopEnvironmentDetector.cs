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

namespace XerahS.Platform.Linux.Capture.Detection;

internal static class DesktopEnvironmentDetector
{
    public static string? Detect()
    {
        var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        if (!string.IsNullOrEmpty(desktop))
        {
            var desktops = desktop.Split(':');
            foreach (var value in desktops)
            {
                var normalized = value.Trim().ToUpperInvariant();
                if (normalized.Contains("GNOME")) return "GNOME";
                if (normalized.Contains("KDE") || normalized.Contains("PLASMA")) return "KDE";
                if (normalized.Contains("HYPRLAND")) return "HYPRLAND";
                if (normalized.Contains("SWAY")) return "SWAY";
                if (normalized.Contains("XFCE")) return "XFCE";
                if (normalized.Contains("MATE")) return "MATE";
                if (normalized.Contains("CINNAMON")) return "CINNAMON";
                if (normalized.Contains("LXQT")) return "LXQT";
                if (normalized.Contains("LXDE")) return "LXDE";
            }
        }

        var session = Environment.GetEnvironmentVariable("DESKTOP_SESSION");
        if (!string.IsNullOrEmpty(session))
        {
            var normalized = session.ToUpperInvariant();
            if (normalized.Contains("GNOME")) return "GNOME";
            if (normalized.Contains("PLASMA") || normalized.Contains("KDE")) return "KDE";
            if (normalized.Contains("HYPRLAND")) return "HYPRLAND";
            if (normalized.Contains("SWAY")) return "SWAY";
        }

        return null;
    }
}
