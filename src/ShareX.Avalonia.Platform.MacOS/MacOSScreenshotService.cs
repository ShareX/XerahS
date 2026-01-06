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

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ShareX.Ava.Platform.Abstractions;
using SkiaSharp;
using DebugHelper = ShareX.Ava.Common.DebugHelper;
// REMOVED: System.Drawing usage

namespace ShareX.Ava.Platform.MacOS
{
    /// <summary>
    /// macOS screen capture implementation using the native screencapture CLI.
    /// </summary>
    public class MacOSScreenshotService : IScreenCaptureService
    {
        public Task<SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null)
        {
            return CaptureWithArgumentsAsync("-i -t png");
        }

        public Task<SKBitmap?> CaptureRectAsync(SKRect rect, CaptureOptions? options = null)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return Task.FromResult<SKBitmap?>(null);
            }

            var args = $"-R{rect.Left},{rect.Top},{rect.Width},{rect.Height} -x -t png";
            return CaptureWithArgumentsAsync(args);
        }

        public Task<SKBitmap?> CaptureFullScreenAsync(CaptureOptions? options = null)
        {
            return CaptureWithArgumentsAsync("-x -t png");
        }

        public Task<SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService, CaptureOptions? options = null)
        {
            return CaptureWithArgumentsAsync("-w -t png");
        }

        private static Task<SKBitmap?> CaptureWithArgumentsAsync(string arguments)
        {
            return Task.Run<SKBitmap?>(() =>
            {
                var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_ava_{Guid.NewGuid():N}.png");
                var totalStopwatch = Stopwatch.StartNew();

                try
                {
                    DebugHelper.WriteLine($"[MacOSCapture] Starting screencapture: args={arguments}");
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "screencapture",
                        Arguments = $"{arguments} \"{tempFile}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };

                    var startStopwatch = Stopwatch.StartNew();
                    using var process = Process.Start(startInfo);
                    if (process == null)
                    {
                        DebugHelper.WriteLine("[MacOSCapture] Failed to start screencapture process.");
                        return null;
                    }

                    process.WaitForExit();
                    startStopwatch.Stop();

                    if (process.ExitCode != 0 || !File.Exists(tempFile))
                    {
                        DebugHelper.WriteLine($"[MacOSCapture] screencapture failed: ExitCode={process.ExitCode}, FileExists={File.Exists(tempFile)}");
                        return null;
                    }

                    var fileInfo = new FileInfo(tempFile);
                    DebugHelper.WriteLine($"[MacOSCapture] screencapture completed in {startStopwatch.ElapsedMilliseconds}ms, size={fileInfo.Length} bytes");

                    var decodeStopwatch = Stopwatch.StartNew();
                    using var fileStream = File.OpenRead(tempFile);
                    // Copy to memory stream to decouple from file lock if needed?
                    // SKBitmap.Decode should read it fully.
                    // But to be safe and allow deleting file:
                    using (var ms = new MemoryStream())
                    {
                        fileStream.CopyTo(ms);
                        ms.Position = 0;
                        var bitmap = SKBitmap.Decode(ms);
                        decodeStopwatch.Stop();
                        DebugHelper.WriteLine($"[MacOSCapture] Decode completed in {decodeStopwatch.ElapsedMilliseconds}ms, bitmap={bitmap?.Width}x{bitmap?.Height}");
                        return bitmap;
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "MacOSScreenshotService: Capture failed");
                    return null;
                }
                finally
                {
                    totalStopwatch.Stop();
                    DebugHelper.WriteLine($"[MacOSCapture] Total capture elapsed: {totalStopwatch.ElapsedMilliseconds}ms");
                    if (File.Exists(tempFile))
                    {
                        try
                        {
                            File.Delete(tempFile);
                        }
                        catch
                        {
                        }
                    }
                }
            });
        }
    }
}
