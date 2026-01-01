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

namespace ShareX.Ava.UI.Views.RegionCapture
{
    public partial class RegionCaptureWindow : Window
    {
        private Point _startPoint;
        private bool _isSelecting;
        
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

        public async Task SetBackgroundScreenshot()
        {
            // Capture full screen before showing the window
            if (ShareX.Ava.Platform.Abstractions.PlatformServices.IsInitialized)
            {
                var screenshot = await ShareX.Ava.Platform.Abstractions.PlatformServices.ScreenCapture.CaptureFullScreenAsync();
                if (screenshot != null)
                {
                    // Convert System.Drawing.Image to Avalonia Bitmap
                    using var memoryStream = new System.IO.MemoryStream();
                    screenshot.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                    memoryStream.Position = 0;
                    
                    var avaloniaBitmap = new Bitmap(memoryStream);
                    
                    var backgroundImage = this.FindControl<Image>("BackgroundImage");
                    if (backgroundImage != null)
                    {
                        backgroundImage.Source = avaloniaBitmap;
                    }
                    
                    screenshot.Dispose();
                }
            }
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            
            // Multi-monitor support: Span all screens
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
                
                var totalWidth = maxX - minX;
                var totalHeight = maxY - minY;

                Position = new PixelPoint(minX, minY);
                Width = totalWidth / RenderScaling;
                Height = totalHeight / RenderScaling;
                
                // Size the background image and darkening overlay to match
                var backgroundImage = this.FindControl<Image>("BackgroundImage");
                if (backgroundImage != null)
                {
                    backgroundImage.Width = Width;
                    backgroundImage.Height = Height;
                }

                // Initialize the darkening overlay to cover the entire screen
                InitializeFullScreenDarkening();
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
                _startPoint = point.Position;
                _isSelecting = true;
                
                // Show both border rectangles
                var border = this.FindControl<Rectangle>("SelectionBorder");
                if (border != null)
                {
                    border.IsVisible = true;
                    Canvas.SetLeft(border, _startPoint.X);
                    Canvas.SetTop(border, _startPoint.Y);
                    border.Width = 0;
                    border.Height = 0;
                }

                var borderInner = this.FindControl<Rectangle>("SelectionBorderInner");
                if (borderInner != null)
                {
                    borderInner.IsVisible = true;
                    Canvas.SetLeft(borderInner, _startPoint.X);
                    Canvas.SetTop(borderInner, _startPoint.Y);
                    borderInner.Width = 0;
                    borderInner.Height = 0;
                }

                // Update darkening overlay with zero-size selection (keeps full screen dimmed)
                UpdateDarkeningOverlay(_startPoint.X, _startPoint.Y, 0, 0);
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

            var currentPoint = point.Position;
            var border = this.FindControl<Rectangle>("SelectionBorder");
            var borderInner = this.FindControl<Rectangle>("SelectionBorderInner");
            var infoText = this.FindControl<TextBlock>("InfoText");

            if (border != null)
            {
                // Calculate rect
                var x = Math.Min(_startPoint.X, currentPoint.X);
                var y = Math.Min(_startPoint.Y, currentPoint.Y);
                var width = Math.Abs(_startPoint.X - currentPoint.X);
                var height = Math.Abs(_startPoint.Y - currentPoint.Y);

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
                
                if (infoText != null)
                {
                    infoText.IsVisible = true;
                    infoText.Text = $"X: {x:F0}, Y: {y:F0} - {width:F0} x {height:F0}";
                    Canvas.SetLeft(infoText, x);
                    Canvas.SetTop(infoText, y - 25);
                }
            }
        }

        private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (_isSelecting)
            {
                _isSelecting = false;
                var currentPoint = e.GetCurrentPoint(this).Position;
                
                // Calculate final rect in logical pixels relative to Window
                var x = Math.Min(_startPoint.X, currentPoint.X);
                var y = Math.Min(_startPoint.Y, currentPoint.Y);
                var width = Math.Abs(_startPoint.X - currentPoint.X);
                var height = Math.Abs(_startPoint.Y - currentPoint.Y);

                // Get render scaling (DPI)
                var scaling = this.RenderScaling;

                // Convert to physical pixels (Size)
                var physWidth = (int)(width * scaling);
                var physHeight = (int)(height * scaling);
                
                // Calculate physical position relative to screen (Absolute)
                // Window Position (phys) + Selection Position (phys approx)
                var winX = Position.X;
                var winY = Position.Y;
                var physX = winX + (int)(x * scaling);
                var physY = winY + (int)(y * scaling);

                // Ensure non-zero size
                if (physWidth <= 0) physWidth = 1;
                if (physHeight <= 0) physHeight = 1;

                // Create result rectangle
                var resultRect = new System.Drawing.Rectangle(physX, physY, physWidth, physHeight);
                
                _tcs.TrySetResult(resultRect);
                Close();
            }
        }
    }
}
