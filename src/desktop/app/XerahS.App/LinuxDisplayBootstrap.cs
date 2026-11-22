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

namespace XerahS.App;

/// <summary>
/// Resolves a usable Linux display socket (native Wayland via <c>WAYLAND_DISPLAY</c> or X11 /
/// XWayland via <c>DISPLAY</c>). On Wayland sessions started from autostart, neither socket may be
/// ready immediately; this class waits briefly for a usable socket, adopts an XWayland socket when
/// <c>DISPLAY</c> is unset, and turns a final failure into a readable message and desktop
/// notification instead of a stack trace (XIP0088 Phase 0 item 6).
/// </summary>
public static class LinuxDisplayBootstrap
{
    public const string X11SocketDirectory = "/tmp/.X11-unix";
    public static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(15);

    public sealed record Result(bool Usable, string? Display, bool DisplayAdopted, string Message);

    /// <summary>
    /// Resolves a usable Wayland or X11 display. Pure function over injected environment/file-system
    /// access so every branch is unit tested.
    /// </summary>
    public static Result Resolve(
        Func<string, string?> getEnvironment,
        Func<IEnumerable<string>> listX11Sockets,
        Func<string, bool> socketExists)
    {
        if (!IsWaylandDisabled(getEnvironment))
        {
            string? waylandDisplay = getEnvironment("WAYLAND_DISPLAY");
            if (!string.IsNullOrWhiteSpace(waylandDisplay))
            {
                string trimmedWayland = waylandDisplay.Trim();
                string? xdgRuntimeDir = getEnvironment("XDG_RUNTIME_DIR");
                string? waylandSocketPath = Path.IsPathRooted(trimmedWayland)
                    ? trimmedWayland
                    : (!string.IsNullOrWhiteSpace(xdgRuntimeDir)
                        ? Path.Combine(xdgRuntimeDir, trimmedWayland)
                        : null);
                if (waylandSocketPath != null && socketExists(waylandSocketPath))
                {
                    return new Result(true, trimmedWayland, false, $"Using Wayland display {trimmedWayland} ({waylandSocketPath}).");
                }
            }
        }

        string? display = getEnvironment("DISPLAY");
        if (!string.IsNullOrWhiteSpace(display))
        {
            // Always let Avalonia try an explicit DISPLAY: TCP displays and abstract-namespace
            // sockets have no file under /tmp/.X11-unix, so a missing file is only a hint.
            string? socketPath = GetX11SocketPath(display);
            string note = socketPath != null && !socketExists(socketPath)
                ? $" ({socketPath} not found; the server may use an abstract socket)"
                : string.Empty;
            return new Result(true, display, false, $"Using DISPLAY={display}{note}.");
        }

        string? adopted = listX11Sockets()
            .Select(Path.GetFileName)
            .Where(name => name != null && name.Length > 1 && name[0] == 'X' && int.TryParse(name.AsSpan(1), out _))
            .OrderBy(name => int.Parse(name!.AsSpan(1)))
            .Select(name => ":" + name![1..])
            .FirstOrDefault();

        if (adopted != null)
        {
            return new Result(true, adopted, true, $"DISPLAY was unset; using X server socket {adopted}.");
        }

        return new Result(false, null, false, "DISPLAY and WAYLAND_DISPLAY are unset (or unavailable) and no X server (XWayland) socket was found.");
    }

    private static bool IsWaylandDisabled(Func<string, string?> getEnvironment)
    {
        string? value = getEnvironment("XERAHS_DISABLE_WAYLAND");
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        return string.Equals(trimmed, "1", StringComparison.Ordinal) ||
               string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Waits up to <paramref name="maxWait"/> for a usable display on Wayland sessions and adopts
    /// it. Returns the last resolution result. Never waits on X11 sessions or when DISPLAY works.
    /// </summary>
    public static Result EnsureDisplay(TimeSpan? maxWait = null)
    {
        Result result = Resolve(Environment.GetEnvironmentVariable, ListSockets, File.Exists);
        bool isWayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) ||
            string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase);

        var stopwatch = Stopwatch.StartNew();
        TimeSpan wait = maxWait ?? DefaultWait;
        while (!result.Usable && isWayland && stopwatch.Elapsed < wait)
        {
            Thread.Sleep(250);
            result = Resolve(Environment.GetEnvironmentVariable, ListSockets, File.Exists);
        }

        if (result.Usable && result.DisplayAdopted && result.Display != null)
        {
            Environment.SetEnvironmentVariable("DISPLAY", result.Display);
        }

        return result;
    }

    public static string BuildUserMessage(Result result)
    {
        return "XerahS needs a Wayland compositor socket or an X11 display (XWayland on Wayland sessions) and none is available. " +
            result.Message +
            " Enable XWayland in your compositor (Hyprland: xwayland { enabled = true }) or verify WAYLAND_DISPLAY / DISPLAY and start XerahS again.";
    }

    /// <summary>Best-effort desktop notification; the tray does not exist yet at this point.</summary>
    public static void NotifyStartupFailure(string message)
    {
        try
        {
            var startInfo = new ProcessStartInfo("notify-send")
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            startInfo.ArgumentList.Add("--app-name=XerahS");
            startInfo.ArgumentList.Add("--urgency=critical");
            startInfo.ArgumentList.Add("XerahS could not start");
            startInfo.ArgumentList.Add(message);
            using var process = Process.Start(startInfo);
            process?.WaitForExit(2000);
        }
        catch
        {
            // notify-send is optional; the log and stderr already carry the message.
        }
    }

    public static string? GetX11SocketPath(string display)
    {
        string value = display.Trim();
        int colonIndex = value.LastIndexOf(':');
        if (colonIndex < 0 || colonIndex == value.Length - 1)
        {
            return null;
        }

        if (colonIndex > 0 && !value.StartsWith("unix", StringComparison.Ordinal))
        {
            // host:N is a TCP display.
            return null;
        }

        string token = value[(colonIndex + 1)..];
        int dotIndex = token.IndexOf('.');
        if (dotIndex >= 0)
        {
            token = token[..dotIndex];
        }

        return int.TryParse(token, out int number) ? $"{X11SocketDirectory}/X{number}" : null;
    }

    private static IEnumerable<string> ListSockets()
    {
        try
        {
            return Directory.Exists(X11SocketDirectory)
                ? Directory.GetFileSystemEntries(X11SocketDirectory)
                : [];
        }
        catch
        {
            return [];
        }
    }
}
