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
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using XerahS.Editor;
using XerahS.Editor.Annotations;
using XerahS.Editor.Views.Controls;
using SkiaSharp;
using XerahS.RegionCapture.Models;
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
    private readonly MonitorInfo _monitor;
    private readonly TaskCompletionSource<RegionSelectionResult?> _completionSource;
    private readonly RegionCaptureControl _captureControl;
    private readonly RegionCaptureAnnotationViewModel _viewModel;
    private readonly SKBitmap? _backgroundBitmap;
    private Canvas? _annotationCanvas;

    // Annotation drawing state
    private Control? _currentShape;
    private bool _isDrawing;
    private Point _drawStartPoint;

    // CTRL modifier state for toggling between drawing and region selection
    private bool _ctrlPressed;

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

        // Load background image into EditorCore if available
        if (_backgroundBitmap != null)
        {
            _viewModel.LoadBackgroundImage(_backgroundBitmap.Copy());
        }

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
            UpdateAnnotationCanvasHitTesting();
        }
    }

    /// <summary>
    /// Updates the AnnotationCanvas hit testing based on active tool and CTRL modifier.
    /// Select tool: hit testing OFF (allow RegionCaptureControl to handle mouse)
    /// Drawing tools + CTRL: hit testing OFF (CTRL allows region selection)
    /// Drawing tools (no CTRL): hit testing ON (canvas handles drawing)
    /// </summary>
    private void UpdateAnnotationCanvasHitTesting()
    {
        if (_annotationCanvas == null) return;

        // Annotation mode is active when:
        // 1. A drawing tool is selected (not Select)
        // 2. CTRL is NOT pressed (CTRL allows region selection even with drawing tool)
        bool isAnnotationMode = _viewModel.ActiveTool != EditorTool.Select && !_ctrlPressed;

        _annotationCanvas.IsHitTestVisible = isAnnotationMode;

        // Update the capture control's mode indicator
        _captureControl.IsAnnotationMode = isAnnotationMode;
        _captureControl.InvalidateVisual();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Track CTRL key for toggling between drawing and region selection
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            _ctrlPressed = true;
            UpdateAnnotationCanvasHitTesting();
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
                case Key.H: _viewModel.SelectToolCommand.Execute(EditorTool.Highlighter); e.Handled = true; break;
                case Key.P: _viewModel.SelectToolCommand.Execute(EditorTool.Pen); e.Handled = true; break;
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
            UpdateAnnotationCanvasHitTesting();
        }
    }

    #region Annotation Canvas Events

    private void OnAnnotationCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_annotationCanvas == null) return;

        var point = e.GetPosition(_annotationCanvas);
        var props = e.GetCurrentPoint(_annotationCanvas).Properties;

        if (!props.IsLeftButtonPressed) return;

        // Only handle drawing for non-Select tools
        if (_viewModel.ActiveTool == EditorTool.Select)
        {
            // Handle selection - find if we clicked on an existing shape
            return;
        }

        _isDrawing = true;
        _drawStartPoint = point;

        // Create the shape based on active tool
        _currentShape = CreateShapeForTool(_viewModel.ActiveTool, point);
        if (_currentShape != null)
        {
            _annotationCanvas.Children.Add(_currentShape);
        }

        e.Pointer.Capture(_annotationCanvas);
    }

    private void OnAnnotationCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDrawing || _currentShape == null || _annotationCanvas == null) return;

        var currentPoint = e.GetPosition(_annotationCanvas);
        UpdateShapeGeometry(_currentShape, _drawStartPoint, currentPoint);
    }

    private void OnAnnotationCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDrawing || _currentShape == null || _annotationCanvas == null) return;

        var endPoint = e.GetPosition(_annotationCanvas);
        e.Pointer.Capture(null);
        _isDrawing = false;

        // Finalize the shape in EditorCore
        if (_currentShape.Tag is Annotation annotation)
        {
            annotation.StartPoint = new SKPoint((float)_drawStartPoint.X, (float)_drawStartPoint.Y);
            annotation.EndPoint = new SKPoint((float)endPoint.X, (float)endPoint.Y);
            _viewModel.EditorCore.AddAnnotation(annotation);
            _viewModel.HasAnnotations = true;

            // Update capture control's annotation state
            _captureControl.HasAnnotations = true;
            _captureControl.InvalidateVisual();
        }

        _currentShape = null;
    }

    private Control? CreateShapeForTool(EditorTool tool, Point startPoint)
    {
        Control? shape = null;
        Annotation? annotation = null;

        var strokeBrush = new SolidColorBrush(Color.Parse(_viewModel.SelectedColor));
        IBrush fillBrush = _viewModel.FillColor == "#00000000"
            ? Brushes.Transparent
            : new SolidColorBrush(Color.Parse(_viewModel.FillColor));

        switch (tool)
        {
            case EditorTool.Rectangle:
                annotation = new RectangleAnnotation
                {
                    StrokeColor = _viewModel.SelectedColor,
                    FillColor = _viewModel.FillColor,
                    StrokeWidth = _viewModel.StrokeWidth,
                    ShadowEnabled = _viewModel.ShadowEnabled
                };
                shape = new Rectangle
                {
                    Stroke = strokeBrush,
                    StrokeThickness = _viewModel.StrokeWidth,
                    Fill = fillBrush,
                    Tag = annotation
                };
                Canvas.SetLeft(shape, startPoint.X);
                Canvas.SetTop(shape, startPoint.Y);
                break;

            case EditorTool.Ellipse:
                annotation = new EllipseAnnotation
                {
                    StrokeColor = _viewModel.SelectedColor,
                    FillColor = _viewModel.FillColor,
                    StrokeWidth = _viewModel.StrokeWidth,
                    ShadowEnabled = _viewModel.ShadowEnabled
                };
                shape = new Ellipse
                {
                    Stroke = strokeBrush,
                    StrokeThickness = _viewModel.StrokeWidth,
                    Fill = fillBrush,
                    Tag = annotation
                };
                Canvas.SetLeft(shape, startPoint.X);
                Canvas.SetTop(shape, startPoint.Y);
                break;

            case EditorTool.Line:
                annotation = new LineAnnotation
                {
                    StrokeColor = _viewModel.SelectedColor,
                    StrokeWidth = _viewModel.StrokeWidth
                };
                shape = new Line
                {
                    Stroke = strokeBrush,
                    StrokeThickness = _viewModel.StrokeWidth,
                    StartPoint = startPoint,
                    EndPoint = startPoint,
                    Tag = annotation
                };
                break;

            case EditorTool.Arrow:
                annotation = new ArrowAnnotation
                {
                    StrokeColor = _viewModel.SelectedColor,
                    StrokeWidth = _viewModel.StrokeWidth
                };
                var arrowPath = new Avalonia.Controls.Shapes.Path
                {
                    Stroke = strokeBrush,
                    Fill = strokeBrush,
                    StrokeThickness = _viewModel.StrokeWidth,
                    Tag = annotation
                };
                shape = arrowPath;
                break;

            case EditorTool.Highlighter:
                annotation = new HighlightAnnotation
                {
                    StrokeColor = "#FFFF00" // Yellow highlighter
                };
                var highlightColor = Color.FromArgb(0x55, 0xFF, 0xFF, 0x00);
                shape = new Rectangle
                {
                    Fill = new SolidColorBrush(highlightColor),
                    Tag = annotation
                };
                Canvas.SetLeft(shape, startPoint.X);
                Canvas.SetTop(shape, startPoint.Y);
                break;
        }

        return shape;
    }

    private void UpdateShapeGeometry(Control shape, Point start, Point current)
    {
        var left = Math.Min(start.X, current.X);
        var top = Math.Min(start.Y, current.Y);
        var width = Math.Abs(current.X - start.X);
        var height = Math.Abs(current.Y - start.Y);

        switch (shape)
        {
            case Rectangle rect:
                Canvas.SetLeft(rect, left);
                Canvas.SetTop(rect, top);
                rect.Width = Math.Max(1, width);
                rect.Height = Math.Max(1, height);
                break;

            case Ellipse ellipse:
                Canvas.SetLeft(ellipse, left);
                Canvas.SetTop(ellipse, top);
                ellipse.Width = Math.Max(1, width);
                ellipse.Height = Math.Max(1, height);
                break;

            case Line line:
                line.EndPoint = current;
                break;

            case Avalonia.Controls.Shapes.Path arrowPath when shape.Tag is ArrowAnnotation arrow:
                arrowPath.Data = arrow.CreateArrowGeometry(start, current, _viewModel.StrokeWidth * ArrowAnnotation.ArrowHeadWidthMultiplier);
                break;
        }
    }

    #endregion

    #region Event Handlers

    private void OnInvalidateRequested()
    {
        Dispatcher.UIThread.Post(InvalidateVisual);
    }

    private void OnAnnotationsRestored()
    {
        Dispatcher.UIThread.Post(RebuildAnnotationCanvas);
    }

    private void RebuildAnnotationCanvas()
    {
        if (_annotationCanvas == null) return;

        _annotationCanvas.Children.Clear();

        foreach (var annotation in _viewModel.EditorCore.Annotations)
        {
            var shape = CreateControlForAnnotation(annotation);
            if (shape != null)
            {
                _annotationCanvas.Children.Add(shape);
            }
        }
    }

    private Control? CreateControlForAnnotation(Annotation annotation)
    {
        var bounds = annotation.GetBounds();
        var strokeBrush = new SolidColorBrush(Color.Parse(annotation.StrokeColor));

        Control? control = null;

        if (annotation is RectangleAnnotation rect)
        {
            control = rect.CreateVisual();
        }
        else if (annotation is EllipseAnnotation ellipse)
        {
            control = ellipse.CreateVisual();
        }
        else if (annotation is LineAnnotation line)
        {
            control = line.CreateVisual();
        }
        else if (annotation is ArrowAnnotation arrow)
        {
            control = arrow.CreateVisual();
        }

        if (control != null)
        {
            Canvas.SetLeft(control, bounds.Left);
            Canvas.SetTop(control, bounds.Top);
            if (control is Shape shape && !(control is Line))
            {
                shape.Width = bounds.Width;
                shape.Height = bounds.Height;
            }
        }

        return control;
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
