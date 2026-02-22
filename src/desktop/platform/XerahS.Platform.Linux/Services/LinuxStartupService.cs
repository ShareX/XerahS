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

using System.Diagnostics;
using System.IO;
using System.Text;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services;

public sealed class LinuxStartupService : IStartupService
{
    private readonly string _desktopFilePath;
    private readonly string _executablePath;

    public LinuxStartupService()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ??
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".config");
        var autostartFolder = Path.Combine(configHome, "autostart");
        Directory.CreateDirectory(autostartFolder);

        _desktopFilePath = Path.Combine(autostartFolder, $"{AppResources.AppName}.desktop");
        _executablePath = GetExecutablePath() ?? string.Empty;
    }

    public bool IsRunAtStartupEnabled()
    {
        return File.Exists(_desktopFilePath);
    }

    public bool SetRunAtStartup(bool enable)
    {
        try
        {
            if (enable)
            {
                if (string.IsNullOrEmpty(_executablePath))
                {
                    DebugHelper.WriteLine("LinuxStartupService: Executable path is empty.");
                    return false;
                }

                var directory = Path.GetDirectoryName(_desktopFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_desktopFilePath, BuildDesktopEntry(), Encoding.UTF8);
                return true;
            }

            if (File.Exists(_desktopFilePath))
            {
                File.Delete(_desktopFilePath);
            }

            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "LinuxStartupService: Failed to update autostart entry");
            return false;
        }
    }

    private string BuildDesktopEntry()
    {
        string exec = $"\"{_executablePath}\"";
        var builder = new StringBuilder();
        builder.AppendLine("[Desktop Entry]");
        builder.AppendLine("Type=Application");
        builder.AppendLine($"Name={AppResources.AppName}");
        builder.AppendLine($"Exec={exec}");
        builder.AppendLine("Terminal=false");
        builder.AppendLine("Hidden=false");
        builder.AppendLine("X-GNOME-Autostart-enabled=true");
        builder.AppendLine("NoDisplay=false");
        builder.AppendLine("X-KDE-DBUS-Restricted-Interfaces=org.kde.KWin.ScreenShot2");
        builder.AppendLine($"Comment=Auto-start {AppResources.AppName}");
        return builder.ToString();
    }

    private static string? GetExecutablePath()
    {
        return Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
    }
}
