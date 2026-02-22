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
using System.CommandLine;
using System.IO;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.Core.Tasks;
using XerahS.Media;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.CLI.Commands;

public static class VerifyGifRecordingCommand
{
    private const int DefaultDuration = 5;
    private const int DefaultMaxWidth = 1280;

    public static Command Create()
    {
        var cmd = new Command("verify-gif-recording", "Verify GIF recording and conversion workflow");

        var durationOption = new Option<int>("--duration")
        {
            Description = "Recording duration in seconds (default: 5)"
        };

        var outputOption = new Option<string?>("--output")
        {
            Description = "Optional output path for the generated GIF"
        };

        var maxWidthOption = new Option<int?>("--max-width")
        {
            Description = "Max GIF width in pixels (default: 1280, set 0 to disable scaling)"
        };

        var debugOption = new Option<bool>("--debug")
        {
            Description = "Enable verbose debug logging"
        };

        cmd.Add(durationOption);
        cmd.Add(outputOption);
        cmd.Add(maxWidthOption);
        cmd.Add(debugOption);

        cmd.SetAction(parseResult =>
        {
            var duration = parseResult.GetValue(durationOption);
            var outputPath = parseResult.GetValue(outputOption);
            var maxWidth = parseResult.GetValue(maxWidthOption);
            var debug = parseResult.GetValue(debugOption);

            if (duration <= 0) duration = DefaultDuration;
            int resolvedMaxWidth = maxWidth ?? DefaultMaxWidth;
            if (resolvedMaxWidth < 0) resolvedMaxWidth = 0;

            Environment.ExitCode = RunVerifyGifAsync(duration, outputPath, resolvedMaxWidth, debug).GetAwaiter().GetResult();
        });

        return cmd;
    }

    private static async Task<int> RunVerifyGifAsync(int duration, string? outputPath, int maxWidth, bool debug)
    {
        try
        {
            Console.WriteLine("=== Verify GIF Recording ===");
            Console.WriteLine($"Duration: {duration}s");
            Console.WriteLine($"Output: {(string.IsNullOrWhiteSpace(outputPath) ? "(default)" : outputPath)}");
            Console.WriteLine($"MaxWidth: {maxWidth}");
            Console.WriteLine($"Debug: {debug}");

            if (debug)
            {
                System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(Console.Out));
            }

            string ffmpegPath = PathsManager.GetFFmpegPath();
            if (string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath))
            {
                Console.Error.WriteLine("FFmpeg path not found. Configure FFmpeg in XerahS settings before running this command.");
                return 2;
            }

            var settings = new TaskSettings
            {
                Job = WorkflowType.ScreenRecorderGIFCustomRegion,
                CaptureSettings = new TaskSettingsCapture
                {
                    UseModernCapture = false,
                    ShowCursor = true,
                    ScreenRecordingSettings = new ScreenRecordingSettings
                    {
                        FPS = 15,
                        Codec = VideoCodec.H264,
                        ShowCursor = true,
                        ForceFFmpeg = true
                    }
                }
            };

            settings.CaptureSettings.FFmpegOptions.GIFMaxWidth = maxWidth;

            using var worker = WorkerTask.Create(settings);

            Console.WriteLine("Starting GIF recording task...");
            var task = worker.StartAsync();

            Console.WriteLine($"Recording for {duration} seconds...");
            await Task.Delay(duration * 1000);

            Console.WriteLine("Stopping recording...");
            await ScreenRecordingManager.Instance.StopRecordingAsync();

            Console.WriteLine("Waiting for task completion (GIF conversion)...");
            await task;

            if (!worker.IsSuccessful || string.IsNullOrEmpty(worker.Info.FilePath))
            {
                Console.Error.WriteLine($"Task failed or produced no output. Status: {worker.Status}");
                if (worker.Error != null)
                {
                    Console.Error.WriteLine($"Exception: {worker.Error}");
                }
                return 1;
            }

            string sourcePath = worker.Info.FilePath;
            if (!File.Exists(sourcePath))
            {
                Console.Error.WriteLine($"Failure: Output file does not exist. Path: {sourcePath}");
                return 1;
            }

            if (!sourcePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Failure: Output is not a GIF. Path: {sourcePath}");
                return 1;
            }

            var info = new FileInfo(sourcePath);
            if (info.Length == 0)
            {
                Console.Error.WriteLine($"Failure: GIF output is empty. Path: {sourcePath}");
                return 1;
            }

            if (!GifHelpers.IsGif(sourcePath))
            {
                Console.Error.WriteLine("Failure: Output file is not a valid GIF (SKCodec validation failed).");
                return 1;
            }

            int frames = GifHelpers.GetFrameCount(sourcePath);
            if (frames <= 0)
            {
                Console.Error.WriteLine("Failure: GIF has zero frames.");
                return 1;
            }

            string finalPath = ResolveOutputPath(outputPath, sourcePath);
            if (!string.Equals(finalPath, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                var directory = Path.GetDirectoryName(finalPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.Move(sourcePath, finalPath, overwrite: true);
                worker.Info.FilePath = finalPath;
                info = new FileInfo(finalPath);
            }

            Console.WriteLine($"Success! Generated GIF: {info.Length} bytes");
            Console.WriteLine($"GIF frames: {frames}");
            Console.WriteLine($"Output: {worker.Info.FilePath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Verification Exception: {ex}");
            return 2;
        }
    }

    private static string ResolveOutputPath(string? outputPath, string defaultPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return defaultPath;
        }

        string trimmed = outputPath.Trim();
        if (trimmed.EndsWith(Path.DirectorySeparatorChar) || trimmed.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return Path.Combine(trimmed, Path.GetFileName(defaultPath));
        }

        if (Directory.Exists(trimmed) || string.IsNullOrEmpty(Path.GetExtension(trimmed)))
        {
            return Path.Combine(trimmed, Path.GetFileName(defaultPath));
        }

        string resolved = trimmed;
        if (!string.Equals(Path.GetExtension(resolved), ".gif", StringComparison.OrdinalIgnoreCase))
        {
            resolved = Path.ChangeExtension(resolved, ".gif");
        }

        return resolved;
    }
}
