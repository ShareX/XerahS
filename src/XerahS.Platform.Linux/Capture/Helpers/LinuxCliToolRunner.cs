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

namespace XerahS.Platform.Linux.Capture.Helpers;

/// <summary>
/// Runs a screenshot CLI tool (e.g. gnome-screenshot, spectacle, scrot, import)
/// with a temp file output and returns the decoded bitmap. Testable utility.
/// </summary>
internal static class LinuxCliToolRunner
{
    /// <summary>
    /// Default timeout for non-interactive tools (e.g. fullscreen capture).
    /// </summary>
    public const int DefaultTimeoutMs = 10000;

    /// <summary>
    /// Timeout for interactive tools (e.g. region selection with gnome-screenshot -a).
    /// </summary>
    public const int InteractiveTimeoutMs = 60000;

    /// <summary>
    /// Runs the given tool with the args prefix; tool is expected to write PNG to the temp path we pass.
    /// </summary>
    /// <param name="toolName">Executable name (e.g. "gnome-screenshot", "spectacle").</param>
    /// <param name="argsPrefix">Arguments before the output path, e.g. "-a -f" or "-b -n -o". Empty to pass only the path.</param>
    /// <param name="timeoutMs">Max wait in ms.</param>
    /// <returns>Decoded bitmap or null on failure/timeout.</returns>
    public static async Task<SKBitmap?> RunAsync(string toolName, string argsPrefix, int timeoutMs = DefaultTimeoutMs)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");

        try
        {
            var arguments = string.IsNullOrEmpty(argsPrefix)
                ? $"\"{tempFile}\""
                : $"{argsPrefix} \"{tempFile}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = toolName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            var completed = await Task.Run(() => process.WaitForExit(timeoutMs)).ConfigureAwait(false);
            if (!completed)
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // ignore
                }

                return null;
            }

            if (process.ExitCode != 0 || !File.Exists(tempFile))
            {
                return null;
            }

            DebugHelper.WriteLine($"LinuxScreenCaptureService: Screenshot captured with {toolName}");

            using var stream = File.OpenRead(tempFile);
            return SKBitmap.Decode(stream);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
