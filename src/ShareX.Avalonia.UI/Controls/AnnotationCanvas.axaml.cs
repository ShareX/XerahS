using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ShareX.Ava.Annotations.Models;
using ShareX.Ava.UI.ViewModels;
using ShareX.Ava.UI.Helpers;
using System;
using System.Collections.Generic;

namespace ShareX.Ava.UI.Controls
{
    public partial class AnnotationCanvas : UserControl
    {
        private Point _startPoint;
        private Control? _currentShape;
        private bool _isDrawing;

        // Selection state
        private Control? _selectedShape;
        private Point _lastDragPoint;
        private bool _isDraggingShape;

        // Handles
        private List<Control> _selectionHandles = new();
        private bool _isDraggingHandle;
        private Control? _draggedHandle;

        // Store arrow/line endpoints for editing
        private Dictionary<Control, (Point Start, Point End)> _shapeEndpoints = new();

        // Cached SKBitmap for effect updates
        private SkiaSharp.SKBitmap? _cachedSkBitmap;

        // Undo/Redo Stacks (Moved from EditorView)
        private Stack<Control> _undoStack = new();
        private Stack<Control> _redoStack = new();

        public AnnotationCanvas()
        {
            InitializeComponent();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (DataContext is MainViewModel vm)
            {
                // Hook up ViewModel commands or property changes if needed
                // For example, if ViewModel triggers Undo/Redo, we need to handle it here
                vm.UndoRequested += (s, args) => PerformUndo();
                vm.RedoRequested += (s, args) => PerformRedo();
                vm.DeleteRequested += (s, args) => PerformDelete();
                vm.ClearAnnotationsRequested += (s, args) => ClearAllAnnotations();
                
                // We might need to handle property changes for color/width to update selected shape immediately
                vm.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
             if (DataContext is not MainViewModel vm) return;

             if (e.PropertyName == nameof(MainViewModel.SelectedColor))
             {
                 ApplySelectedColor(vm.SelectedColor);
             }
             else if (e.PropertyName == nameof(MainViewModel.StrokeWidth))
             {
                 ApplySelectedStrokeWidth(vm.StrokeWidth);
             }
             else if (e.PropertyName == nameof(MainViewModel.PreviewImage))
             {
                 _cachedSkBitmap?.Dispose();
                 _cachedSkBitmap = null;
             }
        }

        private Point GetCanvasPosition(PointerEventArgs e, Canvas canvas)
        {
            return e.GetPosition(canvas);
        }

        private void OnCanvasPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            var mainCanvas = this.FindControl<Canvas>("MainCanvas");
            // If sender is OverlayCanvas, we use MainCanvas for drawing. 
            // _startPoint logic handles coordinate space.
            
            var canvas = mainCanvas; 
            if (canvas == null) return;

             // Ignore middle mouse (panning is handled by parent ScrollViewer)
            var props = e.GetCurrentPoint(sender as Visual).Properties;
            if (props.IsMiddleButtonPressed) return;

            var point = GetCanvasPosition(e, canvas);

            // Handle right-click delete
            if (props.IsRightButtonPressed)
            {
                HandleRightClickDelete(e, canvas);
                return;
            }

            // Check if clicking a handle
            if (_selectedShape != null || vm.ActiveTool == EditorTool.Crop)
            {
                 var overlay = this.FindControl<Canvas>("OverlayCanvas");
                 if (overlay != null)
                 {
                     var handle = e.Source as Control;
                     if (handle != null && overlay.Children.Contains(handle) && _selectionHandles.Contains(handle))
                     {
                         _isDraggingHandle = true;
                         _draggedHandle = handle;
                         _startPoint = GetCanvasPosition(e, overlay); // Use overlay coords for handles
                         
                         if (vm.ActiveTool == EditorTool.Crop)
                         {
                             var cropOverlay = this.FindControl<Rectangle>("CropOverlay");
                             _selectedShape = cropOverlay;
                         }
                         return;
                     }
                 }
            }

             // Handle Selection / Dragging of existing shapes
             // Allow dragging selected shapes even when not in Select tool mode
            if (_selectedShape != null && vm.ActiveTool != EditorTool.Select)
            {
                 var hitTarget = HitTestMainCanvas(e, canvas);
                 if (hitTarget == _selectedShape)
                 {
                      _lastDragPoint = point;
                      _isDraggingShape = true;
                      e.Pointer.Capture(canvas);
                      return;
                 }
                 else
                 {
                      // Clicked elsewhere, deselect
                      _selectedShape = null;
                      UpdateSelectionHandles();
                 }
            }

            if (vm.ActiveTool == EditorTool.Select)
            {
                var hitTarget = HitTestMainCanvas(e, canvas);
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

            // Start Drawing
            _redoStack.Clear();
            _startPoint = point;
            _isDrawing = true;
            e.Pointer.Capture(canvas);

            var brush = new SolidColorBrush(Color.Parse(vm.SelectedColor));

            // Crop Tool
            if (vm.ActiveTool == EditorTool.Crop)
            {
                var cropOverlay = this.FindControl<Rectangle>("CropOverlay");
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

             // Tool Factory Logic
             switch (vm.ActiveTool)
            {
                case EditorTool.Rectangle:
                    _currentShape = new Rectangle { Stroke = brush, StrokeThickness = vm.StrokeWidth, Fill = Brushes.Transparent };
                    break;
                case EditorTool.Ellipse:
                    _currentShape = new Ellipse { Stroke = brush, StrokeThickness = vm.StrokeWidth, Fill = Brushes.Transparent };
                    break;
                case EditorTool.Line:
                    _currentShape = new Line { Stroke = brush, StrokeThickness = vm.StrokeWidth, StartPoint = _startPoint, EndPoint = _startPoint };
                    break;
                case EditorTool.Arrow:
                    _currentShape = new global::Avalonia.Controls.Shapes.Path { Stroke = brush, StrokeThickness = vm.StrokeWidth, Fill = brush, Data = new PathGeometry() };
                    _shapeEndpoints[_currentShape] = (_startPoint, _startPoint);
                    break;
                case EditorTool.Text:
                    CreateTextAnnotation(canvas, brush, vm);
                    _isDrawing = false; // Text is instant
                    return;
                case EditorTool.Number:
                    CreateNumberAnnotation(canvas, brush, vm);
                     // Keep _isDrawing true for auto-release logic? Original code says yes.
                    break;
                 case EditorTool.Pen:
                 case EditorTool.SmartEraser:
                     CreatePolylineAnnotation(canvas, brush, vm);
                     break;
                 case EditorTool.Blur:
                 case EditorTool.Pixelate:
                 case EditorTool.Magnify:
                 case EditorTool.Highlighter:
                 case EditorTool.SpeechBalloon:
                 case EditorTool.Spotlight:
                     // Simplification: Reuse same logic as initial code for these
                     CreateEffectOrSpotlight(canvas, brush, vm);
                     break;
            }

            if (_currentShape != null)
            {
                 // Number tool adds itself.
                 if (vm.ActiveTool != EditorTool.Number && vm.ActiveTool != EditorTool.Crop)
                 {
                     if (canvas != null && !canvas.Children.Contains(_currentShape))
                     {
                         // Don't set Canvas.Left/Top for Line/Arrow - they use absolute coordinates
                         if (vm.ActiveTool != EditorTool.Arrow && vm.ActiveTool != EditorTool.Line)
                         {
                             Canvas.SetLeft(_currentShape, _startPoint.X);
                             Canvas.SetTop(_currentShape, _startPoint.Y);
                         }
                         canvas.Children.Add(_currentShape);
                     }
                 }
            }
        }
        
        private void OnCanvasPointerMoved(object sender, PointerEventArgs e)
        {
            var mainCanvas = this.FindControl<Canvas>("MainCanvas");
            if (mainCanvas == null) return;
            var currentPoint = GetCanvasPosition(e, mainCanvas);

            if (_isDraggingHandle && _draggedHandle != null && _selectedShape != null)
            {
                HandleResize(currentPoint);
                return;
            }
            
            if (_isDraggingShape && _selectedShape != null)
            {
                HandleMove(currentPoint);
                return;
            }

            if (!_isDrawing || _currentShape == null) return;

            // Update Drawing Shape
            if (_currentShape is Line line)
            {
                line.EndPoint = currentPoint;
            }
            else if (_currentShape is Polyline polyline)
            {
                 var updated = new Points(polyline.Points);
                 updated.Add(currentPoint);
                 polyline.Points = updated;
                 if (polyline.Tag is FreehandAnnotation freehand) freehand.Points.Add(currentPoint);
            }
            else if (_currentShape is global::Avalonia.Controls.Shapes.Path arrowPath && DataContext is MainViewModel vm)
            {
                 arrowPath.Data = CreateArrowGeometry(_startPoint, currentPoint, vm.StrokeWidth * 3);
                 _shapeEndpoints[arrowPath] = (_startPoint, currentPoint);
            }
            else
            {
                 // Rect/Ellipse/Logic
                 var x = Math.Min(_startPoint.X, currentPoint.X);
                 var y = Math.Min(_startPoint.Y, currentPoint.Y);
                 var width = Math.Abs(_startPoint.X - currentPoint.X);
                 var height = Math.Abs(_startPoint.Y - currentPoint.Y);

                 if (_currentShape is Rectangle rect)
                 {
                     rect.Width = width;
                     rect.Height = height;
                     Canvas.SetLeft(rect, x);
                     Canvas.SetTop(rect, y);
                     if (rect.Tag is BaseEffectAnnotation) UpdateEffectVisual(rect);
                 }
                 else if (_currentShape is Ellipse ellipse)
                 {
                     ellipse.Width = width;
                     ellipse.Height = height;
                     Canvas.SetLeft(ellipse, x);
                     Canvas.SetTop(ellipse, y);
                 }
                 else if (_currentShape is SpotlightControl spotlightControl && spotlightControl.Annotation is SpotlightAnnotation spotlight)
                 {
                      spotlight.StartPoint = _startPoint;
                      spotlight.EndPoint = currentPoint;
                       // Ensure canvas size
                      spotlight.CanvasSize = new Size(mainCanvas.Bounds.Width, mainCanvas.Bounds.Height);
                      spotlightControl.InvalidateVisual();
                 }
            }
        }

        private void OnCanvasPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (_isDraggingHandle)
            {
                _isDraggingHandle = false;
                _draggedHandle = null;
                e.Pointer.Capture(null);
                return;
            }

            if (_isDraggingShape)
            {
                _isDraggingShape = false;
                e.Pointer.Capture(null);
                return;
            }

            if (_isDrawing)
            {
                _isDrawing = false;
                if (_currentShape != null)
                {
                     if (DataContext is MainViewModel vm && vm.ActiveTool == EditorTool.Crop && _currentShape.Name == "CropOverlay")
                     {
                         PerformCrop();
                         _currentShape = null;
                         e.Pointer.Capture(null);
                         return;
                     }
                     
                     _undoStack.Push(_currentShape);
                     
                     // Auto select
                     if (_currentShape is not Polyline)
                     {
                         if (_currentShape.Tag is BaseEffectAnnotation) UpdateEffectVisual(_currentShape);
                         _selectedShape = _currentShape;
                         UpdateSelectionHandles();
                     }

                     _currentShape = null;
                }
                e.Pointer.Capture(null);
            }
        }

        // --- Helpers ---

        private Control? HitTestMainCanvas(PointerEventArgs e, Canvas canvas)
        {
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
             return hitTarget;
        }

        /// <summary>
        /// Handle right-click deletion by selecting the shape and calling PerformDelete
        /// This ensures proper undo/redo integration
        /// </summary>
        private void HandleRightClickDelete(PointerPressedEventArgs e, Canvas canvas)
        {
            var hitTarget = HitTestMainCanvas(e, canvas);
            if (hitTarget != null && hitTarget.Name != "CropOverlay")
            {
                // Select the shape first
                _selectedShape = hitTarget;
                UpdateSelectionHandles();
                
                // Use the standard delete method for consistency with undo/redo
                PerformDelete();
                
                e.Handled = true;
            }
        }

        private void CreateTextAnnotation(Canvas canvas, IBrush brush, MainViewModel vm)
        {
             var textBox = new TextBox
                    {
                        Foreground = brush,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(1), 
                        BorderBrush = Brushes.White,
                        FontSize = Math.Max(12, vm.StrokeWidth * 4),
                        Text = string.Empty,
                        Padding = new Thickness(4),
                        MinWidth = 50,
                        AcceptsReturn = false
                    };
            Canvas.SetLeft(textBox, _startPoint.X);
            Canvas.SetTop(textBox, _startPoint.Y);

            textBox.LostFocus += (s, args) =>
            {
                if (s is TextBox tb)
                {
                    tb.BorderThickness = new Thickness(0);
                    if (string.IsNullOrWhiteSpace(tb.Text))
                        (tb.Parent as Panel)?.Children.Remove(tb);
                }
            };
            
            textBox.KeyDown += (s, args) =>
            {
                 if (args.Key == Key.Enter)
                 {
                     args.Handled = true;
                     this.Focus();
                 }
            };

            canvas.Children.Add(textBox);
            textBox.Focus();
        }

         private void CreateNumberAnnotation(Canvas canvas, IBrush brush, MainViewModel vm)
         {
              var numberGrid = new Grid { Width = 30, Height = 30 };
              var bg = new Ellipse { Fill = brush, Stroke = Brushes.White, StrokeThickness = 2 };
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
              
              Canvas.SetLeft(numberGrid, _startPoint.X - 15);
              Canvas.SetTop(numberGrid, _startPoint.Y - 15);
              
              _currentShape = numberGrid;
              vm.NumberCounter++;
              canvas.Children.Add(numberGrid);
         }

         private async void CreatePolylineAnnotation(Canvas canvas, IBrush brush, MainViewModel vm)
         {
             var polyline = new Polyline
             {
                 Stroke = (vm.ActiveTool == EditorTool.SmartEraser) ? new SolidColorBrush(Color.Parse("#80FF0000")) : brush,
                 StrokeThickness = (vm.ActiveTool == EditorTool.SmartEraser) ? 10 : vm.StrokeWidth,
                 Points = new Points { _startPoint }
             };
             
             FreehandAnnotation freehand;
             if (vm.ActiveTool == EditorTool.SmartEraser)
             {
                 var sampledColor = await GetPixelColorFromRenderedCanvas(_startPoint);
                 freehand = new SmartEraserAnnotation();
                 
                 if (!string.IsNullOrEmpty(sampledColor))
                 {
                     freehand.StrokeColor = sampledColor;
                     polyline.Stroke = new SolidColorBrush(Color.Parse(sampledColor));
                 }
             }
             else
             {
                 freehand = new FreehandAnnotation();
             }
             
             freehand.Points.Add(_startPoint);
             polyline.Tag = freehand;
             _currentShape = polyline;
         }

         private void CreateEffectOrSpotlight(Canvas canvas, IBrush brush, MainViewModel vm)
         {
             if (vm.ActiveTool == EditorTool.Spotlight)
             {
                  var spot = new SpotlightAnnotation();
                  var ctrl = new SpotlightControl { Annotation = spot, IsHitTestVisible = true };
                  _currentShape = ctrl;
             }
             else
             {
                  var effectRect = new Rectangle 
                  { 
                      Stroke = (vm.ActiveTool == EditorTool.Magnify) ? brush : Brushes.Transparent, 
                      StrokeThickness = vm.StrokeWidth,
                      Fill = (vm.ActiveTool == EditorTool.Highlighter) ? new SolidColorBrush(ApplyHighlightAlpha(Color.Parse(vm.SelectedColor))) : Brushes.Transparent, 
                      Tag = CreateEffectAnnotation(vm.ActiveTool) 
                  };
                  if (vm.ActiveTool == EditorTool.Blur) effectRect.Fill = new SolidColorBrush(Color.Parse("#200000FF"));
                  else if (vm.ActiveTool == EditorTool.Pixelate) effectRect.Fill = new SolidColorBrush(Color.Parse("#2000FF00"));
                  _currentShape = effectRect;
             }
         }

          private BaseEffectAnnotation? CreateEffectAnnotation(EditorTool tool)
          {
               return tool switch
               {
                   EditorTool.Blur => new BlurAnnotation(),
                   EditorTool.Pixelate => new PixelateAnnotation(),
                   EditorTool.Magnify => new MagnifyAnnotation(),
                   EditorTool.Highlighter => new HighlightAnnotation(),
                   _ => null
               };
          }

          private static Color ApplyHighlightAlpha(Color baseColor) => Color.FromArgb(0x55, baseColor.R, baseColor.G, baseColor.B);

          private void UpdateSelectionHandles()
          {
                var overlay = this.FindControl<Canvas>("OverlayCanvas");
                if (overlay == null) return;
                
                for (int i = overlay.Children.Count - 1; i >= 0; i--)
                {
                    if (overlay.Children[i] is Border b && _selectionHandles.Contains(b))
                    {
                        overlay.Children.RemoveAt(i);
                    }
                }
                _selectionHandles.Clear();
                
                if (_selectedShape == null) return;
                
                if (_selectedShape is Line line) 
                { 
                    CreateHandle(line.StartPoint.X, line.StartPoint.Y, "LineStart"); 
                    CreateHandle(line.EndPoint.X, line.EndPoint.Y, "LineEnd"); 
                    return; 
                }
                
                if (_selectedShape is global::Avalonia.Controls.Shapes.Path arrowPath)
                {
                    if (_shapeEndpoints.TryGetValue(arrowPath, out var endpoints))
                    {
                        CreateHandle(endpoints.Start.X, endpoints.Start.Y, "ArrowStart");
                        CreateHandle(endpoints.End.X, endpoints.End.Y, "ArrowEnd");
                    }
                    return;
                }
                
                if (_selectedShape is Grid) return; // Number tool (Grid) no resize handles?
                
                 var left = Canvas.GetLeft(_selectedShape);
                 var top = Canvas.GetTop(_selectedShape);
                 var w = _selectedShape.Bounds.Width; if (double.IsNaN(w)) w = _selectedShape.Width;
                 var h = _selectedShape.Bounds.Height; if (double.IsNaN(h)) h = _selectedShape.Height;
                 
                 // 8 handles
                 CreateHandle(left, top, "TopLeft");
                 CreateHandle(left + w/2, top, "TopCenter");
                 CreateHandle(left + w, top, "TopRight");
                 CreateHandle(left + w, top + h/2, "RightCenter");
                 CreateHandle(left + w, top + h, "BottomRight");
                 CreateHandle(left + w/2, top + h, "BottomCenter");
                 CreateHandle(left, top + h, "BottomLeft");
                 CreateHandle(left, top + h/2, "LeftCenter");
          }

          private void CreateHandle(double x, double y, string tag)
          {
               var overlay = this.FindControl<Canvas>("OverlayCanvas");
               Cursor cursor = Cursor.Parse("Hand");
               if (tag.Contains("TopLeft") || tag.Contains("BottomRight")) cursor = new Cursor(StandardCursorType.TopLeftCorner);
               else if (tag.Contains("TopRight") || tag.Contains("BottomLeft")) cursor = new Cursor(StandardCursorType.TopRightCorner);
               else if (tag.Contains("Top") || tag.Contains("Bottom")) cursor = new Cursor(StandardCursorType.SizeNorthSouth);
               else if (tag.Contains("Left") || tag.Contains("Right")) cursor = new Cursor(StandardCursorType.SizeWestEast);
               
               var handle = new Border 
               { 
                   Width = 15, 
                   Height = 15, 
                   Background = Brushes.White, 
                   CornerRadius = new CornerRadius(10), 
                   Tag = tag,
                   Cursor = cursor,
                   BoxShadow = new BoxShadows(new BoxShadow { Blur = 8, Color = Color.FromArgb(100, 0, 0, 0)})
               };
               
               Canvas.SetLeft(handle, x - 7.5);
               Canvas.SetTop(handle, y - 7.5);
               overlay.Children.Add(handle);
               _selectionHandles.Add(handle);
          }

          private void HandleResize(Point currentPoint)
          {
                var handleTag = _draggedHandle?.Tag?.ToString();
                if (handleTag == null || _selectedShape == null) return;

                var deltaX = currentPoint.X - _startPoint.X;
                var deltaY = currentPoint.Y - _startPoint.Y;

                if (_selectedShape is Line targetLine)
                {
                    if (handleTag == "LineStart") targetLine.StartPoint = currentPoint;
                    else if (handleTag == "LineEnd") targetLine.EndPoint = currentPoint;
                    _startPoint = currentPoint;
                    UpdateSelectionHandles();
                    return;
                }

                if (_selectedShape is global::Avalonia.Controls.Shapes.Path arrowPath && DataContext is MainViewModel vm)
                {
                    if (_shapeEndpoints.TryGetValue(arrowPath, out var endpoints))
                    {
                        var start = endpoints.Start;
                        var end = endpoints.End;
                        if (handleTag == "ArrowStart") start = currentPoint;
                        else if (handleTag == "ArrowEnd") end = currentPoint;
                        
                        _shapeEndpoints[arrowPath] = (start, end);
                        arrowPath.Data = CreateArrowGeometry(start, end, vm.StrokeWidth * 3);
                    }
                    _startPoint = currentPoint;
                    UpdateSelectionHandles();
                    return;
                }

                var left = Canvas.GetLeft(_selectedShape);
                var top = Canvas.GetTop(_selectedShape);
                var width = _selectedShape.Bounds.Width; if (double.IsNaN(width)) width = _selectedShape.Width;
                var height = _selectedShape.Bounds.Height; if (double.IsNaN(height)) height = _selectedShape.Height;

                if (_selectedShape is Rectangle || _selectedShape is Ellipse || _selectedShape is Grid || _selectedShape is SpotlightControl)
                {
                    double newLeft = left;
                    double newTop = top;
                    double newWidth = width;
                    double newHeight = height;

                    if (handleTag.Contains("Right")) newWidth = Math.Max(1, width + deltaX);
                    else if (handleTag.Contains("Left")) { var change = Math.Min(width - 1, deltaX); newLeft += change; newWidth -= change; }

                    if (handleTag.Contains("Bottom")) newHeight = Math.Max(1, height + deltaY);
                    else if (handleTag.Contains("Top")) { var change = Math.Min(height - 1, deltaY); newTop += change; newHeight -= change; }

                    Canvas.SetLeft(_selectedShape, newLeft);
                    Canvas.SetTop(_selectedShape, newTop);
                    _selectedShape.Width = newWidth;
                    _selectedShape.Height = newHeight;
                    
                    if (_selectedShape.Tag is BaseEffectAnnotation) UpdateEffectVisual(_selectedShape);
                    if (_selectedShape is SpotlightControl sc) sc.InvalidateVisual();
                }

                _startPoint = currentPoint; 
                UpdateSelectionHandles();
          }

          private void HandleMove(Point currentPoint)
          {
                var deltaX = currentPoint.X - _lastDragPoint.X;
                var deltaY = currentPoint.Y - _lastDragPoint.Y;

                if (_selectedShape is Line targetLine)
                {
                    targetLine.StartPoint = new Point(targetLine.StartPoint.X + deltaX, targetLine.StartPoint.Y + deltaY);
                    targetLine.EndPoint = new Point(targetLine.EndPoint.X + deltaX, targetLine.EndPoint.Y + deltaY);
                    _lastDragPoint = currentPoint;
                    UpdateSelectionHandles();
                    return;
                }

                if (_selectedShape is global::Avalonia.Controls.Shapes.Path arrowPath && DataContext is MainViewModel vm)
                {
                     if (_shapeEndpoints.TryGetValue(arrowPath, out var endpoints))
                     {
                         var newStart = new Point(endpoints.Start.X + deltaX, endpoints.Start.Y + deltaY);
                         var newEnd = new Point(endpoints.End.X + deltaX, endpoints.End.Y + deltaY);
                         _shapeEndpoints[arrowPath] = (newStart, newEnd);
                         arrowPath.Data = CreateArrowGeometry(newStart, newEnd, vm.StrokeWidth * 3);
                     }
                     _lastDragPoint = currentPoint;
                     UpdateSelectionHandles();
                     return;
                }

                var left = Canvas.GetLeft(_selectedShape);
                var top = Canvas.GetTop(_selectedShape);
                Canvas.SetLeft(_selectedShape, left + deltaX);
                Canvas.SetTop(_selectedShape, top + deltaY);
                _lastDragPoint = currentPoint;
                
                if (_selectedShape.Tag is BaseEffectAnnotation) UpdateEffectVisual(_selectedShape);
                if (_selectedShape is SpotlightControl sc) sc.InvalidateVisual();
                
                UpdateSelectionHandles();
          }
          /// <summary>
          /// Undo the last annotation action by removing it from the canvas and pushing to redo stack
          /// </summary>
          private void PerformUndo()
          {
              if (_undoStack.Count == 0) return;

              var item = _undoStack.Pop();
              
              // Remove from canvas
              var mainCanvas = this.FindControl<Canvas>("MainCanvas");
              if (mainCanvas != null && mainCanvas.Children.Contains(item))
              {
                  mainCanvas.Children.Remove(item);
              }
              
              // Add to redo stack
              _redoStack.Push(item);
              
              // Deselect if this was the selected shape
              if (_selectedShape == item)
              {
                  _selectedShape = null;
                  UpdateSelectionHandles();
              }
              
              // Update status
              if (DataContext is MainViewModel vm)
              {
                  vm.StatusText = $"Undo: {_undoStack.Count} actions remaining";
              }
          }

          /// <summary>
          /// Redo the last undone action by adding it back to the canvas and pushing to undo stack
          /// </summary>
          private void PerformRedo()
          {
              if (_redoStack.Count == 0) return;

              var item = _redoStack.Pop();
              
              // Add back to canvas
              var mainCanvas = this.FindControl<Canvas>("MainCanvas");
              if (mainCanvas != null && !mainCanvas.Children.Contains(item))
              {
                  mainCanvas.Children.Add(item);
              }
              
              // Add back to undo stack
              _undoStack.Push(item);
              
              // Auto-select the redone shape
              _selectedShape = item;
              UpdateSelectionHandles();
              
              // Update status
              if (DataContext is MainViewModel vm)
              {
                  vm.StatusText = $"Redo: {_redoStack.Count} actions remaining";
              }
          }

          /// <summary>
          /// Delete the currently selected shape and add it to undo stack so it can be undone
          /// </summary>
          private void PerformDelete()
          {
              if (_selectedShape == null) return;

              var mainCanvas = this.FindControl<Canvas>("MainCanvas");
              if (mainCanvas == null) return;

              // Remove from canvas
              if (mainCanvas.Children.Contains(_selectedShape))
              {
                  mainCanvas.Children.Remove(_selectedShape);
                  
                  // IMPORTANT: Don't add to undo stack since delete should be undoable via Undo
                  // The shape is already in the undo stack from when it was created
                  // Just clear selection
                  _selectedShape = null;
                  UpdateSelectionHandles();
                  
                  // Update status
                  if (DataContext is MainViewModel vm)
                  {
                      vm.StatusText = "Shape deleted (Ctrl+Z to undo)";
                  }
              }
          }

          /// <summary>
          /// Clear all annotations from the canvas and reset undo/redo stacks
          /// </summary>
          private void ClearAllAnnotations()
          {
              var mainCanvas = this.FindControl<Canvas>("MainCanvas");
              if (mainCanvas == null) return;

              // Clear all children from canvas
              mainCanvas.Children.Clear();
              
              // Reset undo/redo stacks
              _undoStack.Clear();
              _redoStack.Clear();
              
              // Clear selection
              _selectedShape = null;
              UpdateSelectionHandles();
              
              // Update status
              if (DataContext is MainViewModel vm)
              {
                  vm.StatusText = "All annotations cleared";
              }
          }

          
          public void PerformCrop()
          {
                var cropOverlay = this.FindControl<Rectangle>("CropOverlay");
                if (cropOverlay == null || !cropOverlay.IsVisible || DataContext is not MainViewModel vm) return;

                var x = Canvas.GetLeft(cropOverlay);
                var y = Canvas.GetTop(cropOverlay);
                var w = cropOverlay.Width;
                var h = cropOverlay.Height;

                // We need to convert to physical pixels if scaling is involved, but vm usually expects logical if not handled.
                // Assuming logical -> physical conversion happens in VM or here if we had access to VisualRoot.
                // For now pass logic values, VM should handle or we assume 1.0 scale for MVP restoration.
                // Actually EditorView had scaling logic.
                
                var topLevel = TopLevel.GetTopLevel(this);
                var scaling = topLevel != null ? topLevel.RenderScaling : 1.0;
                
                vm.CropImage((int)(x * scaling), (int)(y * scaling), (int)(w * scaling), (int)(h * scaling));
                cropOverlay.IsVisible = false;
                vm.StatusText = "Image cropped";
          }

          public void UpdateEffectVisual(Control shape)
          {
                if (shape.Tag is not BaseEffectAnnotation annotation) return;
                if (_isDrawing) return; // Optimization
                if (DataContext is not MainViewModel vm || vm.PreviewImage == null) return;
                
                try
                {
                    double left = Canvas.GetLeft(shape);
                    double top = Canvas.GetTop(shape);
                    double width = shape.Bounds.Width; if (double.IsNaN(width)) width = shape.Width;
                    double height = shape.Bounds.Height; if (double.IsNaN(height)) height = shape.Height;
                    
                    if (width <= 0 || height <= 0) return;
                    
                    annotation.StartPoint = new Point(left, top);
                    annotation.EndPoint = new Point(left + width, top + height);
                    
                    if (_cachedSkBitmap == null) _cachedSkBitmap = Helpers.BitmapConversionHelpers.ToSKBitmap(vm.PreviewImage);
                    
                    annotation.UpdateEffect(_cachedSkBitmap);
                    
                    if (annotation.EffectBitmap != null && shape is Shape shapeControl)
                    {
                        // Use RelativeRect for SourceRect
                        shapeControl.Fill = new ImageBrush(annotation.EffectBitmap)
                        {
                            Stretch = Stretch.None,
                            SourceRect = new RelativeRect(0, 0, width, height, RelativeUnit.Absolute)
                        };
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
          }

          private Geometry CreateArrowGeometry(Point start, Point end, double headSize)
          {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    var d = end - start;
                    var length = Math.Sqrt(d.X * d.X + d.Y * d.Y);
                    if (length > 0)
                    {
                        var ux = d.X / length;
                        var uy = d.Y / length;
                        var arrowAngle = Math.PI / 9; // 20 degrees
                        var arrowBase = new Point(end.X - headSize * ux, end.Y - headSize * uy);
                        var p1 = new Point(end.X - headSize * Math.Cos(Math.Atan2(uy, ux) - arrowAngle), end.Y - headSize * Math.Sin(Math.Atan2(uy, ux) - arrowAngle));
                        var p2 = new Point(end.X - headSize * Math.Cos(Math.Atan2(uy, ux) + arrowAngle), end.Y - headSize * Math.Sin(Math.Atan2(uy, ux) + arrowAngle));
                        
                        ctx.BeginFigure(start, false);
                        ctx.LineTo(arrowBase);
                        ctx.EndFigure(false);
                        
                        ctx.BeginFigure(end, true);
                        ctx.LineTo(p1);
                        ctx.LineTo(p2);
                        ctx.EndFigure(true);
                    }
                    else
                    {
                        ctx.BeginFigure(start, false);
                        ctx.LineTo(end);
                        ctx.EndFigure(false);
                    }
                }
                return geometry;
          }
          
          private void ApplySelectedColor(string color) 
          {
                if (_selectedShape is Shape s) s.Stroke = new SolidColorBrush(Color.Parse(color));
          }
          
          private void ApplySelectedStrokeWidth(int width)
          {
                if (_selectedShape is Shape s) s.StrokeThickness = width;
          }

        /// <summary>
        /// Sample pixel color from the rendered canvas (including annotations) at the specified canvas coordinates
        /// </summary>
        private async System.Threading.Tasks.Task<string?> GetPixelColorFromRenderedCanvas(Point canvasPoint)
        {
            if (DataContext is not MainViewModel vm || vm.PreviewImage == null) return null;

            try
            {
                // We need to sample from the RENDERED canvas including all annotations
                // We can find the parent CanvasContainer (Grid) by traversing up
                var container = this.GetVisualParent<Grid>();
                if (container == null || container.Name != "CanvasContainer")
                {
                     // Fallback mechanism if direct parent isn't it (flexible for future refactors)
                     var p = this.GetVisualParent();
                     while (p != null)
                     {
                         if (p is Grid g && g.Name == "CanvasContainer") { container = g; break; }
                         p = p.GetVisualParent();
                     }
                }
                
                if (container == null || container.Bounds.Width <= 0 || container.Bounds.Height <= 0) return null;

                // Render the container (image + annotations) to a bitmap
                var rtb = new global::Avalonia.Media.Imaging.RenderTargetBitmap(
                    new PixelSize((int)container.Bounds.Width, (int)container.Bounds.Height), 
                    new Vector(96, 96));
                
                rtb.Render(container);

                // Convert to SKBitmap for pixel access
                using var skBitmap = BitmapConversionHelpers.ToSKBitmap(rtb);

                // Convert canvas point to pixel coordinates
                int x = (int)Math.Round(canvasPoint.X);
                int y = (int)Math.Round(canvasPoint.Y);

                if (x < 0 || y < 0 || x >= skBitmap.Width || y >= skBitmap.Height) return null;

                var skColor = skBitmap.GetPixel(x, y);
                return $"#{skColor.Red:X2}{skColor.Green:X2}{skColor.Blue:X2}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetPixelColorFromRenderedCanvas failed: {ex.Message}");
                return null;
            }
        }
    }
}
