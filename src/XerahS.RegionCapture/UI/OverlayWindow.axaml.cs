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
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using ShareX.ImageEditor;
using ShareX.ImageEditor.Annotations;
using XerahS.RegionCapture.UI.Controls;
using SkiaSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;
using XerahS.RegionCapture.Models;
using XerahS.RegionCapture.Services;
using XerahS.RegionCapture.ViewModels;
using AvPixelRect = Avalonia.PixelRect;
using AvPixelPoint = Avalonia.PixelPoint;
using PixelRect = XerahS.RegionCapture.Models.PixelRect;
using PixelPoint = XerahS.RegionCapture.Models.PixelPoint;

namespace XerahS.RegionCapture.UI;

/// <summary>
/// A transparent overlay window for a single monitor.
/// Each monitor gets its own overlay to avoid mixed-DPI scaling issues.
/// XIP-0023: Includes AnnotationToolbar for annotating during capture.
/// </summary>
public partial class OverlayWindow : Window
{
    private static readonly long SelectionDragRebuildIntervalTicks = Math.Max(1, Stopwatch.Frequency / 30);

    private readonly MonitorInfo _monitor;
    private readonly TaskCompletionSource<RegionSelectionResult?> _completionSource;
    private readonly RegionCaptureControl _captureControl;
    private readonly RegionCaptureAnnotationViewModel _viewModel;
    private readonly SKBitmap? _backgroundBitmap;
    private Canvas? _annotationCanvas;

    // Annotation drawing state - delegates to EditorCore for lifecycle
    private Control? _currentShape;
    private bool _isDrawing;
    private Annotation? _currentAnnotation;
    private bool _rebuildScheduled;
    private bool _rebuildPending;
    private long _lastRebuildTicks;
    private bool _selectionInteractionActive;

    // CTRL modifier state for toggling between drawing and region selection
    private bool _ctrlPressed;

    // Inline text editing state
    private TextBox? _inlineTextBox;
    private Annotation? _editingAnnotation;

    public OverlayWindow()
    {
        // Design-time constructor
        _monitor = new MonitorInfo("Design", new PixelRect(0, 0, 1920, 1080),
            new PixelRect(0, 0, 1920, 1040), 1.0, true);
        _completionSource = new TaskCompletionSource<RegionSelectionResult?>();
        _captureControl = new RegionCaptureControl(_monitor);
        _viewModel = new RegionCaptureAnnotationViewModel();
        InitializeComponent();
        DataContext = _viewModel;
    }

    public OverlayWindow(
        MonitorInfo monitor,
        TaskCompletionSource<RegionSelectionResult?> completionSource,
        Action<PixelRect>? selectionChanged = null,
        XerahS.Platform.Abstractions.CursorInfo? initialCursor = null,
        RegionCaptureOptions? options = null)
    {
        _monitor = monitor;
        _completionSource = completionSource;
        _backgroundBitmap = options?.BackgroundImage;

        // XIP-0023: Create ViewModel for annotation toolbar
        _viewModel = new RegionCaptureAnnotationViewModel();
        _viewModel.InvalidateRequested += OnInvalidateRequested;
        _viewModel.AnnotationsRestored += OnAnnotationsRestored;

        // Load a monitor-scoped background image into EditorCore at logical resolution
        // so annotation coordinates (from Avalonia pointer events) match image coordinates.
        if (_backgroundBitmap != null)
        {
            var editorBitmap = CreateMonitorLogicalBackgroundBitmap(_backgroundBitmap, monitor);
            if (editorBitmap != null)
            {
                _viewModel.LoadBackgroundImage(editorBitmap);
            }
        }

        // Wire up EditorCore events
        _viewModel.EditorCore.EditAnnotationRequested += OnEditAnnotationRequested;

        InitializeComponent();
        DataContext = _viewModel;

        // Position window to cover the entire monitor
        Position = new AvPixelPoint((int)monitor.PhysicalBounds.X, (int)monitor.PhysicalBounds.Y);
        Width = monitor.PhysicalBounds.Width / monitor.ScaleFactor;
        Height = monitor.PhysicalBounds.Height / monitor.ScaleFactor;

        // Create and add the capture control
        _captureControl = new RegionCaptureControl(_monitor, options, initialCursor);
        if (selectionChanged is not null)
            _captureControl.SelectionChanged += selectionChanged;
        _captureControl.RegionSelected += OnRegionSelected;
        _captureControl.Cancelled += OnCancelled;

        var panel = this.FindControl<Panel>("RootPanel")!;
        panel.Children.Add(_captureControl);

        // XIP-0023: Wire up annotation canvas events
        _annotationCanvas = this.FindControl<Canvas>("AnnotationCanvas");
        if (_annotationCanvas != null)
        {
            _annotationCanvas.PointerPressed += OnAnnotationCanvasPointerPressed;
            _annotationCanvas.PointerMoved += OnAnnotationCanvasPointerMoved;
            _annotationCanvas.PointerReleased += OnAnnotationCanvasPointerReleased;
        }

        // Subscribe to ActiveTool changes to toggle canvas hit testing
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Ensure window can receive keyboard input
        Focusable = true;

        WireUpToolbarEvents();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // Focus the capture control so it receives keyboard events
        _captureControl.Focus();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RegionCaptureAnnotationViewModel.ActiveTool))
        {
            if (UpdateAnnotationCanvasHitTesting())
            {
                _captureControl.InvalidateVisual();
            }
        }
    }

    /// <summary>
    /// Updates the AnnotationCanvas hit testing based on active tool and CTRL modifier.
    /// Select tool: hit testing OFF (allow RegionCaptureControl to handle mouse)
    /// Drawing tools + CTRL: hit testing OFF (CTRL allows region selection)
    /// Drawing tools (no CTRL): hit testing ON (canvas handles drawing)
    /// </summary>
    private bool UpdateAnnotationCanvasHitTesting()
    {
        if (_annotationCanvas == null) return false;

        // Annotation mode is active when:
        // 1. CTRL is NOT pressed (CTRL always allows region selection)
        // 2. Either a drawing tool is active, or Select is active with existing annotations
        //    so users can select/move/resize previously drawn annotations.
        bool hasAnnotations = _viewModel.EditorCore.Annotations.Count > 0;
        bool isAnnotationMode = !_ctrlPressed &&
                                (_viewModel.ActiveTool != EditorTool.Select || hasAnnotations);

        if (_annotationCanvas.IsHitTestVisible != isAnnotationMode)
        {
            _annotationCanvas.IsHitTestVisible = isAnnotationMode;
        }

        // Update the capture control's mode indicator
        if (_captureControl.IsAnnotationMode != isAnnotationMode)
        {
            _captureControl.IsAnnotationMode = isAnnotationMode;
            return true;
        }

        return false;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // If inline text editing is active, let the TextBox handle keys
        if (_inlineTextBox != null)
        {
            if (e.Key == Key.Escape)
            {
                CancelInlineText();
                e.Handled = true;
            }
            return;
        }

        // Track CTRL key for toggling between drawing and region selection
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            _ctrlPressed = true;
            if (UpdateAnnotationCanvasHitTesting())
            {
                _captureControl.InvalidateVisual();
            }
        }

        if (e.Key == Key.Escape)
        {
            OnCancelled();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            // XIP-0023: Toggle annotation toolbar visibility
            ToggleAnnotationToolbar();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            // XIP-0023: ENTER confirms capture with annotations
            ConfirmCaptureWithAnnotations();
            e.Handled = true;
        }
        // Tool shortcuts (only when no modifiers)
        else if (e.KeyModifiers == KeyModifiers.None)
        {
            switch (e.Key)
            {
                case Key.V: _viewModel.SelectToolCommand.Execute(EditorTool.Select); e.Handled = true; break;
                case Key.R: _viewModel.SelectToolCommand.Execute(EditorTool.Rectangle); e.Handled = true; break;
                case Key.E: _viewModel.SelectToolCommand.Execute(EditorTool.Ellipse); e.Handled = true; break;
                case Key.A: _viewModel.SelectToolCommand.Execute(EditorTool.Arrow); e.Handled = true; break;
                case Key.L: _viewModel.SelectToolCommand.Execute(EditorTool.Line); e.Handled = true; break;
                case Key.T: _viewModel.SelectToolCommand.Execute(EditorTool.Text); e.Handled = true; break;
                case Key.H: _viewModel.SelectToolCommand.Execute(EditorTool.Highlight); e.Handled = true; break;
                case Key.P: _viewModel.SelectToolCommand.Execute(EditorTool.Freehand); e.Handled = true; break;
                case Key.B: _viewModel.SelectToolCommand.Execute(EditorTool.Blur); e.Handled = true; break;
            }
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Key == Key.Z)
            {
                _viewModel.UndoCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Y)
            {
                _viewModel.RedoCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        // Track CTRL key release
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            _ctrlPressed = false;
            if (UpdateAnnotationCanvasHitTesting())
            {
                _captureControl.InvalidateVisual();
            }
        }
    }

    #region Annotation Canvas Events

    private void OnAnnotationCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_annotationCanvas == null) return;

        // Commit any pending inline text edit. The click that commits text should not also
        // start a new annotation.
        if (_inlineTextBox != null)
        {
            CommitInlineText();
            e.Handled = true;
            return;
        }

        var point = e.GetPosition(_annotationCanvas);
        var props = e.GetCurrentPoint(_annotationCanvas).Properties;
        var skPoint = new SKPoint((float)point.X, (float)point.Y);

        // Right-click: delete annotation under cursor
        if (props.IsRightButtonPressed)
        {
            int annotationCountBeforeDelete = _viewModel.EditorCore.Annotations.Count;
            _viewModel.EditorCore.OnPointerPressed(skPoint, isRightButton: true);
            _selectionInteractionActive = false;
            SyncAnnotationState();
            if (_viewModel.EditorCore.Annotations.Count != annotationCountBeforeDelete)
            {
                RebuildAnnotationCanvas();
            }
            return;
        }

        if (!props.IsLeftButtonPressed) return;

        // Select tool still routes to EditorCore so existing annotations can be selected/moved/resized.
        if (_viewModel.ActiveTool == EditorTool.Select)
        {
            var selectedBefore = _viewModel.EditorCore.SelectedAnnotation;
            _viewModel.EditorCore.OnPointerPressed(skPoint);
            _selectionInteractionActive = true;
            SyncAnnotationState();
            if (!ReferenceEquals(selectedBefore, _viewModel.EditorCore.SelectedAnnotation))
            {
                RebuildAnnotationCanvas();
            }
            e.Pointer.Capture(_annotationCanvas);
            return;
        }

        // Clear any previous preview state before forwarding the new press to EditorCore.
        if (_currentShape != null)
        {
            _annotationCanvas.Children.Remove(_currentShape);
            _currentShape = null;
        }
        _currentAnnotation = null;
        _isDrawing = false;
        _selectionInteractionActive = false;

        // Delegate to EditorCore for annotation creation and initialization.
        int countBefore = _viewModel.EditorCore.Annotations.Count;
        _viewModel.EditorCore.OnPointerPressed(skPoint);

        // Check if EditorCore created a new annotation
        if (_viewModel.EditorCore.Annotations.Count > countBefore)
        {
            _currentAnnotation = _viewModel.EditorCore.Annotations[_viewModel.EditorCore.Annotations.Count - 1];
            _isDrawing = true;

            // Apply ViewModel properties that EditorCore doesn't manage
            _currentAnnotation.FillColor = _viewModel.FillColor;
            _currentAnnotation.ShadowEnabled = _viewModel.ShadowEnabled;

            if (_currentAnnotation is TextAnnotation textAnn)
                textAnn.FontSize = _viewModel.FontSize;
            else if (_currentAnnotation is NumberAnnotation numAnn)
                numAnn.FontSize = _viewModel.FontSize;
            else if (_currentAnnotation is SpeechBalloonAnnotation balloonAnn)
                balloonAnn.FontSize = _viewModel.FontSize;

            if (_currentAnnotation is BaseEffectAnnotation effectAnn)
                effectAnn.Amount = _viewModel.EffectStrength;

            // Override highlighter color to yellow (matching original behavior)
            if (_currentAnnotation is HighlightAnnotation)
                _currentAnnotation.StrokeColor = "#FFFF00";

            // SmartEraser: always resolve color from the frozen screen snapshot first.
            // This avoids overlay-color contamination and prevents a persistent red fallback brush.
            if (_currentAnnotation is SmartEraserAnnotation smartEraserAnn)
            {
                var sampledColor = ResolveSmartEraserStrokeColor(skPoint);
                if (!string.IsNullOrWhiteSpace(sampledColor))
                {
                    smartEraserAnn.StrokeColor = sampledColor;
                }
            }

            // Create Avalonia preview shape for visual feedback during drawing
            _currentShape = CreatePreviewForAnnotation(_currentAnnotation);
            if (_currentShape != null)
            {
                _annotationCanvas.Children.Add(_currentShape);
            }
        }

        e.Pointer.Capture(_annotationCanvas);
    }

    private void OnAnnotationCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_annotationCanvas == null) return;

        // Match EditorCanvas behavior: forward move events while a button is pressed or while captured.
        var props = e.GetCurrentPoint(_annotationCanvas).Properties;
        if (e.Pointer.Captured != _annotationCanvas &&
            !props.IsLeftButtonPressed &&
            !props.IsRightButtonPressed)
        {
            return;
        }

        var point = e.GetPosition(_annotationCanvas);
        var skPoint = new SKPoint((float)point.X, (float)point.Y);

        // Delegate to EditorCore for annotation state updates (drawing/dragging/resizing).
        _viewModel.EditorCore.OnPointerMoved(skPoint);

        // Update preview shape from the annotation's updated state.
        if (_isDrawing && _currentAnnotation != null && _currentShape != null)
        {
            UpdatePreviewFromAnnotation(_currentShape, _currentAnnotation);
        }
    }

    private void OnAnnotationCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_annotationCanvas == null) return;

        var endPoint = e.GetPosition(_annotationCanvas);
        var skPoint = new SKPoint((float)endPoint.X, (float)endPoint.Y);
        _selectionInteractionActive = false;

        if (e.Pointer.Captured == _annotationCanvas)
        {
            e.Pointer.Capture(null);
        }

        // Always forward release, even when not drawing, so EditorCore can end drag/resize state.
        _viewModel.EditorCore.OnPointerReleased(skPoint);

        // Remove preview shape if one was created for this draw operation.
        if (_isDrawing && _currentShape != null)
        {
            _annotationCanvas.Children.Remove(_currentShape);
            _currentShape = null;
        }
        _isDrawing = false;
        _currentAnnotation = null;

        // Rebuild canvas with finalized annotations (effects rendered, etc.)
        // Skip rebuild if inline text editing is about to start (EditAnnotationRequested handler will rebuild)
        if (_editingAnnotation == null)
        {
            RebuildAnnotationCanvas();
        }

        SyncAnnotationState();
    }

    /// <summary>
    /// Attempts to resolve SmartEraser color using a robust fallback chain:
    /// 1) Full virtual-screen background bitmap with monitor mapping,
    /// 2) Editor source image,
    /// 3) Current editor snapshot (background + existing annotations),
    /// 4) Windows live-screen sampling (last resort).
    /// </summary>
    private string? ResolveSmartEraserStrokeColor(SKPoint logicalPoint)
    {
        if (TrySampleVirtualBackgroundColor(logicalPoint, out string? virtualColor))
        {
            return virtualColor;
        }

        if (TrySampleBitmapColor(_viewModel.EditorCore.SourceImage, logicalPoint, out string? sourceColor))
        {
            return sourceColor;
        }

        using var snapshot = _viewModel.EditorCore.GetSnapshot();
        if (TrySampleBitmapColor(snapshot, logicalPoint, out string? snapshotColor))
        {
            return snapshotColor;
        }

#if WINDOWS
        if (TrySampleLiveScreenColor(logicalPoint, out string? liveScreenColor))
        {
            return liveScreenColor;
        }
#endif

        return null;
    }

    private bool TrySampleVirtualBackgroundColor(SKPoint logicalPoint, out string? color)
    {
        color = null;
        if (_backgroundBitmap == null || _backgroundBitmap.Width <= 0 || _backgroundBitmap.Height <= 0)
        {
            return false;
        }

        int physX = (int)Math.Round(logicalPoint.X * _monitor.ScaleFactor);
        int physY = (int)Math.Round(logicalPoint.Y * _monitor.ScaleFactor);

        var coordService = new Services.CoordinateTranslationService();
        var virtualBounds = coordService.GetVirtualScreenBounds();
        int bmpX = (int)Math.Round(_monitor.PhysicalBounds.X - virtualBounds.X) + physX;
        int bmpY = (int)Math.Round(_monitor.PhysicalBounds.Y - virtualBounds.Y) + physY;
        bmpX = Math.Clamp(bmpX, 0, _backgroundBitmap.Width - 1);
        bmpY = Math.Clamp(bmpY, 0, _backgroundBitmap.Height - 1);

        var pixel = _backgroundBitmap.GetPixel(bmpX, bmpY);
        color = ToRgbHex(pixel);
        return true;
    }

#if WINDOWS
    private bool TrySampleLiveScreenColor(SKPoint logicalPoint, out string? color)
    {
        color = null;

        int physicalScreenX = (int)Math.Round(_monitor.PhysicalBounds.X + logicalPoint.X * _monitor.ScaleFactor);
        int physicalScreenY = (int)Math.Round(_monitor.PhysicalBounds.Y + logicalPoint.Y * _monitor.ScaleFactor);

        IntPtr hdc = GetDC(IntPtr.Zero);
        if (hdc == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            uint pixel = GetPixel(hdc, physicalScreenX, physicalScreenY);
            if (pixel == 0xFFFFFFFF)
            {
                return false;
            }

            byte r = (byte)(pixel & 0x000000FF);
            byte g = (byte)((pixel & 0x0000FF00) >> 8);
            byte b = (byte)((pixel & 0x00FF0000) >> 16);
            color = $"#{r:X2}{g:X2}{b:X2}";
            return true;
        }
        finally
        {
            _ = ReleaseDC(IntPtr.Zero, hdc);
        }
    }
#endif

    private static bool TrySampleBitmapColor(SKBitmap? bitmap, SKPoint logicalPoint, out string? color)
    {
        color = null;

        if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            return false;
        }

        int x = Math.Clamp((int)Math.Round(logicalPoint.X), 0, bitmap.Width - 1);
        int y = Math.Clamp((int)Math.Round(logicalPoint.Y), 0, bitmap.Height - 1);
        var pixel = bitmap.GetPixel(x, y);
        color = ToRgbHex(pixel);
        return true;
    }

    private static string ToRgbHex(SKColor color)
    {
        return $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
    }

#if WINDOWS
    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);
#endif

    /// <summary>
    /// Creates a lightweight Avalonia preview shape for visual feedback while drawing.
    /// </summary>
    private Control? CreatePreviewForAnnotation(Annotation annotation)
    {
        var strokeBrush = new SolidColorBrush(Color.Parse(annotation.StrokeColor));
        IBrush fillBrush = annotation.FillColor == "#00000000"
            ? Brushes.Transparent
            : new SolidColorBrush(Color.Parse(annotation.FillColor));

        Control? shape = null;

        switch (annotation)
        {
            case RectangleAnnotation:
                shape = new Rectangle
                {
                    Stroke = strokeBrush,
                    StrokeThickness = annotation.StrokeWidth,
                    Fill = fillBrush,
                    Tag = annotation
                };
                Canvas.SetLeft(shape, annotation.StartPoint.X);
                Canvas.SetTop(shape, annotation.StartPoint.Y);
                break;

            case EllipseAnnotation:
                shape = new Ellipse
                {
                    Stroke = strokeBrush,
                    StrokeThickness = annotation.StrokeWidth,
                    Fill = fillBrush,
                    Tag = annotation
                };
                Canvas.SetLeft(shape, annotation.StartPoint.X);
                Canvas.SetTop(shape, annotation.StartPoint.Y);
                break;

            case LineAnnotation:
                shape = new Line
                {
                    Stroke = strokeBrush,
                    StrokeThickness = annotation.StrokeWidth,
                    StartPoint = new Point(annotation.StartPoint.X, annotation.StartPoint.Y),
                    EndPoint = new Point(annotation.StartPoint.X, annotation.StartPoint.Y),
                    Tag = annotation
                };
                break;

            case ArrowAnnotation:
                shape = new Avalonia.Controls.Shapes.Path
                {
                    Stroke = strokeBrush,
                    Fill = strokeBrush,
                    StrokeThickness = annotation.StrokeWidth,
                    Tag = annotation
                };
                break;

            case HighlightAnnotation:
                var highlightColor = Color.FromArgb(0x55, 0xFF, 0xFF, 0x00);
                shape = new Rectangle
                {
                    Fill = new SolidColorBrush(highlightColor),
                    Tag = annotation
                };
                Canvas.SetLeft(shape, annotation.StartPoint.X);
                Canvas.SetTop(shape, annotation.StartPoint.Y);
                break;

            case TextAnnotation:
                // Dashed rectangle placeholder showing text bounds
                shape = new Rectangle
                {
                    Stroke = strokeBrush,
                    StrokeThickness = 1,
                    StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 4 },
                    Fill = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                    Tag = annotation
                };
                Canvas.SetLeft(shape, annotation.StartPoint.X);
                Canvas.SetTop(shape, annotation.StartPoint.Y);
                break;

            case SpeechBalloonAnnotation:
                // Filled rectangle placeholder showing balloon bounds
                var balloonFill = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255));
                shape = new Rectangle
                {
                    Stroke = strokeBrush,
                    StrokeThickness = annotation.StrokeWidth,
                    Fill = balloonFill,
                    RadiusX = 10,
                    RadiusY = 10,
                    Tag = annotation
                };
                Canvas.SetLeft(shape, annotation.StartPoint.X);
                Canvas.SetTop(shape, annotation.StartPoint.Y);
                break;

            case NumberAnnotation numAnn:
                // Circle at click point
                var radius = numAnn.Radius;
                var numGrid = new Grid
                {
                    Width = radius * 2,
                    Height = radius * 2,
                    Tag = annotation
                };
                numGrid.Children.Add(new Ellipse
                {
                    Fill = fillBrush,
                    Stroke = strokeBrush,
                    StrokeThickness = annotation.StrokeWidth
                });
                numGrid.Children.Add(new TextBlock
                {
                    Text = numAnn.Number.ToString(),
                    Foreground = Brushes.White,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    FontWeight = FontWeight.Bold,
                    FontSize = numAnn.FontSize * 0.6
                });
                Canvas.SetLeft(numGrid, annotation.StartPoint.X - radius);
                Canvas.SetTop(numGrid, annotation.StartPoint.Y - radius);
                shape = numGrid;
                break;

            case BlurAnnotation:
                shape = new Rectangle
                {
                    Fill = new SolidColorBrush(Color.Parse("#200000FF")),
                    Stroke = new SolidColorBrush(Color.FromArgb(80, 0, 0, 255)),
                    StrokeThickness = 1,
                    Tag = annotation
                };
                Canvas.SetLeft(shape, annotation.StartPoint.X);
                Canvas.SetTop(shape, annotation.StartPoint.Y);
                break;

            case PixelateAnnotation:
                shape = new Rectangle
                {
                    Fill = new SolidColorBrush(Color.Parse("#2000FF00")),
                    Stroke = new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)),
                    StrokeThickness = 1,
                    Tag = annotation
                };
                Canvas.SetLeft(shape, annotation.StartPoint.X);
                Canvas.SetTop(shape, annotation.StartPoint.Y);
                break;

            case MagnifyAnnotation:
                shape = new Rectangle
                {
                    Fill = new SolidColorBrush(Color.FromArgb(30, 211, 211, 211)),
                    Stroke = new SolidColorBrush(Color.FromArgb(80, 100, 100, 100)),
                    StrokeThickness = 1,
                    Tag = annotation
                };
                Canvas.SetLeft(shape, annotation.StartPoint.X);
                Canvas.SetTop(shape, annotation.StartPoint.Y);
                break;

            case SpotlightAnnotation:
                // Dark rectangle showing where the spotlight hole will be
                shape = new Rectangle
                {
                    Fill = Brushes.Transparent,
                    Stroke = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    StrokeThickness = 2,
                    StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 6, 3 },
                    Tag = annotation
                };
                Canvas.SetLeft(shape, annotation.StartPoint.X);
                Canvas.SetTop(shape, annotation.StartPoint.Y);
                break;

            case SmartEraserAnnotation:
            case FreehandAnnotation:
                // Path that builds during drag
                var pathBrush = new SolidColorBrush(Color.Parse(annotation.StrokeColor));
                shape = new Avalonia.Controls.Shapes.Path
                {
                    Stroke = pathBrush,
                    StrokeThickness = annotation.StrokeWidth,
                    StrokeLineCap = PenLineCap.Round,
                    StrokeJoin = PenLineJoin.Round,
                    Tag = annotation
                };
                break;
        }

        return shape;
    }

    /// <summary>
    /// Updates the preview shape's position and geometry from the annotation's current state.
    /// </summary>
    private void UpdatePreviewFromAnnotation(Control shape, Annotation annotation)
    {
        var bounds = annotation.GetBounds();
        var left = bounds.Left;
        var top = bounds.Top;
        var width = Math.Max(1, bounds.Width);
        var height = Math.Max(1, bounds.Height);

        switch (annotation)
        {
            case ArrowAnnotation arrow when shape is Avalonia.Controls.Shapes.Path arrowPath:
                var start = new Point(annotation.StartPoint.X, annotation.StartPoint.Y);
                var end = new Point(annotation.EndPoint.X, annotation.EndPoint.Y);
                arrowPath.Data = arrow.CreateArrowGeometry(start, end,
                    annotation.StrokeWidth * ArrowAnnotation.ArrowHeadWidthMultiplier);
                break;

            case LineAnnotation when shape is Line line:
                line.StartPoint = new Point(annotation.StartPoint.X, annotation.StartPoint.Y);
                line.EndPoint = new Point(annotation.EndPoint.X, annotation.EndPoint.Y);
                break;

            case FreehandAnnotation freehand when shape is Avalonia.Controls.Shapes.Path freehandPath:
                freehandPath.Data = freehand.CreateSmoothedGeometry();
                break;

            case NumberAnnotation:
                // Number stays at StartPoint, no update needed during drag
                break;

            default:
                // Rectangle-based shapes: update position and size
                if (shape is Rectangle || shape is Ellipse)
                {
                    Canvas.SetLeft(shape, left);
                    Canvas.SetTop(shape, top);
                    if (shape is Rectangle rect)
                    {
                        rect.Width = width;
                        rect.Height = height;
                    }
                    else if (shape is Ellipse ellipse)
                    {
                        ellipse.Width = width;
                        ellipse.Height = height;
                    }
                }
                else if (shape is Grid grid)
                {
                    Canvas.SetLeft(grid, left);
                    Canvas.SetTop(grid, top);
                    grid.Width = width;
                    grid.Height = height;
                }
                break;
        }
    }

    #endregion

    #region Event Handlers

    private void OnInvalidateRequested()
    {
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
        Dispatcher.UIThread.Post(ProcessPendingRebuild, DispatcherPriority.Background);
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
            InvalidateVisual();
        }

        _rebuildScheduled = false;
        if (_rebuildPending)
        {
            _rebuildScheduled = true;
            Dispatcher.UIThread.Post(ProcessPendingRebuild, DispatcherPriority.Background);
        }
    }

    private void RebuildAnnotationCanvas()
    {
        if (_annotationCanvas == null) return;

        // Preserve inline text box if active
        var preserveTextBox = _inlineTextBox;

        _annotationCanvas.Children.Clear();

        foreach (var annotation in _viewModel.EditorCore.Annotations)
        {
            var shape = CreateControlForAnnotation(annotation);
            if (shape != null)
            {
                _annotationCanvas.Children.Add(shape);
            }
        }

        // Re-add inline text box if it was active
        if (preserveTextBox != null)
        {
            _annotationCanvas.Children.Add(preserveTextBox);
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

    private void WireUpToolbarEvents()
    {
        var toolbar = this.FindControl<AnnotationToolbar>("AnnotationToolbarControl");
        if (toolbar == null)
        {
            return;
        }

        toolbar.ColorChanged += OnToolbarColorChanged;
        toolbar.FillColorChanged += OnToolbarFillColorChanged;
        toolbar.WidthChanged += OnToolbarWidthChanged;
        toolbar.FontSizeChanged += OnToolbarFontSizeChanged;
        toolbar.StrengthChanged += OnToolbarStrengthChanged;
        toolbar.ShadowButtonClick += OnToolbarShadowButtonClicked;
    }

    private void OnToolbarColorChanged(object? sender, IBrush color)
    {
        if (color is SolidColorBrush solidBrush)
        {
            _viewModel.SelectedColor = $"#{solidBrush.Color.A:X2}{solidBrush.Color.R:X2}{solidBrush.Color.G:X2}{solidBrush.Color.B:X2}";
        }
    }

    private void OnToolbarFillColorChanged(object? sender, IBrush color)
    {
        if (color is SolidColorBrush solidBrush)
        {
            _viewModel.FillColor = $"#{solidBrush.Color.A:X2}{solidBrush.Color.R:X2}{solidBrush.Color.G:X2}{solidBrush.Color.B:X2}";
        }
    }

    private void OnToolbarWidthChanged(object? sender, int width)
    {
        _viewModel.StrokeWidth = width;
    }

    private void OnToolbarFontSizeChanged(object? sender, float fontSize)
    {
        _viewModel.FontSize = fontSize;
    }

    private void OnToolbarStrengthChanged(object? sender, float strength)
    {
        _viewModel.EffectStrength = strength;
    }

    private void OnToolbarShadowButtonClicked(object? sender, EventArgs e)
    {
        _viewModel.ShadowEnabled = !_viewModel.ShadowEnabled;
    }

    /// <summary>
    /// Creates a full Avalonia visual control for a finalized annotation.
    /// Uses each annotation's CreateVisual() method for accurate rendering.
    /// Handles positioning differently based on annotation coordinate model:
    /// - Absolute-coordinate types (Line, Arrow, Freehand, SmartEraser): no Canvas offset
    /// - Spotlight: positioned at (0,0) with full canvas size
    /// - Effect annotations with EffectBitmap: rendered as Avalonia Image
    /// - Local-coordinate types (Rectangle, Ellipse, Text, etc.): Canvas.SetLeft/Top + Width/Height
    /// </summary>
    private Control? CreateControlForAnnotation(Annotation annotation)
    {
        var bounds = annotation.GetBounds();

        // Effect annotations: show EffectBitmap as Avalonia Image if available
        if (annotation is BaseEffectAnnotation effectAnn &&
            annotation is not HighlightAnnotation &&
            effectAnn.EffectBitmap != null)
        {
            var avBitmap = ConvertSKBitmapToAvalonia(effectAnn.EffectBitmap);
            if (avBitmap != null)
            {
                var image = new Image
                {
                    Source = avBitmap,
                    Width = Math.Max(1, bounds.Width),
                    Height = Math.Max(1, bounds.Height),
                    Tag = annotation
                };
                Canvas.SetLeft(image, bounds.Left);
                Canvas.SetTop(image, bounds.Top);
                return image;
            }
        }

        Control? control = null;

        // Create the Avalonia visual using each annotation's CreateVisual()
        switch (annotation)
        {
            case RectangleAnnotation rect:
                control = rect.CreateVisual();
                break;
            case EllipseAnnotation ellipse:
                control = ellipse.CreateVisual();
                break;
            case LineAnnotation line:
                control = line.CreateVisual();
                break;
            case ArrowAnnotation arrow:
                control = arrow.CreateVisual();
                // Rebuild arrow geometry from annotation points (CreateVisual returns empty Path)
                if (control is Avalonia.Controls.Shapes.Path arrowPath)
                {
                    var start = new Point(annotation.StartPoint.X, annotation.StartPoint.Y);
                    var end = new Point(annotation.EndPoint.X, annotation.EndPoint.Y);
                    arrowPath.Data = arrow.CreateArrowGeometry(start, end,
                        annotation.StrokeWidth * ArrowAnnotation.ArrowHeadWidthMultiplier);
                }
                break;
            case TextAnnotation text:
                control = text.CreateVisual();
                break;
            case SpeechBalloonAnnotation balloon:
                control = balloon.CreateVisual();
                break;
            case NumberAnnotation number:
                control = number.CreateVisual();
                break;
            case BlurAnnotation blur:
                control = blur.CreateVisual();
                break;
            case PixelateAnnotation pixelate:
                control = pixelate.CreateVisual();
                break;
            case MagnifyAnnotation magnify:
                control = magnify.CreateVisual();
                break;
            case SpotlightAnnotation spotlight:
                control = spotlight.CreateVisual();
                break;
            case HighlightAnnotation highlight:
                control = highlight.CreateVisual();
                break;
            case SmartEraserAnnotation smartEraser:
                control = smartEraser.CreateVisual();
                break;
            case FreehandAnnotation freehand:
                control = freehand.CreateVisual();
                break;
        }

        if (control == null) return null;

        // Position and size the control based on annotation type
        switch (annotation)
        {
            // Absolute-coordinate types: geometry uses absolute canvas coordinates
            // Do NOT set Canvas.SetLeft/Top as it would double-offset
            // SmartEraserAnnotation must come before FreehandAnnotation (it extends FreehandAnnotation)
            case LineAnnotation:
            case ArrowAnnotation:
            case SmartEraserAnnotation:
            case FreehandAnnotation:
                break;

            // Spotlight covers the full canvas with a darkening overlay
            case SpotlightAnnotation:
                Canvas.SetLeft(control, 0);
                Canvas.SetTop(control, 0);
                control.Width = Width;
                control.Height = Height;
                break;

            // NumberAnnotation: Grid already has Width/Height from CreateVisual()
            case NumberAnnotation:
                Canvas.SetLeft(control, bounds.Left);
                Canvas.SetTop(control, bounds.Top);
                break;

            // All other local-coordinate types: position + dimensions
            default:
                Canvas.SetLeft(control, bounds.Left);
                Canvas.SetTop(control, bounds.Top);
                if (control is Shape s && s is not Line)
                {
                    s.Width = Math.Max(1, bounds.Width);
                    s.Height = Math.Max(1, bounds.Height);
                }
                else if (control is not Shape)
                {
                    // TextBox, SpeechBalloonControl, Grid, etc.
                    control.Width = Math.Max(1, bounds.Width);
                    control.Height = Math.Max(1, bounds.Height);
                }
                break;
        }

        return control;
    }

    /// <summary>
    /// Converts an SKBitmap to an Avalonia Bitmap for display in Image controls.
    /// </summary>
    private static Avalonia.Media.Imaging.Bitmap? ConvertSKBitmapToAvalonia(SKBitmap skBitmap)
    {
        if (skBitmap == null || skBitmap.Width <= 0 || skBitmap.Height <= 0) return null;
        using var skImage = SKImage.FromBitmap(skBitmap);
        using var skData = skImage.Encode(SKEncodedImageFormat.Png, 100);
        if (skData == null) return null;
        var stream = new System.IO.MemoryStream(skData.ToArray());
        return new Avalonia.Media.Imaging.Bitmap(stream);
    }

    /// <summary>
    /// Crops the full virtual-screen capture to this monitor and scales it to the monitor's logical size.
    /// This keeps effect tool sampling aligned with pointer coordinates on per-monitor overlays.
    /// </summary>
    private static SKBitmap? CreateMonitorLogicalBackgroundBitmap(SKBitmap fullBackground, MonitorInfo monitor)
    {
        if (fullBackground.Width <= 0 || fullBackground.Height <= 0)
        {
            return null;
        }

        var coordinateService = new CoordinateTranslationService();
        var virtualBounds = coordinateService.GetVirtualScreenBounds();

        int sourceX = (int)Math.Round(monitor.PhysicalBounds.X - virtualBounds.X);
        int sourceY = (int)Math.Round(monitor.PhysicalBounds.Y - virtualBounds.Y);
        int sourceWidth = Math.Max(1, (int)Math.Round(monitor.PhysicalBounds.Width));
        int sourceHeight = Math.Max(1, (int)Math.Round(monitor.PhysicalBounds.Height));

        var sourceRect = new SKRectI(sourceX, sourceY, sourceX + sourceWidth, sourceY + sourceHeight);
        sourceRect.Intersect(new SKRectI(0, 0, fullBackground.Width, fullBackground.Height));
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
        {
            return null;
        }

        var monitorBitmap = new SKBitmap(sourceRect.Width, sourceRect.Height, fullBackground.ColorType, fullBackground.AlphaType);
        if (!fullBackground.ExtractSubset(monitorBitmap, sourceRect))
        {
            using var subsetCanvas = new SKCanvas(monitorBitmap);
            subsetCanvas.DrawBitmap(
                fullBackground,
                sourceRect,
                new SKRect(0, 0, monitorBitmap.Width, monitorBitmap.Height));
        }

        int logicalWidth = Math.Max(1, (int)Math.Round(monitor.PhysicalBounds.Width / monitor.ScaleFactor));
        int logicalHeight = Math.Max(1, (int)Math.Round(monitor.PhysicalBounds.Height / monitor.ScaleFactor));
        if (monitorBitmap.Width == logicalWidth && monitorBitmap.Height == logicalHeight)
        {
            return monitorBitmap;
        }

        var logicalBitmap = monitorBitmap.Resize(new SKImageInfo(logicalWidth, logicalHeight), SKFilterQuality.High);
        if (logicalBitmap != null)
        {
            monitorBitmap.Dispose();
            return logicalBitmap;
        }

        return monitorBitmap;
    }

    #endregion

    #region Inline Text Editing

    /// <summary>
    /// Handles EditorCore's EditAnnotationRequested event for Text and SpeechBalloon tools.
    /// Shows an inline TextBox at the annotation position.
    /// </summary>
    private void OnEditAnnotationRequested(Annotation annotation)
    {
        if (_annotationCanvas == null) return;
        if (annotation is not TextAnnotation && annotation is not SpeechBalloonAnnotation) return;

        _editingAnnotation = annotation;

        var bounds = annotation.GetBounds();
        float fontSize = annotation is TextAnnotation t ? t.FontSize :
                         annotation is SpeechBalloonAnnotation s ? s.FontSize : 16;

        _inlineTextBox = new TextBox
        {
            Width = Math.Max(200, bounds.Width),
            Height = Math.Max(40, bounds.Height),
            FontSize = fontSize,
            Foreground = new SolidColorBrush(Color.Parse(annotation.StrokeColor)),
            Background = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Colors.DodgerBlue),
            BorderThickness = new Thickness(2),
            AcceptsReturn = false,
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(4),
            Watermark = "Type text here..."
        };

        Canvas.SetLeft(_inlineTextBox, bounds.Left);
        Canvas.SetTop(_inlineTextBox, bounds.Top);

        _inlineTextBox.KeyDown += OnInlineTextBoxKeyDown;
        _inlineTextBox.LostFocus += OnInlineTextBoxLostFocus;

        _annotationCanvas.Children.Add(_inlineTextBox);

        // Rebuild canvas first to show underlying annotations, then focus the text box
        RebuildAnnotationCanvas();

        Dispatcher.UIThread.Post(() => _inlineTextBox?.Focus(), DispatcherPriority.Input);
    }

    private void OnInlineTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitInlineText();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelInlineText();
            e.Handled = true;
        }
    }

    private void OnInlineTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        // Don't auto-commit if already cleaned up
        if (_inlineTextBox != null && _editingAnnotation != null)
        {
            CommitInlineText();
        }
    }

    private void CommitInlineText()
    {
        if (_inlineTextBox == null || _editingAnnotation == null) return;

        var text = _inlineTextBox.Text ?? "";

        // Persist final bounds from the inline editor so finalized text remains visible.
        var left = Canvas.GetLeft(_inlineTextBox);
        if (double.IsNaN(left))
        {
            left = _editingAnnotation.StartPoint.X;
        }

        var top = Canvas.GetTop(_inlineTextBox);
        if (double.IsNaN(top))
        {
            top = _editingAnnotation.StartPoint.Y;
        }

        var width = _inlineTextBox.Bounds.Width > 0 ? _inlineTextBox.Bounds.Width : _inlineTextBox.Width;
        if (double.IsNaN(width) || width <= 0)
        {
            width = 10;
        }

        var height = _inlineTextBox.Bounds.Height > 0 ? _inlineTextBox.Bounds.Height : _inlineTextBox.Height;
        if (double.IsNaN(height) || height <= 0)
        {
            height = 10;
        }

        _editingAnnotation.StartPoint = new SKPoint((float)left, (float)top);
        _editingAnnotation.EndPoint = new SKPoint((float)(left + width), (float)(top + height));

        if (_editingAnnotation is TextAnnotation textAnn)
            textAnn.Text = text;
        else if (_editingAnnotation is SpeechBalloonAnnotation balloonAnn)
            balloonAnn.Text = text;

        CleanupInlineTextBox();
        RebuildAnnotationCanvas();
    }

    private void CancelInlineText()
    {
        if (_editingAnnotation != null)
        {
            // Remove the annotation since user cancelled text input
            _viewModel.EditorCore.RemoveAnnotation(_editingAnnotation);
            _viewModel.HasAnnotations = _viewModel.EditorCore.Annotations.Count > 0;
        }

        CleanupInlineTextBox();
        RebuildAnnotationCanvas();
    }

    private void CleanupInlineTextBox()
    {
        if (_inlineTextBox != null)
        {
            _inlineTextBox.KeyDown -= OnInlineTextBoxKeyDown;
            _inlineTextBox.LostFocus -= OnInlineTextBoxLostFocus;
            _annotationCanvas?.Children.Remove(_inlineTextBox);
            _inlineTextBox = null;
        }
        _editingAnnotation = null;
    }

    #endregion

    #region Toolbar Visibility

    /// <summary>
    /// XIP-0023: Toggles the visibility of the annotation toolbar in the overlay.
    /// </summary>
    private void ToggleAnnotationToolbar()
    {
        var toolbar = this.FindControl<AnnotationToolbar>("AnnotationToolbarControl");
        if (toolbar != null)
        {
            toolbar.IsVisible = !toolbar.IsVisible;
        }
    }

    /// <summary>
    /// XIP-0023: Shows the annotation toolbar.
    /// </summary>
    public void ShowAnnotationToolbar()
    {
        var toolbar = this.FindControl<AnnotationToolbar>("AnnotationToolbarControl");
        if (toolbar != null)
        {
            toolbar.IsVisible = true;
        }
    }

    /// <summary>
    /// XIP-0023: Hides the annotation toolbar.
    /// </summary>
    public void HideAnnotationToolbar()
    {
        var toolbar = this.FindControl<AnnotationToolbar>("AnnotationToolbarControl");
        if (toolbar != null)
        {
            toolbar.IsVisible = false;
        }
    }

    #endregion

    #region Capture Completion

    /// <summary>
    /// XIP-0023: Confirms capture with annotations using ENTER key.
    /// Uses the pending selection result if available, otherwise captures full monitor.
    /// </summary>
    private void ConfirmCaptureWithAnnotations()
    {
        // Use the pending selection if user has made a region selection
        if (_pendingSelectionResult.HasValue)
        {
            var result = CreateResultWithAnnotations(_pendingSelectionResult.Value);
            _completionSource.TrySetResult(result);
            return;
        }

        // Fallback: Get the full monitor bounds if no selection was made
        var bounds = new PixelRect(0, 0, (int)_monitor.PhysicalBounds.Width, (int)_monitor.PhysicalBounds.Height);
        var cursorPos = new PixelPoint(bounds.Width / 2, bounds.Height / 2);
        var result2 = CreateResultWithAnnotations(new RegionSelectionResult(bounds, cursorPos));
        _completionSource.TrySetResult(result2);
    }

    private void OnRegionSelected(RegionSelectionResult result)
    {
        // If annotations have been drawn, don't auto-complete on region selection
        // User must press ENTER to confirm capture with annotations
        if (_viewModel.HasAnnotations || (_annotationCanvas?.Children.Count ?? 0) > 0)
        {
            // Store the selection result for later use when ENTER is pressed
            _pendingSelectionResult = result;

            // Update capture control to show the reminder
            _captureControl.HasPendingSelection = true;
            _captureControl.HasAnnotations = true;
            _captureControl.InvalidateVisual();
            return;
        }

        _completionSource.TrySetResult(result);
    }

    /// <summary>
    /// Creates a RegionSelectionResult with the annotation layer rendered.
    /// </summary>
    private RegionSelectionResult CreateResultWithAnnotations(RegionSelectionResult baseResult)
    {
        // If no annotations, return the base result
        if (!_viewModel.HasAnnotations && (_annotationCanvas?.Children.Count ?? 0) == 0)
        {
            return baseResult;
        }

        // Render annotations to a transparent bitmap
        var annotationLayer = RenderAnnotationLayer();

        // Pass the monitor origin so the compositing code can adjust coordinates
        // (selection is in absolute screen coords, but annotation layer is monitor-relative)
        var monitorOrigin = new PixelPoint(
            (int)_monitor.PhysicalBounds.X,
            (int)_monitor.PhysicalBounds.Y);

        return new RegionSelectionResult(baseResult.Region, baseResult.CursorPosition, annotationLayer, monitorOrigin);
    }

    /// <summary>
    /// Renders all annotations to a transparent SKBitmap sized to the full monitor.
    /// The annotation layer can then be composited onto the captured image.
    /// </summary>
    private SKBitmap? RenderAnnotationLayer()
    {
        var annotations = _viewModel.EditorCore.Annotations;
        if (annotations.Count == 0 && (_annotationCanvas?.Children.Count ?? 0) == 0)
        {
            return null;
        }

        // Create a transparent bitmap the size of the full physical screen
        int width = (int)_monitor.PhysicalBounds.Width;
        int height = (int)_monitor.PhysicalBounds.Height;

        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        // Scale canvas to account for DPI: annotations are in logical coordinates,
        // but the bitmap is in physical coordinates
        float scaleFactor = (float)_monitor.ScaleFactor;
        canvas.Scale(scaleFactor, scaleFactor);

        // Render each annotation using EditorCore's rendering
        foreach (var annotation in annotations)
        {
            annotation.Render(canvas);
        }

        return bitmap;
    }

    // Stores the selection result when annotations exist, for use with ENTER key
    private RegionSelectionResult? _pendingSelectionResult;

    private void OnCancelled()
    {
        _completionSource.TrySetResult(null);
    }

    #endregion
}

/// <summary>
/// Extension method to convert SKColor to Avalonia Color.
/// </summary>
internal static class SKColorExtensions
{
    public static Color ToAvalonia(this SKColor color)
    {
        return Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
    }
}

