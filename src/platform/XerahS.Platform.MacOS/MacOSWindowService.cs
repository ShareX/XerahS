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
        internal const char FrontWindowInfoSeparator = '\u001F';
        internal static readonly IntPtr FrontWindowHandle = new(1);

        private static readonly HashSet<string> Warned = new(StringComparer.Ordinal);
        private static readonly Lock WarnLock = new();

        internal readonly record struct FrontWindowInfo(string AppName, string WindowTitle, Rectangle Bounds, uint ProcessId);

        public IntPtr GetForegroundWindow()
        {
            return TryGetFrontWindowInfo(out _) ? FrontWindowHandle : IntPtr.Zero;
        }

        public bool SetForegroundWindow(IntPtr handle)
        {
            if (!TryGetFrontWindowInfo(out var windowInfo))
            {
                return false;
            }

            var script = $"tell application \"{EscapeAppleScriptString(windowInfo.AppName)}\" to activate";
            var output = RunOsaScriptWithOutput(script);
            return output != null;
        }

        public string GetWindowText(IntPtr handle)
        {
            return TryGetFrontWindowInfo(out var windowInfo) ? windowInfo.WindowTitle : string.Empty;
        }

        public string GetWindowClassName(IntPtr handle)
        {
            return TryGetFrontWindowInfo(out var windowInfo) ? windowInfo.AppName : string.Empty;
        }

        public Rectangle GetWindowBounds(IntPtr handle)
        {
            return TryGetFrontWindowInfo(out var windowInfo) ? windowInfo.Bounds : Rectangle.Empty;
        }

        public Rectangle GetWindowClientBounds(IntPtr handle)
        {
            return TryGetFrontWindowInfo(out var windowInfo) ? windowInfo.Bounds : Rectangle.Empty;
        }

        public bool IsWindowVisible(IntPtr handle)
        {
            return TryGetFrontWindowInfo(out _);
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
            // XIP0078 P5: native CGWindowList enumeration returns every on-screen window in a few
            // milliseconds with no Automation prompt, instead of the frontmost-only osascript query.
            // Handles carry the CGWindowID so window capture can go through the native SCK bridge.
            try
            {
                var windows = Native.QuartzWindowList.GetApplicationWindows();
                if (windows.Count > 0)
                {
                    var result = new XerahS.Platform.Abstractions.WindowInfo[windows.Count];
                    for (int i = 0; i < windows.Count; i++)
                    {
                        var window = windows[i];
                        result[i] = new XerahS.Platform.Abstractions.WindowInfo
                        {
                            Handle = new IntPtr(window.WindowNumber),
                            // Titles are empty without Screen Recording permission; degrade to the app name.
                            Title = string.IsNullOrEmpty(window.Title) ? window.OwnerName : window.Title,
                            ClassName = window.OwnerName,
                            Bounds = window.Bounds,
                            ProcessId = (uint)window.OwnerPid,
                            IsVisible = true,
                            IsMaximized = false,
                            IsMinimized = false
                        };
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "MacOSWindowService.GetAllWindows: native enumeration failed, falling back to AppleScript");
            }

            if (!TryGetFrontWindowInfo(out var windowInfo))
            {
                return Array.Empty<XerahS.Platform.Abstractions.WindowInfo>();
            }

            return new[]
            {
                new XerahS.Platform.Abstractions.WindowInfo
                {
                    Handle = FrontWindowHandle,
                    Title = windowInfo.WindowTitle,
                    ClassName = windowInfo.AppName,
                    Bounds = windowInfo.Bounds,
                    ProcessId = windowInfo.ProcessId,
                    IsVisible = true,
                    IsMaximized = false,
                    IsMinimized = false
                }
            };
        }

        public uint GetWindowProcessId(IntPtr handle)
        {
            return TryGetFrontWindowInfo(out var windowInfo) ? windowInfo.ProcessId : 0;
        }

        public IntPtr SearchWindow(string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
            {
                return IntPtr.Zero;
            }

            if (TryGetFrontWindowInfo(out var windowInfo) && IsSearchMatch(windowInfo, windowTitle))
            {
                return FrontWindowHandle;
            }

            return IntPtr.Zero;
        }

        public bool ActivateWindow(IntPtr handle)
        {
            // macOS uses AppleScript, SetForegroundWindow already does this
            return SetForegroundWindow(handle);
        }

        public bool SetWindowClickThrough(IntPtr handle)
        {
            // Click-through windows on macOS would require NSWindow.ignoresMouseEvents property.
            // This requires Objective-C interop which is out of scope for the MVP.
            LogNotImplemented(nameof(SetWindowClickThrough));
            return false;
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

        private static bool TryGetFrontWindowInfo(out FrontWindowInfo windowInfo)
        {
            var script =
                "tell application \"System Events\"\n" +
                "set frontApp to first application process whose frontmost is true\n" +
                "set win to front window of frontApp\n" +
                "set winPos to position of win\n" +
                "set winSize to size of win\n" +
                $"return (name of win as text) & \"{FrontWindowInfoSeparator}\" & (name of frontApp as text) & \"{FrontWindowInfoSeparator}\" & (item 1 of winPos) & \"{FrontWindowInfoSeparator}\" & (item 2 of winPos) & \"{FrontWindowInfoSeparator}\" & (item 1 of winSize) & \"{FrontWindowInfoSeparator}\" & (item 2 of winSize) & \"{FrontWindowInfoSeparator}\" & (unix id of frontApp)\n" +
                "end tell";

            return TryParseFrontWindowInfo(RunOsaScriptWithOutput(script), out windowInfo);
        }

        internal static bool TryParseFrontWindowInfo(string? output, out FrontWindowInfo windowInfo)
        {
            windowInfo = default;

            if (string.IsNullOrWhiteSpace(output))
            {
                return false;
            }

            var parts = output.TrimEnd('\r', '\n').Split(FrontWindowInfoSeparator);
            if (parts.Length < 7)
            {
                return false;
            }

            if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
                !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
                !int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) ||
                !int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height) ||
                !uint.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var processId))
            {
                return false;
            }

            if (width <= 0 || height <= 0)
            {
                return false;
            }

            var windowTitle = string.IsNullOrWhiteSpace(parts[0]) ? parts[1] : parts[0];
            windowInfo = new FrontWindowInfo(parts[1], windowTitle, new Rectangle(x, y, width, height), processId);
            return true;
        }

        internal static bool IsSearchMatch(FrontWindowInfo windowInfo, string windowTitle)
        {
            return (!string.IsNullOrEmpty(windowInfo.WindowTitle) && windowInfo.WindowTitle.Contains(windowTitle, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(windowInfo.AppName) && windowInfo.AppName.Contains(windowTitle, StringComparison.OrdinalIgnoreCase));
        }

        private static string? RunOsaScriptWithOutput(string script)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "osascript",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add(script);

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return null;
                }

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                string output = outputTask.GetAwaiter().GetResult();
                _ = errorTask.GetAwaiter().GetResult();
                return process.ExitCode == 0 ? output : null;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "MacOSWindowService.RunOsaScriptWithOutput failed");
                return null;
            }
        }

        private static string EscapeAppleScriptString(string value)
        {
            return value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
        }
    }
}
