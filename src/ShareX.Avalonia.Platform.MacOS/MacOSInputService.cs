#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
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

using XerahS.Platform.Abstractions;
using System.Diagnostics;
using System.Drawing;
using DebugHelper = XerahS.Common.DebugHelper;

namespace XerahS.Platform.MacOS
{
    /// <summary>
    /// macOS implementation of IInputService using AppleScript via osascript.
    /// </summary>
    public class MacOSInputService : IInputService
    {
        private const string AppleScript = "tell application \\\"System Events\\\" to get {mouse location's x, mouse location's y}";

        public Point GetCursorPosition()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "osascript",
                    Arguments = $"-e \"{AppleScript}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return Point.Empty;
                }

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                {
                    return Point.Empty;
                }

                // Expected format: "123, 456"
                var parts = output.Trim().Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 &&
                    int.TryParse(parts[0], out int x) &&
                    int.TryParse(parts[1], out int y))
                {
                    return new Point(x, y);
                }

                return Point.Empty;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "MacOSInputService.GetCursorPosition failed");
                return Point.Empty;
            }
        }
    }
}
