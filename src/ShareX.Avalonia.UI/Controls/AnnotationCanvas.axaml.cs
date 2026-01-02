using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Markup.Xaml;
using ShareX.Ava.Annotations.Models;
using ShareX.Ava.UI.ViewModels;
using ShareX.Ava.UI.Helpers;
using SkiaSharp;
using Avalonia.Media.Imaging;

namespace ShareX.Ava.UI.Controls;

public partial class AnnotationCanvas : UserControl
{
    public event EventHandler<Rect>? CropRequested;

    public static readonly StyledProperty<AnnotationCanvasViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<AnnotationCanvas, AnnotationCanvasViewModel?>(nameof(ViewModel));

    public AnnotationCanvasViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly StyledProperty<Bitmap?> SourceImageProperty =
        AvaloniaProperty.Register<AnnotationCanvas, Bitmap?>(nameof(SourceImage));

    public Bitmap? SourceImage
    {
        get => GetValue(SourceImageProperty);
        set => SetValue(SourceImageProperty, value);
    }

    private Canvas? _canvas;
    private Canvas? _overlay;

    private Annotation? _activeAnnotation;
    private bool _isDrawing;
    private bool _isMoving;
    private bool _isResizing;
    private Point _startPoint;
    private Point _lastPoint;
    private HandleKind _activeHandle = HandleKind.None;
    private TextBox? _textEditor;

    private const double HandleSize = 15;

    private bool _isCropping;

    private SKBitmap? _cachedSourceSk;
    
    private List<Border> _selectionHandles = new List<Border>();

    static AnnotationCanvas()
    {
        SourceImageProperty.Changed.AddClassHandler<AnnotationCanvas>((o, e) => o.OnSourceImageChanged());
    }

    private void OnSourceImageChanged()
    {
        _cachedSourceSk?.Dispose();
        _cachedSourceSk = null;
        if (SourceImage != null)
        {
            _cachedSourceSk = BitmapConversionHelpers.ToSKBitmap(SourceImage);
        }
    }

    public AnnotationCanvas()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPointerPressed, Avalonia.Interactivity.RoutingStrategies.Bubble);
        AddHandler(PointerMovedEvent, OnPointerMoved, Avalonia.Interactivity.RoutingStrategies.Bubble);
        AddHandler(PointerReleasedEvent, OnPointerReleased, Avalonia.Interactivity.RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, true);
        Loaded += OnLoaded;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _canvas = this.FindControl<Canvas>("PART_Canvas");
        _overlay = this.FindControl<Canvas>("PART_Overlay");
        HookCollectionChanged();
    }

    private void HookCollectionChanged()
    {
        if (ViewModel == null) return;
        
        // Ensure overlay is hit test visible so handles can be clicked
        if (_overlay != null)
        {
            _overlay.IsHitTestVisible = true;
        }

        if (ViewModel?.Annotations != null)
        {
            ViewModel.Annotations.CollectionChanged -= OnAnnotationsChanged;
            ViewModel.Annotations.CollectionChanged += OnAnnotationsChanged;
        }

        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AnnotationCanvasViewModel.SelectedAnnotation) ||
            e.PropertyName == nameof(AnnotationCanvasViewModel.ActiveTool) ||
            e.PropertyName == nameof(AnnotationCanvasViewModel.StrokeColor) ||
            e.PropertyName == nameof(AnnotationCanvasViewModel.StrokeWidth))
        {
            InvalidateVisual();
            UpdateSelectionHandles();
        }
    }

    private void OnAnnotationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ViewModelProperty)
        {
            HookCollectionChanged();
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (ViewModel?.Annotations == null) return;

        foreach (var annotation in ViewModel.Annotations.OrderBy(a => a.ZIndex))
        {
            annotation.Render(context);
        }

        // Draw selection outline only (handles are now Border controls in overlay)
        if (ViewModel.SelectedAnnotation != null && ViewModel.SelectedAnnotation is not CropAnnotation)
        {
            var bounds = ViewModel.SelectedAnnotation.GetBounds();
            
            // Use theme-aware colors for selection outline
            IBrush accentBrush = Brushes.DodgerBlue;
            if (Application.Current?.TryFindResource("SystemAccentColor", out var accentRes) == true && accentRes is Color accentColor)
            {
                accentBrush = new SolidColorBrush(accentColor);
            }
            
            var selectionPen = new Pen(accentBrush, 1, dashStyle: new DashStyle(new double[] { 4, 4 }, 0));
            context.DrawRectangle(null, selectionPen, bounds.Inflate(2));
        }
    }

    private IEnumerable<Rect> GetHandles(Rect bounds)
    {
        var hs = HandleSize;
        var half = hs / 2;
        yield return new Rect(bounds.TopLeft.X - half, bounds.TopLeft.Y - half, hs, hs); // tl
        yield return new Rect(bounds.Center.X - half, bounds.Top - half, hs, hs); // tc
        yield return new Rect(bounds.TopRight.X - half, bounds.TopRight.Y - half, hs, hs); // tr
        yield return new Rect(bounds.Right - half, bounds.Center.Y - half, hs, hs); // rc
        yield return new Rect(bounds.BottomRight.X - half, bounds.BottomRight.Y - half, hs, hs); // br
        yield return new Rect(bounds.Center.X - half, bounds.Bottom - half, hs, hs); // bc
        yield return new Rect(bounds.BottomLeft.X - half, bounds.BottomLeft.Y - half, hs, hs); // bl
        yield return new Rect(bounds.Left - half, bounds.Center.Y - half, hs, hs); // lc
    }

    private HandleKind HitHandle(Rect bounds, Point point)
    {
        var hs = HandleSize;
        var half = hs / 2;
        var handles = GetHandles(bounds).ToArray();
        for (int i = 0; i < handles.Length; i++)
        {
            if (handles[i].Contains(point)) return (HandleKind)i;
        }
        return HandleKind.None;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel == null) return;
        
        var pos = e.GetPosition(this);
        _startPoint = pos;
        _lastPoint = pos;

        // Check if clicking on a handle - Use robust Source check
        // Note: _overlay must be IsHitTestVisible=True for this to work
        if (e.Source is Control sourceControl && _selectionHandles.Contains(sourceControl))
        {
            if (sourceControl.Tag is HandleKind kind)
            {
                _activeHandle = kind;
                _isResizing = true;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
        }

        if (ViewModel.ActiveTool == EditorTool.Select)
        {
            var hit = HitTestAnnotation(pos);
            
            if (hit != null)
            {
                ViewModel.SelectedAnnotation = hit;
                _isMoving = true;
                e.Pointer.Capture(this);
                InvalidateVisual();
            }
            else
            {
                ViewModel.SelectedAnnotation = null;
                InvalidateVisual();
            }
            return;
        }

        if (ViewModel.ActiveTool == EditorTool.Text)
        {
            BeginTextEdit(pos);
            return;
        }

        if (ViewModel.ActiveTool == EditorTool.Crop)
        {
            var crop = new CropAnnotation { StartPoint = pos, EndPoint = pos };
            _activeAnnotation = crop;
            _isDrawing = true;
            _isCropping = true;
            ViewModel.AddAnnotation(crop, pushState: false);
            e.Pointer.Capture(this);
            return;
        }

        _activeAnnotation = CreateAnnotation(ViewModel.ActiveTool, pos, ViewModel.StrokeColor, ViewModel.StrokeWidth, ViewModel.NumberCounter);
        if (_activeAnnotation != null)
        {
            if (_activeAnnotation is NumberAnnotation)
            {
                ViewModel.IncrementNumberCounter();
            }
            ViewModel.AddAnnotation(_activeAnnotation);
            _isDrawing = true;
            e.Pointer.Capture(this);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (ViewModel == null) return;
        var pos = e.GetPosition(this);

        if (_isResizing && ViewModel.SelectedAnnotation != null)
        {
            ApplyResize(ViewModel.SelectedAnnotation, _activeHandle, pos);
            _lastPoint = pos;
            InvalidateVisual();
            UpdateSelectionHandles(); // Update handle positions during resize
            return;
        }

        if (_isMoving && ViewModel.SelectedAnnotation != null)
        {
            var delta = pos - _lastPoint;
            MoveAnnotation(ViewModel.SelectedAnnotation, delta);
            _lastPoint = pos;
            InvalidateVisual();
            UpdateSelectionHandles(); // Update handle positions during move
            return;
        }

        if (_isDrawing && _activeAnnotation != null)
        {
            UpdateActiveAnnotation(_activeAnnotation, pos);
            InvalidateVisual();
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (ViewModel == null) return;

        if (_isDrawing)
        {
            _isDrawing = false;

            if (_isCropping && _activeAnnotation is CropAnnotation crop)
            {
                var rect = new Rect(crop.StartPoint, crop.EndPoint);
                rect = new Rect(Math.Min(rect.Left, rect.Right), Math.Min(rect.Top, rect.Bottom), Math.Abs(rect.Width), Math.Abs(rect.Height));
                CropRequested?.Invoke(this, rect);
                ViewModel.Annotations.Remove(crop);
                _isCropping = false;
            }
            else
            {
                if (_activeAnnotation is BaseEffectAnnotation effect)
                {
                    UpdateEffect(effect);
                }
                
                // Select the newly drawn annotation and show handles
                if (_activeAnnotation != null)
                {
                    ViewModel.SelectedAnnotation = _activeAnnotation;
                    UpdateSelectionHandles();
                }
                
                ViewModel.CommitCurrentState();
            }

            _activeAnnotation = null;
        }

        if (_isMoving || _isResizing)
        {
            _isMoving = false;
            _isResizing = false;
            _activeHandle = HandleKind.None;
            if (ViewModel.SelectedAnnotation is BaseEffectAnnotation eff)
            {
                UpdateEffect(eff);
            }
            ViewModel.CommitCurrentState();
            UpdateSelectionHandles(); // Final handle update after operation complete
        }

        e.Pointer.Capture(null);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel == null) return;
        if (e.Key == Key.Delete)
        {
            ViewModel.RemoveSelected();
            InvalidateVisual();
            e.Handled = true;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Z)
        {
            ViewModel.Undo();
            InvalidateVisual();
            e.Handled = true;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Y)
        {
            ViewModel.Redo();
            InvalidateVisual();
            e.Handled = true;
        }
    }

    private Annotation? HitTestAnnotation(Point point)
    {
        if (ViewModel == null) return null;
        foreach (var annotation in ViewModel.Annotations.OrderByDescending(a => a.ZIndex))
        {
            if (annotation.HitTest(point))
            {
                return annotation;
            }
        }
        return null;
    }

    private void UpdateActiveAnnotation(Annotation annotation, Point current)
    {
        switch (annotation)
        {
            case RectangleAnnotation rect:
                rect.EndPoint = current;
                break;
            case EllipseAnnotation ellipse:
                ellipse.EndPoint = current;
                break;
            case LineAnnotation line:
                line.EndPoint = current;
                break;
            case ArrowAnnotation arrow:
                arrow.EndPoint = current;
                break;
            case HighlightAnnotation highlight:
                highlight.EndPoint = current;
                break;
            case FreehandAnnotation freehand:
                freehand.Points.Add(current);
                freehand.EndPoint = current;
                break;
            case CropAnnotation crop:
                crop.EndPoint = current;
                break;
            case SpotlightAnnotation spot:
                spot.EndPoint = current;
                spot.CanvasSize = new Size(SourceImage?.Size.Width ?? Bounds.Width, SourceImage?.Size.Height ?? Bounds.Height);
                break;
            default:
                annotation.EndPoint = current;
                break;
        }
    }

    private void MoveAnnotation(Annotation annotation, Vector delta)
    {
        annotation.StartPoint = annotation.StartPoint + delta;
        annotation.EndPoint = annotation.EndPoint + delta;

        if (annotation is FreehandAnnotation freehand)
        {
            for (int i = 0; i < freehand.Points.Count; i++)
            {
                freehand.Points[i] = freehand.Points[i] + delta;
            }
        }
    }

    private void ApplyResize(Annotation annotation, HandleKind handle, Point pos)
    {
        var bounds = annotation.GetBounds();
        double left = bounds.Left;
        double top = bounds.Top;
        double right = bounds.Right;
        double bottom = bounds.Bottom;

        switch (handle)
        {
            case HandleKind.TopLeft:
                left = pos.X; top = pos.Y; break;
            case HandleKind.TopCenter:
                top = pos.Y; break;
            case HandleKind.TopRight:
                right = pos.X; top = pos.Y; break;
            case HandleKind.RightCenter:
                right = pos.X; break;
            case HandleKind.BottomRight:
                right = pos.X; bottom = pos.Y; break;
            case HandleKind.BottomCenter:
                bottom = pos.Y; break;
            case HandleKind.BottomLeft:
                left = pos.X; bottom = pos.Y; break;
            case HandleKind.LeftCenter:
                left = pos.X; break;
        }

        var newStart = new Point(Math.Min(left, right), Math.Min(top, bottom));
        var newEnd = new Point(Math.Max(left, right), Math.Max(top, bottom));
        annotation.StartPoint = newStart;
        annotation.EndPoint = newEnd;

        if (annotation is FreehandAnnotation freehand)
        {
            // Simple scale around bounds
            var oldWidth = bounds.Width;
            var oldHeight = bounds.Height;
            if (oldWidth <= 0 || oldHeight <= 0) return;
            for (int i = 0; i < freehand.Points.Count; i++)
            {
                var p = freehand.Points[i];
                var nx = (p.X - bounds.Left) / oldWidth;
                var ny = (p.Y - bounds.Top) / oldHeight;
                freehand.Points[i] = new Point(newStart.X + nx * (newEnd.X - newStart.X), newStart.Y + ny * (newEnd.Y - newStart.Y));
            }
        }
    }

    private Annotation? CreateAnnotation(EditorTool tool, Point start, string color, double width, int numberCounter)
    {
        switch (tool)
        {
            case EditorTool.Rectangle:
                return new RectangleAnnotation { StartPoint = start, EndPoint = start, StrokeColor = color, StrokeWidth = width };
            case EditorTool.Ellipse:
                return new EllipseAnnotation { StartPoint = start, EndPoint = start, StrokeColor = color, StrokeWidth = width };
            case EditorTool.Line:
                return new LineAnnotation { StartPoint = start, EndPoint = start, StrokeColor = color, StrokeWidth = width };
            case EditorTool.Arrow:
                return new ArrowAnnotation { StartPoint = start, EndPoint = start, StrokeColor = color, StrokeWidth = width };
            case EditorTool.Highlighter:
                var c = Color.Parse(color);
                var hlColor = Color.FromArgb(0x55, c.R, c.G, c.B);
                return new HighlightAnnotation { StartPoint = start, EndPoint = start, StrokeColor = hlColor.ToString(), StrokeWidth = width };
            case EditorTool.Pen:
                return new FreehandAnnotation { StartPoint = start, EndPoint = start, StrokeColor = color, StrokeWidth = width, Points = new List<Point> { start } };
            case EditorTool.SmartEraser:
                var erase = new SmartEraserAnnotation { StartPoint = start, EndPoint = start, Points = new List<Point> { start } };
                var sampled = SampleColor(start);
                if (!string.IsNullOrEmpty(sampled)) erase.StrokeColor = sampled!;
                return erase;
            case EditorTool.Number:
                return new NumberAnnotation
                {
                    StartPoint = start,
                    EndPoint = start,
                    StrokeColor = color,
                    StrokeWidth = width,
                    Number = numberCounter,
                    Radius = Math.Max(12, width * 4),
                    FontSize = Math.Max(12, width * 6)
                };
            case EditorTool.Crop:
                return new CropAnnotation { StartPoint = start, EndPoint = start };
            case EditorTool.Blur:
                return new BlurAnnotation { StartPoint = start, EndPoint = start, StrokeColor = color, StrokeWidth = width };
            case EditorTool.Pixelate:
                return new PixelateAnnotation { StartPoint = start, EndPoint = start, StrokeColor = color, StrokeWidth = width };
            case EditorTool.Magnify:
                return new MagnifyAnnotation { StartPoint = start, EndPoint = start, StrokeColor = color, StrokeWidth = width };
            case EditorTool.Spotlight:
                return new SpotlightAnnotation { StartPoint = start, EndPoint = start, CanvasSize = new Size(SourceImage?.Size.Width ?? Bounds.Width, SourceImage?.Size.Height ?? Bounds.Height) };
            case EditorTool.SpeechBalloon:
                return new SpeechBalloonAnnotation { StartPoint = start, EndPoint = new Point(start.X + 120, start.Y + 80), StrokeColor = color, StrokeWidth = width };
            default:
                return null;
        }
    }

    private string? SampleColor(Point point)
    {
        if (_cachedSourceSk == null) return null;
        int x = (int)Math.Round(point.X);
        int y = (int)Math.Round(point.Y);
        if (x < 0 || y < 0 || x >= _cachedSourceSk.Width || y >= _cachedSourceSk.Height) return null;
        var c = _cachedSourceSk.GetPixel(x, y);
        return $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}";
    }

    private void UpdateEffect(BaseEffectAnnotation effect)
    {
        if (_cachedSourceSk == null) return;
        effect.UpdateEffect(_cachedSourceSk);
        InvalidateVisual();
    }

    private void BeginTextEdit(Point pos)
    {
        if (_canvas == null || _overlay == null || ViewModel == null) return;

        // Use theme-aware border color
        var borderBrush = Application.Current?.TryFindResource("SystemControlForegroundBaseHighBrush", out var borderRes) == true && borderRes is IBrush brush
            ? brush
            : Brushes.White;

        _textEditor = new TextBox
        {
            Width = 200,
            Height = 32,
            Foreground = new SolidColorBrush(Color.Parse(ViewModel.StrokeColor)),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = borderBrush,
            Padding = new Thickness(4)
        };

        Canvas.SetLeft(_textEditor, pos.X);
        Canvas.SetTop(_textEditor, pos.Y);
        _overlay.Children.Add(_textEditor);
        _overlay.IsHitTestVisible = true;
        _textEditor.Focus();

        _textEditor.LostFocus += OnTextCommit;
        _textEditor.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                OnTextCommit(s, EventArgs.Empty);
            }
        };
    }

    private void OnTextCommit(object? sender, EventArgs e)
    {
        if (_textEditor == null || ViewModel == null || _overlay == null) return;
        var text = _textEditor.Text ?? string.Empty;
        var pos = new Point(Canvas.GetLeft(_textEditor), Canvas.GetTop(_textEditor));
        _textEditor.LostFocus -= OnTextCommit;

        _overlay.Children.Remove(_textEditor);
        // Do NOT set IsHitTestVisible to false, otherwise handles become unclickable
        // _overlay.IsHitTestVisible = false; 
        _textEditor = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var annotation = new TextAnnotation
        {
            StartPoint = pos,
            EndPoint = pos,
            Text = text,
            StrokeColor = ViewModel.StrokeColor,
            StrokeWidth = ViewModel.StrokeWidth,
            FontSize = Math.Max(12, ViewModel.StrokeWidth * 6)
        };
        ViewModel.AddAnnotation(annotation);
        ViewModel.CommitCurrentState();
        InvalidateVisual();
    }

    private void UpdateSelectionHandles()
    {
        if (_overlay == null) return;

        // Clear existing handles
        foreach (var handle in _selectionHandles)
        {
            _overlay.Children.Remove(handle);
        }
        _selectionHandles.Clear();

        if (ViewModel?.SelectedAnnotation == null || ViewModel.SelectedAnnotation is CropAnnotation) return;

        var bounds = ViewModel.SelectedAnnotation.GetBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // Create 8 handles
        CreateHandle(bounds.TopLeft, HandleKind.TopLeft);
        CreateHandle(new Point(bounds.Center.X, bounds.Top), HandleKind.TopCenter);
        CreateHandle(bounds.TopRight, HandleKind.TopRight);
        CreateHandle(new Point(bounds.Right, bounds.Center.Y), HandleKind.RightCenter);
        CreateHandle(bounds.BottomRight, HandleKind.BottomRight);
        CreateHandle(new Point(bounds.Center.X, bounds.Bottom), HandleKind.BottomCenter);
        CreateHandle(bounds.BottomLeft, HandleKind.BottomLeft);
        CreateHandle(new Point(bounds.Left, bounds.Center.Y), HandleKind.LeftCenter);
    }

    private void CreateHandle(Point position, HandleKind kind)
    {
        if (_overlay == null) return;

        // Determine cursor based on handle kind
        StandardCursorType cursorType = kind switch
        {
            HandleKind.TopLeft or HandleKind.BottomRight => StandardCursorType.TopLeftCorner,
            HandleKind.TopRight or HandleKind.BottomLeft => StandardCursorType.TopRightCorner,
            HandleKind.TopCenter or HandleKind.BottomCenter => StandardCursorType.SizeNorthSouth,
            HandleKind.LeftCenter or HandleKind.RightCenter => StandardCursorType.SizeWestEast,
            _ => StandardCursorType.Hand
        };

        // Get theme-aware colors
        IBrush accentBrush = Brushes.DodgerBlue;
        if (Application.Current?.TryFindResource("SystemAccentColor", out var accentRes) == true && accentRes is Color accentColor)
        {
            accentBrush = new SolidColorBrush(accentColor);
        }

        var handleBorder = new Border
        {
            Width = HandleSize,
            Height = HandleSize,
            CornerRadius = new CornerRadius(HandleSize / 2), // Circular
            Background = Brushes.White,
            BorderBrush = accentBrush,
            BorderThickness = new Thickness(2),
            Tag = kind,
            Cursor = new Cursor(cursorType),
            BoxShadow = new BoxShadows(new BoxShadow { Blur = 4, Color = Colors.Black, OffsetX = 0, OffsetY = 2, Spread = 0 })
        };

        // Note: No event handlers attached - events bubble to main AnnotationCanvas handlers

        // Center the handle on the position
        Canvas.SetLeft(handleBorder, position.X - HandleSize / 2);
        Canvas.SetTop(handleBorder, position.Y - HandleSize / 2);

        _overlay.Children.Add(handleBorder);
        _selectionHandles.Add(handleBorder);
    }



    private enum HandleKind
    {
        TopLeft = 0,
        TopCenter = 1,
        TopRight = 2,
        RightCenter = 3,
        BottomRight = 4,
        BottomCenter = 5,
        BottomLeft = 6,
        LeftCenter = 7,
        None = 8
    }
}
