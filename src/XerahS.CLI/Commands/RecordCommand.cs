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

using System.CommandLine;
using System.Drawing;
using XerahS.Common;
using XerahS.Core.Managers;
using XerahS.ScreenCapture.ScreenRecording;

namespace XerahS.CLI.Commands
{
    public static class RecordCommand
    {
        public static Command Create()
        {
            var recordCommand = new Command("record", "Screen recording operations");

            // Start recording subcommand
            var startCommand = new Command("start", "Start screen recording");

            var modeOption = new Option<string>(
                name: "--mode",
                description: "Capture mode: screen, window, region",
                getDefaultValue: () => "screen");

            var regionOption = new Option<string?>(
                name: "--region",
                description: "Region in format 'x,y,width,height'");

            var fpsOption = new Option<int>(
                name: "--fps",
                description: "Frames per second",
                getDefaultValue: () => 30);

            var codecOption = new Option<string>(
                name: "--codec",
                description: "Video codec: h264, hevc, vp9, av1",
                getDefaultValue: () => "h264");

            var bitrateOption = new Option<int>(
                name: "--bitrate",
                description: "Bitrate in Kbps",
                getDefaultValue: () => 4000);

            var audioOption = new Option<bool>(
                name: "--audio",
                description: "Capture system audio");

            var microphoneOption = new Option<bool>(
                name: "--microphone",
                description: "Capture microphone");

            var outputOption = new Option<string?>(
                name: "--output",
                description: "Output file path");

            startCommand.AddOption(modeOption);
            startCommand.AddOption(regionOption);
            startCommand.AddOption(fpsOption);
            startCommand.AddOption(codecOption);
            startCommand.AddOption(bitrateOption);
            startCommand.AddOption(audioOption);
            startCommand.AddOption(microphoneOption);
            startCommand.AddOption(outputOption);

            startCommand.SetHandler(async (string mode, string? region, int fps,
                string codec, int bitrate, bool audio, bool microphone, string? output) =>
            {
                Environment.ExitCode = await StartRecordingAsync(mode, region, fps, codec, bitrate,
                    audio, microphone, output);
            }, modeOption, regionOption, fpsOption, codecOption, bitrateOption,
               audioOption, microphoneOption, outputOption);

            // Stop recording subcommand
            var stopCommand = new Command("stop", "Stop active recording");
            stopCommand.SetHandler(async () =>
            {
                Environment.ExitCode = await StopRecordingAsync();
            });

            // Abort recording subcommand
            var abortCommand = new Command("abort", "Abort recording without saving");
            abortCommand.SetHandler(async () =>
            {
                Environment.ExitCode = await AbortRecordingAsync();
            });

            recordCommand.AddCommand(startCommand);
            recordCommand.AddCommand(stopCommand);
            recordCommand.AddCommand(abortCommand);

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

                // Generate default output path if not provided
                // Manager will handle it if null
                string? outputPath = output;

                var recordingOptions = new RecordingOptions
                {
                    Mode = captureMode,
                    Region = regionRect ?? Rectangle.Empty,
                    OutputPath = outputPath,
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
                if (audio || microphone)
                {
                    Console.WriteLine($"Audio: system={audio}, microphone={microphone}");
                }

                var manager = ScreenRecordingManager.Instance;
                await manager.StartRecordingAsync(recordingOptions);

                Console.WriteLine($"Recording started. Output: {recordingOptions.OutputPath}");
                Console.WriteLine("Use 'xerahs record stop' to finish recording.");

                return 0;
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


    }
}
