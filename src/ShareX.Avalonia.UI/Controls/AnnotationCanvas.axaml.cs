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

namespace ShareX.Ava.UI.Controls;

public partial class AnnotationCanvas : UserControl
{
    public static readonly StyledProperty<AnnotationCanvasViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<AnnotationCanvas, AnnotationCanvasViewModel?>(nameof(ViewModel));

    public AnnotationCanvasViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
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

    private const double HandleSize = 8;

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

        if (ViewModel.SelectedAnnotation != null)
        {
            var bounds = ViewModel.SelectedAnnotation.GetBounds();
            var selectionPen = new Pen(Brushes.DodgerBlue, 1, dashStyle: new DashStyle(new double[] { 4, 4 }, 0));
            context.DrawRectangle(null, selectionPen, bounds.Inflate(2));

            foreach (var handle in GetHandles(bounds))
            {
                context.DrawRectangle(Brushes.White, new Pen(Brushes.DodgerBlue, 1), handle);
            }
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

        if (ViewModel.ActiveTool == EditorTool.Select)
        {
            var hit = HitTestAnnotation(pos);
            if (ViewModel.SelectedAnnotation != null)
            {
                var handle = HitHandle(ViewModel.SelectedAnnotation.GetBounds(), pos);
                if (handle != HandleKind.None)
                {
                    _activeHandle = handle;
                    _isResizing = true;
                    e.Pointer.Capture(this);
                    return;
                }
            }

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

        _activeAnnotation = CreateAnnotation(ViewModel.ActiveTool, pos, ViewModel.StrokeColor, ViewModel.StrokeWidth);
        if (_activeAnnotation != null)
        {
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
            return;
        }

        if (_isMoving && ViewModel.SelectedAnnotation != null)
        {
            var delta = pos - _lastPoint;
            MoveAnnotation(ViewModel.SelectedAnnotation, delta);
            _lastPoint = pos;
            InvalidateVisual();
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
            _activeAnnotation = null;
            ViewModel.CommitCurrentState();
        }

        if (_isMoving || _isResizing)
        {
            _isMoving = false;
            _isResizing = false;
            _activeHandle = HandleKind.None;
            ViewModel.CommitCurrentState();
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

    private Annotation? CreateAnnotation(EditorTool tool, Point start, string color, double width)
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
                return new HighlightAnnotation { StartPoint = start, EndPoint = start, StrokeColor = color, StrokeWidth = width };
            case EditorTool.Pen:
                return new FreehandAnnotation { StartPoint = start, EndPoint = start, StrokeColor = color, StrokeWidth = width, Points = new List<Point> { start } };
            default:
                return null;
        }
    }

    private void BeginTextEdit(Point pos)
    {
        if (_canvas == null || _overlay == null || ViewModel == null) return;

        _textEditor = new TextBox
        {
            Width = 200,
            Height = 32,
            Foreground = new SolidColorBrush(Color.Parse(ViewModel.StrokeColor)),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.White,
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
        _overlay.IsHitTestVisible = false;
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
