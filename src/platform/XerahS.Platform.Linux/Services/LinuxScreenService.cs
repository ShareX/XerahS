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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// Basic screen enumeration using xrandr output. Provides best-effort data for multi-monitor setups.
/// </summary>
public sealed partial class LinuxScreenService : IScreenService
{
    [GeneratedRegex(@"^(?<name>\S+)\s+connected\s+(?<primary>primary\s+)?(?<width>\d+)x(?<height>\d+)\+(?<x>-?\d+)\+(?<y>-?\d+)", RegexOptions.Multiline)]
    private static partial Regex ParseScreensRegex();

    private readonly List<ScreenInfo> _screens;

    public LinuxScreenService()
    {
        _screens = ParseScreens();
    }

    public bool UsePerScreenScalingForRegionCaptureLayout => false;
    public bool UseWindowPositionForRegionCaptureFallback => false;
    public bool UseLogicalCoordinatesForRegionCapture => false;

    public Rectangle GetVirtualScreenBounds()
    {
        if (_screens.Count == 0) return Rectangle.Empty;

        var left = _screens.Min(s => s.Bounds.Left);
        var top = _screens.Min(s => s.Bounds.Top);
        var right = _screens.Max(s => s.Bounds.Right);
        var bottom = _screens.Max(s => s.Bounds.Bottom);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    public Rectangle GetWorkingArea() => GetVirtualScreenBounds();

    public Rectangle GetActiveScreenBounds()
    {
        var cursor = new LinuxInputService().GetCursorPosition();
        var screen = GetScreenFromPoint(cursor);
        return screen.Bounds;
    }

    public Rectangle GetActiveScreenWorkingArea()
    {
        var cursor = new LinuxInputService().GetCursorPosition();
        var screen = GetScreenFromPoint(cursor);
        return screen.WorkingArea;
    }

    public Rectangle GetPrimaryScreenBounds() => _screens.FirstOrDefault(s => s.IsPrimary)?.Bounds ?? Rectangle.Empty;

    public Rectangle GetPrimaryScreenWorkingArea() => _screens.FirstOrDefault(s => s.IsPrimary)?.WorkingArea ?? Rectangle.Empty;

    public ScreenInfo[] GetAllScreens() => _screens.ToArray();

    public ScreenInfo GetScreenFromPoint(Point point)
    {
        return _screens.FirstOrDefault(s => s.Bounds.Contains(point)) ?? _screens.FirstOrDefault() ?? new ScreenInfo();
    }

    public ScreenInfo GetScreenFromRectangle(Rectangle rectangle)
    {
        return _screens.FirstOrDefault(s => s.Bounds.IntersectsWith(rectangle)) ?? _screens.FirstOrDefault() ?? new ScreenInfo();
    }

    private static List<ScreenInfo> ParseScreens()
    {
        var screens = new List<ScreenInfo>();

        try
        {
            var (output, _) = RunXrandrCapture("xrandr", "--current", 1000);
            if (string.IsNullOrEmpty(output))
                return screens;

            var regex = ParseScreensRegex();
            foreach (Match match in regex.Matches(output))
            {
                var name = match.Groups["name"].Value;
                var width = int.Parse(match.Groups["width"].Value);
                var height = int.Parse(match.Groups["height"].Value);
                var x = int.Parse(match.Groups["x"].Value);
                var y = int.Parse(match.Groups["y"].Value);
                var isPrimary = !string.IsNullOrEmpty(match.Groups["primary"].Value);

                var bounds = new Rectangle(x, y, width, height);
                screens.Add(new ScreenInfo
                {
                    DeviceName = name,
                    Bounds = bounds,
                    WorkingArea = bounds,
                    IsPrimary = isPrimary,
                    BitsPerPixel = 24,
                    ScaleFactor = 1.0
                });
            }
        }
        catch
        {
            // Ignore parsing failures; return whatever we gathered
        }

        if (screens.Count == 0)
        {
            // Fallback to a single 1920x1080 screen at origin
            screens.Add(new ScreenInfo
            {
                DeviceName = "Virtual",
                Bounds = new Rectangle(0, 0, 1920, 1080),
                WorkingArea = new Rectangle(0, 0, 1920, 1080),
                IsPrimary = true,
                BitsPerPixel = 24,
                ScaleFactor = 1.0
            });
        }

        return screens;
    }

    /// <summary>
    /// Runs an xrandr-style synchronous CLI tool and returns its captured stdout
    /// plus exit code. Drains stderr asynchronously so a noisy child process
    /// cannot deadlock on a full OS pipe buffer (the same anti-pattern
    /// previously fixed in <see cref="XerahS.Platform.Linux.Capture.Helpers.LinuxCliToolRunner"/>
    /// for capture helpers). Reads stdout asynchronously too, so a child that
    /// sleeps without producing output (e.g. <c>sleep 5</c>) cannot stretch a
    /// 1-second timeout into 5 seconds waiting for the child to close its
    /// stdout pipe.
    /// </summary>
    /// <param name="fileName">Executable to run (e.g. "xrandr").</param>
    /// <param name="arguments">Command-line arguments to pass.</param>
    /// <param name="timeoutMs">Maximum time to wait for the process to exit.</param>
    /// <returns>Tuple of (stdout, exitCode). stdout may be empty; exitCode is null on timeout.</returns>
    internal static (string output, int? exitCode) RunXrandrCapture(string fileName, string arguments, int timeoutMs)
    {
        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process = Process.Start(startInfo);
            if (process == null)
                return (string.Empty, null);

            // Drain stderr asynchronously so a chatty child process cannot
            // block writing to a full 64KB OS pipe buffer. We deliberately
            // discard stderr text — xrandr --current only writes to stdout
            // for connected displays; any stderr line is a warning/error
            // that we do not need to surface at the screen-parse layer.
            var stderrDrain = process.StandardError.ReadToEndAsync().ContinueWith(
                _ => { },
                TaskScheduler.Default);

            // Read stdout asynchronously too so a child that does not close
            // its stdout pipe within the timeout (e.g. `sleep 5` with a 1s
            // timeout) does not stretch the call to 5 seconds. The
            // ReadToEndAsync + Task.WhenAny(timeout) pattern bounds the wait.
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var timeoutTask = Task.Delay(timeoutMs);

            var completed = Task.WaitAny(stdoutTask, timeoutTask);
            if (completed != 0)
            {
                // Timeout: kill the child. After Kill, the child's stdout
                // and stderr handles are closed, so the async drainers
                // will unblock and complete on their own. We use a bounded
                // wait so we do not block forever if the kernel delays
                // the kill delivery.
                try { process.Kill(); } catch { /* best effort */ }
                try
                {
                    Task.WaitAll(new Task[] { stdoutTask, stderrDrain }, 1000);
                }
                catch
                {
                    // Drainer may have faulted on a closed stream; ignore.
                }
                return (string.Empty, null);
            }

            // stdout closed within the timeout — the child has exited (or is
            // about to). Wait for exit to capture the exit code, with a
            // bounded follow-up to handle the case where stdout is closed
            // but the process has not yet fully released.
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { /* best effort */ }
                process.WaitForExit(1000);
                return (stdoutTask.Result, null);
            }

            // Best-effort: make sure the stderr drainer is done so it does
            // not leak a task across process disposal. Bounded so a stuck
            // drainer cannot hang us.
            try
            {
                Task.WaitAll(new Task[] { stderrDrain }, 1000);
            }
            catch
            {
                // Drainer may have faulted on a closed stream; ignore.
            }

            return (stdoutTask.Result, process.ExitCode);
        }
        catch
        {
            return (string.Empty, null);
        }
        finally
        {
            // Manually dispose the process so we do not depend on `using`
            // syntax to clean up after a return from a non-using block.
            process?.Dispose();
        }
    }

    /// <summary>
    /// Exposes <see cref="RunXrandrCapture"/> for regression tests so the
    /// test assembly can drive the run helper with synthetic commands
    /// (e.g. /bin/sh -c "...large stderr...") without needing a real
    /// xrandr binary on the test machine.
    /// </summary>
    internal static class TestAccessor
    {
        public static (string output, int? exitCode) RunXrandrCapture(string fileName, string arguments, int timeoutMs)
        {
            return LinuxScreenService.RunXrandrCapture(fileName, arguments, timeoutMs);
        }
    }
}
