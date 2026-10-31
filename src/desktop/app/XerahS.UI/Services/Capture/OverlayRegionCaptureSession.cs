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
using SkiaSharp;
using XerahS.Core;
using XerahS.Core.Capture;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture;
using XerahS.RegionCapture.Models;
using XerahS.RegionCapture.Services;

namespace XerahS.UI.Services.Capture;

internal static class OverlayRegionCaptureSession
{
    internal readonly record struct OverlayRegionCaptureResult(
        SKRectI Selection,
        SKBitmap? AnnotationLayer,
        PixelPoint AnnotationMonitorOrigin);

    public static async Task<SKRectI> SelectRegionAsync(
        IScreenCaptureService platformImpl,
        CaptureOptions? options,
        bool useFastOverlay)
    {
        SKRectI selection = SKRectI.Empty;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                CursorInfo? cursorInfo = null;
                if (options?.ShowCursor == true)
                {
                    try
                    {
                        cursorInfo = await platformImpl.CaptureCursorAsync();
                    }
                    catch
                    {
                        // Ignore cursor capture errors.
                    }
                }

                SKBitmap? backgroundForMagnifier = null;
                if (!useFastOverlay)
                {
                    try
                    {
                        backgroundForMagnifier = await platformImpl.CaptureFullScreenAsync(new CaptureOptions
                        {
                            ShowCursor = false,
                            UseModernCapture = options?.UseModernCapture ?? true,
                            LinuxRegionSelectorPreference = LinuxCaptureOptionsResolver.GetLinuxRegionSelectorPreference(options),
                            MacOSRegionSelectorPreference = options?.MacOSRegionSelectorPreference ??
                                MacOSInteractiveRegionSelectorPreference.Automatic,
                            MacOSPlayCaptureSound = false
                        });
                    }
                    catch
                    {
                        // Ignore background capture errors.
                    }
                }

                var captureService = new RegionCaptureService
                {
                    Options = CreateOverlayOptions(options, backgroundForMagnifier, useFastOverlay, sessionStartUtc: null)
                };

                RegionSelectionResult? result;
                try
                {
                    result = await captureService.CaptureRegionAsync(cursorInfo);
                }
                finally
                {
                    await RegionCaptureAnnotationOptionsStore.PersistAsync();
                }

                if (result is not null)
                {
                    var region = result.Value.Region;
                    selection = new SKRectI((int)region.X, (int)region.Y, (int)region.Right, (int)region.Bottom);
                    RememberLastRegion(selection);
                }
            });
        }
        catch
        {
            // Ignore errors to keep capture resilient.
        }

        return selection;
    }

    public static async Task<OverlayRegionCaptureResult> CaptureRegionAsync(
        CaptureOptions? effectiveOptions,
        DateTime sessionStartUtc,
        bool useFastOverlay,
        SKBitmap? fullScreenBitmap,
        CursorInfo? ghostCursor)
    {
        SKRectI selection = SKRectI.Empty;
        SKBitmap? annotationLayer = null;
        PixelPoint annotationMonitorOrigin = default;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var captureService = new RegionCaptureService
                {
                    Options = CreateOverlayOptions(effectiveOptions, fullScreenBitmap, useFastOverlay, sessionStartUtc)
                };

                RegionSelectionResult? result;
                try
                {
                    result = await captureService.CaptureRegionAsync(ghostCursor);
                }
                finally
                {
                    await RegionCaptureAnnotationOptionsStore.PersistAsync();
                }

                if (result is not null)
                {
                    var region = result.Value.Region;
                    selection = new SKRectI((int)region.X, (int)region.Y, (int)region.Right, (int)region.Bottom);
                    annotationLayer = result.Value.AnnotationLayer;
                    annotationMonitorOrigin = result.Value.MonitorOrigin;
                    RememberLastRegion(selection);
                }
            });
        }
        catch
        {
            // Ignore overlay session errors to preserve existing behavior.
        }

        return new OverlayRegionCaptureResult(selection, annotationLayer, annotationMonitorOrigin);
    }

    private static XerahS.RegionCapture.RegionCaptureOptions CreateOverlayOptions(
        CaptureOptions? options,
        SKBitmap? backgroundImage,
        bool useFastOverlay,
        DateTime? sessionStartUtc)
    {
        var regionOptions = ResolveTaskSettings(options?.WorkflowId)?.CaptureSettings?.RegionCaptureOptions;
        IReadOnlyList<CaptureSnapSize> snapSizes = CaptureSnapSize.DefaultPresets;
        if (regionOptions?.SnapSizes is { Count: > 0 } configuredSizes)
        {
            snapSizes = configuredSizes.Select(size => new CaptureSnapSize(size.Width, size.Height)).ToArray();
        }

        return new XerahS.RegionCapture.RegionCaptureOptions
        {
            ShowCursor = options?.ShowCursor ?? false,
            DimOpacity = regionOptions?.UseDimming == false ? 0 : Math.Clamp(regionOptions?.BackgroundDimStrength ?? 20, 0, 100) / 100.0,
            DimAllExceptSelection = regionOptions?.DimAllExceptSelection ?? false,
            BackgroundImage = backgroundImage,
            UseTransparentOverlay = useFastOverlay,
            EditorOptions = RegionCaptureAnnotationOptionsStore.GetEditorOptions(options?.WorkflowId),
            SessionStartUtc = sessionStartUtc,
            QuickCrop = regionOptions?.QuickCrop ?? true,
            DetectControls = regionOptions?.DetectControls ?? true,
            EnableWindowSnapping = regionOptions?.DetectWindows ?? true,
            EnableMagnifier = regionOptions?.ShowMagnifier ?? true,
            UseSquareMagnifier = regionOptions?.UseSquareMagnifier ?? false,
            MagnifierPixelCount = regionOptions?.MagnifierPixelCount ?? 15,
            ShowInfo = regionOptions?.ShowInfo ?? true,
            CustomInfoFormat = regionOptions?.UseCustomInfoText == true ? regionOptions.CustomInfoText : null,
            ShowScreenCrosshair = regionOptions?.ShowScreenCrosshair ?? true,
            SnapSizes = snapSizes,
            SnapDistance = XerahS.Core.RegionCaptureOptions.SnapDistance
        };
    }

    private static TaskSettings? ResolveTaskSettings(string? workflowId)
    {
        if (!string.IsNullOrWhiteSpace(workflowId))
        {
            return SettingsManager.GetWorkflowTaskSettings(workflowId);
        }

        return SettingsManager.DefaultTaskSettings;
    }

    private static void RememberLastRegion(SKRectI selection)
    {
        if (selection.Width <= 0 || selection.Height <= 0)
            return;

        LastRegionStore.Set(selection.Left, selection.Top, selection.Width, selection.Height);
    }
}
