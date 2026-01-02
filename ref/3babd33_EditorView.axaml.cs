#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
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
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ShareX.Ava.Annotations.Models;
using ShareX.Ava.UI.ViewModels;
using System.ComponentModel;

namespace ShareX.Ava.UI.Views
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
        private List<Control> _selectionHandles = new();
        private bool _isDraggingHandle;
        private Control? _draggedHandle;

        // Store arrow/line endpoints for editing
        private Dictionary<Control, (Point Start, Point End)> _shapeEndpoints = new();
        
        // Cached SKBitmap for effect updates (avoid repeated conversions)
        private SkiaSharp.SKBitmap? _cachedSkBitmap;
        
        // Track if we're in the middle of creating an effect shape
        private bool _isCreatingEffect;

        private Point GetCanvasPosition(PointerEventArgs e, Canvas canvas)
        {
            return e.GetPosition(canvas);
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
                var container = this.FindControl<Grid>("CanvasContainer");
                if (container == null || container.Width <= 0 || container.Height <= 0) return null;

                // Render the container (image + annotations) to a bitmap
                var rtb = new global::Avalonia.Media.Imaging.RenderTargetBitmap(
                    new PixelSize((int)container.Width, (int)container.Height), 
                    new Vector(96, 96));
                
                rtb.Render(container);

                // Convert to SKBitmap for pixel access
                using var skBitmap = Helpers.BitmapConversionHelpers.ToSKBitmap(rtb);

                // Convert canvas point to pixel coordinates
                int x = (int)Math.Round(canvasPoint.X);
                int y = (int)Math.Round(canvasPoint.Y);

                System.Diagnostics.Debug.WriteLine($"GetPixelColorFromRenderedCanvas: Canvas point ({canvasPoint.X:F2}, {canvasPoint.Y:F2}) -> Pixel ({x}, {y})");
                System.Diagnostics.Debug.WriteLine($"GetPixelColorFromRenderedCanvas: Rendered size ({skBitmap.Width}, {skBitmap.Height}), Zoom: {vm.Zoom}");

                // Valid ate bounds
                if (x < 0 || y < 0 || x >= skBitmap.Width || y >= skBitmap.Height)
                {
                    System.Diagnostics.Debug.WriteLine($"GetPixelColorFromRenderedCanvas: Out of bounds!");
                    return null;
                }

                // Get pixel color from rendered output
                var skColor = skBitmap.GetPixel(x, y);
                
                // Convert to hex string
                var colorHex = $"#{skColor.Red:X2}{skColor.Green:X2}{skColor.Blue:X2}";
                System.Diagnostics.Debug.WriteLine($"GetPixelColorFromRenderedCanvas: Sampled color {colorHex} at ({x}, {y})");
                
                return colorHex;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetPixelColorFromRenderedCanvas failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Sample pixel color from the preview image at the specified canvas coordinates
        /// </summary>
        private string? GetPixelColor(Point canvasPoint)
        {
            if (DataContext is not MainViewModel vm || vm.PreviewImage == null) return null;

            try
            {
                // Canvas point is already in image coordinates (no zoom adjustment needed here
                // because GetCanvasPosition gets position relative to the canvas which is already scaled)
                int x = (int)Math.Round(canvasPoint.X);
                int y = (int)Math.Round(canvasPoint.Y);

                System.Diagnostics.Debug.WriteLine($"GetPixelColor: Canvas point ({canvasPoint.X:F2}, {canvasPoint.Y:F2}) -> Pixel ({x}, {y})");
                System.Diagnostics.Debug.WriteLine($"GetPixelColor: Image size ({vm.PreviewImage.Size.Width}, {vm.PreviewImage.Size.Height}), Zoom: {vm.Zoom}");

                // Validate bounds
                if (x < 0 || y < 0 || x >= vm.PreviewImage.Size.Width || y >= vm.PreviewImage.Size.Height)
                {
                    System.Diagnostics.Debug.WriteLine($"GetPixelColor: Out of bounds!");
                    return null;
                }

                // IMPORTANT: We sample from the BASE image (vm.PreviewImage), not from the rendered canvas
                // This means we get the original pixel color, ignoring any annotations drawn on top.
                // This is the correct behavior for Smart Eraser - it should match the background,
                // not other annotations.

                // Use cached SKBitmap if available, otherwise create one
                SkiaSharp.SKBitmap? skBitmap = _cachedSkBitmap;
                bool shouldDispose = false;

                if (skBitmap == null)
                {
                    skBitmap = Helpers.BitmapConversionHelpers.ToSKBitmap(vm.PreviewImage);
                    shouldDispose = true;
                }

                try
                {
                    // Get pixel color
                    var skColor = skBitmap.GetPixel(x, y);
                    
                    // Convert to hex string
                    var colorHex = $"#{skColor.Red:X2}{skColor.Green:X2}{skColor.Blue:X2}";
                    System.Diagnostics.Debug.WriteLine($"GetPixelColor: Sampled color {colorHex} at ({x}, {y})");
                    
                    return colorHex;
                }
                finally
                {
                    if (shouldDispose)
                    {
                        skBitmap?.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetPixelColor failed: {ex.Message}");
                return null;
            }
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
            // When a text annotation TextBox has focus, let it handle all typing
            // so that tool hotkeys (R, E, T, etc.) do not switch tools while editing.
            if (e.Source is TextBox)
            {
                return;
            }

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

            // Special handling for Lines and Arrows - create endpoint handles
            if (_selectedShape is global::Avalonia.Controls.Shapes.Line line)
            {
                CreateHandle(line.StartPoint.X, line.StartPoint.Y, "LineStart");
                CreateHandle(line.EndPoint.X, line.EndPoint.Y, "LineEnd");
                return;
            }

            // Special handling for Arrow (Path with geometry)
            if (_selectedShape is global::Avalonia.Controls.Shapes.Path arrowPath)
            {
                // Get stored start/end points from dictionary
                if (_shapeEndpoints.TryGetValue(arrowPath, out var endpoints))
                {
                    CreateHandle(endpoints.Start.X, endpoints.Start.Y, "ArrowStart");
                    CreateHandle(endpoints.End.X, endpoints.End.Y, "ArrowEnd");
                }
                return;
            }

            // Skip handles for Grid controls (Number/Step tool) - they have fixed size and don't need resizing
            if (_selectedShape is Grid)
            {
                return;
            }

            // Skip handles for shapes without measurable bounds (e.g., other lines)
            if (_selectedShape.Bounds.Width <= 0 || _selectedShape.Bounds.Height <= 0) return;

            // Calculate bounds for regular shapes
            var shapeLeft = Canvas.GetLeft(_selectedShape);
            var shapeTop = Canvas.GetTop(_selectedShape);
            var width = _selectedShape.Bounds.Width;
            var height = _selectedShape.Bounds.Height;

            // Allow handles even if Width/Height are NaN (e.g. Line)? 
            // For now assume shapes have explicit size setting in OnPointerMoved
            if (double.IsNaN(width)) width = _selectedShape.Width;
            if (double.IsNaN(height)) height = _selectedShape.Height;

            // Create 8 handles for regular shapes
            CreateHandle(shapeLeft, shapeTop, "TopLeft");
            CreateHandle(shapeLeft + width / 2, shapeTop, "TopCenter");
            CreateHandle(shapeLeft + width, shapeTop, "TopRight");
            CreateHandle(shapeLeft + width, shapeTop + height / 2, "RightCenter");
            CreateHandle(shapeLeft + width, shapeTop + height, "BottomRight");
            CreateHandle(shapeLeft + width / 2, shapeTop + height, "BottomCenter");
            CreateHandle(shapeLeft, shapeTop + height, "BottomLeft");
            CreateHandle(shapeLeft, shapeTop + height / 2, "LeftCenter");
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

            // Create modern handle with shadow using a border wrapper
            var handleBorder = new Border
            {
                Width = 15,
                Height = 15,
                CornerRadius = new CornerRadius(10), // Perfect circle
                Background = Brushes.White,
                Tag = tag,
                Cursor = cursor,
                BoxShadow = new BoxShadows(new BoxShadow
                {
                    OffsetX = 0,
                    OffsetY = 0,
                    Blur = 8,
                    Spread = 0,
                    Color = Color.FromArgb(100, 0, 0, 0)
                })
            };

            // Center the handle
            Canvas.SetLeft(handleBorder, x - handleBorder.Width / 2);
            Canvas.SetTop(handleBorder, y - handleBorder.Height / 2);

            overlay.Children.Add(handleBorder);
            _selectionHandles.Add(handleBorder);
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
                vm.ClearAnnotationsRequested += (s, args) => ClearAllAnnotations();
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
            else if (e.PropertyName == nameof(MainViewModel.PreviewImage))
            {
                // Invalidate cached bitmap when preview image changes
                _cachedSkBitmap?.Dispose();
                _cachedSkBitmap = null;
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
                ShareX.Ava.Platform.Abstractions.PlatformServices.Clipboard.SetImage(drawingImage);
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
                MaxWidth = 460
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

        private void ClearAllAnnotations()
        {
            var canvas = this.FindControl<Canvas>("AnnotationCanvas");
            if (canvas != null)
            {
                canvas.Children.Clear();
            }

            var overlay = this.FindControl<Canvas>("OverlayCanvas");
            if (overlay != null)
            {
                foreach (var handle in _selectionHandles)
                {
                    overlay.Children.Remove(handle);
                }
            }
            _selectionHandles.Clear();

            var cropOverlay = this.FindControl<global::Avalonia.Controls.Shapes.Rectangle>("CropOverlay");
            if (cropOverlay != null)
            {
                cropOverlay.IsVisible = false;
                cropOverlay.Width = 0;
                cropOverlay.Height = 0;
            }

            _selectedShape = null;
            _currentShape = null;
            _isDrawing = false;
            _isDraggingHandle = false;
            _draggedHandle = null;
            _isDraggingShape = false;

            _undoStack.Clear();
            _redoStack.Clear();
            
            // Clean up cached bitmap
            _cachedSkBitmap?.Dispose();
            _cachedSkBitmap = null;
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
            // Always draw on the main annotation canvas even if the overlay receives the event.
            var canvas = this.FindControl<Canvas>("AnnotationCanvas") ?? sender as Canvas;
            if (canvas == null) return;

            // Ignore middle mouse to avoid creating annotations while panning
            var props = e.GetCurrentPoint(canvas).Properties;
            if (props.IsMiddleButtonPressed) return;

            var point = GetCanvasPosition(e, canvas);

            // Handle right-click to delete shape
            if (props.IsRightButtonPressed)
            {
                // Hit test to find the shape under cursor
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

                // Delete the shape if found (don't delete crop overlay)
                if (hitTarget != null && hitTarget.Name != "CropOverlay")
                {
                    canvas.Children.Remove(hitTarget);
                    
                    // Remove from undo stack if present
                    if (_undoStack.Contains(hitTarget))
                    {
                        var tempStack = new Stack<Control>();
                        while (_undoStack.Count > 0)
                        {
                            var item = _undoStack.Pop();
                            if (item != hitTarget)
                            {
                                tempStack.Push(item);
                            }
                        }
                        while (tempStack.Count > 0)
                        {
                            _undoStack.Push(tempStack.Pop());
                        }
                    }

                    // Clear selection if this was the selected shape
                    if (_selectedShape == hitTarget)
                    {
                        _selectedShape = null;
                        UpdateSelectionHandles();
                    }

                    vm.StatusText = "Shape deleted";
                    e.Handled = true;
                }
                return;
            }

            // Check if clicking a handle (always allow when a shape is selected, regardless of active tool)
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

            // Allow dragging selected shapes even when not in Select tool mode
            // This enables immediate repositioning after creating an annotation
            if (_selectedShape != null && vm.ActiveTool != EditorTool.Select)
            {
                // Hit test to see if we clicked on the selected shape
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

                // If we clicked on the currently selected shape, start dragging it
                if (hitTarget == _selectedShape)
                {
                    _lastDragPoint = point;
                    _isDraggingShape = true;
                    e.Pointer.Capture(canvas);
                    return;
                }
                else
                {
                    // Clicked elsewhere, deselect and continue with new shape creation
                    _selectedShape = null;
                    UpdateSelectionHandles();
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
            e.Pointer.Capture(canvas);

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
                        try
                        {
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
                    // Store initial endpoint for later editing
                    _shapeEndpoints[_currentShape] = (_startPoint, _startPoint);
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
                        Text = string.Empty,
                        Padding = new Thickness(4),
                        MinWidth = 50,
                        AcceptsReturn = false // Single-line text annotation
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

                    // Pressing Enter should finalize the text annotation instead of inserting a newline.
                    textBox.KeyDown += (s, args) =>
                    {
                        if (args.Key == Key.Enter)
                        {
                            args.Handled = true;
                            // Move focus away so LostFocus handler finalizes/removes the editing chrome.
                            this.Focus();
                        }
                    };

                    canvas.Children.Add(textBox);
                    textBox.Focus();
                    _isDrawing = false;
                    return;

                case EditorTool.Spotlight:
                    // Create a spotlight control that will render the darkening effect
                    var spotlightAnnotation = new SpotlightAnnotation();
                    
                    // Set canvas size for the darkening overlay
                    spotlightAnnotation.CanvasSize = new Size(canvas.Bounds.Width, canvas.Bounds.Height);
                    
                    var spotlightControl = new ShareX.Ava.UI.Controls.SpotlightControl
                    {
                        Annotation = spotlightAnnotation,
                        IsHitTestVisible = true
                    };
                    
                    _currentShape = spotlightControl;
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
                    polyline.SetValue(Panel.ZIndexProperty, 1);

                    FreehandAnnotation freehand;
                    if (vm.ActiveTool == EditorTool.SmartEraser)
                    {
                        // Sample pixel color from rendered canvas (including annotations)
                        var sampledColor = await GetPixelColorFromRenderedCanvas(_startPoint);
                        
                        freehand = new SmartEraserAnnotation();
                        
                        // If we got a valid color, use it as solid color; otherwise fall back to semi-transparent red
                        if (!string.IsNullOrEmpty(sampledColor))
                        {
                            freehand.StrokeColor = sampledColor;
                            // Update polyline to use solid sampled color
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
                    break;

                case EditorTool.Number:
                    // Create number annotation
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
                    // Number is positioned and added to canvas, but keep _isDrawing true
                    // so it goes through OnCanvasPointerReleased for auto-selection
                    break;
            }

            if (_currentShape != null)
            {
                // Number tool already adds itself to canvas, so don't add it again
                if (vm.ActiveTool == EditorTool.Number)
                {
                    // Number is already added to canvas, nothing more to do here
                    // Keep _isDrawing true so it goes through OnCanvasPointerReleased properly
                }
                else if (vm.ActiveTool != EditorTool.Line && vm.ActiveTool != EditorTool.Arrow && vm.ActiveTool != EditorTool.Pen && vm.ActiveTool != EditorTool.SmartEraser && vm.ActiveTool != EditorTool.Spotlight)
                {
                    Canvas.SetLeft(_currentShape, _startPoint.X);
                    Canvas.SetTop(_currentShape, _startPoint.Y);
                    canvas.Children.Add(_currentShape);
                }
                else
                {
                    canvas.Children.Add(_currentShape);
                }
            }
        }

        private void OnCanvasPointerMoved(object sender, PointerEventArgs e)
        {
            // Keep all drawing relative to the annotation canvas so overlay hit tests don't misplace strokes.
            var canvas = this.FindControl<Canvas>("AnnotationCanvas") ?? sender as Canvas;
            if (canvas == null) return;
            var currentPoint = GetCanvasPosition(e, canvas);

            if (_isDraggingHandle && _draggedHandle != null && _selectedShape != null)
            {
                var handleTag = _draggedHandle.Tag?.ToString();
                var deltaX = currentPoint.X - _startPoint.X;
                var deltaY = currentPoint.Y - _startPoint.Y;

                // Special handling for Line endpoints
                if (_selectedShape is global::Avalonia.Controls.Shapes.Line targetLine)
                {
                    if (handleTag == "LineStart")
                    {
                        targetLine.StartPoint = currentPoint;
                    }
                    else if (handleTag == "LineEnd")
                    {
                        targetLine.EndPoint = currentPoint;
                    }

                    _startPoint = currentPoint;
                    UpdateSelectionHandles();
                    return;
                }

                // Special handling for Arrow endpoints
                if (_selectedShape is global::Avalonia.Controls.Shapes.Path arrowPath && DataContext is MainViewModel vm)
                {
                    // Get stored endpoints
                    if (_shapeEndpoints.TryGetValue(arrowPath, out var endpoints))
                    {
                        Point arrowStart = endpoints.Start;
                        Point arrowEnd = endpoints.End;

                        // Update the appropriate endpoint
                        if (handleTag == "ArrowStart")
                        {
                            arrowStart = currentPoint;
                        }
                        else if (handleTag == "ArrowEnd")
                        {
                            arrowEnd = currentPoint;
                        }

                        // Store updated endpoints
                        _shapeEndpoints[arrowPath] = (arrowStart, arrowEnd);

                        // Recreate arrow geometry with new points
                        arrowPath.Data = CreateArrowGeometry(arrowStart, arrowEnd, vm.StrokeWidth * 3);
                    }

                    _startPoint = currentPoint;
                    UpdateSelectionHandles();
                    return;
                }

                // Get current bounds for regular shapes
                var left = Canvas.GetLeft(_selectedShape);
                var top = Canvas.GetTop(_selectedShape);
                var width = _selectedShape.Bounds.Width;
                var height = _selectedShape.Bounds.Height;
                if (double.IsNaN(width)) width = _selectedShape.Width;
                if (double.IsNaN(height)) height = _selectedShape.Height;

                // Helper to update properties
                // Rectangle/Ellipse use Width/Height

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

                _startPoint = currentPoint; // Update for next delta
                UpdateSelectionHandles();
                return;
            }


            if (_isDraggingShape && _selectedShape != null)
            {
                var deltaX = currentPoint.X - _lastDragPoint.X;
                var deltaY = currentPoint.Y - _lastDragPoint.Y;

                // Special handling for moving Lines - update start and end points
                if (_selectedShape is global::Avalonia.Controls.Shapes.Line targetLine)
                {
                    targetLine.StartPoint = new Point(targetLine.StartPoint.X + deltaX, targetLine.StartPoint.Y + deltaY);
                    targetLine.EndPoint = new Point(targetLine.EndPoint.X + deltaX, targetLine.EndPoint.Y + deltaY);

                    _lastDragPoint = currentPoint;
                    UpdateSelectionHandles();
                    return;
                }

                // Special handling for moving arrows - update stored endpoints
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

                // Regular shape moving
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
                // Freehand drawing: create a new Points collection to force property change and redraw.
                var updated = new Points();
                foreach (var p in polyline.Points)
                {
                    updated.Add(p);
                }
                updated.Add(currentPoint);
                polyline.Points = updated;
                polyline.InvalidateVisual();

                if (polyline.Tag is FreehandAnnotation freehand)
                {
                    freehand.Points.Add(currentPoint);
                }
            }
            else if (_currentShape is global::Avalonia.Controls.Shapes.Path arrowPath && DataContext is MainViewModel vm)
            {
                // Update Arrow Geometry
                arrowPath.Data = CreateArrowGeometry(_startPoint, currentPoint, vm.StrokeWidth * 3);
                // Store endpoints for later editing
                _shapeEndpoints[arrowPath] = (_startPoint, currentPoint);
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
                else if (_currentShape is ShareX.Ava.UI.Controls.SpotlightControl spotlightControl && spotlightControl.Annotation is SpotlightAnnotation spotlight)
                {
                    // Update spotlight annotation bounds
                    spotlight.StartPoint = _startPoint;
                    spotlight.EndPoint = currentPoint;
                    
                    // Update canvas size for the entire image (needed for darkening overlay)
                    var parentCanvas = this.FindControl<Canvas>("AnnotationCanvas");
                    if (parentCanvas != null)
                    {
                        spotlight.CanvasSize = new Size(parentCanvas.Bounds.Width, parentCanvas.Bounds.Height);
                    }
                    
                    spotlightControl.InvalidateVisual();
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
            // OPTIMIZATION: Skip expensive effect processing during drag
            // Only show a simple preview rectangle while dragging
            // Full effect is applied on mouse release
            
            if (shape.Tag is not BaseEffectAnnotation annotation) return;
            
            // During dragging, just show the preview fill color (no expensive processing)
            if (_isDrawing)
            {
                // Shape already has preview fill set, no need to update
                return;
            }
            
            if (DataContext is not MainViewModel vm || vm.PreviewImage == null) return;

            try
            {
                double left = Canvas.GetLeft(shape);
                double top = Canvas.GetTop(shape);
                double width = shape.Bounds.Width;
                double height = shape.Bounds.Height;

                if (width <= 0 || height <= 0) return;

                annotation.StartPoint = new Point(left, top);
                annotation.EndPoint = new Point(left + width, top + height);

                // Cache SKBitmap conversion to avoid repeated conversions
                if (_cachedSkBitmap == null)
                {
                    _cachedSkBitmap = Helpers.BitmapConversionHelpers.ToSKBitmap(vm.PreviewImage);
                }

                annotation.UpdateEffect(_cachedSkBitmap);

                if (annotation.EffectBitmap != null && shape is Shape shapeControl)
                {
                    shapeControl.Fill = new ImageBrush(annotation.EffectBitmap)
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

        private Geometry CreateArrowGeometry(Point start, Point end, double headSize)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                // Calculate arrow head
                var d = end - start;
                var length = Math.Sqrt(d.X * d.X + d.Y * d.Y);

                if (length > 0)
                {
                    var ux = d.X / length;
                    var uy = d.Y / length;

                    // Modern arrow: narrower angle (20 degrees instead of 30)
                    var arrowAngle = Math.PI / 9; // 20 degrees for sleeker look

                    // Calculate arrowhead base point (where arrow meets the line)
                    var arrowBase = new Point(
                        end.X - headSize * ux,
                        end.Y - headSize * uy);

                    // Arrow head wing points
                    var p1 = new Point(
                        end.X - headSize * Math.Cos(Math.Atan2(uy, ux) - arrowAngle),
                        end.Y - headSize * Math.Sin(Math.Atan2(uy, ux) - arrowAngle));

                    var p2 = new Point(
                        end.X - headSize * Math.Cos(Math.Atan2(uy, ux) + arrowAngle),
                        end.Y - headSize * Math.Sin(Math.Atan2(uy, ux) + arrowAngle));

                    // Draw line from start to arrow base (not to endpoint)
                    ctx.BeginFigure(start, false);
                    ctx.LineTo(arrowBase);
                    ctx.EndFigure(false);

                    // Draw filled arrowhead triangle
                    ctx.BeginFigure(end, true);
                    ctx.LineTo(p1);
                    ctx.LineTo(p2);
                    ctx.EndFigure(true);
                }
                else
                {
                    // Fallback for zero-length arrow
                    ctx.BeginFigure(start, false);
                    ctx.LineTo(end);
                    ctx.EndFigure(false);
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
                    var createdShape = _currentShape;
                    
                    // Special handling for crop tool - execute crop immediately on mouse release
                    if (DataContext is MainViewModel vm && vm.ActiveTool == EditorTool.Crop && createdShape.Name == "CropOverlay")
                    {
                        // Execute the crop operation
                        PerformCrop();
                        
                        // Clear the current shape and don't add to undo stack
                        _currentShape = null;
                        e.Pointer.Capture(null);
                        return;
                    }
                    
                    _undoStack.Push(createdShape);

                    // Auto-select newly created shape so resize handles appear immediately,
                    // but skip selection for freehand pen/eraser strokes (Polyline) which
                    // are not resizable with our current handle logic.
                    if (createdShape is not Polyline)
                    {
                        // Apply final effect for effect tools
                        if (createdShape.Tag is BaseEffectAnnotation)
                        {
                            UpdateEffectVisual(createdShape);
                        }
                        
                        _selectedShape = createdShape;
                        UpdateSelectionHandles();
                    }

                    // _currentShape is now managed by the canvas/undo stack
                    _currentShape = null;
                }
                e.Pointer.Capture(null);
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
