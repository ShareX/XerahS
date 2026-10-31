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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace XerahS.Platform.Linux.Input.Evdev;

/// <summary>
/// Produces an actionable diagnostic report for the direct evdev global hotkey path
/// (XIP0080), powering <c>xerahs doctor --linux-input</c>.
/// </summary>
public static class LinuxInputDiagnostics
{
    /// <summary>
    /// Builds the diagnostic report.
    /// </summary>
    /// <param name="json">When true, emit machine-readable JSON instead of text.</param>
    /// <returns>
    /// A tuple of the rendered report and a process exit code (0 when at least one
    /// keyboard is readable and evdev hotkeys are viable, non-zero otherwise).
    /// </returns>
    public static (string Report, int ExitCode) BuildReport(bool json)
    {
        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "unknown";
        var currentDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "unknown";
        bool inputDirExists = InputDeviceEnumerator.InputDirectoryExists();
        var devices = inputDirExists
            ? InputDeviceEnumerator.Enumerate()
            : new List<InputDeviceInfo>();

        var keyboards = devices.Where(d => d.IsKeyboard).ToList();
        var readableKeyboards = keyboards.Where(d => d.CanRead && !d.IsVirtual).ToList();
        bool permissionDenied = keyboards.Any(k => !k.CanRead && k.OpenErrno == EvdevNative.EACCES);
        bool inInputGroup = IsUserInInputGroup();
        bool ready = readableKeyboards.Count > 0;

        if (json)
        {
            return BuildJson(sessionType, currentDesktop, inputDirExists, devices,
                keyboards, readableKeyboards, permissionDenied, inInputGroup, ready);
        }

        return BuildText(sessionType, currentDesktop, inputDirExists, devices,
            keyboards, readableKeyboards, permissionDenied, inInputGroup, ready);
    }

    private static (string, int) BuildText(
        string sessionType,
        string currentDesktop,
        bool inputDirExists,
        IReadOnlyList<InputDeviceInfo> devices,
        IReadOnlyList<InputDeviceInfo> keyboards,
        IReadOnlyList<InputDeviceInfo> readableKeyboards,
        bool permissionDenied,
        bool inInputGroup,
        bool ready)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================");
        sb.AppendLine("            XerahS Linux Global Hotkey (evdev) Doctor");
        sb.AppendLine("================================================================");
        sb.AppendLine();
        sb.AppendLine($"Session type     : {sessionType}");
        sb.AppendLine($"Current desktop  : {currentDesktop}");
        sb.AppendLine($"/dev/input found : {YesNo(inputDirExists)}");
        sb.AppendLine($"User in 'input'  : {YesNo(inInputGroup)}");
        sb.AppendLine($"Total devices    : {devices.Count}");
        sb.AppendLine($"Keyboards        : {keyboards.Count} (readable: {readableKeyboards.Count})");
        sb.AppendLine();

        sb.AppendLine("[KEYBOARD DEVICES]");
        if (keyboards.Count == 0)
        {
            sb.AppendLine("  (none detected)");
        }
        else
        {
            foreach (var kb in keyboards)
            {
                string status = kb.CanRead
                    ? (kb.IsVirtual ? "readable (virtual, skipped)" : "readable")
                    : $"NOT readable (errno {kb.OpenErrno})";
                sb.AppendLine($"  - {kb.Name} [{kb.Path}] -> {status}");
            }
        }
        sb.AppendLine();

        sb.AppendLine("[SELECTED HOTKEY BACKEND]");
        sb.AppendLine($"  {DescribeBackend(ready)}");
        sb.AppendLine();

        sb.AppendLine("[RESULT]");
        if (ready)
        {
            sb.AppendLine("  OK: Direct evdev global hotkeys are available.");
        }
        else
        {
            sb.AppendLine("  PROBLEM: No readable keyboard devices; evdev hotkeys are unavailable.");
            AppendRemediation(sb, permissionDenied, inInputGroup, inputDirExists);
        }

        return (sb.ToString(), ready ? 0 : 1);
    }

    private static (string, int) BuildJson(
        string sessionType,
        string currentDesktop,
        bool inputDirExists,
        IReadOnlyList<InputDeviceInfo> devices,
        IReadOnlyList<InputDeviceInfo> keyboards,
        IReadOnlyList<InputDeviceInfo> readableKeyboards,
        bool permissionDenied,
        bool inInputGroup,
        bool ready)
    {
        var payload = new
        {
            sessionType,
            currentDesktop,
            inputDirectoryExists = inputDirExists,
            userInInputGroup = inInputGroup,
            evdevHotkeysAvailable = ready,
            permissionDenied,
            selectedBackend = ready ? "evdev" : "portal-or-x11",
            keyboards = keyboards.Select(k => new
            {
                k.Name,
                k.Path,
                k.CanRead,
                k.IsVirtual,
                openErrno = k.OpenErrno
            }),
            totalDevices = devices.Count,
            remediation = ready ? Array.Empty<string>() : Remediation(permissionDenied, inInputGroup, inputDirExists)
        };

        string jsonText = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        return (jsonText, ready ? 0 : 1);
    }

    private static string DescribeBackend(bool evdevReady)
    {
        if (evdevReady)
        {
            return "evdev (direct input device listener) — XIP0080";
        }

        return "evdev unavailable; XerahS will fall back to the XDG GlobalShortcuts portal " +
               "(Wayland) or X11 key grabs.";
    }

    private static void AppendRemediation(StringBuilder sb, bool permissionDenied, bool inInputGroup, bool inputDirExists)
    {
        sb.AppendLine();
        sb.AppendLine("[HOW TO FIX]");
        foreach (var line in Remediation(permissionDenied, inInputGroup, inputDirExists))
        {
            sb.AppendLine("  - " + line);
        }
    }

    private static string[] Remediation(bool permissionDenied, bool inInputGroup, bool inputDirExists)
    {
        var tips = new List<string>();

        if (!inputDirExists)
        {
            tips.Add("/dev/input does not exist. This usually means there is no local input subsystem " +
                     "(e.g. a headless or container session). evdev hotkeys are not supported here.");
            return tips.ToArray();
        }

        tips.Add("Quickest fix: in XerahS open Settings > Advanced > Global Hotkeys and click 'Grant keyboard access'. " +
                 "It asks for your password once and gives read access to keyboards only, until the next reboot.");

        if (!inInputGroup)
        {
            tips.Add("Permanent fix: add your user to the 'input' group:  sudo usermod -aG input $USER  (then log out and back in). " +
                     "On NixOS add \"input\" to users.users.<you>.extraGroups instead.");
        }

        tips.Add("Install the XerahS udev rule so input devices are group-readable, then reload:");
        tips.Add("    sudo cp 99-xerahs-input.rules /etc/udev/rules.d/  &&  sudo udevadm control --reload-rules  &&  sudo udevadm trigger");

        if (permissionDenied)
        {
            tips.Add("Keyboard devices exist but cannot be opened (permission denied). The group/udev steps above resolve this.");
        }

        tips.Add("After applying changes, re-run:  xerahs doctor --linux-input");
        tips.Add("As a temporary check only (not recommended for daily use), 'sudo xerahs' can confirm the device path works.");

        return tips.ToArray();
    }

    private static bool IsUserInInputGroup()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "id",
                Arguments = "-nG",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            return output.Split(' ', '\n', '\t')
                .Any(g => string.Equals(g.Trim(), "input", StringComparison.Ordinal));
        }
        catch
        {
            return false;
        }
    }

    private static string YesNo(bool value) => value ? "yes" : "no";
}
