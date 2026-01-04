using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Point = Avalonia.Point;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.VisualTree;

namespace ShareX.Ava.UI.Views.RegionCapture
{
    public partial class RegionCaptureWindow : Window
    {
        private System.Drawing.Point GetGlobalMousePosition()
        {
            if (ShareX.Ava.Platform.Abstractions.PlatformServices.IsInitialized)
            {
                return ShareX.Ava.Platform.Abstractions.PlatformServices.Input.GetCursorPosition();
            }
            return System.Drawing.Point.Empty;
        }

        // Start point in physical screen coordinates (for final screenshot region)
        private System.Drawing.Point _startPointPhysical;
        // Start point in logical window coordinates (for visual rendering)
        private Point _startPointLogical;
        private bool _isSelecting;
        
        // Store window position for coordinate conversion
        private int _windowLeft = 0;
        private int _windowTop = 0;
        
        // Result task completion source to return value to caller
        private readonly System.Threading.Tasks.TaskCompletionSource<System.Drawing.Rectangle> _tcs;

        // Darkening overlay settings (configurable from RegionCaptureOptions)
        private const byte DarkenOpacity = 128; // 0-255, where 128 is 50% opacity (can be calculated from BackgroundDimStrength)
        
        public RegionCaptureWindow()
        {
            InitializeComponent();
            _tcs = new System.Threading.Tasks.TaskCompletionSource<System.Drawing.Rectangle>();
            
            // Close on Escape key
            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    _tcs.TrySetResult(System.Drawing.Rectangle.Empty);
                    Close();
                }
            };
        }

        public System.Threading.Tasks.Task<System.Drawing.Rectangle> GetResultAsync()
        {
            return _tcs.Task;
        }

        // Helper to convert System.Drawing.Bitmap to Avalonia Bitmap
        private Bitmap ConvertToAvaloniaBitmap(System.Drawing.Image source)
        {
            using var memoryStream = new System.IO.MemoryStream();
            source.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
            memoryStream.Position = 0;
            return new Bitmap(memoryStream);
        }

        protected override async void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            
            // Multi-monitor support: Span all screens at physical pixel dimensions
            if (Screens.ScreenCount > 0)
            {
                var minX = 0;
                var minY = 0;
                var maxX = 0;
                var maxY = 0;
                
                bool first = true;
                foreach (var screen in Screens.All)
                {
                    if (first)
                    {
                        minX = screen.Bounds.X;
                        minY = screen.Bounds.Y;
                        maxX = screen.Bounds.Right;
                        maxY = screen.Bounds.Bottom;
                        first = false;
                    }
                    else
                    {
                        minX = Math.Min(minX, screen.Bounds.X);
                        minY = Math.Min(minY, screen.Bounds.Y);
                        maxX = Math.Max(maxX, screen.Bounds.Right);
                        maxY = Math.Max(maxY, screen.Bounds.Bottom);
                    }
                }
                
                // Position window at absolute top-left of virtual screen
                Position = new PixelPoint(minX, minY);
                
                // Calculate logical size required to cover all screens
                // We use PointToClient to determine the Logical extent of the virtual screen relative to the window origin
                // This automatically handles the mixed DPI scaling logic of the underlying platform
                var topLeft = this.PointToClient(new PixelPoint(minX, minY));
                var bottomRight = this.PointToClient(new PixelPoint(maxX, maxY));
                var logicalWidth = bottomRight.X - topLeft.X;
                var logicalHeight = bottomRight.Y - topLeft.Y;

                // Set window size to logical units
                Width = logicalWidth;
                Height = logicalHeight;
                
                // Store window position for coordinate conversion
                _windowLeft = minX;
                _windowTop = minY;
                
                // Populate background images for each screen
                var container = this.FindControl<Canvas>("BackgroundContainer");
                if (container != null && ShareX.Ava.Platform.Abstractions.PlatformServices.IsInitialized)
                {
                    container.Children.Clear();
                    
                    foreach (var screen in Screens.All)
                    {
                        // 1. Capture screen content (Physical)
                        var screenRect = new System.Drawing.Rectangle(
                            screen.Bounds.X, screen.Bounds.Y, 
                            screen.Bounds.Width, screen.Bounds.Height);
                            
                        var screenshot = await ShareX.Ava.Platform.Abstractions.PlatformServices.ScreenCapture.CaptureRectAsync(screenRect);
                        
                        if (screenshot != null)
                        {
                            var avaloniaBitmap = ConvertToAvaloniaBitmap(screenshot);
                            var imageControl = new Image
                            {
                                Source = avaloniaBitmap,
                                Stretch = Stretch.Fill
                            };
                            RenderOptions.SetBitmapInterpolationMode(imageControl, BitmapInterpolationMode.HighQuality);

                            // 2. Calculate Logical Position and Size for this screen image
                            var screenTopLeft = this.PointToClient(screen.Bounds.TopLeft);
                            var screenBottomRight = this.PointToClient(screen.Bounds.BottomRight);
                            
                            var screenLogicalWidth = screenBottomRight.X - screenTopLeft.X;
                            var screenLogicalHeight = screenBottomRight.Y - screenTopLeft.Y;

                            imageControl.Width = screenLogicalWidth;
                            imageControl.Height = screenLogicalHeight;

                            Canvas.SetLeft(imageControl, screenTopLeft.X);
                            Canvas.SetTop(imageControl, screenTopLeft.Y);

                            container.Children.Add(imageControl);
                            
                            screenshot.Dispose();
                        }
                    }
                }

                // Initialize the darkening overlay to cover the entire screen
                InitializeFullScreenDarkening();
                
                ShareX.Ava.Common.DebugHelper.WriteLine($"RegionCapture: Window position: ({minX}, {minY})");
                ShareX.Ava.Common.DebugHelper.WriteLine($"RegionCapture: Window size: {logicalWidth}x{logicalHeight}");
                ShareX.Ava.Common.DebugHelper.WriteLine($"RegionCapture: RenderScaling: {RenderScaling}");
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeFullScreenDarkening()
        {
            var overlay = this.FindControl<Path>("DarkeningOverlay");
            if (overlay == null) return;

            // Create a simple rectangle covering the entire screen (no cutout yet)
            var darkeningGeometry = new PathGeometry();
            var fullScreenFigure = new PathFigure 
            { 
                StartPoint = new Point(0, 0), 
                IsClosed = true 
            };
            fullScreenFigure.Segments.Add(new LineSegment { Point = new Point(Width, 0) });
            fullScreenFigure.Segments.Add(new LineSegment { Point = new Point(Width, Height) });
            fullScreenFigure.Segments.Add(new LineSegment { Point = new Point(0, Height) });
            darkeningGeometry.Figures.Add(fullScreenFigure);

            overlay.Data = darkeningGeometry;
            overlay.IsVisible = true;
        }

        private void UpdateDarkeningOverlay(double selX, double selY, double selWidth, double selHeight)
        {
            var overlay = this.FindControl<Path>("DarkeningOverlay");
            if (overlay == null) return;

            // Create geometry with EvenOdd fill rule to create a "cutout" effect
            // This darkens the entire screen except the selection rectangle
            var darkeningGeometry = new PathGeometry { FillRule = FillRule.EvenOdd };

            // Outer rectangle: entire screen
            var outerFigure = new PathFigure 
            { 
                StartPoint = new Point(0, 0), 
                IsClosed = true 
            };
            outerFigure.Segments.Add(new LineSegment { Point = new Point(Width, 0) });
            outerFigure.Segments.Add(new LineSegment { Point = new Point(Width, Height) });
            outerFigure.Segments.Add(new LineSegment { Point = new Point(0, Height) });
            darkeningGeometry.Figures.Add(outerFigure);

            // Inner rectangle: selection area (this creates the "hole")
            var innerFigure = new PathFigure 
            { 
                StartPoint = new Point(selX, selY), 
                IsClosed = true 
            };
            innerFigure.Segments.Add(new LineSegment { Point = new Point(selX + selWidth, selY) });
            innerFigure.Segments.Add(new LineSegment { Point = new Point(selX + selWidth, selY + selHeight) });
            innerFigure.Segments.Add(new LineSegment { Point = new Point(selX, selY + selHeight) });
            darkeningGeometry.Figures.Add(innerFigure);

            overlay.Data = darkeningGeometry;
        }

        private void CancelSelection()
        {
            // Hide selection borders and info text
            var border = this.FindControl<Rectangle>("SelectionBorder");
            if (border != null)
            {
                border.IsVisible = false;
            }

            var borderInner = this.FindControl<Rectangle>("SelectionBorderInner");
            if (borderInner != null)
            {
                borderInner.IsVisible = false;
            }

            var infoText = this.FindControl<TextBlock>("InfoText");
            if (infoText != null)
            {
                infoText.IsVisible = false;
            }

            // Reset to full screen dimming
            InitializeFullScreenDarkening();

            // Reset selection state
            _isSelecting = false;
        }

        private void OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            
            // Handle right-click
            if (point.Properties.IsRightButtonPressed)
            {
                if (_isSelecting)
                {
                    // Cancel current selection
                    CancelSelection();
                    e.Handled = true;
                }
                else
                {
                    // No active selection - close the window
                    _tcs.TrySetResult(System.Drawing.Rectangle.Empty);
                    Close();
                    e.Handled = true;
                }
                return;
            }
            
            // Handle left-click to start selection
            if (point.Properties.IsLeftButtonPressed)
            {
                // Store physical coordinates for final screenshot region (from Win32 API)
                _startPointPhysical = GetGlobalMousePosition();
                
                // Store logical coordinates for visual rendering (from Avalonia - already correct)
                _startPointLogical = point.Position;
                _isSelecting = true;
                
                // Use Avalonia's logical coordinates directly for rendering
                var relativeX = _startPointLogical.X;
                var relativeY = _startPointLogical.Y;
                
                // Show both border rectangles
                var border = this.FindControl<Rectangle>("SelectionBorder");
                if (border != null)
                {
                    border.IsVisible = true;
                    Canvas.SetLeft(border, relativeX);
                    Canvas.SetTop(border, relativeY);
                    border.Width = 0;
                    border.Height = 0;
                }

                var borderInner = this.FindControl<Rectangle>("SelectionBorderInner");
                if (borderInner != null)
                {
                    borderInner.IsVisible = true;
                    Canvas.SetLeft(borderInner, relativeX);
                    Canvas.SetTop(borderInner, relativeY);
                    borderInner.Width = 0;
                    borderInner.Height = 0;
                }

                // Update darkening overlay with zero-size selection (keeps full screen dimmed)
                UpdateDarkeningOverlay(relativeX, relativeY, 0, 0);
            }
        }

        private void OnPointerMoved(object sender, PointerEventArgs e)
        {
            // Check for right-click during drag to cancel selection
            var point = e.GetCurrentPoint(this);
            if (point.Properties.IsRightButtonPressed && _isSelecting)
            {
                CancelSelection();
                e.Handled = true;
                return;
            }

            if (!_isSelecting) return;

            // Use Avalonia's logical coordinates for visual rendering (already correct)
            var currentPointLogical = point.Position;
            
            var border = this.FindControl<Rectangle>("SelectionBorder");
            var borderInner = this.FindControl<Rectangle>("SelectionBorderInner");
            var infoText = this.FindControl<TextBlock>("InfoText");

            if (border != null)
            {
                // Calculate rect in logical coordinates for visual rendering
                var x = Math.Min(_startPointLogical.X, currentPointLogical.X);
                var y = Math.Min(_startPointLogical.Y, currentPointLogical.Y);
                var width = Math.Abs(_startPointLogical.X - currentPointLogical.X);
                var height = Math.Abs(_startPointLogical.Y - currentPointLogical.Y);

                // Update outer border (white solid)
                Canvas.SetLeft(border, x);
                Canvas.SetTop(border, y);
                border.Width = width;
                border.Height = height;

                // Update inner border (black dashed - static marching ants pattern)
                if (borderInner != null)
                {
                    Canvas.SetLeft(borderInner, x);
                    Canvas.SetTop(borderInner, y);
                    borderInner.Width = width;
                    borderInner.Height = height;
                }

                // Update darkening overlay to cut out the selection area
                UpdateDarkeningOverlay(x, y, width, height);
                
                // Get physical coordinates for info display
                var currentPointPhysical = GetGlobalMousePosition();
                var physicalX = Math.Min(_startPointPhysical.X, currentPointPhysical.X);
                var physicalY = Math.Min(_startPointPhysical.Y, currentPointPhysical.Y);
                var physicalWidth = Math.Abs(_startPointPhysical.X - currentPointPhysical.X);
                var physicalHeight = Math.Abs(_startPointPhysical.Y - currentPointPhysical.Y);
                
                if (infoText != null)
                {
                    infoText.IsVisible = true;
                    // Format: Rectangle info in physical coordinates + Mouse pointer coordinates
                    infoText.Text = $"X: {physicalX} Y: {physicalY} W: {physicalWidth} H: {physicalHeight} | Mouse: ({currentPointPhysical.X}, {currentPointPhysical.Y})";
                    
                    // Position label above the selection with more clearance
                    Canvas.SetLeft(infoText, x);
                    
                    // Calculate vertical position - place above selection with padding
                    var labelHeight = 30; // Approximate height of label with padding
                    var topPadding = 5; // Additional padding from selection top
                    var labelY = y - labelHeight - topPadding;
                    
                    // If label would go off top of screen, place it below the top edge
                    if (labelY < 5)
                    {
                        labelY = 5;
                    }
                    
                    Canvas.SetTop(infoText, labelY);
                }
            }
        }

        private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (_isSelecting)
            {
                _isSelecting = false;
                
                // Get final position in physical screen coordinates (from Win32 API)
                var currentPointPhysical = GetGlobalMousePosition();
                
                // Calculate final rect in physical coordinates for screenshot
                var x = Math.Min(_startPointPhysical.X, currentPointPhysical.X);
                var y = Math.Min(_startPointPhysical.Y, currentPointPhysical.Y);
                var width = Math.Abs(_startPointPhysical.X - currentPointPhysical.X);
                var height = Math.Abs(_startPointPhysical.Y - currentPointPhysical.Y);

                // Ensure non-zero size
                if (width <= 0) width = 1;
                if (height <= 0) height = 1;

                // Create result rectangle in physical screen coordinates
                var resultRect = new System.Drawing.Rectangle(x, y, width, height);
                
                _tcs.TrySetResult(resultRect);
                Close();
            }
        }
    }
}
