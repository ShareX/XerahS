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
using System.CommandLine.Parsing;
using System.Drawing;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Core.Managers;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.CLI.Commands;

/// <summary>
/// CLI command to automate verification of the screen recording pipeline.
/// Supports random region selection, configurable duration, and multiple iterations.
/// </summary>
public static class VerifyRecordingCommand
{
    private const string DefaultWorkflowId = "67f116dc";
    private const int DefaultDuration = 10;
    private const int DefaultIterations = 1;
    private const int DefaultRegionSize = 500;

    public static Command Create()
    {
        var cmd = new Command("verify-recording", "Automated screen recording verification test");

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

        cmd.SetAction((parseResult) =>
        {
            var workflowId = parseResult.GetValue(workflowIdArg);
            var randomRegion = parseResult.GetValue(randomRegionOption);
            var duration = parseResult.GetValue(durationOption);
            var iterations = parseResult.GetValue(iterationsOption);
            var debug = parseResult.GetValue(debugOption);
            var outputDir = parseResult.GetValue(outputDirOption);
            var regionSize = parseResult.GetValue(regionSizeOption);

            // Apply defaults
            if (string.IsNullOrEmpty(workflowId)) workflowId = DefaultWorkflowId;
            if (duration == 0) duration = DefaultDuration;
            if (iterations == 0) iterations = DefaultIterations;
            if (regionSize == 0) regionSize = DefaultRegionSize;

            Environment.ExitCode = RunVerificationAsync(
                workflowId!, randomRegion, duration, iterations, debug, outputDir, regionSize
            ).GetAwaiter().GetResult();
        });

        return cmd;
    }

    private static async Task<int> RunVerificationAsync(
        string workflowId,
        bool randomRegion,
        int duration,
        int iterations,
        bool debug,
        string? outputDir,
        int regionSize)
    {
        try
        {
            var verifier = new RecordingVerifier
            {
                WorkflowId = workflowId,
                RandomRegion = randomRegion,
                Duration = duration,
                Iterations = iterations,
                Debug = debug,
                OutputDir = outputDir ?? Path.Combine(PathsManager.PersonalFolder, "CaptureTroubleshooting", "RecordingVerify"),
                RegionSize = regionSize
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
    public string WorkflowId { get; set; } = "67f116dc";
    public bool RandomRegion { get; set; }
    public int Duration { get; set; } = 10;
    public int Iterations { get; set; } = 1;
    public bool Debug { get; set; }
    public string OutputDir { get; set; } = string.Empty;
    public int RegionSize { get; set; } = 500;

    private Random _random = new();
    private Rectangle _virtualDesktop;

    public async Task<RecordingVerificationResult> RunAsync()
    {
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

        // Check modern capture setting
        bool useModernCapture = workflow.TaskSettings?.CaptureSettings?.UseModernCapture ?? true;
        if (useModernCapture)
        {
            Console.WriteLine("WARNING: Workflow has UseModernCapture=true. This test targets FFmpeg (legacy) mode.");
            Console.WriteLine("Consider setting UseModernCapture=false in the workflow for proper FFmpeg testing.");
        }

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

            // Initialize platform services
            ScreenRecordingManager.PlatformInitializationTask = Task.CompletedTask;
            SettingsManager.LoadAllSettings();

            // Create recording options
            var recordingOptions = new RecordingOptions
            {
                Mode = region.HasValue ? CaptureMode.Region : CaptureMode.Screen,
                Region = region ?? Rectangle.Empty,
                OutputPath = GetDefaultRecordingPath(iteration),
                UseModernCapture = workflow.TaskSettings?.CaptureSettings?.UseModernCapture ?? false,
                Settings = new ScreenRecordingSettings
                {
                    FPS = workflow.TaskSettings?.CaptureSettings?.ScreenRecordFPS ?? 30,
                    Codec = VideoCodec.H264,
                    BitrateKbps = 4000,
                    ShowCursor = true,
                    ForceFFmpeg = true // Always use FFmpeg for this test
                }
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
            var manager = ScreenRecordingManager.Instance;
            await manager.StartRecordingAsync(recordingOptions);

            Console.WriteLine($"  Recording for {Duration} seconds...");
            await Task.Delay(Duration * 1000);

            // Stop recording
            Console.WriteLine($"  Stopping recording...");
            var finalPath = await manager.StopRecordingAsync();

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
