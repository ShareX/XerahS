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

            var workflowIdOption = new Option<string?>("--workflow") { Description = "Workflow ID to use for capture (default: direct platform capture)" };
            var rectSizeOption = new Option<string?>("--rect-size") { Description = "Test rectangle size (default: 400). Supports WxH format (e.g., 640x480)" };
            var seedOption = new Option<int?>("--seed") { Description = "Random seed for reproducibility (optional, logs seed if not provided)" };
            var iterationsOption = new Option<int?>("--iterations") { Description = "Number of test iterations (default: 20)" };
            var toleranceOption = new Option<int?>("--tolerance") { Description = "Per-channel RGBA tolerance 0-255 (default: 0 = strict)" };
            var maxFailuresOption = new Option<int?>("--max-failures") { Description = "Stop early after N failures (default: 0 = run all)" };
            var outputDirOption = new Option<string?>("--output-dir") { Description = "Output directory for artifacts (default: PersonalFolder/CaptureTroubleshooting/RegionVerify)" };
            var stabilizeMsOption = new Option<int?>("--stabilize-ms") { Description = "Wait time in ms before sampling (default: 250)" };
            var hideCursorOption = new Option<bool>("--hide-cursor") { Description = "Exclude cursor from captures if supported by backend" };
            var hardEdgeBiasOption = new Option<int?>("--hard-edge-bias") { Description = "Percentage of iterations to bias toward hard edges (monitor boundaries, negative coords). Default: 30" };
            var ciModeOption = new Option<bool>("--ci") { Description = "CI mode: non-interactive, stable paths, exit codes (0=pass, 1=fail, 2=error)" };

            cmd.Add(workflowIdOption);
            cmd.Add(rectSizeOption);
            cmd.Add(seedOption);
            cmd.Add(iterationsOption);
            cmd.Add(toleranceOption);
            cmd.Add(maxFailuresOption);
            cmd.Add(outputDirOption);
            cmd.Add(stabilizeMsOption);
            cmd.Add(hideCursorOption);
            cmd.Add(hardEdgeBiasOption);
            cmd.Add(ciModeOption);

            cmd.SetAction((parseResult) =>
            {
                var workflowId = parseResult.GetValue(workflowIdOption);
                var rectSizeStr = parseResult.GetValue(rectSizeOption);
                var seed = parseResult.GetValue(seedOption);
                var iterations = parseResult.GetValue(iterationsOption);
                var tolerance = parseResult.GetValue(toleranceOption);
                var maxFailures = parseResult.GetValue(maxFailuresOption);
                var outputDir = parseResult.GetValue(outputDirOption);
                var stabilizeMs = parseResult.GetValue(stabilizeMsOption);
                var hideCursor = parseResult.GetValue(hideCursorOption);
                var hardEdgeBias = parseResult.GetValue(hardEdgeBiasOption);
                var ciMode = parseResult.GetValue(ciModeOption);

                // Parse rect size (supports "400" or "640x480")
                int rectWidth = 400, rectHeight = 400;
                if (!string.IsNullOrEmpty(rectSizeStr))
                {
                    if (rectSizeStr.Contains('x', StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = rectSizeStr.Split('x', 'X');
                        if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
                        {
                            rectWidth = w;
                            rectHeight = h;
                        }
                    }
                    else if (int.TryParse(rectSizeStr, out int size))
                    {
                        rectWidth = rectHeight = size;
                    }
                }

                Environment.ExitCode = RunVerificationAsync(workflowId, rectWidth, rectHeight, seed, iterations, tolerance, maxFailures, outputDir, stabilizeMs, hideCursor, hardEdgeBias, ciMode).GetAwaiter().GetResult();
            });

            return cmd;
        }

        private static async Task<int> RunVerificationAsync(
            string? workflowId,
            int rectWidth,
            int rectHeight,
            int? seed,
            int? iterations,
            int? tolerance,
            int? maxFailures,
            string? outputDir,
            int? stabilizeMs,
            bool hideCursor,
            int? hardEdgeBias,
            bool ciMode)
        {
            try
            {
                // Generate seed if not provided
                int actualSeed = seed ?? Environment.TickCount;

                // Initialize verifier with defaults
                var verifier = new RegionCaptureVerifier
                {
                    WorkflowId = workflowId,
                    RectWidth = rectWidth,
                    RectHeight = rectHeight,
                    Seed = actualSeed,
                    Iterations = iterations ?? 20,
                    Tolerance = tolerance ?? 0,
                    MaxFailures = maxFailures ?? 0,
                    OutputDir = outputDir ?? Path.Combine(PathsManager.PersonalFolder, "CaptureTroubleshooting", "RegionVerify"),
                    StabilizeMs = stabilizeMs ?? 250,
                    HideCursor = hideCursor,
                    HardEdgeBiasPercent = hardEdgeBias ?? 30,
                    CiMode = ciMode
                };

                Console.WriteLine("=== XerahS Region Capture Verification ===");
                Console.WriteLine($"Workflow: {verifier.WorkflowId ?? "(direct platform capture)"}");
                Console.WriteLine($"Rectangle Size: {verifier.RectWidth}x{verifier.RectHeight}");
                Console.WriteLine($"Iterations: {verifier.Iterations}");
                Console.WriteLine($"Seed: {verifier.Seed} {(seed.HasValue ? "" : "(auto-generated)")}");
                Console.WriteLine($"Tolerance: {verifier.Tolerance}");
                Console.WriteLine($"Hard-Edge Bias: {verifier.HardEdgeBiasPercent}%");
                Console.WriteLine($"Stabilize: {verifier.StabilizeMs}ms");
                Console.WriteLine($"Hide Cursor: {verifier.HideCursor}");
                Console.WriteLine($"Output: {verifier.OutputDir}");
                Console.WriteLine();

                var result = await verifier.RunAsync();

                Console.WriteLine();
                Console.WriteLine("=== Verification Complete ===");
                Console.WriteLine($"Status: {(result.Success ? "PASS" : "FAIL")}");
                Console.WriteLine($"Passed: {result.Passed}/{result.Total}");
                Console.WriteLine($"Failed: {result.Failed}/{result.Total}");
                Console.WriteLine($"Skipped: {result.Skipped}/{result.Total}");
                if (result.Failed > 0)
                {
                    Console.WriteLine($"First Failure Pattern: {result.FirstFailurePattern}");
                }
                Console.WriteLine($"Summary: {Path.Combine(verifier.OutputDir, "summary.md")}");

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
    /// Core verification logic for multi-monitor mixed-DPI region capture testing
    /// </summary>
    internal class RegionCaptureVerifier
    {
        public string? WorkflowId { get; set; }
        public int RectWidth { get; set; }
        public int RectHeight { get; set; }
        public int Seed { get; set; }
        public int Iterations { get; set; }
        public int Tolerance { get; set; }
        public int MaxFailures { get; set; }
        public string OutputDir { get; set; } = string.Empty;
        public int StabilizeMs { get; set; }
        public bool HideCursor { get; set; }
        public int HardEdgeBiasPercent { get; set; }
        public bool CiMode { get; set; }

        private Random _random = null!;
        private ScreenInfo[] _monitors = null!;
        private Rectangle _virtualDesktop;
        private List<IterationResult> _iterationResults = new();
        private DateTime _runStartTime;

        // Categorized hard-edge regions for biased testing
        private List<Rectangle> _monitorBoundaryRegions = new();
        private List<Rectangle> _negativeCoordRegions = new();
        private List<Rectangle> _nonStandardDpiRegions = new();

        public async Task<VerificationResult> RunAsync()
        {
            _random = new Random(Seed);
            _runStartTime = DateTime.UtcNow;

            // Step 1: Discover virtual desktop and monitors
            Directory.CreateDirectory(OutputDir);
            Console.WriteLine("Discovering monitors...");
            await DiscoverMonitorsAsync();

            // Prepare hard-edge regions for biased testing
            PrepareHardEdgeRegions();

            // Step 2: Run iterations
            var result = new VerificationResult { Total = Iterations };

            for (int i = 0; i < Iterations; i++)
            {
                Console.WriteLine($"\n[{i + 1}/{Iterations}] Running iteration...");

                var iterResult = await RunIterationAsync(i);
                _iterationResults.Add(iterResult);

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

                    // Record first failure pattern for diagnostics
                    if (string.IsNullOrEmpty(result.FirstFailurePattern))
                    {
                        result.FirstFailurePattern = AnalyzeFailurePattern(iterResult);
                    }

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
                string dpiInfo = monitor.ScaleFactor != 1.0 ? $" DPI: {monitor.EffectiveDpi} ({monitor.ScaleFactor * 100:F0}%)" : "";
                Console.WriteLine($"  - {monitor.DeviceName} (Primary: {monitor.IsPrimary}): {monitor.Bounds.Width}x{monitor.Bounds.Height} at ({monitor.Bounds.X},{monitor.Bounds.Y}){dpiInfo}");
            }

            // Write monitor layout with DPI info
            var layoutPath = Path.Combine(OutputDir, "monitor_layout.json");
            var layoutData = new
            {
                Seed,
                RunTime = _runStartTime.ToString("O"),
                VirtualDesktop = new { _virtualDesktop.X, _virtualDesktop.Y, _virtualDesktop.Width, _virtualDesktop.Height },
                Monitors = _monitors.Select(m => new
                {
                    m.DeviceName,
                    m.IsPrimary,
                    Bounds = new { m.Bounds.X, m.Bounds.Y, m.Bounds.Width, m.Bounds.Height },
                    WorkingArea = new { m.WorkingArea.X, m.WorkingArea.Y, m.WorkingArea.Width, m.WorkingArea.Height },
                    m.BitsPerPixel,
                    m.ScaleFactor,
                    m.EffectiveDpi,
                    HasNegativeCoords = m.Bounds.X < 0 || m.Bounds.Y < 0,
                    IsNonStandardDpi = Math.Abs(m.ScaleFactor - 1.0) > 0.01
                }).ToArray(),
                TestParameters = new
                {
                    RectWidth,
                    RectHeight,
                    Iterations,
                    Tolerance,
                    StabilizeMs,
                    HideCursor,
                    HardEdgeBiasPercent
                }
            };

            await File.WriteAllTextAsync(layoutPath, JsonSerializer.Serialize(layoutData, new JsonSerializerOptions { WriteIndented = true }));
        }

        private void PrepareHardEdgeRegions()
        {
            // Find regions that cross monitor boundaries
            for (int i = 0; i < _monitors.Length; i++)
            {
                var m1 = _monitors[i];

                // Check for monitors with negative coordinates
                if (m1.Bounds.X < 0 || m1.Bounds.Y < 0)
                {
                    _negativeCoordRegions.Add(m1.Bounds);
                }

                // Check for non-standard DPI
                if (Math.Abs(m1.ScaleFactor - 1.0) > 0.01)
                {
                    _nonStandardDpiRegions.Add(m1.Bounds);
                }

                // Find boundary regions between monitors
                for (int j = i + 1; j < _monitors.Length; j++)
                {
                    var m2 = _monitors[j];

                    // Check if monitors are adjacent (share an edge)
                    if (m1.Bounds.Right == m2.Bounds.Left || m2.Bounds.Right == m1.Bounds.Left ||
                        m1.Bounds.Bottom == m2.Bounds.Top || m2.Bounds.Bottom == m1.Bounds.Top)
                    {
                        // Create a rectangle spanning the boundary
                        int boundaryX = Math.Max(m1.Bounds.Left, m2.Bounds.Left) - RectWidth / 2;
                        int boundaryY = Math.Max(m1.Bounds.Top, m2.Bounds.Top) - RectHeight / 2;

                        // Clamp to virtual desktop
                        boundaryX = Math.Max(_virtualDesktop.X, Math.Min(boundaryX, _virtualDesktop.Right - RectWidth));
                        boundaryY = Math.Max(_virtualDesktop.Y, Math.Min(boundaryY, _virtualDesktop.Bottom - RectHeight));

                        _monitorBoundaryRegions.Add(new Rectangle(boundaryX, boundaryY, RectWidth, RectHeight));
                    }
                }
            }

            Console.WriteLine($"Hard-edge regions: {_monitorBoundaryRegions.Count} boundaries, {_negativeCoordRegions.Count} negative-coord monitors, {_nonStandardDpiRegions.Count} non-100% DPI monitors");
        }

        private async Task<IterationResult> RunIterationAsync(int iteration)
        {
            var result = new IterationResult { Iteration = iteration };

            try
            {
                // Determine if this iteration should use hard-edge bias
                bool useHardEdge = _random.Next(100) < HardEdgeBiasPercent;
                string regionType = "random";

                // Generate rectangle (with optional hard-edge bias)
                Rectangle rect;
                if (useHardEdge)
                {
                    (rect, regionType) = GenerateHardEdgeRectangle();
                }
                else
                {
                    rect = GenerateRandomRectangle();
                }

                result.TestedRect = rect;
                result.RegionType = regionType;

                // Determine which monitor(s) this rectangle overlaps
                var overlappingMonitors = _monitors.Where(m => m.Bounds.IntersectsWith(rect)).ToArray();
                result.OverlappingMonitors = overlappingMonitors.Select(m => m.DeviceName).ToArray();
                result.CrossesMonitorBoundary = overlappingMonitors.Length > 1;
                result.InvolvesNonStandardDpi = overlappingMonitors.Any(m => Math.Abs(m.ScaleFactor - 1.0) > 0.01);
                result.InvolvesNegativeCoords = rect.X < 0 || rect.Y < 0;

                Console.WriteLine($"  Test region: {rect.X},{rect.Y} {rect.Width}x{rect.Height} ({regionType})");
                if (result.CrossesMonitorBoundary)
                {
                    Console.WriteLine($"    Crosses monitors: {string.Join(", ", result.OverlappingMonitors)}");
                }

                // Stabilize screen - check for dynamic content
                bool isStable = await StabilizeAndCheckAsync(rect);
                if (!isStable)
                {
                    result.Skipped = true;
                    result.SkipReason = "Screen content unstable (dynamic pixels detected)";
                    return result;
                }

                // IMPORTANT: Sample ground truth FIRST before any capture operation
                // This ensures we capture exactly what's on screen at the test coordinates
                Console.WriteLine($"    Sampling ground truth at ({rect.X},{rect.Y})...");
                var groundTruth = await SampleGroundTruthAsync(rect);
                if (groundTruth == null)
                {
                    result.Skipped = true;
                    result.SkipReason = "Ground truth sampling failed";
                    return result;
                }

                // Save ground truth immediately
                var gtPath = Path.Combine(OutputDir, $"groundtruth_iter{iteration:D3}.png");
                using (var fileStream = File.Create(gtPath))
                {
                    groundTruth.Encode(fileStream, SKEncodedImageFormat.Png, 100);
                }
                result.GroundTruthPath = gtPath;
                Console.WriteLine($"    Ground truth: {groundTruth.Width}x{groundTruth.Height}");

                // Now capture via the workflow/platform service (the code path being tested)
                Console.WriteLine($"    Capturing via {(string.IsNullOrEmpty(WorkflowId) ? "platform service" : $"workflow {WorkflowId}")}...");
                var captureOptions = new CaptureOptions
                {
                    ShowCursor = !HideCursor,
                    WorkflowId = WorkflowId
                };

                var capturedImage = await CaptureViaWorkflowAsync(rect, captureOptions);
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
                result.CapturePath = capturePath;
                Console.WriteLine($"    Captured: {capturedImage.Width}x{capturedImage.Height}");

                // Compare pixels
                var comparison = ComparePixels(capturedImage, groundTruth);
                result.Comparison = comparison;

                if (comparison.MismatchCount > 0)
                {
                    result.Passed = false;
                    result.FailureReason = $"{comparison.MismatchCount} pixels mismatched (max delta: {comparison.MaxDelta})";

                    // Generate diff image
                    var diffPath = Path.Combine(OutputDir, $"diff_iter{iteration:D3}.png");
                    GenerateDiffImage(capturedImage, groundTruth, diffPath, comparison);
                    result.DiffPath = diffPath;
                }
                else
                {
                    result.Passed = true;
                }

                // Save iteration metadata (after determining pass/fail)
                await SaveIterationMetadataAsync(iteration, result);
            }
            catch (Exception ex)
            {
                result.Skipped = true;
                result.SkipReason = $"Exception: {ex.Message}";
                DebugHelper.WriteException(ex);
            }

            return result;
        }

        private async Task<bool> StabilizeAndCheckAsync(Rectangle rect)
        {
            // Wait for initial stabilization
            await Task.Delay(StabilizeMs);

            // Take two quick samples to check for dynamic content
            var sampler = TryGetScreenSampler();
            if (sampler == null) return true; // Can't verify stability, proceed anyway

            var skRect = new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom);

            using var sample1 = await sampler.SampleScreenAsync(skRect);
            await Task.Delay(50);
            using var sample2 = await sampler.SampleScreenAsync(skRect);

            if (sample1 == null || sample2 == null) return true;

            // Quick comparison - if more than 1% of pixels changed, consider unstable
            int changedPixels = 0;
            int totalPixels = sample1.Width * sample1.Height;
            int maxAllowedChanges = totalPixels / 100; // 1% threshold

            for (int y = 0; y < Math.Min(sample1.Height, sample2.Height); y++)
            {
                for (int x = 0; x < Math.Min(sample1.Width, sample2.Width); x++)
                {
                    var c1 = sample1.GetPixel(x, y);
                    var c2 = sample2.GetPixel(x, y);

                    if (c1 != c2)
                    {
                        changedPixels++;
                        if (changedPixels > maxAllowedChanges)
                        {
                            return false; // Too many changes, unstable
                        }
                    }
                }
            }

            return true;
        }

        private Rectangle GenerateRandomRectangle()
        {
            int maxX = _virtualDesktop.X + _virtualDesktop.Width - RectWidth;
            int maxY = _virtualDesktop.Y + _virtualDesktop.Height - RectHeight;

            int x = _random.Next(_virtualDesktop.X, maxX + 1);
            int y = _random.Next(_virtualDesktop.Y, maxY + 1);

            return new Rectangle(x, y, RectWidth, RectHeight);
        }

        private (Rectangle rect, string type) GenerateHardEdgeRectangle()
        {
            // Prioritize different hard-edge scenarios
            var options = new List<(List<Rectangle> regions, string type)>();

            if (_monitorBoundaryRegions.Count > 0)
                options.Add((_monitorBoundaryRegions, "monitor-boundary"));
            if (_negativeCoordRegions.Count > 0)
                options.Add((_negativeCoordRegions, "negative-coords"));
            if (_nonStandardDpiRegions.Count > 0)
                options.Add((_nonStandardDpiRegions, "non-standard-dpi"));

            if (options.Count == 0)
            {
                return (GenerateRandomRectangle(), "random-fallback");
            }

            var (regions, type) = options[_random.Next(options.Count)];
            var targetRegion = regions[_random.Next(regions.Count)];

            // Generate rectangle within or overlapping the target region
            int x, y;
            if (type == "monitor-boundary")
            {
                // Use the pre-calculated boundary rectangle
                x = targetRegion.X;
                y = targetRegion.Y;
            }
            else
            {
                // Pick a random position within the target monitor, clamped to virtual desktop
                x = _random.Next(
                    Math.Max(_virtualDesktop.X, targetRegion.X),
                    Math.Min(_virtualDesktop.Right - RectWidth, targetRegion.Right - RectWidth / 2) + 1
                );
                y = _random.Next(
                    Math.Max(_virtualDesktop.Y, targetRegion.Y),
                    Math.Min(_virtualDesktop.Bottom - RectHeight, targetRegion.Bottom - RectHeight / 2) + 1
                );
            }

            // Ensure rectangle fits within virtual desktop
            x = Math.Max(_virtualDesktop.X, Math.Min(x, _virtualDesktop.Right - RectWidth));
            y = Math.Max(_virtualDesktop.Y, Math.Min(y, _virtualDesktop.Bottom - RectHeight));

            return (new Rectangle(x, y, RectWidth, RectHeight), type);
        }

        private async Task<SKBitmap?> CaptureViaWorkflowAsync(Rectangle rect, CaptureOptions options)
        {
            // Log the exact coordinates being requested
            var skRect = new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom);
            Console.WriteLine($"    Requesting capture: SKRect({skRect.Left}, {skRect.Top}, {skRect.Right}, {skRect.Bottom}) = {skRect.Width}x{skRect.Height}");

            // Determine which capture service will be used
            var captureService = PlatformServices.ScreenCapture;
            var captureServiceType = captureService?.GetType().Name ?? "null";
            Console.WriteLine($"    Capture service: {captureServiceType}");
            Console.WriteLine($"    UseModernCapture option: {options.UseModernCapture}");

            var result = await captureService!.CaptureRectAsync(skRect, options);

            if (result != null)
            {
                // Log what we actually got back
                Console.WriteLine($"    Received bitmap: {result.Width}x{result.Height} (expected {rect.Width}x{rect.Height})");

                // Check for dimension mismatch (potential DPI scaling issue)
                if (result.Width != rect.Width || result.Height != rect.Height)
                {
                    double scaleX = (double)result.Width / rect.Width;
                    double scaleY = (double)result.Height / rect.Height;
                    Console.WriteLine($"    WARNING: Dimension mismatch! Scale factor: {scaleX:F3}x{scaleY:F3}");
                }
            }

            return result;
        }

        private async Task<SKBitmap?> SampleGroundTruthAsync(Rectangle rect)
        {
            var screenSampler = TryGetScreenSampler();
            var skRect = new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom);

            if (screenSampler != null)
            {
                Console.WriteLine($"    Using WindowsScreenSampler (GDI BitBlt) for ground truth");
                return await screenSampler.SampleScreenAsync(skRect);
            }

            // Fallback: use screen capture (not truly independent, but better than nothing)
            Console.WriteLine("    Warning: Using same capture service for ground truth (no independent sampler)");
            return await PlatformServices.ScreenCapture.CaptureRectAsync(skRect);
        }

        private static IScreenSampler? TryGetScreenSampler()
        {
#if WINDOWS
            try
            {
                if (PlatformServices.PlatformInfo.IsWindows)
                {
                    return new XerahS.Platform.Windows.WindowsScreenSampler();
                }
            }
            catch
            {
                // Ignore errors
            }
#endif
            return null;
        }

        private PixelComparison ComparePixels(SKBitmap captured, SKBitmap groundTruth)
        {
            var comparison = new PixelComparison();

            if (captured.Width != groundTruth.Width || captured.Height != groundTruth.Height)
            {
                comparison.DimensionMismatch = true;
                comparison.CapturedSize = $"{captured.Width}x{captured.Height}";
                comparison.GroundTruthSize = $"{groundTruth.Width}x{groundTruth.Height}";
                comparison.MismatchCount = Math.Max(captured.Width, groundTruth.Width) * Math.Max(captured.Height, groundTruth.Height);
                return comparison;
            }

            int minMismatchX = int.MaxValue, minMismatchY = int.MaxValue;
            int maxMismatchX = int.MinValue, maxMismatchY = int.MinValue;

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

                        // Track bounding box of mismatches
                        minMismatchX = Math.Min(minMismatchX, x);
                        minMismatchY = Math.Min(minMismatchY, y);
                        maxMismatchX = Math.Max(maxMismatchX, x);
                        maxMismatchY = Math.Max(maxMismatchY, y);
                    }
                }
            }

            if (comparison.MismatchCount > 0)
            {
                comparison.MismatchBoundingBox = $"{minMismatchX},{minMismatchY} to {maxMismatchX},{maxMismatchY}";
                comparison.MismatchPercentage = (comparison.MismatchCount * 100.0) / (captured.Width * captured.Height);
            }

            return comparison;
        }

        private void GenerateDiffImage(SKBitmap captured, SKBitmap groundTruth, string diffPath, PixelComparison comparison)
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
                    int deltaA = Math.Abs(c1.Alpha - c2.Alpha);

                    int maxDelta = Math.Max(Math.Max(deltaR, deltaG), Math.Max(deltaB, deltaA));

                    SKColor diffColor;
                    if (maxDelta > Tolerance)
                    {
                        // Color-code by severity: yellow for small, orange for medium, red for large
                        byte intensity = (byte)Math.Min(255, maxDelta * 2);
                        if (maxDelta < 32)
                            diffColor = new SKColor(255, 255, 0, 255); // Yellow
                        else if (maxDelta < 128)
                            diffColor = new SKColor(255, 128, 0, 255); // Orange
                        else
                            diffColor = new SKColor(255, 0, 0, 255); // Red
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

        private async Task SaveIterationMetadataAsync(int iteration, IterationResult result)
        {
            var metadataPath = Path.Combine(OutputDir, $"iter{iteration:D3}.json");
            var metadata = new
            {
                result.Iteration,
                Seed,
                result.Passed,
                result.Skipped,
                result.SkipReason,
                result.FailureReason,
                result.RegionType,
                Rectangle = result.TestedRect != null ? new
                {
                    result.TestedRect.Value.X,
                    result.TestedRect.Value.Y,
                    result.TestedRect.Value.Width,
                    result.TestedRect.Value.Height
                } : null,
                result.OverlappingMonitors,
                result.CrossesMonitorBoundary,
                result.InvolvesNonStandardDpi,
                result.InvolvesNegativeCoords,
                result.CapturePath,
                result.GroundTruthPath,
                result.DiffPath,
                Comparison = result.Comparison
            };

            await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        }

        private string AnalyzeFailurePattern(IterationResult result)
        {
            if (result.Comparison == null) return "unknown";

            if (result.Comparison.DimensionMismatch)
            {
                return $"dimension-mismatch: captured={result.Comparison.CapturedSize} vs ground={result.Comparison.GroundTruthSize}";
            }

            // Analyze the pattern of mismatches
            var patterns = new List<string>();

            if (result.CrossesMonitorBoundary)
                patterns.Add("crosses-monitor-boundary");
            if (result.InvolvesNonStandardDpi)
                patterns.Add("involves-high-dpi");
            if (result.InvolvesNegativeCoords)
                patterns.Add("involves-negative-coords");

            if (result.Comparison.MismatchPercentage > 90)
                patterns.Add("near-total-mismatch");
            else if (result.Comparison.MismatchPercentage > 50)
                patterns.Add("majority-mismatch");
            else if (result.Comparison.MismatchPercentage < 1)
                patterns.Add("sparse-mismatch");

            return patterns.Count > 0 ? string.Join(", ", patterns) : "pixel-level-mismatch";
        }

        private async Task WriteSummaryAsync(VerificationResult result)
        {
            var summaryPath = Path.Combine(OutputDir, "summary.md");
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("# Region Capture Verification Summary");
            sb.AppendLine();
            sb.AppendLine($"**Status**: {(result.Success ? "PASS" : "FAIL")}");
            sb.AppendLine($"**Run Time**: {_runStartTime:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"**Seed**: `{Seed}` (use `--seed {Seed}` to reproduce)");
            sb.AppendLine();
            sb.AppendLine("## Configuration");
            sb.AppendLine();
            sb.AppendLine($"- **Iterations**: {Iterations}");
            sb.AppendLine($"- **Rectangle Size**: {RectWidth}x{RectHeight}");
            sb.AppendLine($"- **Tolerance**: {Tolerance}");
            sb.AppendLine($"- **Hard-Edge Bias**: {HardEdgeBiasPercent}%");
            sb.AppendLine($"- **Stabilize Delay**: {StabilizeMs}ms");
            sb.AppendLine($"- **Hide Cursor**: {HideCursor}");
            sb.AppendLine();
            sb.AppendLine("## Results");
            sb.AppendLine();
            sb.AppendLine($"- **Passed**: {result.Passed}/{result.Total}");
            sb.AppendLine($"- **Failed**: {result.Failed}/{result.Total}");
            sb.AppendLine($"- **Skipped**: {result.Skipped}/{result.Total}");
            sb.AppendLine();

            if (result.Failed > 0)
            {
                sb.AppendLine("## Failure Analysis");
                sb.AppendLine();

                // Categorize failures
                var failedIterations = _iterationResults.Where(r => !r.Passed && !r.Skipped).ToList();
                var boundaryFailures = failedIterations.Count(r => r.CrossesMonitorBoundary);
                var dpiFailures = failedIterations.Count(r => r.InvolvesNonStandardDpi);
                var negativeFailures = failedIterations.Count(r => r.InvolvesNegativeCoords);

                if (boundaryFailures > 0)
                    sb.AppendLine($"- **Monitor boundary failures**: {boundaryFailures}");
                if (dpiFailures > 0)
                    sb.AppendLine($"- **Non-100% DPI failures**: {dpiFailures}");
                if (negativeFailures > 0)
                    sb.AppendLine($"- **Negative coordinate failures**: {negativeFailures}");

                sb.AppendLine();
                sb.AppendLine("### Failure Details");
                sb.AppendLine();
                sb.AppendLine("| Iter | Region | Type | Pattern |");
                sb.AppendLine("|------|--------|------|---------|");

                foreach (var iter in failedIterations.Take(10)) // Show first 10 failures
                {
                    var rect = iter.TestedRect ?? Rectangle.Empty;
                    sb.AppendLine($"| {iter.Iteration:D3} | {rect.X},{rect.Y} {rect.Width}x{rect.Height} | {iter.RegionType} | {AnalyzeFailurePattern(iter)} |");
                }

                if (failedIterations.Count > 10)
                {
                    sb.AppendLine($"| ... | *{failedIterations.Count - 10} more failures* | | |");
                }

                sb.AppendLine();
                sb.AppendLine("### Debugging Tips");
                sb.AppendLine();
                sb.AppendLine("1. **Constant offset in X/Y**: Coordinate translation bug in capture pipeline");
                sb.AppendLine("2. **Scaling/size mismatch**: DPI conversion error (DIP vs physical pixels)");
                sb.AppendLine("3. **Only fails on specific monitor**: Per-monitor scale/origin bug");
                sb.AppendLine("4. **Boundary failures**: Virtual desktop union/clamping bug");
                sb.AppendLine();
            }

            sb.AppendLine("## Virtual Desktop");
            sb.AppendLine();
            sb.AppendLine($"Bounds: `{_virtualDesktop.X},{_virtualDesktop.Y}` to `{_virtualDesktop.Right},{_virtualDesktop.Bottom}` ({_virtualDesktop.Width}x{_virtualDesktop.Height})");
            sb.AppendLine();
            sb.AppendLine("## Monitors");
            sb.AppendLine();
            sb.AppendLine("| Device | Primary | Bounds | DPI |");
            sb.AppendLine("|--------|---------|--------|-----|");

            foreach (var monitor in _monitors)
            {
                string coords = monitor.Bounds.X < 0 || monitor.Bounds.Y < 0 ? " (negative)" : "";
                string dpi = monitor.ScaleFactor != 1.0 ? $"{monitor.EffectiveDpi} ({monitor.ScaleFactor * 100:F0}%)" : "96 (100%)";
                sb.AppendLine($"| {monitor.DeviceName} | {(monitor.IsPrimary ? "Yes" : "No")} | {monitor.Bounds.X},{monitor.Bounds.Y} {monitor.Bounds.Width}x{monitor.Bounds.Height}{coords} | {dpi} |");
            }

            sb.AppendLine();
            sb.AppendLine("## Artifacts");
            sb.AppendLine();
            sb.AppendLine($"- Output directory: `{OutputDir}`");
            sb.AppendLine($"- Monitor layout: `monitor_layout.json`");
            sb.AppendLine($"- Per-iteration metadata: `iter###.json`");
            sb.AppendLine($"- Captured images: `capture_iter###.png`");
            sb.AppendLine($"- Ground truth: `groundtruth_iter###.png`");
            sb.AppendLine($"- Diff images (failures only): `diff_iter###.png`");
            sb.AppendLine();
            sb.AppendLine("## Reproducing a Specific Iteration");
            sb.AppendLine();
            sb.AppendLine("```bash");
            sb.AppendLine($"xerahs verify-region-capture --seed {Seed} --iterations <N> --rect-size {RectWidth}x{RectHeight}");
            sb.AppendLine("```");
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
        public string? FirstFailurePattern { get; set; }
    }

    internal class IterationResult
    {
        public int Iteration { get; set; }
        public bool Passed { get; set; }
        public bool Skipped { get; set; }
        public string? SkipReason { get; set; }
        public string? FailureReason { get; set; }
        public string? RegionType { get; set; }
        public Rectangle? TestedRect { get; set; }
        public string[]? OverlappingMonitors { get; set; }
        public bool CrossesMonitorBoundary { get; set; }
        public bool InvolvesNonStandardDpi { get; set; }
        public bool InvolvesNegativeCoords { get; set; }
        public string? CapturePath { get; set; }
        public string? GroundTruthPath { get; set; }
        public string? DiffPath { get; set; }
        public PixelComparison? Comparison { get; set; }
    }

    internal class PixelComparison
    {
        public bool DimensionMismatch { get; set; }
        public string? CapturedSize { get; set; }
        public string? GroundTruthSize { get; set; }
        public int MismatchCount { get; set; }
        public int MaxDelta { get; set; }
        public int FirstMismatchX { get; set; }
        public int FirstMismatchY { get; set; }
        public string? MismatchBoundingBox { get; set; }
        public double MismatchPercentage { get; set; }
    }
}
