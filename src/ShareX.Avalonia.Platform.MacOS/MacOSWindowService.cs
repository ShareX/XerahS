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

using XerahS.Platform.Abstractions;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using DebugHelper = XerahS.Common.DebugHelper;

namespace XerahS.Platform.MacOS
{
    /// <summary>
    /// macOS window management service (stub for MVP).
    /// </summary>
    public class MacOSWindowService : IWindowService
    {
        private static readonly HashSet<string> Warned = new(StringComparer.Ordinal);
        private static readonly object WarnLock = new();

        public IntPtr GetForegroundWindow()
        {
            return IntPtr.Zero;
        }

        public bool SetForegroundWindow(IntPtr handle)
        {
            if (!TryGetFrontWindowInfo(out var appName, out _, out _))
            {
                return false;
            }

            var script = $"tell application \\\"{appName}\\\" to activate";
            var output = RunOsaScriptWithOutput(script);
            return output != null;
        }

        public string GetWindowText(IntPtr handle)
        {
            return TryGetFrontWindowInfo(out var title, out _, out _) ? title : string.Empty;
        }

        public string GetWindowClassName(IntPtr handle)
        {
            return TryGetFrontWindowInfo(out var title, out _, out _) ? title : string.Empty;
        }

        public Rectangle GetWindowBounds(IntPtr handle)
        {
            return TryGetFrontWindowInfo(out _, out var bounds, out _) ? bounds : Rectangle.Empty;
        }

        public Rectangle GetWindowClientBounds(IntPtr handle)
        {
            return TryGetFrontWindowInfo(out _, out var bounds, out _) ? bounds : Rectangle.Empty;
        }

        public bool IsWindowVisible(IntPtr handle)
        {
            return TryGetFrontWindowInfo(out _, out _, out _);
        }

        public bool IsWindowMaximized(IntPtr handle)
        {
            const string script =
                "tell application \\\"System Events\\\"\\n" +
                "set frontApp to first application process whose frontmost is true\\n" +
                "return zoomed of front window of frontApp\\n" +
                "end tell";

            var output = RunOsaScriptWithOutput(script);
            return output?.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        }

        public bool IsWindowMinimized(IntPtr handle)
        {
            const string script =
                "tell application \\\"System Events\\\"\\n" +
                "set frontApp to first application process whose frontmost is true\\n" +
                "return miniaturized of front window of frontApp\\n" +
                "end tell";

            var output = RunOsaScriptWithOutput(script);
            return output?.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        }

        public bool ShowWindow(IntPtr handle, int cmdShow)
        {
            string? script = cmdShow switch
            {
                6 => "tell application \\\"System Events\\\" to set miniaturized of front window of (first process whose frontmost is true) to true",
                9 => "tell application \\\"System Events\\\" to set miniaturized of front window of (first process whose frontmost is true) to false",
                3 => "tell application \\\"System Events\\\" to set zoomed of front window of (first process whose frontmost is true) to true",
                _ => null
            };

            if (script == null)
            {
                return false;
            }

            return RunOsaScriptWithOutput(script) != null;
        }

        public bool SetWindowPos(IntPtr handle, IntPtr handleInsertAfter, int x, int y, int width, int height, uint flags)
        {
            var script =
                "tell application \\\"System Events\\\"\\n" +
                "set frontApp to first application process whose frontmost is true\\n" +
                $"set position of front window of frontApp to {{{x}, {y}}}\\n" +
                $"set size of front window of frontApp to {{{width}, {height}}}\\n" +
                "end tell";

            return RunOsaScriptWithOutput(script) != null;
        }

        public XerahS.Platform.Abstractions.WindowInfo[] GetAllWindows()
        {
            if (!TryGetFrontWindowInfo(out var title, out var bounds, out var pid))
            {
                return Array.Empty<XerahS.Platform.Abstractions.WindowInfo>();
            }

            return new[]
            {
                new XerahS.Platform.Abstractions.WindowInfo
                {
                    Handle = IntPtr.Zero,
                    Title = title,
                    ClassName = title,
                    Bounds = bounds,
                    ProcessId = pid,
                    IsVisible = true,
                    IsMaximized = false,
                    IsMinimized = false
                }
            };
        }

        public uint GetWindowProcessId(IntPtr handle)
        {
            return TryGetFrontWindowInfo(out _, out _, out var pid) ? pid : 0;
        }

        public IntPtr SearchWindow(string windowTitle)
        {
            // TODO: Implement proper macOS window search via AppleScript
            // For now, check if front window matches
            if (TryGetFrontWindowInfo(out var title, out _, out _))
            {
                if (!string.IsNullOrEmpty(title) && title.Contains(windowTitle, StringComparison.OrdinalIgnoreCase))
                {
                    return IntPtr.Zero; // macOS doesn't use handles the same way
                }
            }
            return IntPtr.Zero;
        }

        public bool ActivateWindow(IntPtr handle)
        {
            // macOS uses AppleScript, SetForegroundWindow already does this
            return SetForegroundWindow(handle);
        }

        private static void LogNotImplemented(string memberName)
        {
            lock (WarnLock)
            {
                if (!Warned.Add(memberName))
                {
                    return;
                }
            }

            DebugHelper.WriteLine($"MacOSWindowService: {memberName} is not implemented yet.");
        }

        private static bool TryGetFrontWindowInfo(out string title, out Rectangle bounds, out uint processId)
        {
            title = string.Empty;
            bounds = Rectangle.Empty;
            processId = 0;

            const string script =
                "tell application \\\"System Events\\\"\n" +
                "set frontApp to first application process whose frontmost is true\n" +
                "set win to front window of frontApp\n" +
                "set winPos to position of win\n" +
                "set winSize to size of win\n" +
                "return (name of frontApp) & \"|\" & (item 1 of winPos) & \"|\" & (item 2 of winPos) & \"|\" & (item 1 of winSize) & \"|\" & (item 2 of winSize) & \"|\" & (unix id of frontApp)\n" +
                "end tell";

            var output = RunOsaScriptWithOutput(script);
            if (string.IsNullOrWhiteSpace(output))
            {
                return false;
            }

            var parts = output.Trim().Split('|');
            if (parts.Length < 6)
            {
                return false;
            }

            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
                !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) ||
                !int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
            {
                return false;
            }

            title = parts[0];
            bounds = new Rectangle(x, y, width, height);

            if (uint.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
            {
                processId = pid;
            }

            return true;
        }

        private static string? RunOsaScriptWithOutput(string script)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e \"{script}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return null;
                }

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0 ? output : null;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "MacOSWindowService.RunOsaScriptWithOutput failed");
                return null;
            }
        }
    }
}
