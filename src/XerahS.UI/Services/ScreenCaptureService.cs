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

using Avalonia.Threading;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Helpers;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture;
using SkiaSharp;
using System;
using System.Diagnostics;

namespace XerahS.UI.Services
{
    public class ScreenCaptureService : IScreenCaptureService
    {
        private readonly IScreenCaptureService _platformImpl;

        public ScreenCaptureService(IScreenCaptureService platformImpl)
        {
            _platformImpl = platformImpl;
        }

        public Task<SKBitmap?> CaptureRectAsync(SKRect rect, CaptureOptions? options = null)
        {
            return _platformImpl.CaptureRectAsync(rect, options);
        }

        public async Task<SKRectI> SelectRegionAsync(CaptureOptions? options = null)
        {
            if (IsLinuxWayland())
            {
                // On Wayland, prefer native region selectors (slurp/portal tools) to avoid
                // compositor issues with custom transparent overlays (notably Hyprland/wlroots).
                try
                {
                    var nativeSelection = await _platformImpl.SelectRegionAsync(options);
                    if (!nativeSelection.IsEmpty && nativeSelection.Width > 0 && nativeSelection.Height > 0)
                    {
                        return nativeSelection;
                    }
                }
                catch
                {
                    // Fall through to in-app selector as a best-effort fallback.
                }
            }

            SKRectI selection = SKRectI.Empty;

            try
            {
                // UI interaction must run on the UI thread
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    // Capture cursor if requested
                    XerahS.Platform.Abstractions.CursorInfo? cursorInfo = null;
                    if (options?.ShowCursor == true)
                    {
                        try
                        {
                            cursorInfo = await _platformImpl.CaptureCursorAsync();
                        }
                        catch
                        {
                            // Ignore cursor capture errors
                        }
                    }

                    // Capture background for magnifier
                    SkiaSharp.SKBitmap? backgroundForMagnifier = null;
                    try
                    {
                        backgroundForMagnifier = await _platformImpl.CaptureFullScreenAsync(new CaptureOptions { ShowCursor = false });
                    }
                    catch { /* Ignore */ }

                    var captureService = new RegionCaptureService
                    {
                        Options = new XerahS.RegionCapture.RegionCaptureOptions
                        {
                            ShowCursor = options?.ShowCursor ?? false,
                            BackgroundImage = backgroundForMagnifier,
                            UseTransparentOverlay = options?.UseTransparentOverlay ?? false,
                            EditorOptions = RegionCaptureAnnotationOptionsStore.GetEditorOptions(options?.WorkflowId),
                        }
                    };

                    XerahS.RegionCapture.Models.RegionSelectionResult? result;
                    try
                    {
                        result = await captureService.CaptureRegionAsync(cursorInfo);
                    }
                    finally
                    {
                        RegionCaptureAnnotationOptionsStore.Persist();
                    }

                    if (result is not null)
                    {
                        var r = result.Value.Region;
                        selection = new SKRectI((int)r.X, (int)r.Y, (int)r.Right, (int)r.Bottom);
                    }
                });
            }
            catch
            {
                // Ignore errors to ensure robustness
            }

            return selection;
        }

        public async Task<SKBitmap?> CaptureFullScreenAsync(CaptureOptions? options = null)
        {
            var totalStopwatch = Stopwatch.StartNew();
            // TroubleshootingHelper.Log("FullscreenCapture", "INIT", "Fullscreen capture started");

            var captureStopwatch = Stopwatch.StartNew();
            var result = await _platformImpl.CaptureFullScreenAsync(options);
            captureStopwatch.Stop();

            var resultText = result == null ? "null" : $"{result.Width}x{result.Height}";
            // TroubleshootingHelper.Log("FullscreenCapture", "CAPTURE", $"Platform capture finished in {captureStopwatch.ElapsedMilliseconds}ms, Result={resultText}");
            totalStopwatch.Stop();
            // TroubleshootingHelper.Log("FullscreenCapture", "TOTAL", $"Fullscreen capture total elapsed: {totalStopwatch.ElapsedMilliseconds}ms");

            return result;
        }

        public Task<SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService, CaptureOptions? options = null)
        {
            return _platformImpl.CaptureActiveWindowAsync(windowService, options);
        }

        public Task<XerahS.Platform.Abstractions.CursorInfo?> CaptureCursorAsync()
        {
            return _platformImpl.CaptureCursorAsync();
        }

        public async Task<SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null)
        {
            if (IsLinuxWayland())
            {
                // Wayland compositors can misbehave with the in-app overlay. Delegate to platform-native
                // region capture (portal/grim/slurp and DE-specific tools) for better compatibility.
                return await _platformImpl.CaptureRegionAsync(options);
            }

            // 1. Capture cursor BEFORE showing overlay (if ShowCursor is enabled)
            XerahS.Platform.Abstractions.CursorInfo? ghostCursor = null;
            if (options?.ShowCursor == true)
            {
                try
                {
                    ghostCursor = await _platformImpl.CaptureCursorAsync();
                }
                catch
                {
                    // Ignore cursor capture errors
                }
            }

            // 2. Capture background for frozen overlay and magnifier BEFORE showing overlay
            // Single capture serves both purposes - avoids duplicate DXGI capture (~200ms saved)
            SKBitmap? fullScreenBitmap = null;
            try
            {
                fullScreenBitmap = await _platformImpl.CaptureFullScreenAsync(new CaptureOptions
                {
                    ShowCursor = false,
                    UseModernCapture = options?.UseModernCapture ?? true
                });
            }
            catch
            {
                // Ignore - full screen capture is optional for fallback
            }

            // 3. Select Region (UI) - pass ghost cursor for overlay display
            SKRectI selection = SKRectI.Empty;
            SKBitmap? annotationLayer = null;
            XerahS.RegionCapture.Models.PixelPoint annotationMonitorOrigin = default;
            try
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var captureService = new RegionCaptureService
                    {
                        Options = new XerahS.RegionCapture.RegionCaptureOptions
                        {
                            ShowCursor = options?.ShowCursor ?? false,
                            BackgroundImage = fullScreenBitmap,
                            UseTransparentOverlay = options?.UseTransparentOverlay ?? false,
                            EditorOptions = RegionCaptureAnnotationOptionsStore.GetEditorOptions(options?.WorkflowId),
                        }
                    };

                    XerahS.RegionCapture.Models.RegionSelectionResult? result;
                    try
                    {
                        result = await captureService.CaptureRegionAsync(ghostCursor);
                    }
                    finally
                    {
                        RegionCaptureAnnotationOptionsStore.Persist();
                    }

                    if (result is not null)
                    {
                        var r = result.Value.Region;
                        selection = new SKRectI((int)r.X, (int)r.Y, (int)r.Right, (int)r.Bottom);
                        annotationLayer = result.Value.AnnotationLayer;
                        annotationMonitorOrigin = result.Value.MonitorOrigin;
                    }
                });
            }
            catch
            {
                // Ignore errors
            }
            // Note: fullScreenBitmap is disposed later after cropping

            if (selection.IsEmpty || selection.Width <= 0 || selection.Height <= 0)
            {
                return null;
            }

            bool showCursor = options?.ShowCursor == true;

            // 3. Optional delay before capture starts (cancellable)
            if (options?.CaptureStartDelaySeconds > 0)
            {
                var delayMs = (int)Math.Round(options.CaptureStartDelaySeconds * 1000, MidpointRounding.AwayFromZero);
                var workflowId = string.IsNullOrWhiteSpace(options.WorkflowId) ? "none" : options.WorkflowId;
                var workflowCategory = string.IsNullOrWhiteSpace(options.WorkflowCategory) ? "Unknown" : options.WorkflowCategory;
                TroubleshootingHelper.Log("CaptureDelay", "REGION", $"WorkflowId={workflowId}, Category={workflowCategory}, DelaySeconds={options.CaptureStartDelaySeconds:F3}, DelayMs={delayMs}");

                try
                {
                    await Task.Delay(delayMs, options.CaptureStartDelayCancellationToken);
                    TroubleshootingHelper.Log("CaptureDelay", "REGION", $"WorkflowId={workflowId}, Category={workflowCategory}, DelayCompleted=true");
                }
                catch (OperationCanceledException)
                {
                    TroubleshootingHelper.Log("CaptureDelay", "REGION", $"WorkflowId={workflowId}, Category={workflowCategory}, DelayCancelled=true");
                    return null;
                }
            }

            // 4. Small delay to allow overlay windows to close fully and cursor to hide
            await Task.Delay(200);

            // 5. Capture Screen (Platform) - ALWAYS without cursor.
            // The ghost cursor (captured at workflow start) is drawn manually in step 6.
            // Never let the platform draw the live cursor, as it may have moved into the
            // captured region during the delay between overlay close and bitmap acquisition.
            var captureOptions = new CaptureOptions
            {
                ShowCursor = false,
                UseModernCapture = options?.UseModernCapture ?? true,
                WorkflowId = options?.WorkflowId,
                WorkflowCategory = options?.WorkflowCategory
            };

            SKBitmap? bitmap = null;
            try
            {
                if (fullScreenBitmap != null)
                {
                    var cropRect = selection;
                    try
                    {
                        var virtualBounds = PlatformServices.Screen.GetVirtualScreenBounds();
                        if (!virtualBounds.IsEmpty)
                        {
                            var offsetX = virtualBounds.X;
                            var offsetY = virtualBounds.Y;
                            cropRect = new SKRectI(
                                selection.Left - offsetX,
                                selection.Top - offsetY,
                                selection.Right - offsetX,
                                selection.Bottom - offsetY);

#if DEBUG
                            if (offsetX != 0 || offsetY != 0)
                            {
                                DebugHelper.WriteLine($"[RegionCapture] Applied virtual screen offset: ({offsetX},{offsetY}) Selection={selection.Left},{selection.Top},{selection.Right},{selection.Bottom} Crop={cropRect.Left},{cropRect.Top},{cropRect.Right},{cropRect.Bottom}");
                            }
#endif
                        }
                    }
                    catch
                    {
                        // Ignore virtual screen lookup failures
                    }

                    SKBitmap cropped = ImageHelpers.Crop(fullScreenBitmap, cropRect);
                    if (cropped.Width > 0 && cropped.Height > 0)
                    {
                        bitmap = cropped;
                    }
                    else
                    {
                        cropped.Dispose();
                    }
                }
            }
            catch
            {
                // Ignore crop errors and fall back to live capture
            }
            finally
            {
                fullScreenBitmap?.Dispose();
            }

            if (bitmap == null)
            {
                var skRect = new SKRect(selection.Left, selection.Top, selection.Right, selection.Bottom);
                bitmap = await _platformImpl.CaptureRectAsync(skRect, captureOptions);
            }

            // 6. Draw ghost cursor onto captured bitmap if available
            // We use the INITIAL ghost cursor (captured at start) to match ShareX behavior ("original location").
            // The Live cursor is hidden from the DXGI capture by the platform service.
            if (bitmap != null && ghostCursor?.Image != null && showCursor)
            {
                try
                {
                    // Calculate cursor position relative to the captured region
                    int cursorX = ghostCursor.Position.X - selection.Left - ghostCursor.Hotspot.X;
                    int cursorY = ghostCursor.Position.Y - selection.Top - ghostCursor.Hotspot.Y;

                    // Draw cursor onto bitmap using SkiaSharp
                    using var canvas = new SKCanvas(bitmap);
                    using var paint = new SKPaint { BlendMode = SKBlendMode.SrcOver };
                    canvas.DrawBitmap(ghostCursor.Image, cursorX, cursorY, paint);
                }
                catch
                {
                    // Ignore cursor drawing errors
                }
            }

            // 7. Composite annotation layer onto captured bitmap if available
            // XIP-0023: Annotations drawn during region capture are rendered onto the final image
            if (bitmap != null && annotationLayer != null)
            {
                try
                {
                    using var canvas = new SKCanvas(bitmap);
                    using var paint = new SKPaint { BlendMode = SKBlendMode.SrcOver };

                    // The annotation layer is monitor-sized, but selection is in absolute screen coords.
                    // Subtract the monitor origin to get coordinates relative to the annotation layer.
                    var srcRect = new SKRect(
                        selection.Left - (float)annotationMonitorOrigin.X,
                        selection.Top - (float)annotationMonitorOrigin.Y,
                        selection.Right - (float)annotationMonitorOrigin.X,
                        selection.Bottom - (float)annotationMonitorOrigin.Y);
                    var dstRect = new SKRect(0, 0, bitmap.Width, bitmap.Height);

                    canvas.DrawBitmap(annotationLayer, srcRect, dstRect, paint);
                }
                catch
                {
                    // Ignore annotation compositing errors
                }
                finally
                {
                    annotationLayer.Dispose();
                }
            }

            return bitmap;
        }

        public Task<SKBitmap?> CaptureWindowAsync(IntPtr windowHandle, IWindowService windowService, CaptureOptions? options = null)
        {
            return _platformImpl.CaptureWindowAsync(windowHandle, windowService, options);
        }

        private static bool IsLinuxWayland()
        {
            return OperatingSystem.IsLinux() &&
                   Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")?.Equals("wayland", StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
