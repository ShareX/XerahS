#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Threading;
using ShareX.Avalonia.UI.ViewModels;
using ShareX.Avalonia.Annotations.Models;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ShareX.Avalonia.UI.Views
{
    public partial class EditorView : UserControl
    {
        private Point _startPoint;
        private Control? _currentShape;
        private bool _isDrawing;

        private const double MinZoom = 0.25;
        private const double MaxZoom = 4.0;
        private const double ZoomStep = 0.1;

        private bool _isPanning;
        private Point _panStart;
        private Vector _panOrigin;

        // Selection state
        private Control? _selectedShape;
        private Point _lastDragPoint;
        private bool _isDraggingShape;
        
        // Handles
        private List<Shape> _selectionHandles = new();
        private bool _isDraggingHandle;
        private Shape? _draggedHandle;

        private Point GetCanvasPosition(PointerEventArgs e, Canvas canvas)
        {
            return e.GetPosition(canvas);
        }

        private void OnPreviewPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;

            var oldZoom = vm.Zoom;
            var direction = e.Delta.Y > 0 ? 1 : -1;
            var newZoom = Math.Clamp(Math.Round((oldZoom + direction * ZoomStep) * 100) / 100, MinZoom, MaxZoom);
            if (Math.Abs(newZoom - oldZoom) < 0.0001) return;

            var scrollViewer = this.FindControl<ScrollViewer>("CanvasScrollViewer");
            if (scrollViewer != null)
            {
                var pointerPosition = e.GetPosition(scrollViewer);
                var offsetBefore = scrollViewer.Offset;
                var logicalPoint = new Vector(
                    (offsetBefore.X + pointerPosition.X) / oldZoom,
                    (offsetBefore.Y + pointerPosition.Y) / oldZoom);

                vm.Zoom = newZoom;

                Dispatcher.UIThread.Post(() =>
                {
                    var targetOffset = new Vector(
                        logicalPoint.X * newZoom - pointerPosition.X,
                        logicalPoint.Y * newZoom - pointerPosition.Y);

                    // Clamp to available extent to avoid jumpy scrolling near edges
                    var maxX = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
                    var maxY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);

                    scrollViewer.Offset = new Vector(
                        Math.Clamp(targetOffset.X, 0, maxX),
                        Math.Clamp(targetOffset.Y, 0, maxY));
                }, DispatcherPriority.Render);
            }
            else
            {
                vm.Zoom = newZoom;
            }

            e.Handled = true;
        }

        private void OnScrollViewerPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer) return;

            var properties = e.GetCurrentPoint(scrollViewer).Properties;
            if (!properties.IsMiddleButtonPressed) return;

            _isPanning = true;
            _panStart = e.GetPosition(scrollViewer);
            _panOrigin = scrollViewer.Offset;
            scrollViewer.Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(scrollViewer);
            e.Handled = true;
        }

        private void OnScrollViewerPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isPanning || sender is not ScrollViewer scrollViewer) return;

            var current = e.GetPosition(scrollViewer);
            var delta = current - _panStart;

            var target = new Vector(
                _panOrigin.X - delta.X,
                _panOrigin.Y - delta.Y);

            var maxX = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
            var maxY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);

            scrollViewer.Offset = new Vector(
                Math.Clamp(target.X, 0, maxX),
                Math.Clamp(target.Y, 0, maxY));

            e.Handled = true;
        }

        private void OnScrollViewerPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer) return;

            if (_isPanning)
            {
                _isPanning = false;
                scrollViewer.Cursor = null;
                e.Pointer.Capture(null);
                e.Handled = true;
            }
        }
        
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                if (e.Key == Key.Delete)
                {
                    vm.DeleteSelectedCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                     if (e.Key == Key.Z)
                     {
                         vm.UndoCommand.Execute(null);
                         e.Handled = true;
                         return;
                     }
                     else if (e.Key == Key.Y) // Standard Redo
                     {
                         vm.RedoCommand.Execute(null);
                         e.Handled = true;
                         return;
                     }
                }
                // Ctrl+Shift+Z is also common for Redo, check modifiers
                else if (e.KeyModifiers.HasFlag(KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.Z)
                {
                    vm.RedoCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

                // Tool shortcuts (no modifiers)
                if (e.KeyModifiers == KeyModifiers.None)
                {
                    switch (e.Key)
                    {
                        case Key.V:
                            vm.SelectToolCommand.Execute(EditorTool.Select);
                            e.Handled = true;
                            break;
                        case Key.R:
                            vm.SelectToolCommand.Execute(EditorTool.Rectangle);
                            e.Handled = true;
                            break;
                        case Key.E:
                            vm.SelectToolCommand.Execute(EditorTool.Ellipse);
                            e.Handled = true;
                            break;
                        case Key.A:
                            vm.SelectToolCommand.Execute(EditorTool.Arrow);
                            e.Handled = true;
                            break;
                        case Key.L:
                            vm.SelectToolCommand.Execute(EditorTool.Line);
                            e.Handled = true;
                            break;
                        case Key.P:
                            vm.SelectToolCommand.Execute(EditorTool.Pen);
                            e.Handled = true;
                            break;
                        case Key.H:
                            vm.SelectToolCommand.Execute(EditorTool.Highlighter);
                            e.Handled = true;
                            break;
                        case Key.T:
                            vm.SelectToolCommand.Execute(EditorTool.Text);
                            e.Handled = true;
                            break;
                        case Key.B:
                            vm.SelectToolCommand.Execute(EditorTool.SpeechBalloon);
                            e.Handled = true;
                            break;
                        case Key.N:
                            vm.SelectToolCommand.Execute(EditorTool.Number);
                            e.Handled = true;
                            break;
                        case Key.C:
                            vm.SelectToolCommand.Execute(EditorTool.Crop);
                            e.Handled = true;
                            break;
                        case Key.M:
                            vm.SelectToolCommand.Execute(EditorTool.Magnify);
                            e.Handled = true;
                            break;
                        case Key.S:
                            vm.SelectToolCommand.Execute(EditorTool.Spotlight);
                            e.Handled = true;
                            break;
                        case Key.F:
                            vm.ToggleEffectsPanelCommand.Execute(null);
                            e.Handled = true;
                            break;
                    }
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

            // Skip handles for shapes without measurable bounds (e.g., lines)
            if (_selectedShape.Bounds.Width <= 0 || _selectedShape.Bounds.Height <= 0) return;

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

            var handle = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush(Color.Parse("#FFFFFFFF")),
                Stroke = new SolidColorBrush(Color.Parse("#8B5CF6")),
                StrokeThickness = 2,
                Tag = tag, // Store position intent
                Cursor = cursor
            };
            
            // Center the handle
            Canvas.SetLeft(handle, x - handle.Width / 2);
            Canvas.SetTop(handle, y - handle.Height / 2);
            
            overlay.Children.Add(handle);
            _selectionHandles.Add(handle);
        }

        private Stack<Control> _undoStack = new();
        private Stack<Control> _redoStack = new();

        public EditorView()
        {
            InitializeComponent();

            // Capture wheel events in tunneling phase so ScrollViewer doesn't scroll when using Ctrl+wheel zoom.
            AddHandler(PointerWheelChangedEvent, OnPreviewPointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
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
                vm.SaveAsRequested += ShowSaveAsDialog;
                vm.CopyRequested += CopyToClipboard;
                vm.ShowErrorDialog += ShowErrorDialog;
                vm.PropertyChanged -= OnViewModelPropertyChanged;
                vm.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.PropertyChanged -= OnViewModelPropertyChanged;
            }

            base.OnDetachedFromVisualTree(e);
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
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

        public async System.Threading.Tasks.Task<string?> ShowSaveAsDialog()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider == null) return null;
            
            try
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save Image",
                    DefaultExtension = "png",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } },
                        new FilePickerFileType("JPEG Image") { Patterns = new[] { "*.jpg", "*.jpeg" } },
                        new FilePickerFileType("Bitmap Image") { Patterns = new[] { "*.bmp" } }
                    },
                    SuggestedFileName = $"ShareX_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"
                });
                
                return file?.Path.LocalPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SaveAs dialog failed: " + ex.Message);
                return null;
            }
        }

        public async System.Threading.Tasks.Task CopyToClipboard(global::Avalonia.Media.Imaging.Bitmap image)
        {
            try
            {
                // Convert Avalonia Bitmap to System.Drawing.Image for native clipboard
                using var memoryStream = new System.IO.MemoryStream();
                image.Save(memoryStream);
                memoryStream.Position = 0;
                
                // Load into System.Drawing.Image (what Windows clipboard expects)
                using var drawingImage = System.Drawing.Image.FromStream(memoryStream);
                
                // Use platform-specific clipboard service for native OS compatibility
                ShareX.Avalonia.Platform.Abstractions.PlatformServices.Clipboard.SetImage(drawingImage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clipboard copy failed: {ex.Message}");
                throw;
            }
            
            await System.Threading.Tasks.Task.CompletedTask;
        }

        public async System.Threading.Tasks.Task ShowErrorDialog(string title, string message)
        {
            var messageBox = new Window
            {
                Title = title,
                Width = 500,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var panel = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 15
            };

            var messageText = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth =460
            };

            var buttonPanel = new StackPanel
            {
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var okButton = new Button
            {
                Content = "OK",
                Padding = new Thickness(30, 8)
            };

            okButton.Click += (s, e) => messageBox.Close();

            buttonPanel.Children.Add(okButton);
            panel.Children.Add(messageText);
            panel.Children.Add(buttonPanel);
            messageBox.Content = panel;

            await messageBox.ShowDialog(TopLevel.GetTopLevel(this) as Window);
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

        private void ApplySelectedColor(string colorHex)
        {
            if (_selectedShape == null) return;

            var brush = new SolidColorBrush(Color.Parse(colorHex));

            switch (_selectedShape)
            {
                case Shape shape:
                    if (shape.Tag is HighlightAnnotation)
                    {
                        var highlightColor = Color.Parse(colorHex);
                        shape.Stroke = Brushes.Transparent;
                        shape.Fill = new SolidColorBrush(ApplyHighlightAlpha(highlightColor));
                        break;
                    }

                    shape.Stroke = brush;

                    if (shape is global::Avalonia.Controls.Shapes.Path path)
                    {
                        path.Fill = brush;
                    }
                    break;
                case TextBox textBox:
                    textBox.Foreground = brush;
                    break;
                case Grid grid:
                    foreach (var child in grid.Children)
                    {
                        if (child is Ellipse ellipse)
                        {
                            ellipse.Fill = brush;
                        }
                    }
                    break;
            }
        }

        private void ApplySelectedStrokeWidth(int width)
        {
            if (_selectedShape == null) return;

            switch (_selectedShape)
            {
                case Shape shape:
                    shape.StrokeThickness = width;
                    break;
                case TextBox textBox:
                    textBox.FontSize = Math.Max(12, width * 4);
                    textBox.BorderThickness = new Thickness(Math.Max(1, width / 2));
                    break;
                case Grid grid:
                    foreach (var child in grid.Children)
                    {
                        if (child is Ellipse ellipse)
                        {
                            ellipse.StrokeThickness = Math.Max(1, width);
                        }
                    }
                    break;
            }
        }

        private static Color ApplyHighlightAlpha(Color baseColor)
        {
            return Color.FromArgb(0x55, baseColor.R, baseColor.G, baseColor.B);
        }

        private async void OnCanvasPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;
            var canvas = sender as Canvas;
            if (canvas == null) return;

            // Ignore middle mouse to avoid creating annotations while panning
            var props = e.GetCurrentPoint(canvas).Properties;
            if (props.IsMiddleButtonPressed) return;

            var point = GetCanvasPosition(e, canvas);

            // Check if clicking a handle (always allow when a shape is selected, regardless of active tool)
            if (_selectedShape != null || vm.ActiveTool == EditorTool.Crop)
            {
                 var overlay = this.FindControl<Canvas>("OverlayCanvas");
                 if (overlay != null)
                 {
                     var handle = e.Source as Shape;
                     if (handle != null && overlay.Children.Contains(handle))
                     {
                         _isDraggingHandle = true;
                         _draggedHandle = handle;
                             _startPoint = GetCanvasPosition(e, overlay); // Use overlay coords for handles
                         
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
            
            if (vm.ActiveTool == EditorTool.Image)
            {
                 _isDrawing = false;
                 // File Picker logic
                 var topLevel = TopLevel.GetTopLevel(this);
                 if (topLevel?.StorageProvider != null)
                 {
                     var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                     {
                         Title = "Select Image",
                         AllowMultiple = false,
                         FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
                     });

                     if (files.Count > 0)
                     {
                         try {
                             using var stream = await files[0].OpenReadAsync();
                             var bitmap = new global::Avalonia.Media.Imaging.Bitmap(stream);
                             
                             var imageControl = new Image
                             {
                                 Source = bitmap,
                                 Width = bitmap.Size.Width,
                                 Height = bitmap.Size.Height
                             };
                             
                             var annotation = new ImageAnnotation();
                             annotation.SetImage(bitmap);
                             imageControl.Tag = annotation;
                             
                             // Center on click point
                             Canvas.SetLeft(imageControl, _startPoint.X - bitmap.Size.Width / 2);
                             Canvas.SetTop(imageControl, _startPoint.Y - bitmap.Size.Height / 2);
                             
                             canvas.Children.Add(imageControl);
                             _undoStack.Push(imageControl);
                             
                             _currentShape = imageControl;
                             _selectedShape = imageControl;
                             UpdateSelectionHandles();
                         }
                         catch (Exception ex)
                         {
                             System.Diagnostics.Debug.WriteLine(ex.Message);
                         }
                     }
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
                        Stroke = new SolidColorBrush(Color.Parse("#B0000000")), 
                        StrokeThickness = 4000, 
                        Fill = Brushes.Transparent, 
                        IsHitTestVisible = true 
                    };
                    break;

                // --- NEW TOOLS ---

                case EditorTool.Blur:
                case EditorTool.Pixelate:
                case EditorTool.Magnify:
                case EditorTool.Highlighter:
                    // For these region-based effects, we primarily draw a rectangle as a visual container
                    // The "Actual" rendering would ideally be done by a custom control or custom drawing visual.
                    // For MVP, since we implemented BaseEffectAnnotation as logical models, 
                    // we can't directly add them to a Canvas unless they are Avalonia Controls or we wrap them.
                    
                    // QUICK FIX STRATEGY: 
                    // Use a standardized Avalonia Border/Rectangle for the UI representation 
                    // and attaching the logic via attached properties or Tag or ViewModel synchronization.
                    
                    // Better approach for Avalonia: 
                    // Create an 'AnnotationControl' wrapper that takes the 'Annotation' model and renders it.
                    // But we don't have that yet.
                    
                     // Fallback to simple shapes representing the logical annotation:
                    
                    var effectRect = new global::Avalonia.Controls.Shapes.Rectangle
                    {
                        Stroke = (vm.ActiveTool == EditorTool.Magnify) ? brush : Brushes.Transparent,
                        StrokeThickness = vm.StrokeWidth,
                        Fill = (vm.ActiveTool == EditorTool.Highlighter) ? new SolidColorBrush(ApplyHighlightAlpha(Color.Parse(vm.SelectedColor))) : Brushes.Transparent,
                        Tag = CreateEffectAnnotation(vm.ActiveTool) // Create and attach logic model
                    };
                    
                    // For Blur/Pixelate, we might want a translucent overlay to show where it is
                    if (vm.ActiveTool == EditorTool.Blur)
                         effectRect.Fill = new SolidColorBrush(Color.Parse("#200000FF")); // Faint blue
                    else if (vm.ActiveTool == EditorTool.Pixelate)
                         effectRect.Fill = new SolidColorBrush(Color.Parse("#2000FF00")); // Faint green
                    
                    _currentShape = effectRect;
                    break;
                    
                 case EditorTool.SpeechBalloon:
                    // Placeholder: Draw a generic path or just a rect for now
                    // A real speech balloon needs a custom shape control
                    _currentShape = new global::Avalonia.Controls.Shapes.Rectangle
                    {
                        Stroke = brush,
                        StrokeThickness = vm.StrokeWidth,
                        Fill = Brushes.White,
                        RadiusX = 10,
                        RadiusY = 10
                    };
                    break;
 
                 case EditorTool.Pen:
                 case EditorTool.SmartEraser:
                    // Freehand drawing requires a Polyline
                         var polyline = new Polyline
                         {
                             Stroke = (vm.ActiveTool == EditorTool.SmartEraser) ? new SolidColorBrush(Color.Parse("#80FF0000")) : brush,
                             StrokeThickness = (vm.ActiveTool == EditorTool.SmartEraser) ? 10 : vm.StrokeWidth,
                             Points = new Points { _startPoint }
                         };
                         
                         FreehandAnnotation freehand;
                         if (vm.ActiveTool == EditorTool.SmartEraser) 
                             freehand = new SmartEraserAnnotation();
                         else 
                             freehand = new FreehandAnnotation();
                             
                         freehand.Points.Add(_startPoint);
                         polyline.Tag = freehand;
                         
                         _currentShape = polyline;
                     break;

                case EditorTool.Number:
                    // Use existing number logic (it was here before)
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
                    
                    Canvas.SetLeft(numberGrid, _startPoint.X - 15);
                    Canvas.SetTop(numberGrid, _startPoint.Y - 15);
                    
                    _currentShape = numberGrid;
                    vm.NumberCounter++;
                    
                    canvas.Children.Add(numberGrid);
                    _undoStack.Push(numberGrid);
                    _redoStack.Clear();
                    _currentShape = null; 
                    _isDrawing = false;
                    return;
            }

            if (_currentShape != null)
            {
                if (vm.ActiveTool != EditorTool.Line && vm.ActiveTool != EditorTool.Arrow && vm.ActiveTool != EditorTool.Pen && vm.ActiveTool != EditorTool.SmartEraser)
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
            var currentPoint = GetCanvasPosition(e, canvas);

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
            else if (_currentShape is Polyline polyline)
            {
                // Freehand drawing: Add point to existing points
                // We must create a new collection or modify existing?
                // Observable collection updates might trigger redraw
                // Polyline.Points is a Points collection.
                polyline.Points.Add(currentPoint);
                
                if (polyline.Tag is FreehandAnnotation freehand)
                {
                    freehand.Points.Add(currentPoint);
                }
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
                    // Existing logic
                    rect.Width = width;
                    rect.Height = height;
                    Canvas.SetLeft(rect, x);
                    Canvas.SetTop(rect, y);
                    
                    // Trigger update for effects
                    if (rect.Tag is BaseEffectAnnotation)
                    {
                        UpdateEffectVisual(rect);
                    }
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

        private void UpdateEffectVisual(Control shape)
        {
            if (shape.Tag is BaseEffectAnnotation annotation && DataContext is MainViewModel vm && vm.PreviewImage != null)
            {
                try
                {
                    double left = Canvas.GetLeft(shape);
                    double top = Canvas.GetTop(shape);
                    double width = shape.Bounds.Width;
                    double height = shape.Bounds.Height;
                    
                    if (width <= 0 || height <= 0) return;

                    annotation.StartPoint = new Point(left, top);
                    annotation.EndPoint = new Point(left + width, top + height);
                    
                    // Convert to SKBitmap 
                    using var skBitmap = Helpers.BitmapConversionHelpers.ToSKBitmap(vm.PreviewImage);
                    
                    annotation.UpdateEffect(skBitmap);
                    
                    if (annotation.EffectBitmap != null && shape is Shape visibleShape)
                    {
                        visibleShape.Fill = new ImageBrush(annotation.EffectBitmap) 
                        { 
                            Stretch = Stretch.None,
                            SourceRect = new RelativeRect(0, 0, width, height, RelativeUnit.Absolute) 
                        };
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Effect update failed: {ex.Message}");
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
                    var createdShape = _currentShape;
                    _undoStack.Push(createdShape);

                    // Auto-select newly created shape so resize handles appear immediately
                    _selectedShape = createdShape;
                    UpdateSelectionHandles();

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
        private void OnEffectsPanelApplyRequested(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.ApplyEffectCommand.Execute(null);
            }
        }
    }
}
