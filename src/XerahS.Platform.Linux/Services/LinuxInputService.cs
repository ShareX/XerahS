#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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
using System.Drawing;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// Basic cursor position retrieval using xdotool (X11) or wl-paste fallback.
/// </summary>
public sealed class LinuxInputService : IInputService
{
    public Point GetCursorPosition()
    {
        // Prefer xdotool which is widely available on X11
        if (TryGetWithXdotool(out var point))
            return point;

        return Point.Empty;
    }

    private static bool TryGetWithXdotool(out Point point)
    {
        point = Point.Empty;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "xdotool",
                Arguments = "getmouselocation --shell",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return false;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(500);

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return false;

            int x = 0, y = 0;
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("X=", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(line.Substring(2), out x);
                else if (line.StartsWith("Y=", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(line.Substring(2), out y);
            }

            point = new Point(x, y);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
