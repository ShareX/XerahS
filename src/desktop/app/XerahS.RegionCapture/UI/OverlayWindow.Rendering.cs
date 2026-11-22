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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Presentation.Rendering;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using XerahS.Common;
using XerahS.RegionCapture.Services;
using PixelRect = XerahS.RegionCapture.Models.PixelRect;

namespace XerahS.RegionCapture.UI;

public partial class OverlayWindow
{
    #region Invalidation & Rebuild

    private void OnInvalidateRequested()
    {
        if (_suppressInvalidateRequested)
        {
            return;
        }

        // During active drawing we already update a lightweight preview shape in pointer handlers.
        // Rebuilding every annotation control on each move causes visible lag.
        if (_isDrawing && _currentShape != null)
        {
            return;
        }

        if (_selectionInteractionActive && ShouldThrottleSelectionRebuild())
        {
            return;
        }

        _rebuildPending = true;
        if (_rebuildScheduled)
        {
            return;
        }

        _rebuildScheduled = true;
        Dispatcher.UIThread.Post(ProcessPendingRebuild, DispatcherPriority.Render);
    }

    private void OnAnnotationsRestored()
    {
        Dispatcher.UIThread.Post(() =>
        {
            RebuildAnnotationCanvas();
            SyncAnnotationState();
        });
    }

    private void ProcessPendingRebuild()
    {
        if (_rebuildPending)
        {
            _rebuildPending = false;
            RebuildAnnotationCanvas();
            SyncAnnotationState();
        }

        _rebuildScheduled = false;
        if (_rebuildPending)
        {
            _rebuildScheduled = true;
            Dispatcher.UIThread.Post(ProcessPendingRebuild, DispatcherPriority.Render);
        }
    }

    private void RebuildAnnotationCanvas()
    {
        if (_annotationCanvas == null) return;

        // Remove previous persisted visuals
        foreach (var visual in _persistedAnnotationVisuals)
        {
            _annotationCanvas.Children.Remove(visual);
        }
        _persistedAnnotationVisuals.Clear();

        var annotations = _viewModel.EditorCore.Annotations;
        if (annotations.Count > 0)
        {
            double canvasWidth = Width;
            double canvasHeight = Height;
            if (canvasWidth <= 0 || canvasHeight <= 0) return;

            foreach (var annotation in annotations)
            {
                var visual = AnnotationVisualFactory.CreateVisualControl(
                    annotation, AnnotationVisualMode.Persisted);

                if (visual != null)
                {
                    visual.IsHitTestVisible = false;
                    AnnotationVisualFactory.UpdateVisualControl(
                        visual, annotation, AnnotationVisualMode.Persisted,
                        canvasWidth, canvasHeight);

                    if (annotation is BaseEffectAnnotation)
                    {
                        AnnotationEffectVisualUpdater.UpdateEffectVisual(visual, _viewModel.EditorCore.SourceImage);
                    }

                    _annotationCanvas.Children.Insert(0, visual);
                    _persistedAnnotationVisuals.Add(visual);
                }
            }
        }

        if (_inlineTextBox != null)
        {
            if (_annotationCanvas.Children.Contains(_inlineTextBox))
            {
                _annotationCanvas.Children.Remove(_inlineTextBox);
            }

            _annotationCanvas.Children.Add(_inlineTextBox);
        }

        _lastRebuildTicks = Stopwatch.GetTimestamp();
    }

    private bool ShouldThrottleSelectionRebuild()
    {
        if (_lastRebuildTicks == 0)
        {
            return false;
        }

        long elapsedTicks = Stopwatch.GetTimestamp() - _lastRebuildTicks;
        return elapsedTicks < SelectionDragRebuildIntervalTicks;
    }

    private void SyncAnnotationState()
    {
        bool hasAnnotations = _viewModel.EditorCore.Annotations.Count > 0;
        bool hasSelectedAnnotation = _viewModel.EditorCore.SelectedAnnotation != null;

        _viewModel.HasAnnotations = hasAnnotations;
        _viewModel.HasSelectedAnnotation = hasSelectedAnnotation;
        _viewModel.SelectedAnnotation = _viewModel.EditorCore.SelectedAnnotation;

        bool shouldInvalidateCapture = false;
        if (_captureControl.HasAnnotations != hasAnnotations)
        {
            _captureControl.HasAnnotations = hasAnnotations;
            shouldInvalidateCapture = true;
        }

        if (UpdateAnnotationCanvasHitTesting())
        {
            shouldInvalidateCapture = true;
        }

        if (shouldInvalidateCapture)
        {
            _captureControl.InvalidateVisual();
        }
    }

    #endregion

    #region Annotation Visual Creation

    /// <summary>
    /// Creates a lightweight Avalonia preview shape for visual feedback while drawing.
    /// </summary>
    private Control? CreatePreviewForAnnotation(Annotation annotation)
    {
        var shape = AnnotationVisualFactory.CreateVisualControl(annotation, AnnotationVisualMode.Preview);
        if (shape != null)
        {
            AnnotationVisualFactory.UpdateVisualControl(
                shape,
                annotation,
                AnnotationVisualMode.Preview,
                Width,
                Height);

            if (annotation is BaseEffectAnnotation)
            {
                AnnotationEffectVisualUpdater.UpdateEffectVisual(shape, _viewModel.EditorCore.SourceImage);
            }
        }
        return shape;
    }

    /// <summary>
    /// Updates the preview shape's position and geometry from the annotation's current state.
    /// </summary>
    private void UpdatePreviewFromAnnotation(Control shape, Annotation annotation)
    {
        AnnotationVisualFactory.UpdateVisualControl(
            shape,
            annotation,
            AnnotationVisualMode.Preview,
            Width,
            Height);

        if (annotation is BaseEffectAnnotation)
        {
            AnnotationEffectVisualUpdater.UpdateEffectVisual(shape, _viewModel.EditorCore.SourceImage);
        }
    }

    #endregion

    #region Background Bitmap

    /// <summary>
    /// Crops the full virtual-screen capture to this monitor and scales it to the monitor's logical size.
    /// This keeps effect tool sampling aligned with pointer coordinates on per-monitor overlays.
    /// </summary>
    private static SKBitmap? CreateMonitorLogicalBackgroundBitmap(SKBitmap fullBackground, Models.MonitorInfo monitor)
    {
        if (fullBackground.Width <= 0 || fullBackground.Height <= 0)
        {
            return null;
        }

        var coordinateService = new CoordinateTranslationService();
        var virtualBounds = coordinateService.GetVirtualScreenBounds();

        DebugHelper.WriteLine($"[BackgroundBitmap] {monitor.DeviceName}: fullBitmap={fullBackground.Width}x{fullBackground.Height} virtualBounds=({virtualBounds.X:F0},{virtualBounds.Y:F0},{virtualBounds.Width:F0},{virtualBounds.Height:F0}) PhysicalBounds=({monitor.PhysicalBounds.X:F0},{monitor.PhysicalBounds.Y:F0},{monitor.PhysicalBounds.Width:F0},{monitor.PhysicalBounds.Height:F0}) Scale={monitor.ScaleFactor:F4}");

        var sourceRect = BackgroundBitmapMapper.MapRectToBitmapInt(
            monitor.PhysicalBounds,
            virtualBounds,
            fullBackground.Width,
            fullBackground.Height);
        DebugHelper.WriteLine($"[BackgroundBitmap] {monitor.DeviceName}: mappedSourceRect=({sourceRect.Left},{sourceRect.Top},{sourceRect.Width}x{sourceRect.Height}) valid={sourceRect.Width > 0 && sourceRect.Height > 0}");
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
        {
            DebugHelper.WriteLine($"[BackgroundBitmap] {monitor.DeviceName}: sourceRect empty after clamp — returning null");
            return null;
        }

        var monitorBitmap = new SKBitmap(sourceRect.Width, sourceRect.Height, fullBackground.ColorType, fullBackground.AlphaType);
        if (!fullBackground.ExtractSubset(monitorBitmap, sourceRect))
        {
            using var subsetCanvas = new SKCanvas(monitorBitmap);
            subsetCanvas.DrawBitmap(
                fullBackground,
                sourceRect,
                new SKRect(0, 0, monitorBitmap.Width, monitorBitmap.Height),
                SKSamplingOptions.Default);
        }

        int logicalWidth = Math.Max(1, (int)Math.Round(monitor.PhysicalBounds.Width / monitor.ScaleFactor));
        int logicalHeight = Math.Max(1, (int)Math.Round(monitor.PhysicalBounds.Height / monitor.ScaleFactor));
        DebugHelper.WriteLine($"[BackgroundBitmap] {monitor.DeviceName}: extracted={monitorBitmap.Width}x{monitorBitmap.Height} targetLogical={logicalWidth}x{logicalHeight}");
        if (monitorBitmap.Width == logicalWidth && monitorBitmap.Height == logicalHeight)
        {
            DebugHelper.WriteLine($"[BackgroundBitmap] {monitor.DeviceName}: no resize needed → {monitorBitmap.Width}x{monitorBitmap.Height}");
            return monitorBitmap;
        }

        var logicalBitmap = monitorBitmap.Resize(
            new SKImageInfo(logicalWidth, logicalHeight),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        if (logicalBitmap != null)
        {
            DebugHelper.WriteLine($"[BackgroundBitmap] {monitor.DeviceName}: resized {monitorBitmap.Width}x{monitorBitmap.Height} → {logicalBitmap.Width}x{logicalBitmap.Height}");
            monitorBitmap.Dispose();
            return logicalBitmap;
        }

        DebugHelper.WriteLine($"[BackgroundBitmap] {monitor.DeviceName}: resize failed, returning extracted {monitorBitmap.Width}x{monitorBitmap.Height}");
        return monitorBitmap;
    }

    #endregion
}
