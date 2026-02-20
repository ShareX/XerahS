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

using System.Diagnostics;
using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.RegionCapture
{
    /// <summary>
    /// Platform-agnostic scrolling capture manager. Orchestrates the capture loop
    /// and image stitching using platform services for scroll simulation and screen capture.
    /// </summary>
    public class ScrollingCaptureManager
    {
        private readonly IScrollingCaptureService _scrollService;
        private readonly IScreenCaptureService _captureService;
        private readonly IWindowService _windowService;

        public ScrollingCaptureManager(
            IScrollingCaptureService scrollService,
            IScreenCaptureService captureService,
            IWindowService windowService)
        {
            _scrollService = scrollService ?? throw new ArgumentNullException(nameof(scrollService));
            _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        }

        /// <summary>
        /// Performs a scrolling capture of the specified window region.
        /// Captures frames, detects scroll end, and stitches into a single image.
        /// </summary>
        /// <param name="windowHandle">Target window handle</param>
        /// <param name="captureRegion">Screen region to capture each frame</param>
        /// <param name="scrollMethod">Method to use for scrolling</param>
        /// <param name="scrollAmount">Number of scroll units per iteration</param>
        /// <param name="startDelayMs">Delay before first capture (ms)</param>
        /// <param name="scrollDelayMs">Delay between scroll operations (ms)</param>
        /// <param name="autoScrollTop">Whether to scroll to top before starting</param>
        /// <param name="autoIgnoreBottomEdge">Whether to detect non-scrolling bottom elements</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task<ScrollingCaptureResult> CaptureAsync(
            IntPtr windowHandle,
            SKRect captureRegion,
            ScrollMethod scrollMethod,
            int scrollAmount = 2,
            int startDelayMs = 300,
            int scrollDelayMs = 300,
            bool autoScrollTop = false,
            bool autoIgnoreBottomEdge = true,
            IProgress<ScrollingCaptureProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new ScrollingCaptureResult();
            SKBitmap? stitchedResult = null;
            SKBitmap? previousFrame = null;
            int bestMatchCount = 0;
            int lastBestMatchOffset = 0;

            try
            {
                // Focus target window
                _windowService.ActivateWindow(windowHandle);
                await Task.Delay(200, cancellationToken);

                // Optionally scroll to top
                if (autoScrollTop)
                {
                    await _scrollService.ScrollToTopAsync(windowHandle);
                    await Task.Delay(startDelayMs, cancellationToken);
                }

                // Wait start delay
                await Task.Delay(startDelayMs, cancellationToken);

                var stopwatch = new Stopwatch();
                int frameIndex = 0;
                int maxFrames = 100; // Safety limit
                int lastResultHeight = 0;
                int noProgressCount = 0;
                const int NoProgressLimit = 3; // Stop if height unchanged for this many frames

                while (frameIndex < maxFrames && !cancellationToken.IsCancellationRequested)
                {
                    stopwatch.Restart();

                    // Capture current frame
                    var currentFrame = await _captureService.CaptureRectAsync(captureRegion);
                    if (currentFrame == null)
                    {
                        DebugHelper.WriteLine("ScrollingCapture: Failed to capture frame.");
                        break;
                    }

                    frameIndex++;
                    result.FramesCaptured = frameIndex;

                    // Report progress
                    progress?.Report(new ScrollingCaptureProgress
                    {
                        FramesCaptured = frameIndex,
                        LatestFrame = currentFrame
                    });

                    if (previousFrame == null)
                    {
                        // First frame - use as initial result
                        stitchedResult = currentFrame.Copy();
                        previousFrame = currentFrame;
                        lastResultHeight = stitchedResult?.Height ?? 0;
                    }
                    else
                    {
                        // Check if frames are identical (bottom reached)
                        if (AreFramesIdentical(previousFrame, currentFrame))
                        {
                            DebugHelper.WriteLine("ScrollingCapture: Identical frames detected - bottom reached.");
                            currentFrame.Dispose();
                            break;
                        }

                        // Check scroll bar for bottom detection
                        var scrollInfo = _scrollService.GetScrollBarInfo(windowHandle);
                        bool scrollAtBottom = scrollInfo?.IsAtBottom ?? false;

                        // Stitch current frame onto result
                        var stitchResult = StitchFrame(
                            stitchedResult!,
                            currentFrame,
                            autoIgnoreBottomEdge,
                            ref bestMatchCount,
                            ref lastBestMatchOffset);

                        if (stitchResult.NewImage != null)
                        {
                            stitchedResult?.Dispose();
                            stitchedResult = stitchResult.NewImage;

                            if (stitchResult.Status == ScrollingCaptureStatus.Failed && result.Status != ScrollingCaptureStatus.PartiallySuccessful)
                            {
                                result.Status = ScrollingCaptureStatus.Failed;
                            }
                            else if (stitchResult.Status == ScrollingCaptureStatus.PartiallySuccessful)
                            {
                                result.Status = ScrollingCaptureStatus.PartiallySuccessful;
                            }

                            // No-progress detection: stop if stitched height hasn't increased for several frames
                            // (avoids infinite loop when scroll bar never reports bottom or content keeps changing)
                            int currentHeight = stitchedResult?.Height ?? 0;
                            if (currentHeight <= lastResultHeight + 2)
                            {
                                noProgressCount++;
                                if (noProgressCount >= NoProgressLimit)
                                {
                                    DebugHelper.WriteLine("ScrollingCapture: No height progress - stopping to avoid infinite loop.");
                                    break;
                                }
                            }
                            else
                            {
                                noProgressCount = 0;
                            }

                            lastResultHeight = currentHeight;
                        }

                        previousFrame?.Dispose();
                        previousFrame = currentFrame;

                        if (scrollAtBottom)
                        {
                            DebugHelper.WriteLine("ScrollingCapture: Scroll bar at bottom - stopping.");
                            break;
                        }
                    }

                    // Scroll
                    await _scrollService.ScrollWindowAsync(windowHandle, scrollMethod, scrollAmount);

                    // Wait scroll delay, compensating for processing time
                    stopwatch.Stop();
                    int elapsed = (int)stopwatch.ElapsedMilliseconds;
                    int remainingDelay = Math.Max(50, scrollDelayMs - elapsed);
                    await Task.Delay(remainingDelay, cancellationToken);
                }

                if (result.Status != ScrollingCaptureStatus.Failed && result.Status != ScrollingCaptureStatus.PartiallySuccessful)
                {
                    result.Status = ScrollingCaptureStatus.Successful;
                }

                result.Image = stitchedResult;
            }
            catch (OperationCanceledException)
            {
                result.Status = ScrollingCaptureStatus.Failed;
                result.Image = stitchedResult;
            }
            finally
            {
                previousFrame?.Dispose();
            }

            return result;
        }

        /// <summary>
        /// Checks if two frames are pixel-identical using fast span comparison.
        /// </summary>
        private static bool AreFramesIdentical(SKBitmap a, SKBitmap b)
        {
            if (a.Width != b.Width || a.Height != b.Height)
                return false;

            var spanA = a.GetPixelSpan();
            var spanB = b.GetPixelSpan();

            return spanA.SequenceEqual(spanB);
        }

        /// <summary>
        /// Stitches a new frame onto the existing result image using overlap detection.
        /// </summary>
        private static StitchResult StitchFrame(
            SKBitmap result,
            SKBitmap currentFrame,
            bool autoIgnoreBottomEdge,
            ref int bestMatchCount,
            ref int lastBestMatchOffset)
        {
            int width = result.Width;
            int currentHeight = currentFrame.Height;
            int resultHeight = result.Height;

            // Ignore side margins (scrollbar area) during comparison
            int ignoreSideOffset = Math.Max(5, Math.Min(width / 3, 50));

            // Auto-detect bottom edge offset (non-scrolling UI elements like status bars)
            int ignoreBottomOffset = 0;
            if (autoIgnoreBottomEdge)
            {
                ignoreBottomOffset = DetectBottomEdgeOffset(result, currentFrame, ignoreSideOffset);
            }

            // Find the best overlap between bottom of result and top of current frame
            int matchOffset = FindBestOverlap(result, currentFrame, ignoreSideOffset, ignoreBottomOffset, out int matchQuality);

            if (matchOffset <= 0)
            {
                // No overlap found - try using last known good offset as fallback
                if (bestMatchCount > 0 && lastBestMatchOffset > 0)
                {
                    matchOffset = lastBestMatchOffset;
                    return new StitchResult
                    {
                        NewImage = CreateStitchedImage(result, currentFrame, matchOffset, ignoreBottomOffset),
                        Status = ScrollingCaptureStatus.PartiallySuccessful
                    };
                }

                // Complete failure - no overlap found and no fallback
                return new StitchResult
                {
                    NewImage = CreateStitchedImage(result, currentFrame, currentHeight / 2, 0),
                    Status = ScrollingCaptureStatus.Failed
                };
            }

            bestMatchCount++;
            lastBestMatchOffset = matchOffset;

            return new StitchResult
            {
                NewImage = CreateStitchedImage(result, currentFrame, matchOffset, ignoreBottomOffset),
                Status = ScrollingCaptureStatus.Successful
            };
        }

        /// <summary>
        /// Detects the bottom edge offset — how many bottom rows are identical between
        /// consecutive frames (non-scrolling UI elements like status bars).
        /// </summary>
        private static int DetectBottomEdgeOffset(SKBitmap result, SKBitmap current, int ignoreSideOffset)
        {
            int width = result.Width;
            int resultHeight = result.Height;
            int currentHeight = current.Height;
            int maxOffset = Math.Min(resultHeight / 3, currentHeight / 3);

            int bytesPerPixel = result.BytesPerPixel;
            int compareStart = ignoreSideOffset * bytesPerPixel;
            int compareLength = (width - ignoreSideOffset * 2) * bytesPerPixel;

            if (compareLength <= 0) return 0;

            var resultSpan = result.GetPixelSpan();
            var currentSpan = current.GetPixelSpan();
            int resultStride = result.RowBytes;
            int currentStride = current.RowBytes;

            for (int i = 0; i < maxOffset; i++)
            {
                int resultRowStart = (resultHeight - 1 - i) * resultStride + compareStart;
                int currentRowStart = (currentHeight - 1 - i) * currentStride + compareStart;

                if (resultRowStart + compareLength > resultSpan.Length ||
                    currentRowStart + compareLength > currentSpan.Length)
                    break;

                var resultRow = resultSpan.Slice(resultRowStart, compareLength);
                var currentRow = currentSpan.Slice(currentRowStart, compareLength);

                if (!resultRow.SequenceEqual(currentRow))
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>
        /// Finds the best overlap between the bottom portion of the result and the top portion
        /// of the current frame. Returns the offset (number of new pixels in the current frame).
        /// </summary>
        private static int FindBestOverlap(
            SKBitmap result,
            SKBitmap current,
            int ignoreSideOffset,
            int ignoreBottomOffset,
            out int matchQuality)
        {
            matchQuality = 0;
            int width = result.Width;
            int resultHeight = result.Height;
            int currentHeight = current.Height;

            int bytesPerPixel = result.BytesPerPixel;
            int compareStart = ignoreSideOffset * bytesPerPixel;
            int compareLength = (width - ignoreSideOffset * 2) * bytesPerPixel;

            if (compareLength <= 0) return -1;

            var resultSpan = result.GetPixelSpan();
            var currentSpan = current.GetPixelSpan();
            int resultStride = result.RowBytes;
            int currentStride = current.RowBytes;

            // Search for the bottom rows of result matching top rows of current
            int searchLimit = Math.Min(currentHeight / 2, resultHeight);
            int bestMatchStart = -1;
            int bestMatchLength = 0;

            for (int resultOffset = 1; resultOffset < searchLimit; resultOffset++)
            {
                int currentMatchLength = 0;

                // Check how many consecutive rows match starting from resultOffset rows up from bottom
                for (int row = 0; row < currentHeight - ignoreBottomOffset && resultOffset + row < resultHeight; row++)
                {
                    int resultRowStart = (resultHeight - ignoreBottomOffset - resultOffset + row) * resultStride + compareStart;
                    int currentRowStart = row * currentStride + compareStart;

                    if (resultRowStart < 0 || resultRowStart + compareLength > resultSpan.Length ||
                        currentRowStart + compareLength > currentSpan.Length)
                        break;

                    var resultRow = resultSpan.Slice(resultRowStart, compareLength);
                    var currentRow = currentSpan.Slice(currentRowStart, compareLength);

                    if (resultRow.SequenceEqual(currentRow))
                    {
                        currentMatchLength++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (currentMatchLength > bestMatchLength)
                {
                    bestMatchLength = currentMatchLength;
                    bestMatchStart = resultOffset;
                }
            }

            // Require minimum match of 3 rows to be considered valid
            if (bestMatchLength >= 3 && bestMatchStart > 0)
            {
                matchQuality = bestMatchLength;
                // The new content starts after the overlapping rows
                int newContentHeight = currentHeight - ignoreBottomOffset - bestMatchLength;
                return Math.Max(1, newContentHeight);
            }

            return -1;
        }

        /// <summary>
        /// Creates a new stitched image combining the existing result with new content from the current frame.
        /// </summary>
        private static SKBitmap CreateStitchedImage(SKBitmap result, SKBitmap currentFrame, int newContentHeight, int ignoreBottomOffset)
        {
            int width = result.Width;
            int resultUsableHeight = result.Height - ignoreBottomOffset;
            int totalHeight = resultUsableHeight + newContentHeight;

            // Safety: cap total height to prevent memory issues
            if (totalHeight > 32768)
            {
                DebugHelper.WriteLine($"ScrollingCapture: Result height {totalHeight} exceeds limit, capping at 32768.");
                totalHeight = 32768;
                newContentHeight = totalHeight - resultUsableHeight;
                if (newContentHeight <= 0) return result.Copy();
            }

            var newResult = new SKBitmap(width, totalHeight);
            using (var canvas = new SKCanvas(newResult))
            {
                // Draw existing result (minus bottom edge offset)
                var srcResultRect = new SKRect(0, 0, width, resultUsableHeight);
                var dstResultRect = new SKRect(0, 0, width, resultUsableHeight);
                canvas.DrawBitmap(result, srcResultRect, dstResultRect);

                // Draw new content from current frame (the non-overlapping bottom portion)
                int currentFrameNewStart = currentFrame.Height - ignoreBottomOffset - newContentHeight;
                if (currentFrameNewStart < 0) currentFrameNewStart = 0;

                var srcCurrentRect = new SKRect(0, currentFrameNewStart, width, currentFrame.Height - ignoreBottomOffset);
                var dstCurrentRect = new SKRect(0, resultUsableHeight, width, totalHeight);
                canvas.DrawBitmap(currentFrame, srcCurrentRect, dstCurrentRect);
            }

            return newResult;
        }

        private struct StitchResult
        {
            public SKBitmap? NewImage;
            public ScrollingCaptureStatus Status;
        }
    }

}
