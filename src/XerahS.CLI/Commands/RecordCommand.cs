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

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Drawing;
using XerahS.CLI;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.CLI.Commands
{
    public static class RecordCommand
    {
        public static Command Create()
        {
            var recordCommand = new Command("record", "Screen recording operations");

            // Start recording subcommand
            var startCommand = new Command("start", "Start screen recording");

            var modeOption = new Option<string>("--mode") { Description = "Capture mode: screen, window, region" };
            var regionOption = new Option<string?>("--region") { Description = "Region in format 'x,y,width,height'" };
            var fpsOption = new Option<int>("--fps") { Description = "Frames per second" };
            var codecOption = new Option<string>("--codec") { Description = "Video codec: h264, hevc, vp9, av1" };
            var bitrateOption = new Option<int>("--bitrate") { Description = "Bitrate in Kbps" };
            var audioOption = new Option<bool>("--audio") { Description = "Capture system audio" };
            var microphoneOption = new Option<bool>("--microphone") { Description = "Capture microphone" };
            var outputOption = new Option<string?>("--output") { Description = "Output file path" };

            startCommand.Add(modeOption);
            startCommand.Add(regionOption);
            startCommand.Add(fpsOption);
            startCommand.Add(codecOption);
            startCommand.Add(bitrateOption);
            startCommand.Add(audioOption);
            startCommand.Add(microphoneOption);
            startCommand.Add(outputOption);

            startCommand.SetAction((parseResult) =>
            {
                var mode = parseResult.GetValue(modeOption) ?? "screen";
                var region = parseResult.GetValue(regionOption);
                var fps = parseResult.GetValue(fpsOption);
                var codec = parseResult.GetValue(codecOption) ?? "h264";
                var bitrate = parseResult.GetValue(bitrateOption);
                var audio = parseResult.GetValue(audioOption);
                var microphone = parseResult.GetValue(microphoneOption);
                var output = parseResult.GetValue(outputOption);

                // apply defaults manually
                if (fps == 0) fps = 30;
                if (bitrate == 0) bitrate = 4000;

                Environment.ExitCode = StartRecordingAsync(mode, region, fps, codec, bitrate,
                    audio, microphone, output).GetAwaiter().GetResult();
            });

            // Stop recording subcommand
            var stopCommand = new Command("stop", "Stop active recording");
            stopCommand.SetAction((parseResult) =>
            {
                Environment.ExitCode = StopRecordingAsync().GetAwaiter().GetResult();
            });

            // Abort recording subcommand
            var abortCommand = new Command("abort", "Abort recording without saving");
            abortCommand.SetAction((parseResult) =>
            {
                Environment.ExitCode = AbortRecordingAsync().GetAwaiter().GetResult();
            });

            recordCommand.Add(startCommand);
            recordCommand.Add(stopCommand);
            recordCommand.Add(abortCommand);

            return recordCommand;
        }

        private static async Task<int> StartRecordingAsync(string mode, string? region,
            int fps, string codec, int bitrate, bool audio, bool microphone, string? output)
        {
            try
            {
                var captureMode = mode.ToLower() switch
                {
                    "screen" => CaptureMode.Screen,
                    "window" => CaptureMode.Window,
                    "region" => CaptureMode.Region,
                    _ => throw new ArgumentException($"Invalid mode: {mode}")
                };

                var videoCodec = codec.ToLower() switch
                {
                    "h264" => VideoCodec.H264,
                    "hevc" => VideoCodec.HEVC,
                    "vp9" => VideoCodec.VP9,
                    "av1" => VideoCodec.AV1,
                    _ => throw new ArgumentException($"Invalid codec: {codec}")
                };

                Rectangle? regionRect = null;
                if (captureMode == CaptureMode.Region && !string.IsNullOrEmpty(region))
                {
                    var parts = region.Split(',');
                    if (parts.Length == 4)
                    {
                        regionRect = new Rectangle(
                            int.Parse(parts[0]),
                            int.Parse(parts[1]),
                            int.Parse(parts[2]),
                            int.Parse(parts[3]));
                    }
                    else
                    {
                        Console.Error.WriteLine("Invalid region format. Expected: 'x,y,width,height'");
                        return 1;
                    }
                }

                // Generate default output path if not provided (match app naming conventions)
                string? outputPath = output;

                var recordingOptions = new RecordingOptions
                {
                    Mode = captureMode,
                    Region = regionRect ?? Rectangle.Empty,
                    OutputPath = outputPath,
                    UseModernCapture = true, // Default to modern capture in CLI
                    Settings = new ScreenRecordingSettings
                    {
                        FPS = fps,
                        Codec = videoCodec,
                        BitrateKbps = bitrate,
                        CaptureSystemAudio = audio,
                        CaptureMicrophone = microphone,
                        ShowCursor = true,
                        ForceFFmpeg = audio || microphone // Force FFmpeg if audio needed
                    }
                };

                Console.WriteLine($"Starting recording: {mode} mode, {fps}fps, {codec} codec");
                // Initialize platform services
            // In CLI, we might need a minimal initialization
            ScreenRecordingManager.PlatformInitializationTask = Task.CompletedTask;
            
            // Initialize Settings
            SettingsManager.LoadAllSettings();

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = GetDefaultRecordingPath(captureMode);
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    Console.Error.WriteLine("Recording output path could not be resolved.");
                    return 1;
                }
            }
            var manager = ScreenRecordingManager.Instance;
            await manager.StartRecordingAsync(recordingOptions);

                Console.WriteLine($"Recording started. Output: {recordingOptions.OutputPath}");
                Console.WriteLine("Press ENTER to stop recording...");

                // Block and wait for user input to stop
                Console.ReadLine();

                Console.WriteLine("Stopping recording...");
                var finalPath = await manager.StopRecordingAsync();
                
                if (!string.IsNullOrEmpty(finalPath))
                {
                    Console.WriteLine($"Recording saved: {finalPath}");
                    return 0;
                }
                else
                {
                    Console.Error.WriteLine("Recording stopped but output path not available.");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to start recording: {ex.Message}");
                DebugHelper.WriteException(ex);
                return 1;
            }
        }

        private static async Task<int> StopRecordingAsync()
        {
            try
            {
                var manager = ScreenRecordingManager.Instance;

                if (!manager.IsRecording)
                {
                    Console.WriteLine("No active recording.");
                    return 0;
                }

                Console.WriteLine("Stopping recording...");
                var outputPath = await manager.StopRecordingAsync();

                if (!string.IsNullOrEmpty(outputPath))
                {
                    Console.WriteLine($"Recording saved: {outputPath}");
                    return 0;
                }
                else
                {
                    Console.Error.WriteLine("Recording stopped but output path not available.");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to stop recording: {ex.Message}");
                DebugHelper.WriteException(ex);
                return 1;
            }
        }

        private static async Task<int> AbortRecordingAsync()
        {
            try
            {
                var manager = ScreenRecordingManager.Instance;

                if (!manager.IsRecording)
                {
                    Console.WriteLine("No active recording.");
                    return 0;
                }

                Console.WriteLine("Aborting recording...");
                await manager.AbortRecordingAsync();
                Console.WriteLine("Recording aborted.");

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to abort recording: {ex.Message}");
                DebugHelper.WriteException(ex);
                return 1;
            }
        }

        private static string? GetDefaultRecordingPath(CaptureMode captureMode)
        {
            WorkflowType workflowType = captureMode switch
            {
                CaptureMode.Window => WorkflowType.ScreenRecorderActiveWindow,
                CaptureMode.Region => WorkflowType.ScreenRecorderCustomRegion,
                _ => WorkflowType.ScreenRecorder
            };

            var workflow = SettingsManager.GetFirstWorkflowOrDefault(workflowType);
            TaskSettings taskSettings = workflow?.TaskSettings ?? SettingsManager.DefaultTaskSettings;

            string folder = TaskHelpers.GetScreenshotsFolder(taskSettings);
            FileHelpers.CreateDirectory(folder);

            string fileName = TaskHelpers.GetFileName(taskSettings, "mp4");
            string filePath = Path.Combine(folder, fileName);

            filePath = TaskHelpers.HandleExistsFile(filePath, taskSettings);
            return string.IsNullOrWhiteSpace(filePath) ? null : filePath;
        }


    }
}
