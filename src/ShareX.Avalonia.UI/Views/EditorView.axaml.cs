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
        
        // Handles
        private List<global::Avalonia.Controls.Shapes.Rectangle> _selectionHandles = new();
        private bool _isDraggingHandle;
        private global::Avalonia.Controls.Shapes.Rectangle? _draggedHandle;
        
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                if (e.Key == Key.Delete)
                {
                    vm.DeleteSelectedCommand.Execute(null);
                }
                else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                     if (e.Key == Key.Z)
                     {
                         vm.UndoCommand.Execute(null);
                     }
                     else if (e.Key == Key.Y) // Standard Redo
                     {
                         vm.RedoCommand.Execute(null);
                     }
                }
                // Ctrl+Shift+Z is also common for Redo, check modifiers
                else if (e.KeyModifiers.HasFlag(KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.Z)
                {
                    vm.RedoCommand.Execute(null);
                }
            }
        }
        
        private void UpdateSelectionHandles()
        {
            var overlay = this.FindControl<Canvas>("OverlayCanvas");
            if (overlay == null) return;

            // Clear existing handles
            foreach (var handle in _selectionHandles)
            {
                overlay.Children.Remove(handle);
            }
            _selectionHandles.Clear();

            if (_selectedShape == null) return;

            // Calculate bounds
            var left = Canvas.GetLeft(_selectedShape);
            var top = Canvas.GetTop(_selectedShape);
            var width = _selectedShape.Bounds.Width;
            var height = _selectedShape.Bounds.Height;
            
            // Allow handles even if Width/Height are NaN (e.g. Line)? 
            // For now assume shapes have explicit size setting in OnPointerMoved
            if (double.IsNaN(width)) width = _selectedShape.Width;
            if (double.IsNaN(height)) height = _selectedShape.Height;

            // Create 8 handles
            CreateHandle(left, top, "TopLeft");
            CreateHandle(left + width / 2, top, "TopCenter");
            CreateHandle(left + width, top, "TopRight");
            CreateHandle(left + width, top + height / 2, "RightCenter");
            CreateHandle(left + width, top + height, "BottomRight");
            CreateHandle(left + width / 2, top + height, "BottomCenter");
            CreateHandle(left, top + height, "BottomLeft");
            CreateHandle(left, top + height / 2, "LeftCenter");
        }

        private void CreateHandle(double x, double y, string tag)
        {
            var overlay = this.FindControl<Canvas>("OverlayCanvas");
            
            // Determine cursor based on position tag
            Cursor cursor = Cursor.Parse("Hand");
            if (tag.Contains("TopLeft") || tag.Contains("BottomRight")) cursor = new Cursor(StandardCursorType.TopLeftCorner);
            else if (tag.Contains("TopRight") || tag.Contains("BottomLeft")) cursor = new Cursor(StandardCursorType.TopRightCorner);
            else if (tag.Contains("Top") || tag.Contains("Bottom")) cursor = new Cursor(StandardCursorType.SizeNorthSouth);
            else if (tag.Contains("Left") || tag.Contains("Right")) cursor = new Cursor(StandardCursorType.SizeWestEast);

            var handle = new global::Avalonia.Controls.Shapes.Rectangle
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.White,
                Stroke = Brushes.Blue,
                StrokeThickness = 1,
                Tag = tag, // Store position intent
                Cursor = cursor
            };
            
            // Center the handle
            Canvas.SetLeft(handle, x - 5);
            Canvas.SetTop(handle, y - 5);
            
            overlay.Children.Add(handle);
            _selectionHandles.Add(handle);
        }

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
                vm.SnapshotRequested += GetSnapshot;
            }
        }

        public async System.Threading.Tasks.Task<global::Avalonia.Media.Imaging.Bitmap?> GetSnapshot()
        {
            var container = this.FindControl<Grid>("CanvasContainer");
            if (container == null || container.Width <= 0 || container.Height <= 0) return null;
            
            // Wait for layout update if needed?
            // Render the container to a bitmap
            // Since the container is sized to the Image (e.g. 1920x1080), extracting it should yield full res
            
            try 
            {
                var rtb = new global::Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)container.Width, (int)container.Height), new Vector(96, 96));
                rtb.Render(container);
                return rtb;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Snapshot failed: " + ex.Message);
                return null;
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

            // Check if clicking a handle
            if ((_selectedShape != null && vm.ActiveTool == EditorTool.Select) || vm.ActiveTool == EditorTool.Crop)
            {
                 var overlay = this.FindControl<Canvas>("OverlayCanvas");
                 if (overlay != null)
                 {
                     var handle = e.Source as global::Avalonia.Controls.Shapes.Rectangle;
                     if (handle != null && overlay.Children.Contains(handle))
                     {
                         _isDraggingHandle = true;
                         _draggedHandle = handle;
                         _startPoint = e.GetPosition(overlay); // Use overlay coords for handles
                         
                         // If we are cropping, ensure we are selecting the crop overlay
                         if (vm.ActiveTool == EditorTool.Crop)
                         {
                              var cropOverlay = this.FindControl<global::Avalonia.Controls.Shapes.Rectangle>("CropOverlay");
                              _selectedShape = cropOverlay;
                         }
                         return;
                     }
                 }
            }

            if (vm.ActiveTool == EditorTool.Select)
            {
                // Hit test - find the direct child of the canvas
                var hitSource = e.Source as global::Avalonia.Visual;
                Control? hitTarget = null;

                while (hitSource != null && hitSource != canvas)
                {
                    if (canvas.Children.Contains(hitSource as Control))
                    {
                        hitTarget = hitSource as Control;
                        break;
                    }
                    hitSource = hitSource.GetVisualParent();
                }

                if (hitTarget != null && hitTarget.Name != "CropOverlay")
                {
                    _selectedShape = hitTarget;
                    _lastDragPoint = point;
                    _isDraggingShape = true;
                    UpdateSelectionHandles();
                }
                else
                {
                    _selectedShape = null;
                    UpdateSelectionHandles();
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
                    canvas.Children.Add(textBox);
                    textBox.Focus();
                    _isDrawing = false; 
                    return; 

                case EditorTool.Spotlight:
                    _currentShape = new global::Avalonia.Controls.Shapes.Ellipse
                    {
                        Stroke = new SolidColorBrush(Color.Parse("#B0000000")), // Dark overlay
                        StrokeThickness = 4000, // Massive stroke to cover the canvas
                        Fill = Brushes.Transparent, // The 'hole'
                        IsHitTestVisible = true // Allow selecting to move/delete
                    };
                    break;
                    
                case EditorTool.Number:
                    // Create Number badge
                    var numberGrid = new Grid
                    {
                        Width = 30,
                        Height = 30
                    };
                    
                    var bg = new global::Avalonia.Controls.Shapes.Ellipse
                    {
                        Fill = brush,
                        Stroke = Brushes.White,
                        StrokeThickness = 2
                    };
                    
                    var numText = new TextBlock
                    {
                        Text = vm.NumberCounter.ToString(),
                        Foreground = Brushes.White,
                        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                        VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                        FontWeight = FontWeight.Bold
                    };
                    
                    numberGrid.Children.Add(bg);
                    numberGrid.Children.Add(numText);
                    
                    // Position centered on click
                    Canvas.SetLeft(numberGrid, _startPoint.X - 15);
                    Canvas.SetTop(numberGrid, _startPoint.Y - 15);
                    
                    _currentShape = numberGrid;
                    vm.NumberCounter++; // Increment for next click
                    
                    // Add immediately (single click tool)
                    canvas.Children.Add(numberGrid);
                    _undoStack.Push(numberGrid);
                    _redoStack.Clear();
                    _currentShape = null; 
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

            if (_isDraggingHandle && _draggedHandle != null && _selectedShape != null)
            {
                var handleTag = _draggedHandle.Tag?.ToString();
                var deltaX = currentPoint.X - _startPoint.X;
                var deltaY = currentPoint.Y - _startPoint.Y;
                
                // Get current bounds
                var left = Canvas.GetLeft(_selectedShape);
                var top = Canvas.GetTop(_selectedShape);
                var width = _selectedShape.Bounds.Width;
                var height = _selectedShape.Bounds.Height;
                 if (double.IsNaN(width)) width = _selectedShape.Width;
                 if (double.IsNaN(height)) height = _selectedShape.Height;

                // Helper to update properties
                // Rectangle/Ellipse use Width/Height
                // Line uses Start/End Point
                
                if (_selectedShape is global::Avalonia.Controls.Shapes.Rectangle || _selectedShape is global::Avalonia.Controls.Shapes.Ellipse || _selectedShape is Grid)
                {
                    double newLeft = left;
                    double newTop = top;
                    double newWidth = width;
                    double newHeight = height;

                    if (handleTag.Contains("Right"))
                    {
                        newWidth = Math.Max(1, width + deltaX);
                    }
                    else if (handleTag.Contains("Left"))
                    {
                        var change = Math.Min(width - 1, deltaX);
                        newLeft += change;
                        newWidth -= change;
                    }

                    if (handleTag.Contains("Bottom"))
                    {
                        newHeight = Math.Max(1, height + deltaY);
                    }
                    else if (handleTag.Contains("Top"))
                    {
                        var change = Math.Min(height - 1, deltaY);
                        newTop += change;
                        newHeight -= change;
                    }

                    Canvas.SetLeft(_selectedShape, newLeft);
                    Canvas.SetTop(_selectedShape, newTop);
                    _selectedShape.Width = newWidth;
                    _selectedShape.Height = newHeight;
                }
                else if (_selectedShape is global::Avalonia.Controls.Shapes.Line targetLine)
                {
                    // Line resizing logic... simpler? just move endpoints?
                    // For now, let's just support moving lines, resizing lines via handles is tricky without a bounding box logic wrapper
                    // We can skip line resizing for this iteration or treat it as a box (which might look weird)
                }

                _startPoint = currentPoint; // Update for next delta
                UpdateSelectionHandles();
                return;
            }

            if (_isDraggingShape && _selectedShape != null)
            {
                var deltaX = currentPoint.X - _lastDragPoint.X;
                var deltaY = currentPoint.Y - _lastDragPoint.Y;

                var left = Canvas.GetLeft(_selectedShape);
                var top = Canvas.GetTop(_selectedShape);

                Canvas.SetLeft(_selectedShape, left + deltaX);
                Canvas.SetTop(_selectedShape, top + deltaY);

                _lastDragPoint = currentPoint;
                UpdateSelectionHandles(); // Move handles with shape
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
            if (_isDraggingHandle)
            {
                _isDraggingHandle = false;
                _draggedHandle = null;
                return;
            }

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
