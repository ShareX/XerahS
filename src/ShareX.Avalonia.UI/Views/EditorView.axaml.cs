using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using ShareX.Avalonia.UI.ViewModels;
using System;
using System.Collections.Generic;

namespace ShareX.Avalonia.UI.Views
{
    public partial class EditorView : UserControl
    {
        private Point _startPoint;
        private Control? _currentShape;
        private bool _isDrawing;

        // Selection state
        private Control? _selectedShape;
        private Point _lastDragPoint;
        private bool _isDraggingShape;

        private Stack<Control> _undoStack = new();
        private Stack<Control> _redoStack = new();

        public EditorView()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (DataContext is MainViewModel vm)
            {
                vm.UndoRequested += (s, args) => PerformUndo();
                vm.RedoRequested += (s, args) => PerformRedo();
                vm.DeleteRequested += (s, args) => PerformDelete();
            }
        }
        
        // --- LOGIC MIGRATED FROM MAINWINDOW.AXAML.CS ---

        private void PerformUndo()
        {
            if (_undoStack.Count > 0)
            {
                var shape = _undoStack.Pop();
                var canvas = this.FindControl<Canvas>("AnnotationCanvas");
                if (canvas != null && canvas.Children.Contains(shape))
                {
                    canvas.Children.Remove(shape);
                    _redoStack.Push(shape);
                    
                    if (_selectedShape == shape) _selectedShape = null;
                }
            }
        }

        private void PerformRedo()
        {
            if (_redoStack.Count > 0)
            {
                var shape = _redoStack.Pop();
                var canvas = this.FindControl<Canvas>("AnnotationCanvas");
                if (canvas != null)
                {
                    canvas.Children.Add(shape);
                    _undoStack.Push(shape);
                }
            }
        }

        private void PerformDelete()
        {
            if (_selectedShape != null)
            {
                var canvas = this.FindControl<Canvas>("AnnotationCanvas");
                if (canvas != null && canvas.Children.Contains(_selectedShape))
                {
                    canvas.Children.Remove(_selectedShape);
                    // Simple deletion, no undo support for delete yet
                    _selectedShape = null;
                }
            }
        }

        private void OnCanvasPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;
            var canvas = sender as Canvas;
            if (canvas == null) return;

            var point = e.GetPosition(canvas);

            if (vm.ActiveTool == EditorTool.Select)
            {
                // Hit test
                var source = e.Source as Control;
                if (source != null && source != canvas && canvas.Children.Contains(source) && source.Name != "CropOverlay")
                {
                    _selectedShape = source;
                    _lastDragPoint = point;
                    _isDraggingShape = true;
                }
                else
                {
                    _selectedShape = null;
                }
                return;
            }

            // Clear Redo stack on new action
            _redoStack.Clear();

            _startPoint = point;
            _isDrawing = true;

            var brush = new SolidColorBrush(Color.Parse(vm.SelectedColor));

            // Special handling for Crop
            if (vm.ActiveTool == EditorTool.Crop)
            {
                var cropOverlay = this.FindControl<global::Avalonia.Controls.Shapes.Rectangle>("CropOverlay");
                if (cropOverlay != null)
                {
                    cropOverlay.IsVisible = true;
                    Canvas.SetLeft(cropOverlay, _startPoint.X);
                    Canvas.SetTop(cropOverlay, _startPoint.Y);
                    cropOverlay.Width = 0;
                    cropOverlay.Height = 0;
                    _currentShape = cropOverlay; 
                }
                return;
            }
            
            switch (vm.ActiveTool)
            {
                case EditorTool.Rectangle:
                    _currentShape = new global::Avalonia.Controls.Shapes.Rectangle
                    {
                        Stroke = brush,
                        StrokeThickness = vm.StrokeWidth,
                        Fill = Brushes.Transparent
                    };
                    break;
                case EditorTool.Ellipse:
                    _currentShape = new global::Avalonia.Controls.Shapes.Ellipse
                    {
                        Stroke = brush,
                        StrokeThickness = vm.StrokeWidth,
                        Fill = Brushes.Transparent
                    };
                    break;
                case EditorTool.Line:
                    _currentShape = new global::Avalonia.Controls.Shapes.Line
                    {
                        Stroke = brush,
                        StrokeThickness = vm.StrokeWidth,
                        StartPoint = _startPoint,
                        EndPoint = _startPoint
                    };
                    break;
                case EditorTool.Arrow:
                    _currentShape = new global::Avalonia.Controls.Shapes.Path
                    {
                        Stroke = brush,
                        StrokeThickness = vm.StrokeWidth,
                        Fill = brush, // Fill arrowhead
                        Data = new PathGeometry()
                    };
                    break;
                case EditorTool.Text:
                    // For text, we create a TextBox directly
                    var textBox = new TextBox
                    {
                        Foreground = brush,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(1), // Visible border while editing
                        BorderBrush = Brushes.White, 
                        FontSize = Math.Max(12, vm.StrokeWidth * 4),
                        Text = "Text",
                        Padding = new Thickness(4),
                        MinWidth = 50
                    };
                    
                    // Position it
                    Canvas.SetLeft(textBox, _startPoint.X);
                    Canvas.SetTop(textBox, _startPoint.Y);
                    
                    // Add logic to remove border when lost focus?
                    textBox.LostFocus += (s, args) => 
                    {
                        if (s is TextBox tb)
                        {
                            tb.BorderThickness = new Thickness(0);
                            if (string.IsNullOrWhiteSpace(tb.Text))
                            {
                                // Remove empty text boxes
                                var parentKey = tb.Parent as Panel;
                                parentKey?.Children.Remove(tb);
                            }
                        }
                    };

                    canvas.Children.Add(textBox);
                    textBox.Focus();
                    _isDrawing = false; 
                    return; 
            }

            if (_currentShape != null)
            {
                if (vm.ActiveTool != EditorTool.Line && vm.ActiveTool != EditorTool.Arrow)
                {
                    Canvas.SetLeft(_currentShape, _startPoint.X);
                    Canvas.SetTop(_currentShape, _startPoint.Y);
                }
                canvas.Children.Add(_currentShape);
            }
        }

        private void OnCanvasPointerMoved(object sender, PointerEventArgs e)
        {
            var canvas = sender as Canvas;
            if (canvas == null) return;
            var currentPoint = e.GetPosition(canvas);

            if (_isDraggingShape && _selectedShape != null)
            {
                var deltaX = currentPoint.X - _lastDragPoint.X;
                var deltaY = currentPoint.Y - _lastDragPoint.Y;

                var left = Canvas.GetLeft(_selectedShape);
                var top = Canvas.GetTop(_selectedShape);

                Canvas.SetLeft(_selectedShape, left + deltaX);
                Canvas.SetTop(_selectedShape, top + deltaY);

                _lastDragPoint = currentPoint;
                return;
            }

            if (!_isDrawing || _currentShape == null) return;
            
            if (_currentShape is global::Avalonia.Controls.Shapes.Line line)
            {
                line.EndPoint = currentPoint;
            }
            else if (_currentShape is global::Avalonia.Controls.Shapes.Path arrowPath && DataContext is MainViewModel vm)
            {
                // Update Arrow Geometry
                arrowPath.Data = CreateArrowGeometry(_startPoint, currentPoint, vm.StrokeWidth * 3);
            }
            else
            {
                var x = Math.Min(_startPoint.X, currentPoint.X);
                var y = Math.Min(_startPoint.Y, currentPoint.Y);
                var width = Math.Abs(_startPoint.X - currentPoint.X);
                var height = Math.Abs(_startPoint.Y - currentPoint.Y);

                if (_currentShape is global::Avalonia.Controls.Shapes.Rectangle rect)
                {
                    rect.Width = width;
                    rect.Height = height;
                    Canvas.SetLeft(rect, x);
                    Canvas.SetTop(rect, y);
                }
                else if (_currentShape is global::Avalonia.Controls.Shapes.Ellipse ellipse)
                {
                    ellipse.Width = width;
                    ellipse.Height = height;
                    Canvas.SetLeft(ellipse, x);
                    Canvas.SetTop(ellipse, y);
                }
            }
        }

        private Geometry CreateArrowGeometry(Point start, Point end, double headSize)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                // Draw line
                ctx.BeginFigure(start, false);
                ctx.LineTo(end);

                // Calculate arrow head
                var d = end - start;
                var length = Math.Sqrt(d.X * d.X + d.Y * d.Y);
                
                if (length > 0)
                {
                    var ux = d.X / length;
                    var uy = d.Y / length;

                    // Arrow head points
                    var p1 = new Point(end.X - headSize * ux + headSize * 0.5 * uy, end.Y - headSize * uy - headSize * 0.5 * ux);
                    var p2 = new Point(end.X - headSize * ux - headSize * 0.5 * uy, end.Y - headSize * uy + headSize * 0.5 * ux);

                    ctx.EndFigure(false);
                    
                    ctx.BeginFigure(end, true); // Filled
                    ctx.LineTo(p1);
                    ctx.LineTo(p2);
                    ctx.LineTo(end);
                }
            }
            return geometry;
        }

        private void OnCanvasPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (_isDraggingShape)
            {
                _isDraggingShape = false;
                return;
            }

            if (_isDrawing)
            {
                _isDrawing = false;
                if (_currentShape != null)
                {
                    _undoStack.Push(_currentShape);
                    // _currentShape is now managed by the canvas/undo stack
                    _currentShape = null;
                }
            }
        }

        // Public method to be called from MainWindow if key is pressed, 
        // OR better: we handle keys in EditorView separately? 
        // UserControls can handle keys if focused, but Window handles global keys better.
        // We will expose this method or command.
        public void PerformCrop()
        {
            var cropOverlay = this.FindControl<global::Avalonia.Controls.Shapes.Rectangle>("CropOverlay");
            if (cropOverlay == null || !cropOverlay.IsVisible || DataContext is not MainViewModel vm) return;

            var x = Canvas.GetLeft(cropOverlay);
            var y = Canvas.GetTop(cropOverlay);
            var w = cropOverlay.Width;
            var h = cropOverlay.Height;

             var scaling = 1.0; 
             if (VisualRoot is TopLevel tl) scaling = tl.RenderScaling;

            var physX = (int)(x * scaling);
            var physY = (int)(y * scaling);
            var physW = (int)(w * scaling);
            var physH = (int)(h * scaling);

            vm.CropImage(physX, physY, physW, physH);
            
            cropOverlay.IsVisible = false;
            vm.StatusText = "Image cropped";
        }
    }
}
