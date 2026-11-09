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
using System.Runtime.InteropServices;

namespace XerahS.App;

/// <summary>
/// Tells the user, in a window or notification, that XerahS could not start and where the log is.
/// Before this, a startup crash on Windows and macOS was only a line in a log file: the app simply never appeared.
/// </summary>
public static class StartupFailureReporter
{
    private const int MaxReasonLength = 300;
    public const string IssuesUrl = "https://github.com/ShareX/XerahS/issues";

    public static string BuildMessage(Exception exception, string? logPath)
    {
        string reason = (exception.GetBaseException().Message ?? string.Empty).Trim();
        if (reason.Length == 0)
        {
            reason = exception.GetType().Name;
        }

        if (reason.Length > MaxReasonLength)
        {
            reason = reason[..MaxReasonLength] + "…";
        }

        string where = string.IsNullOrWhiteSpace(logPath)
            ? "Details were written to the XerahS log folder."
            : $"Details were saved to:\n{logPath}";

        return $"XerahS could not start.\n\n{reason}\n\n{where}\n\nIf it keeps happening, attach that file when you report it at {IssuesUrl}";
    }

    /// <summary>Best effort: showing the message must never hide or replace the original failure.</summary>
    public static void Show(Exception exception, string? logPath)
    {
        try
        {
            string message = BuildMessage(exception, logPath);
            if (OperatingSystem.IsWindows())
            {
                MessageBoxW(IntPtr.Zero, message, "XerahS could not start", 0x10 /* MB_ICONERROR */);
            }
            else if (OperatingSystem.IsMacOS())
            {
                ShowMacAlert(message);
            }
            else if (OperatingSystem.IsLinux())
            {
                LinuxDisplayBootstrap.NotifyStartupFailure(message);
            }
        }
        catch
        {
            // The log and stderr already carry the details.
        }
    }

    public static string EscapeForAppleScript(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", string.Empty).Replace("\n", "\\n");

    private static void ShowMacAlert(string message)
    {
        var startInfo = new ProcessStartInfo("/usr/bin/osascript")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add(
            $"display alert \"XerahS could not start\" message \"{EscapeForAppleScript(message)}\" as critical");
        using Process? process = Process.Start(startInfo);
        process?.WaitForExit(60000);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
