using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ShareX.Ava.Platform.Abstractions;
using SkiaSharp;
// REMOVED: System.Drawing usage

namespace ShareX.Ava.Platform.MacOS
{
    /// <summary>
    /// macOS screen capture implementation using the native screencapture CLI.
    /// </summary>
    public class MacOSScreenshotService : IScreenCaptureService
    {
        public Task<SKBitmap?> CaptureRegionAsync()
        {
            return CaptureWithArgumentsAsync("-i -t png");
        }

        public Task<SKBitmap?> CaptureRectAsync(SKRect rect)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return Task.FromResult<SKBitmap?>(null);
            }

            var args = $"-R{rect.Left},{rect.Top},{rect.Width},{rect.Height} -x -t png";
            return CaptureWithArgumentsAsync(args);
        }

        public Task<SKBitmap?> CaptureFullScreenAsync()
        {
            return CaptureWithArgumentsAsync("-x -t png");
        }

        public Task<SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService)
        {
            return CaptureWithArgumentsAsync("-w -t png");
        }

        private static Task<SKBitmap?> CaptureWithArgumentsAsync(string arguments)
        {
            return Task.Run<SKBitmap?>(() =>
            {
                var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_ava_{Guid.NewGuid():N}.png");

                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "screencapture",
                        Arguments = $"{arguments} \"{tempFile}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };

                    using var process = Process.Start(startInfo);
                    if (process == null)
                    {
                        return null;
                    }

                    process.WaitForExit();
                    if (process.ExitCode != 0 || !File.Exists(tempFile))
                    {
                        return null;
                    }

                    using var fileStream = File.OpenRead(tempFile);
                    // Copy to memory stream to decouple from file lock if needed?
                    // SKBitmap.Decode should read it fully.
                    // But to be safe and allow deleting file:
                    using (var ms = new MemoryStream())
                    {
                        fileStream.CopyTo(ms);
                        ms.Position = 0;
                        return SKBitmap.Decode(ms);
                    }
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
                        }
                    }
                }
            });
        }
    }
}
