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
using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Linux.Capture.Contracts;

namespace XerahS.Platform.Linux.Capture.Wayland;

/// <summary>
/// Wayland capture via grim, slurp, grimblast, hyprshot. Use only when Wayland is active.
/// </summary>
internal static class WaylandCliCapture
{
    public static bool IsWayland =>
        Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")?.Equals("wayland", StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsSlurpAvailable()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "slurp",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using var process = Process.Start(startInfo);
            process?.WaitForExit(3000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Region selection using slurp (coordinates only, no screenshot). For recording.
    /// </summary>
    public static async Task<SKRectI> SelectRegionWithSlurpAsync()
    {
        try
        {
            var slurpStartInfo = new ProcessStartInfo
            {
                FileName = "slurp",
                Arguments = "-f \"%x %y %w %h\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var slurpProcess = Process.Start(slurpStartInfo);
            if (slurpProcess == null)
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: Failed to start slurp process");
                return SKRectI.Empty;
            }
            var completed = await Task.Run(() => slurpProcess.WaitForExit(60000)).ConfigureAwait(false);
            if (!completed)
            {
                try { slurpProcess.Kill(); } catch { }
                DebugHelper.WriteLine("LinuxScreenCaptureService: slurp timed out");
                return SKRectI.Empty;
            }
            if (slurpProcess.ExitCode != 0)
            {
                DebugHelper.WriteLine($"LinuxScreenCaptureService: slurp exited with code {slurpProcess.ExitCode} (likely cancelled)");
                return SKRectI.Empty;
            }
            string output = (await slurpProcess.StandardOutput.ReadToEndAsync().ConfigureAwait(false)).Trim();
            DebugHelper.WriteLine($"LinuxScreenCaptureService: slurp output: '{output}'");
            var parts = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4 &&
                int.TryParse(parts[0], out int x) &&
                int.TryParse(parts[1], out int y) &&
                int.TryParse(parts[2], out int w) &&
                int.TryParse(parts[3], out int h))
            {
                DebugHelper.WriteLine($"LinuxScreenCaptureService: slurp region selected: x={x}, y={y}, w={w}, h={h}");
                return new SKRectI(x, y, x + w, y + h);
            }
            DebugHelper.WriteLine($"LinuxScreenCaptureService: Failed to parse slurp output: '{output}'");
            return SKRectI.Empty;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"LinuxScreenCaptureService: slurp exception: {ex.Message}");
            return SKRectI.Empty;
        }
    }

    /// <summary>
    /// Capture using Wayland CLI tools (grim/slurp/grimblast/hyprshot). Returns null if not Wayland.
    /// </summary>
    public static async Task<SKBitmap?> CaptureAsync(LinuxCaptureKind kind, string? desktop)
    {
        if (!IsWayland)
        {
            return null;
        }
        DebugHelper.WriteLine("LinuxScreenCaptureService: [Stage 3/4] Trying Wayland protocol/tool fallbacks");

        return kind switch
        {
            LinuxCaptureKind.Region => await CaptureRegionAsync(desktop).ConfigureAwait(false),
            LinuxCaptureKind.FullScreen => await CaptureWithGrimAsync().ConfigureAwait(false),
            LinuxCaptureKind.ActiveWindow => await CaptureActiveWindowAsync(desktop).ConfigureAwait(false),
            _ => null
        };
    }

    private static bool IsWlrootsDesktop(string? desktop) =>
        desktop == "HYPRLAND" || desktop == "SWAY";

    private static async Task<SKBitmap?> CaptureRegionAsync(string? desktop)
    {
        if (desktop == "HYPRLAND")
        {
            var r = await CaptureWithGrimblastRegionAsync().ConfigureAwait(false);
            if (r != null) return r;
            r = await CaptureWithHyprshotRegionAsync().ConfigureAwait(false);
            if (r != null) return r;
        }
        if (IsWlrootsDesktop(desktop) || desktop == null)
        {
            var r = await CaptureWithGrimSlurpAsync().ConfigureAwait(false);
            if (r != null) return r;
        }
        return null;
    }

    private static async Task<SKBitmap?> CaptureActiveWindowAsync(string? desktop)
    {
        if (desktop == "HYPRLAND")
        {
            var r = await CaptureWithGrimblastActiveWindowAsync().ConfigureAwait(false);
            if (r != null) return r;
            r = await CaptureWithHyprshotWindowAsync().ConfigureAwait(false);
            if (r != null) return r;
        }
        if (IsWlrootsDesktop(desktop) || desktop == null)
        {
            return await CaptureWithGrimSlurpAsync().ConfigureAwait(false);
        }
        return null;
    }

    private static async Task<SKBitmap?> CaptureWithGrimblastRegionAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "grimblast",
                Arguments = $"save area \"{tempFile}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process == null) return null;
            var completed = await Task.Run(() => process.WaitForExit(60000)).ConfigureAwait(false);
            if (!completed) { try { process.Kill(); } catch { } return null; }
            if (process.ExitCode != 0 || !File.Exists(tempFile)) return null;
            DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with grimblast");
            using var stream = File.OpenRead(tempFile);
            return SKBitmap.Decode(stream);
        }
        catch { return null; }
        finally { TryDelete(tempFile); }
    }

    private static async Task<SKBitmap?> CaptureWithGrimSlurpAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");
        try
        {
            string? geometry;
            using (var slurpProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "slurp",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }))
            {
                if (slurpProcess == null) return null;
                var completed = await Task.Run(() => slurpProcess.WaitForExit(60000)).ConfigureAwait(false);
                if (!completed) { try { slurpProcess.Kill(); } catch { } return null; }
                if (slurpProcess.ExitCode != 0) return null;
                geometry = (await slurpProcess.StandardOutput.ReadToEndAsync().ConfigureAwait(false)).Trim();
                if (string.IsNullOrEmpty(geometry)) return null;
            }
            using var grimProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "grim",
                Arguments = $"-g \"{geometry}\" \"{tempFile}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (grimProcess == null) return null;
            var grimCompleted = await Task.Run(() => grimProcess.WaitForExit(10000)).ConfigureAwait(false);
            if (!grimCompleted) { try { grimProcess.Kill(); } catch { } return null; }
            if (grimProcess.ExitCode != 0 || !File.Exists(tempFile)) return null;
            DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with grim+slurp");
            using var stream = File.OpenRead(tempFile);
            return SKBitmap.Decode(stream);
        }
        catch { return null; }
        finally { TryDelete(tempFile); }
    }

    private static async Task<SKBitmap?> CaptureWithHyprshotRegionAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "hyprshot",
                Arguments = $"-m region -o \"{Path.GetDirectoryName(tempFile)}\" -f \"{Path.GetFileName(tempFile)}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process == null) return null;
            var completed = await Task.Run(() => process.WaitForExit(60000)).ConfigureAwait(false);
            if (!completed) { try { process.Kill(); } catch { } return null; }
            if (process.ExitCode != 0 || !File.Exists(tempFile)) return null;
            DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with hyprshot");
            using var stream = File.OpenRead(tempFile);
            return SKBitmap.Decode(stream);
        }
        catch { return null; }
        finally { TryDelete(tempFile); }
    }

    private static async Task<SKBitmap?> CaptureWithGrimblastActiveWindowAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "grimblast",
                Arguments = $"save active \"{tempFile}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process == null) return null;
            var completed = await Task.Run(() => process.WaitForExit(60000)).ConfigureAwait(false);
            if (!completed) { try { process.Kill(); } catch { } return null; }
            if (process.ExitCode != 0 || !File.Exists(tempFile)) return null;
            DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with grimblast (active window)");
            using var stream = File.OpenRead(tempFile);
            return SKBitmap.Decode(stream);
        }
        catch { return null; }
        finally { TryDelete(tempFile); }
    }

    private static async Task<SKBitmap?> CaptureWithHyprshotWindowAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "hyprshot",
                Arguments = $"-m window -o \"{Path.GetDirectoryName(tempFile)}\" -f \"{Path.GetFileName(tempFile)}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process == null) return null;
            var completed = await Task.Run(() => process.WaitForExit(60000)).ConfigureAwait(false);
            if (!completed) { try { process.Kill(); } catch { } return null; }
            if (process.ExitCode != 0 || !File.Exists(tempFile)) return null;
            DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with hyprshot (window)");
            using var stream = File.OpenRead(tempFile);
            return SKBitmap.Decode(stream);
        }
        catch { return null; }
        finally { TryDelete(tempFile); }
    }

    private static async Task<SKBitmap?> CaptureWithGrimAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "grim",
                Arguments = $"\"{tempFile}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process == null) return null;
            var completed = await Task.Run(() => process.WaitForExit(10000)).ConfigureAwait(false);
            if (!completed) { try { process.Kill(); } catch { } return null; }
            if (process.ExitCode != 0 || !File.Exists(tempFile)) return null;
            DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with grim");
            using var stream = File.OpenRead(tempFile);
            return SKBitmap.Decode(stream);
        }
        catch { return null; }
        finally { TryDelete(tempFile); }
    }

    private static void TryDelete(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try { File.Delete(path); } catch { }
    }
}
