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
using System.Text.Json;
using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.CLI.Commands
{
    /// <summary>
    /// CLI command for comparing XerahS captures against a baseline (e.g., ShareX).
    /// Used for A/B testing capture correctness between implementations.
    /// </summary>
    public static class CompareCaptureCommand
    {
        public static Command Create()
        {
            var cmd = new Command("compare-capture", "Compare XerahS capture against a baseline image (e.g., from ShareX)");

            var baselineOption = new Option<string>("--baseline") { Description = "Path to baseline image file (e.g., ShareX capture)", Arity = ArgumentArity.ExactlyOne };
            var regionOption = new Option<string>("--region") { Description = "Region to capture in format 'x,y,width,height'", Arity = ArgumentArity.ExactlyOne };
            var outputDirOption = new Option<string?>("--output-dir") { Description = "Output directory for results" };
            var toleranceOption = new Option<int?>("--tolerance") { Description = "Per-channel tolerance (0-255), default: 0" };
            var useModernOption = new Option<bool>("--use-modern") { Description = "Use modern DXGI capture instead of GDI" };

            cmd.Add(baselineOption);
            cmd.Add(regionOption);
            cmd.Add(outputDirOption);
            cmd.Add(toleranceOption);
            cmd.Add(useModernOption);

            cmd.SetAction((parseResult) =>
            {
                var baseline = parseResult.GetValue(baselineOption);
                var region = parseResult.GetValue(regionOption);
                var outputDir = parseResult.GetValue(outputDirOption);
                var tolerance = parseResult.GetValue(toleranceOption) ?? 0;
                var useModern = parseResult.GetValue(useModernOption);

                if (string.IsNullOrEmpty(baseline) || string.IsNullOrEmpty(region))
                {
                    Console.Error.WriteLine("--baseline and --region are required");
                    Environment.ExitCode = 2;
                    return;
                }

                Environment.ExitCode = RunCompareAsync(baseline, region, outputDir, tolerance, useModern).GetAwaiter().GetResult();
            });

            return cmd;
        }

        private static async Task<int> RunCompareAsync(string baselinePath, string regionStr, string? outputDir, int tolerance, bool useModern)
        {
            try
            {
                Console.WriteLine("=== XerahS vs Baseline Capture Comparison ===");
                Console.WriteLine($"Baseline: {baselinePath}");
                Console.WriteLine($"Region: {regionStr}");
                Console.WriteLine($"Tolerance: {tolerance}");
                Console.WriteLine($"Use Modern (DXGI): {useModern}");
                Console.WriteLine();

                // Parse region
                var parts = regionStr.Split(',');
                if (parts.Length != 4)
                {
                    Console.Error.WriteLine("Region must be in format 'x,y,width,height'");
                    return 2;
                }

                int x = int.Parse(parts[0]);
                int y = int.Parse(parts[1]);
                int width = int.Parse(parts[2]);
                int height = int.Parse(parts[3]);
                var rect = new SKRect(x, y, x + width, y + height);

                Console.WriteLine($"Parsed rectangle: X={x}, Y={y}, Width={width}, Height={height}");

                // Load baseline image
                if (!File.Exists(baselinePath))
                {
                    Console.Error.WriteLine($"Baseline file not found: {baselinePath}");
                    return 2;
                }

                using var baselineStream = File.OpenRead(baselinePath);
                using var baselineBitmap = SKBitmap.Decode(baselineStream);

                if (baselineBitmap == null)
                {
                    Console.Error.WriteLine("Failed to decode baseline image");
                    return 2;
                }

                Console.WriteLine($"Baseline image: {baselineBitmap.Width}x{baselineBitmap.Height}");

                // Capture with XerahS
                Console.WriteLine("Capturing with XerahS...");
                var captureService = PlatformServices.ScreenCapture;
                Console.WriteLine($"Capture service type: {captureService.GetType().Name}");

                var options = new CaptureOptions
                {
                    ShowCursor = false,
                    UseModernCapture = useModern
                };

                var xerahsBitmap = await captureService.CaptureRectAsync(rect, options);

                if (xerahsBitmap == null)
                {
                    Console.Error.WriteLine("XerahS capture returned null");
                    return 2;
                }

                Console.WriteLine($"XerahS capture: {xerahsBitmap.Width}x{xerahsBitmap.Height}");

                // Set up output directory
                outputDir ??= Path.GetDirectoryName(baselinePath) ?? Directory.GetCurrentDirectory();
                Directory.CreateDirectory(outputDir);

                // Save XerahS capture
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var xerahsPath = Path.Combine(outputDir, $"xerahs_capture_{timestamp}.png");
                using (var stream = File.Create(xerahsPath))
                {
                    xerahsBitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
                }
                Console.WriteLine($"Saved XerahS capture: {xerahsPath}");

                // Compare pixels
                Console.WriteLine("Comparing pixels...");
                var comparison = ComparePixels(xerahsBitmap, baselineBitmap, tolerance);

                Console.WriteLine();
                Console.WriteLine("=== Comparison Results ===");

                if (comparison.DimensionMismatch)
                {
                    Console.WriteLine($"DIMENSION MISMATCH: XerahS={comparison.XerahsSize} vs Baseline={comparison.BaselineSize}");
                    Console.WriteLine("This typically indicates a DPI scaling issue!");
                }

                Console.WriteLine($"Mismatched pixels: {comparison.MismatchCount}");
                Console.WriteLine($"Mismatch percentage: {comparison.MismatchPercentage:F2}%");

                if (comparison.MismatchCount > 0)
                {
                    Console.WriteLine($"Max delta: {comparison.MaxDelta}");
                    Console.WriteLine($"First mismatch at: ({comparison.FirstMismatchX}, {comparison.FirstMismatchY})");
                    Console.WriteLine($"Mismatch bounding box: {comparison.MismatchBoundingBox}");

                    // Generate diff image
                    var diffPath = Path.Combine(outputDir, $"diff_{timestamp}.png");
                    GenerateDiffImage(xerahsBitmap, baselineBitmap, diffPath, tolerance);
                    Console.WriteLine($"Diff image: {diffPath}");
                }

                // Save comparison metadata
                var metadataPath = Path.Combine(outputDir, $"comparison_{timestamp}.json");
                var metadata = new
                {
                    Timestamp = DateTime.Now,
                    BaselinePath = baselinePath,
                    XerahsPath = xerahsPath,
                    Region = new { x, y, width, height },
                    Tolerance = tolerance,
                    UseModern = useModern,
                    CaptureServiceType = captureService.GetType().Name,
                    Comparison = comparison,
                    Passed = comparison.MismatchCount == 0
                };

                await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"Metadata: {metadataPath}");

                Console.WriteLine();
                if (comparison.MismatchCount == 0)
                {
                    Console.WriteLine("RESULT: PASS - Captures are identical!");
                    return 0;
                }
                else
                {
                    Console.WriteLine($"RESULT: FAIL - {comparison.MismatchCount} pixels differ");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                DebugHelper.WriteException(ex);
                return 2;
            }
        }

        private static ComparisonResult ComparePixels(SKBitmap xerahs, SKBitmap baseline, int tolerance)
        {
            var result = new ComparisonResult();

            if (xerahs.Width != baseline.Width || xerahs.Height != baseline.Height)
            {
                result.DimensionMismatch = true;
                result.XerahsSize = $"{xerahs.Width}x{xerahs.Height}";
                result.BaselineSize = $"{baseline.Width}x{baseline.Height}";
                result.MismatchCount = Math.Max(xerahs.Width, baseline.Width) * Math.Max(xerahs.Height, baseline.Height);
                return result;
            }

            int minMismatchX = int.MaxValue, minMismatchY = int.MaxValue;
            int maxMismatchX = int.MinValue, maxMismatchY = int.MinValue;

            for (int py = 0; py < xerahs.Height; py++)
            {
                for (int px = 0; px < xerahs.Width; px++)
                {
                    var c1 = xerahs.GetPixel(px, py);
                    var c2 = baseline.GetPixel(px, py);

                    int deltaR = Math.Abs(c1.Red - c2.Red);
                    int deltaG = Math.Abs(c1.Green - c2.Green);
                    int deltaB = Math.Abs(c1.Blue - c2.Blue);
                    int deltaA = Math.Abs(c1.Alpha - c2.Alpha);

                    int maxDelta = Math.Max(Math.Max(deltaR, deltaG), Math.Max(deltaB, deltaA));

                    if (maxDelta > tolerance)
                    {
                        if (result.MismatchCount == 0)
                        {
                            result.FirstMismatchX = px;
                            result.FirstMismatchY = py;
                        }

                        result.MismatchCount++;
                        result.MaxDelta = Math.Max(result.MaxDelta, maxDelta);

                        minMismatchX = Math.Min(minMismatchX, px);
                        minMismatchY = Math.Min(minMismatchY, py);
                        maxMismatchX = Math.Max(maxMismatchX, px);
                        maxMismatchY = Math.Max(maxMismatchY, py);
                    }
                }
            }

            if (result.MismatchCount > 0)
            {
                result.MismatchBoundingBox = $"{minMismatchX},{minMismatchY} to {maxMismatchX},{maxMismatchY}";
                result.MismatchPercentage = (result.MismatchCount * 100.0) / (xerahs.Width * xerahs.Height);
            }

            return result;
        }

        private static void GenerateDiffImage(SKBitmap xerahs, SKBitmap baseline, string diffPath, int tolerance)
        {
            int width = Math.Max(xerahs.Width, baseline.Width);
            int height = Math.Max(xerahs.Height, baseline.Height);

            using var diffBitmap = new SKBitmap(width, height);

            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    // Handle dimension mismatch gracefully
                    var c1 = (px < xerahs.Width && py < xerahs.Height)
                        ? xerahs.GetPixel(px, py)
                        : SKColors.Magenta; // Out of bounds = magenta

                    var c2 = (px < baseline.Width && py < baseline.Height)
                        ? baseline.GetPixel(px, py)
                        : SKColors.Cyan; // Out of bounds = cyan

                    int deltaR = Math.Abs(c1.Red - c2.Red);
                    int deltaG = Math.Abs(c1.Green - c2.Green);
                    int deltaB = Math.Abs(c1.Blue - c2.Blue);
                    int deltaA = Math.Abs(c1.Alpha - c2.Alpha);

                    int maxDelta = Math.Max(Math.Max(deltaR, deltaG), Math.Max(deltaB, deltaA));

                    SKColor diffColor;
                    if (maxDelta > tolerance)
                    {
                        // Color-code by severity
                        if (maxDelta < 32)
                            diffColor = new SKColor(255, 255, 0, 255); // Yellow - minor
                        else if (maxDelta < 128)
                            diffColor = new SKColor(255, 128, 0, 255); // Orange - moderate
                        else
                            diffColor = new SKColor(255, 0, 0, 255); // Red - severe
                    }
                    else
                    {
                        // Show dimmed version of matching pixel
                        diffColor = new SKColor((byte)(c1.Red / 2), (byte)(c1.Green / 2), (byte)(c1.Blue / 2), c1.Alpha);
                    }

                    diffBitmap.SetPixel(px, py, diffColor);
                }
            }

            using var stream = File.Create(diffPath);
            diffBitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        }

        private class ComparisonResult
        {
            public bool DimensionMismatch { get; set; }
            public string? XerahsSize { get; set; }
            public string? BaselineSize { get; set; }
            public int MismatchCount { get; set; }
            public int MaxDelta { get; set; }
            public int FirstMismatchX { get; set; }
            public int FirstMismatchY { get; set; }
            public string? MismatchBoundingBox { get; set; }
            public double MismatchPercentage { get; set; }
        }
    }
}
