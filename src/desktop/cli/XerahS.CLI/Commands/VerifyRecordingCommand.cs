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
using System.Drawing;
using System.Text.Json;
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.CLI.Commands;

/// <summary>
/// CLI command to automate verification of the screen recording pipeline.
/// Supports random region selection, configurable duration, and multiple iterations.
/// </summary>
public static class VerifyRecordingCommand
{

    private const int DefaultDuration = 10;
    private const int DefaultIterations = 1;
    private const int DefaultRegionSize = 500;

    public static Command Create(IScreenRecordingCoordinator? recordingCoordinator)
    {
        var cmd = new Command("verify-recording", "Automated screen recording verification test");
        var inputOption = new Option<string?>("--input") { Description = "Inspect an existing recording with ffprobe; print stream and duration JSON" };
        var nativeOption = new Option<bool>("--native") { Description = "Require the native recorder and validate H.264/AAC output; fallback fails verification" };
        var pauseOption = new Option<int>("--pause-seconds") { Description = "Pause halfway through the active recording duration" };
        var audioCountOption = new Option<int?>("--expected-audio-streams") { Description = "Expected audio stream count when inspecting an existing recording (0 or 1)" };
        var expectedDurationOption = new Option<double?>("--expected-duration") { Description = "Expected active duration for --input, in seconds (1s tolerance)" };

        var workflowIdArg = new Argument<string?>("workflow-id")
        { 
            Description = "Workflow ID to execute for recording (default: 67f116dc)",
            Arity = ArgumentArity.ZeroOrOne
        };

        var randomRegionOption = new Option<bool>("--random-region")
        {
            Description = "Generate a random screen region for recording"
        };

        var durationOption = new Option<int>("--duration")
        {
            Description = "Recording duration in seconds (default: 10)"
        };

        var iterationsOption = new Option<int>("--iterations")
        {
            Description = "Number of verification attempts (default: 1)"
        };

        var debugOption = new Option<bool>("--debug")
        {
            Description = "Enable verbose debug logging"
        };

        var outputDirOption = new Option<string?>("--output-dir")
        {
            Description = "Output directory for verification logs (default: PersonalFolder/CaptureTroubleshooting/RecordingVerify)"
        };

        var regionSizeOption = new Option<int>("--region-size")
        {
            Description = "Size of random region (default: 500)"
        };

        cmd.Add(workflowIdArg);
        cmd.Add(randomRegionOption);
        cmd.Add(durationOption);
        cmd.Add(iterationsOption);
        cmd.Add(debugOption);
        cmd.Add(outputDirOption);
        cmd.Add(regionSizeOption);
        cmd.Add(inputOption);
        cmd.Add(nativeOption);
        cmd.Add(pauseOption);
        cmd.Add(audioCountOption);
        cmd.Add(expectedDurationOption);

        cmd.SetAction((parseResult) =>
        {
            if (parseResult.GetValue(inputOption) is string input)
            {
                return InspectFileAsync(input, parseResult.GetValue(nativeOption), parseResult.GetValue(audioCountOption),
                    parseResult.GetValue(expectedDurationOption)).GetAwaiter().GetResult();
            }
            var workflowId = parseResult.GetValue(workflowIdArg);
            var randomRegion = parseResult.GetValue(randomRegionOption);
            var duration = parseResult.GetValue(durationOption);
            var iterations = parseResult.GetValue(iterationsOption);
            var debug = parseResult.GetValue(debugOption);
            var outputDir = parseResult.GetValue(outputDirOption);
            var regionSize = parseResult.GetValue(regionSizeOption);

            // Apply defaults
            if (string.IsNullOrEmpty(workflowId)) workflowId = AppContracts.Cli.DefaultRecordingWorkflowId;
            if (duration == 0) duration = DefaultDuration;
            if (iterations == 0) iterations = DefaultIterations;
            if (regionSize == 0) regionSize = DefaultRegionSize;

            return RunVerificationAsync(
                recordingCoordinator ?? throw new InvalidOperationException("A recording coordinator is required to capture video."),
                workflowId!, randomRegion, duration, iterations, debug, outputDir, regionSize,
                parseResult.GetValue(nativeOption), parseResult.GetValue(pauseOption)
            ).GetAwaiter().GetResult();
        });

        return cmd;
    }

    private static async Task<int> InspectFileAsync(string input, bool native, int? audioCount, double? duration)
    {
        try
        {
            if (audioCount is < 0 or > 1 || duration is <= 0) throw new ArgumentException("Expected audio count must be 0 or 1 and expected duration must be positive.");
            var report = await RecordingMediaInspector.InspectAsync(input);
            var errors = report.Validate(native, audioCount, duration);
            Console.WriteLine(JsonSerializer.Serialize(new { Input = Path.GetFullPath(input), Media = report, Errors = errors },
                new JsonSerializerOptions { WriteIndented = true }));
            return errors.Count == 0 ? 0 : 1;
        }
        catch (Exception ex) { Console.Error.WriteLine($"Inspection failed: {ex.Message}"); return 2; }
    }

    private static async Task<int> RunVerificationAsync(
        IScreenRecordingCoordinator recordingCoordinator,
        string workflowId,
        bool randomRegion,
        int duration,
        int iterations,
        bool debug,
        string? outputDir,
        int regionSize,
        bool native,
        int pauseSeconds)
    {
        try
        {
            var verifier = new RecordingVerifier
            {
                RecordingCoordinator = recordingCoordinator,
                WorkflowId = workflowId,
                RandomRegion = randomRegion,
                Duration = duration,
                Iterations = iterations,
                Debug = debug,
                OutputDir = outputDir ?? Path.Combine(PathsManager.CaptureTroubleshootingFolder, "RecordingVerify"),
                RegionSize = regionSize,
                Native = native,
                PauseSeconds = pauseSeconds
            };

            Console.WriteLine("=== XerahS Recording Verification ===");
            Console.WriteLine($"Workflow: {verifier.WorkflowId}");
            Console.WriteLine($"Duration: {verifier.Duration}s");
            Console.WriteLine($"Iterations: {verifier.Iterations}");
            Console.WriteLine($"Random Region: {verifier.RandomRegion} (size: {verifier.RegionSize})");
            Console.WriteLine($"Debug: {verifier.Debug}");
            Console.WriteLine($"Output: {verifier.OutputDir}");
            Console.WriteLine();

            var result = await verifier.RunAsync();

            Console.WriteLine();
            Console.WriteLine("=== Verification Complete ===");
            Console.WriteLine($"Status: {(result.Success ? "PASS" : "FAIL")}");
            Console.WriteLine($"Passed: {result.Passed}/{result.Total}");
            Console.WriteLine($"Failed: {result.Failed}/{result.Total}");

            if (!string.IsNullOrEmpty(result.LastOutputPath))
            {
                Console.WriteLine($"Last Output: {result.LastOutputPath}");
            }

            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Verification failed: {ex.Message}");
            DebugHelper.WriteException(ex);
            return 2; // Test could not run
        }
    }
}

/// <summary>
/// Core verification logic for screen recording testing.
/// </summary>
internal class RecordingVerifier
{
    public required IScreenRecordingCoordinator RecordingCoordinator { get; init; }
    public string WorkflowId { get; set; } = AppContracts.Cli.DefaultRecordingWorkflowId;
    public bool RandomRegion { get; set; }
    public int Duration { get; set; } = 10;
    public int Iterations { get; set; } = 1;
    public bool Debug { get; set; }
    public string OutputDir { get; set; } = string.Empty;
    public int RegionSize { get; set; } = 500;
    public bool Native { get; set; }
    public int PauseSeconds { get; set; }

    private Random _random = new();
    private Rectangle _virtualDesktop;

    public async Task<RecordingVerificationResult> RunAsync()
    {
        if (Duration <= 0 || Iterations <= 0 || RegionSize < 2 || PauseSeconds < 0)
            throw new ArgumentException("Duration and iterations must be positive, region size at least 2, and pause seconds nonnegative.");
        if (RecordingCoordinator.PlatformInitializationTask is { } initialization) await initialization;
        Directory.CreateDirectory(OutputDir);

        // Discover virtual desktop
        var screenService = PlatformServices.Screen;
        if (screenService == null)
        {
            throw new InvalidOperationException("Screen service not available");
        }

        _virtualDesktop = screenService.GetVirtualScreenBounds();
        
        if (Debug)
        {
            Console.WriteLine($"Virtual Desktop: {_virtualDesktop.X},{_virtualDesktop.Y} {_virtualDesktop.Width}x{_virtualDesktop.Height}");
        }

        // Load workflow
        var workflow = SettingsManager.WorkflowsConfig?.Hotkeys?
            .FirstOrDefault(w => w.Id == WorkflowId);

        if (workflow == null)
        {
            throw new InvalidOperationException($"Workflow not found: {WorkflowId}. Use 'xerahs list workflows' to see available workflows.");
        }

        Console.WriteLine(Native ? "Backend: native required (fallback is a failure)." : "Backend: FFmpeg.");

        var result = new RecordingVerificationResult { Total = Iterations };

        for (int i = 0; i < Iterations; i++)
        {
            Console.WriteLine($"\n[{i + 1}/{Iterations}] Running recording iteration...");

            var iterResult = await RunIterationAsync(i, workflow);

            if (iterResult.Passed)
            {
                result.Passed++;
                result.LastOutputPath = iterResult.OutputPath;
                Console.WriteLine($"[{i + 1}/{Iterations}] PASS: {iterResult.OutputPath} ({iterResult.FileSize} bytes)");
            }
            else
            {
                result.Failed++;
                Console.WriteLine($"[{i + 1}/{Iterations}] FAIL: {iterResult.FailureReason}");
            }
        }

        result.Success = result.Failed == 0 && result.Passed > 0;
        return result;
    }

    private async Task<RecordingIterationResult> RunIterationAsync(int iteration, WorkflowSettings workflow)
    {
        var result = new RecordingIterationResult { Iteration = iteration };

        try
        {
            // Determine recording region
            Rectangle? region = null;
            if (RandomRegion)
            {
                region = GenerateRandomRectangle();
                Console.WriteLine($"  Random region: {region.Value.X},{region.Value.Y} {region.Value.Width}x{region.Value.Height}");
            }

            // Create recording options
            var settings = workflow.TaskSettings?.CaptureSettings?.ScreenRecordingSettings.Copy() ?? new ScreenRecordingSettings();
            settings.ForceFFmpeg = !Native;
            if (Native) settings.Codec = VideoCodec.H264;
            settings.FPS = workflow.TaskSettings?.CaptureSettings?.ScreenRecordFPS ?? settings.FPS;
            var recordingOptions = new RecordingOptions
            {
                Mode = region.HasValue ? CaptureMode.Region : CaptureMode.Screen,
                Region = region ?? Rectangle.Empty,
                OutputPath = GetDefaultRecordingPath(iteration),
                UseModernCapture = Native,
                Settings = settings
            };

            if (Debug)
            {
                Console.WriteLine($"  Output path: {recordingOptions.OutputPath}");
                Console.WriteLine($"  Mode: {recordingOptions.Mode}");
                Console.WriteLine($"  UseModernCapture: {recordingOptions.UseModernCapture}");
                Console.WriteLine($"  ForceFFmpeg: {recordingOptions.Settings?.ForceFFmpeg}");
            }

            // Start recording
            Console.WriteLine($"  Starting recording...");
            await RecordingCoordinator.StartRecordingAsync(recordingOptions);
            if (Native && RecordingCoordinator.IsUsingFallback)
                throw new InvalidOperationException("Native recording fell back to FFmpeg.");

            Console.WriteLine($"  Recording for {Duration} seconds...");
            if (PauseSeconds > 0)
            {
                if (!RecordingCoordinator.CurrentCapabilities.SupportsPauseResume) throw new NotSupportedException("The selected backend does not support pause/resume.");
                await Task.Delay(TimeSpan.FromSeconds(Duration / 2d));
                await RecordingCoordinator.TogglePauseResumeAsync();
                await Task.Delay(TimeSpan.FromSeconds(PauseSeconds));
                await RecordingCoordinator.TogglePauseResumeAsync();
                await Task.Delay(TimeSpan.FromSeconds(Duration / 2d));
            }
            else await Task.Delay(TimeSpan.FromSeconds(Duration));

            // Stop recording
            Console.WriteLine($"  Stopping recording...");
            var finalPath = await RecordingCoordinator.StopRecordingAsync();

            // Verify output
            if (string.IsNullOrEmpty(finalPath))
            {
                result.Passed = false;
                result.FailureReason = "StopRecordingAsync returned null/empty path";
                return result;
            }

            if (!File.Exists(finalPath))
            {
                result.Passed = false;
                result.FailureReason = $"Output file does not exist: {finalPath}";
                return result;
            }

            var fileInfo = new FileInfo(finalPath);
            if (fileInfo.Length == 0)
            {
                result.Passed = false;
                result.FailureReason = $"Output file is empty (0 bytes): {finalPath}";
                return result;
            }

            result.Passed = true;
            result.OutputPath = finalPath;
            result.FileSize = fileInfo.Length;
            var report = await RecordingMediaInspector.InspectAsync(finalPath);
            var errors = report.Validate(Native, settings.CaptureSystemAudio || settings.CaptureMicrophone ? 1 : 0, Duration);
            await File.WriteAllTextAsync(finalPath + ".verification.json", JsonSerializer.Serialize(
                new { Native, Duration, PauseSeconds, Media = report, Errors = errors }, new JsonSerializerOptions { WriteIndented = true }));
            result.Passed = errors.Count == 0;
            result.FailureReason = string.Join(" ", errors);
        }
        catch (Exception ex)
        {
            result.Passed = false;
            result.FailureReason = $"Exception: {ex.Message}";
            if (Debug)
            {
                DebugHelper.WriteException(ex);
            }
        }
        finally
        {
            if (RecordingCoordinator.IsRecording) await RecordingCoordinator.AbortRecordingAsync();
        }

        return result;
    }

    private Rectangle GenerateRandomRectangle()
    {
        int maxX = _virtualDesktop.X + _virtualDesktop.Width - RegionSize;
        int maxY = _virtualDesktop.Y + _virtualDesktop.Height - RegionSize;

        int x = _random.Next(_virtualDesktop.X, Math.Max(_virtualDesktop.X + 1, maxX + 1));
        int y = _random.Next(_virtualDesktop.Y, Math.Max(_virtualDesktop.Y + 1, maxY + 1));

        return new Rectangle(x, y, RegionSize, RegionSize);
    }

    private string GetDefaultRecordingPath(int iteration)
    {
        string folder = OutputDir;
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fileName = $"recording_iter{iteration:D3}_{timestamp}.mp4";
        return Path.Combine(folder, fileName);
    }
}

internal class RecordingVerificationResult
{
    public bool Success { get; set; }
    public int Total { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public string? LastOutputPath { get; set; }
}

internal class RecordingIterationResult
{
    public int Iteration { get; set; }
    public bool Passed { get; set; }
    public string? FailureReason { get; set; }
    public string? OutputPath { get; set; }
    public long FileSize { get; set; }
}
