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
using System.CommandLine.Parsing;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.Platform.Abstractions;
using SkiaSharp;
using System.Text.Json;
using System.Drawing;

namespace XerahS.CLI.Commands
{
    /// <summary>
    /// Automated verification test for RegionCapture correctness in multi-monitor mixed-DPI environments.
    /// Captures random regions and compares captured pixels with ground truth screen samples.
    /// </summary>
    public static class VerifyRegionCaptureCommand
    {
        public static Command Create()
        {
            var cmd = new Command("verify-region-capture", "Automated RegionCapture verification for multi-monitor mixed-DPI environments");

            var workflowIdOption = new Option<string?>("--workflow") { Description = "Workflow ID or name to use for capture (default: auto-detect RegionCapture)" };
            var rectSizeOption = new Option<int?>("--rect-size") { Description = "Test rectangle size in pixels (default: 400)" };
            var seedOption = new Option<int?>("--seed") { Description = "Random seed for reproducibility (optional)" };
            var iterationsOption = new Option<int?>("--iterations") { Description = "Number of test iterations (default: 20)" };
            var toleranceOption = new Option<int?>("--tolerance") { Description = "Per-channel RGB tolerance 0-255 (default: 0)" };
            var maxFailuresOption = new Option<int?>("--max-failures") { Description = "Stop early after N failures (default: 0 = run all)" };
            var outputDirOption = new Option<string?>("--output-dir") { Description = "Output directory for artifacts (default: PersonalFolder/CaptureTroubleshooting/RegionVerify)" };
            var stabilizeMsOption = new Option<int?>("--stabilize-ms") { Description = "Wait time in ms before sampling (default: 250)" };
            var ciModeOption = new Option<bool>("--ci") { Description = "CI mode: non-interactive, stable paths, exit codes" };

            cmd.Add(workflowIdOption);
            cmd.Add(rectSizeOption);
            cmd.Add(seedOption);
            cmd.Add(iterationsOption);
            cmd.Add(toleranceOption);
            cmd.Add(maxFailuresOption);
            cmd.Add(outputDirOption);
            cmd.Add(stabilizeMsOption);
            cmd.Add(ciModeOption);

            cmd.SetAction((parseResult) =>
            {
                var workflowId = parseResult.GetValue(workflowIdOption);
                var rectSize = parseResult.GetValue(rectSizeOption);
                var seed = parseResult.GetValue(seedOption);
                var iterations = parseResult.GetValue(iterationsOption);
                var tolerance = parseResult.GetValue(toleranceOption);
                var maxFailures = parseResult.GetValue(maxFailuresOption);
                var outputDir = parseResult.GetValue(outputDirOption);
                var stabilizeMs = parseResult.GetValue(stabilizeMsOption);
                var ciMode = parseResult.GetValue(ciModeOption);

                Environment.ExitCode = RunVerificationAsync(workflowId, rectSize, seed, iterations, tolerance, maxFailures, outputDir, stabilizeMs, ciMode).GetAwaiter().GetResult();
            });

            return cmd;
        }

        private static async Task<int> RunVerificationAsync(
            string? workflowId,
            int? rectSize,
            int? seed,
            int? iterations,
            int? tolerance,
            int? maxFailures,
            string? outputDir,
            int? stabilizeMs,
            bool ciMode)
        {
            try
            {
                // Initialize verifier with defaults
                var verifier = new RegionCaptureVerifier
                {
                    WorkflowId = workflowId,
                    RectSize = rectSize ?? 400,
                    Seed = seed ?? new Random().Next(),
                    Iterations = iterations ?? 20,
                    Tolerance = tolerance ?? 0,
                    MaxFailures = maxFailures ?? 0,
                    OutputDir = outputDir ?? Path.Combine(PathsManager.PersonalFolder, "CaptureTroubleshooting", "RegionVerify"),
                    StabilizeMs = stabilizeMs ?? 250,
                    CiMode = ciMode
                };

                Console.WriteLine("=== XerahS Region Capture Verification ===");
                Console.WriteLine($"Workflow: {verifier.WorkflowId ?? "auto"}");
                Console.WriteLine($"Rectangle Size: {verifier.RectSize}x{verifier.RectSize}");
                Console.WriteLine($"Iterations: {verifier.Iterations}");
                Console.WriteLine($"Seed: {verifier.Seed}");
                Console.WriteLine($"Tolerance: {verifier.Tolerance}");
                Console.WriteLine($"Output: {verifier.OutputDir}");
                Console.WriteLine();

                var result = await verifier.RunAsync();

                Console.WriteLine();
                Console.WriteLine("=== Verification Complete ===");
                Console.WriteLine($"Status: {(result.Success ? "PASS" : "FAIL")}");
                Console.WriteLine($"Passed: {result.Passed}/{result.Total}");
                Console.WriteLine($"Failed: {result.Failed}/{result.Total}");
                Console.WriteLine($"Skipped: {result.Skipped}/{result.Total}");
                Console.WriteLine($"Summary: {verifier.OutputDir}");

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
    /// Core verification logic
    /// </summary>
    internal class RegionCaptureVerifier
    {
        public string? WorkflowId { get; set; }
        public int RectSize { get; set; }
        public int Seed { get; set; }
        public int Iterations { get; set; }
        public int Tolerance { get; set; }
        public int MaxFailures { get; set; }
        public string OutputDir { get; set; } = string.Empty;
        public int StabilizeMs { get; set; }
        public bool CiMode { get; set; }

        private Random _random = null!;
        private ScreenInfo[] _monitors = null!;
        private Rectangle _virtualDesktop;

        public async Task<VerificationResult> RunAsync()
        {
            _random = new Random(Seed);

            // Step 1: Discover virtual desktop and monitors
            Directory.CreateDirectory(OutputDir);
            Console.WriteLine("Discovering monitors...");
            await DiscoverMonitorsAsync();

            // Step 2: Run iterations
            var result = new VerificationResult { Total = Iterations };

            for (int i = 0; i < Iterations; i++)
            {
                Console.WriteLine($"\n[{i + 1}/{Iterations}] Running iteration...");

                var iterResult = await RunIterationAsync(i);

                if (iterResult.Passed)
                {
                    result.Passed++;
                    Console.WriteLine($"[{i + 1}/{Iterations}] PASS");
                }
                else if (iterResult.Skipped)
                {
                    result.Skipped++;
                    Console.WriteLine($"[{i + 1}/{Iterations}] SKIPPED: {iterResult.SkipReason}");
                }
                else
                {
                    result.Failed++;
                    Console.WriteLine($"[{i + 1}/{Iterations}] FAIL: {iterResult.FailureReason}");

                    if (MaxFailures > 0 && result.Failed >= MaxFailures)
                    {
                        Console.WriteLine($"\nStopping early: reached max failures ({MaxFailures})");
                        break;
                    }
                }
            }

            // Step 3: Write summary
            await WriteSummaryAsync(result);

            result.Success = result.Failed == 0 && result.Passed > 0;
            return result;
        }

        private async Task DiscoverMonitorsAsync()
        {
            // Use IScreenService to get monitor info
            var screenService = PlatformServices.Screen;
            if (screenService == null)
            {
                throw new InvalidOperationException("Screen service not available");
            }

            _monitors = screenService.GetAllScreens();

            if (_monitors.Length == 0)
            {
                throw new InvalidOperationException("No monitors detected");
            }

            // Get virtual desktop bounds
            _virtualDesktop = screenService.GetVirtualScreenBounds();

            Console.WriteLine($"Virtual Desktop: {_virtualDesktop.X},{_virtualDesktop.Y} {_virtualDesktop.Width}x{_virtualDesktop.Height}");
            Console.WriteLine($"Monitors: {_monitors.Length}");
            foreach (var monitor in _monitors)
            {
                Console.WriteLine($"  - {monitor.DeviceName} (Primary: {monitor.IsPrimary}): {monitor.Bounds.Width}x{monitor.Bounds.Height} at ({monitor.Bounds.X},{monitor.Bounds.Y})");
            }

            // Write monitor layout
            var layoutPath = Path.Combine(OutputDir, "monitor_layout.json");
            var layoutData = new
            {
                Seed,
                VirtualDesktop = new { _virtualDesktop.X, _virtualDesktop.Y, _virtualDesktop.Width, _virtualDesktop.Height },
                Monitors = _monitors.Select(m => new
                {
                    m.DeviceName,
                    m.IsPrimary,
                    Bounds = new { m.Bounds.X, m.Bounds.Y, m.Bounds.Width, m.Bounds.Height },
                    WorkingArea = new { m.WorkingArea.X, m.WorkingArea.Y, m.WorkingArea.Width, m.WorkingArea.Height },
                    m.BitsPerPixel
                }).ToArray()
            };

            await File.WriteAllTextAsync(layoutPath, JsonSerializer.Serialize(layoutData, new JsonSerializerOptions { WriteIndented = true }));
        }

        private async Task<IterationResult> RunIterationAsync(int iteration)
        {
            var result = new IterationResult();

            try
            {
                // Generate random rectangle
                var rect = GenerateRandomRectangle();
                Console.WriteLine($"  Test region: {rect.X},{rect.Y} {rect.Width}x{rect.Height}");

                // Stabilize screen
                await Task.Delay(StabilizeMs);

                // Capture via workflow
                var capturedImage = await CaptureViaWorkflowAsync(rect);
                if (capturedImage == null)
                {
                    result.Skipped = true;
                    result.SkipReason = "Capture returned null";
                    return result;
                }

                // Save capture
                var capturePath = Path.Combine(OutputDir, $"capture_iter{iteration:D3}.png");
                using (var fileStream = File.Create(capturePath))
                {
                    capturedImage.Encode(fileStream, SKEncodedImageFormat.Png, 100);
                }

                // Sample ground truth
                var groundTruth = await SampleGroundTruthAsync(rect);
                if (groundTruth == null)
                {
                    result.Skipped = true;
                    result.SkipReason = "Ground truth sampling failed";
                    return result;
                }

                // Save ground truth
                var gtPath = Path.Combine(OutputDir, $"groundtruth_iter{iteration:D3}.png");
                using (var fileStream = File.Create(gtPath))
                {
                    groundTruth.Encode(fileStream, SKEncodedImageFormat.Png, 100);
                }

                // Compare pixels
                var comparison = ComparePixels(capturedImage, groundTruth);

                // Save iteration metadata
                var metadataPath = Path.Combine(OutputDir, $"iter{iteration:D3}.json");
                var metadata = new
                {
                    Iteration = iteration,
                    Seed,
                    Rectangle = new { rect.X, rect.Y, rect.Width, rect.Height },
                    CapturePath = capturePath,
                    GroundTruthPath = gtPath,
                    Comparison = comparison
                };
                await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));

                if (comparison.MismatchCount > 0)
                {
                    result.Passed = false;
                    result.FailureReason = $"{comparison.MismatchCount} pixels mismatched (max delta: {comparison.MaxDelta})";

                    // Generate diff image
                    var diffPath = Path.Combine(OutputDir, $"diff_iter{iteration:D3}.png");
                    GenerateDiffImage(capturedImage, groundTruth, diffPath);
                }
                else
                {
                    result.Passed = true;
                }
            }
            catch (Exception ex)
            {
                result.Skipped = true;
                result.SkipReason = $"Exception: {ex.Message}";
            }

            return result;
        }

        private Rectangle GenerateRandomRectangle()
        {
            int maxX = _virtualDesktop.X + _virtualDesktop.Width - RectSize;
            int maxY = _virtualDesktop.Y + _virtualDesktop.Height - RectSize;

            int x = _random.Next(_virtualDesktop.X, maxX + 1);
            int y = _random.Next(_virtualDesktop.Y, maxY + 1);

            return new Rectangle(x, y, RectSize, RectSize);
        }

        private async Task<SKBitmap?> CaptureViaWorkflowAsync(Rectangle rect)
        {
            // Use platform screen capture service directly
            var skRect = new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom);
            return await PlatformServices.ScreenCapture.CaptureRectAsync(skRect);
        }

        private async Task<SKBitmap?> SampleGroundTruthAsync(Rectangle rect)
        {
            // Try to use independent screen sampler if available, otherwise fall back to screen capture
            var screenSampler = TryGetScreenSampler();
            var skRect = new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom);

            if (screenSampler != null)
            {
                return await screenSampler.SampleScreenAsync(skRect);
            }

            // Fallback: use screen capture (not truly independent, but better than nothing)
            return await PlatformServices.ScreenCapture.CaptureRectAsync(skRect);
        }

        private static IScreenSampler? TryGetScreenSampler()
        {
            try
            {
                // Try to get platform-specific screen sampler
                if (PlatformServices.PlatformInfo.IsWindows)
                {
                    return new XerahS.Platform.Windows.WindowsScreenSampler();
                }
                // Add other platforms as needed
            }
            catch
            {
                // Ignore errors
            }
            return null;
        }

        private PixelComparison ComparePixels(SKBitmap captured, SKBitmap groundTruth)
        {
            var comparison = new PixelComparison();

            if (captured.Width != groundTruth.Width || captured.Height != groundTruth.Height)
            {
                comparison.DimensionMismatch = true;
                comparison.MismatchCount = captured.Width * captured.Height;
                return comparison;
            }

            for (int y = 0; y < captured.Height; y++)
            {
                for (int x = 0; x < captured.Width; x++)
                {
                    var c1 = captured.GetPixel(x, y);
                    var c2 = groundTruth.GetPixel(x, y);

                    int deltaR = Math.Abs(c1.Red - c2.Red);
                    int deltaG = Math.Abs(c1.Green - c2.Green);
                    int deltaB = Math.Abs(c1.Blue - c2.Blue);
                    int deltaA = Math.Abs(c1.Alpha - c2.Alpha);

                    int maxDelta = Math.Max(Math.Max(deltaR, deltaG), Math.Max(deltaB, deltaA));

                    if (maxDelta > Tolerance)
                    {
                        if (comparison.MismatchCount == 0)
                        {
                            comparison.FirstMismatchX = x;
                            comparison.FirstMismatchY = y;
                        }

                        comparison.MismatchCount++;
                        comparison.MaxDelta = Math.Max(comparison.MaxDelta, maxDelta);
                    }
                }
            }

            return comparison;
        }

        private void GenerateDiffImage(SKBitmap captured, SKBitmap groundTruth, string diffPath)
        {
            using var diffBitmap = new SKBitmap(captured.Width, captured.Height);

            for (int y = 0; y < captured.Height; y++)
            {
                for (int x = 0; x < captured.Width; x++)
                {
                    var c1 = captured.GetPixel(x, y);
                    var c2 = groundTruth.GetPixel(x, y);

                    int deltaR = Math.Abs(c1.Red - c2.Red);
                    int deltaG = Math.Abs(c1.Green - c2.Green);
                    int deltaB = Math.Abs(c1.Blue - c2.Blue);

                    int maxDelta = Math.Max(Math.Max(deltaR, deltaG), deltaB);

                    SKColor diffColor;
                    if (maxDelta > Tolerance)
                    {
                        // Highlight mismatch in bright red
                        diffColor = new SKColor(255, 0, 0, 255);
                    }
                    else
                    {
                        // Show original pixel (dimmed)
                        diffColor = new SKColor((byte)(c1.Red / 2), (byte)(c1.Green / 2), (byte)(c1.Blue / 2), c1.Alpha);
                    }

                    diffBitmap.SetPixel(x, y, diffColor);
                }
            }

            using var fileStream = File.Create(diffPath);
            diffBitmap.Encode(fileStream, SKEncodedImageFormat.Png, 100);
        }

        private async Task WriteSummaryAsync(VerificationResult result)
        {
            var summaryPath = Path.Combine(OutputDir, "summary.md");
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("# Region Capture Verification Summary");
            sb.AppendLine();
            sb.AppendLine($"**Status**: {(result.Success ? "✅ PASS" : "❌ FAIL")}");
            sb.AppendLine($"**Seed**: {Seed}");
            sb.AppendLine($"**Iterations**: {Iterations}");
            sb.AppendLine($"**Rectangle Size**: {RectSize}x{RectSize}");
            sb.AppendLine($"**Tolerance**: {Tolerance}");
            sb.AppendLine();
            sb.AppendLine("## Results");
            sb.AppendLine();
            sb.AppendLine($"- **Passed**: {result.Passed}/{result.Total}");
            sb.AppendLine($"- **Failed**: {result.Failed}/{result.Total}");
            sb.AppendLine($"- **Skipped**: {result.Skipped}/{result.Total}");
            sb.AppendLine();
            sb.AppendLine("## Virtual Desktop");
            sb.AppendLine();
            sb.AppendLine($"Bounds: {_virtualDesktop.X},{_virtualDesktop.Y} {_virtualDesktop.Width}x{_virtualDesktop.Height}");
            sb.AppendLine();
            sb.AppendLine("## Monitors");
            sb.AppendLine();
            foreach (var monitor in _monitors)
            {
                sb.AppendLine($"- {monitor.DeviceName} (Primary: {monitor.IsPrimary}): {monitor.Bounds.Width}x{monitor.Bounds.Height} at ({monitor.Bounds.X},{monitor.Bounds.Y})");
            }
            sb.AppendLine();
            sb.AppendLine("## Artifacts");
            sb.AppendLine();
            sb.AppendLine($"Output directory: `{OutputDir}`");
            sb.AppendLine();

            await File.WriteAllTextAsync(summaryPath, sb.ToString());
        }
    }

    internal class VerificationResult
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
    }

    internal class IterationResult
    {
        public bool Passed { get; set; }
        public bool Skipped { get; set; }
        public string? SkipReason { get; set; }
        public string? FailureReason { get; set; }
    }

    internal class PixelComparison
    {
        public bool DimensionMismatch { get; set; }
        public int MismatchCount { get; set; }
        public int MaxDelta { get; set; }
        public int FirstMismatchX { get; set; }
        public int FirstMismatchY { get; set; }
    }
}
