using System;
using System.Diagnostics;
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
        private bool _useDarkening = true;
        
#if DEBUG
        // Debug logging
        private System.IO.StreamWriter? _debugLog;
        private readonly string _debugLogPath;
#endif
        
        public RegionCaptureWindow()
        {
            InitializeComponent();
            _tcs = new System.Threading.Tasks.TaskCompletionSource<System.Drawing.Rectangle>();
            
#if DEBUG
            // Initialize debug logging
            var debugFolder = System.IO.Path.Combine(
                ShareX.Ava.Core.SettingManager.PersonalFolder,
                "debug");
            System.IO.Directory.CreateDirectory(debugFolder);
            
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            _debugLogPath = System.IO.Path.Combine(debugFolder, $"region-capture-{timestamp}.log");
            
            try
            {
                _debugLog = new System.IO.StreamWriter(_debugLogPath, append: true) { AutoFlush = true };
                DebugLog("INIT", "RegionCaptureWindow created");
            }
            catch
            {
                _debugLog = null;
            }
#endif
            
            // Close on Escape key
            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    DebugLog("INPUT", "Escape key pressed - cancelling");
                    _tcs.TrySetResult(System.Drawing.Rectangle.Empty);
                    Close();
                }
            };
            
#if DEBUG
            // Clean up debug log on close
            this.Closed += (s, e) =>
            {
                DebugLog("LIFECYCLE", "Window closing");
                _debugLog?.Flush();
                _debugLog?.Dispose();
            };
#endif
        }
        
        [Conditional("DEBUG")]
        private void DebugLog(string category, string message)
        {
#if DEBUG
            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                _debugLog?.WriteLine($"[{timestamp}] {category,-12} | {message}");
            }
            catch
            {
                // Silently ignore logging errors
            }
#endif
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
            
            DebugLog("LIFECYCLE", "OnOpened started");
            
            // Delay removed per user request
            // await Task.Delay(250);
            
            DebugLog("WINDOW", "Post-delay, beginning screen enumeration");
            
            // Multi-monitor support: Span all screens at physical pixel dimensions
            if (Screens.ScreenCount > 0)
            {
                DebugLog("SCREEN", $"Screen count: {Screens.ScreenCount}");
                
                var minX = 0;
                var minY = 0;
                var maxX = 0;
                var maxY = 0;
                
                bool first = true;
                int screenIndex = 0;
                foreach (var screen in Screens.All)
                {
                    DebugLog("SCREEN", $"Screen {screenIndex}: Bounds={screen.Bounds}, Scaling={screen.Scaling}, IsPrimary={screen.IsPrimary}");
                    
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
                    screenIndex++;
                }
                
                DebugLog("WINDOW", $"Virtual screen bounds: ({minX}, {minY}) to ({maxX}, {maxY})");
                
                // Size first to avoid transient invalid window bounds on macOS.
                UpdateWindowSize(minX, minY);

                // Position window at absolute top-left of virtual screen
                Position = new PixelPoint(minX, minY);
                DebugLog("WINDOW", $"Set Position to: {Position}");
                DebugLog("WINDOW", $"Actual Position after set: {Position}, RenderScaling: {RenderScaling}");
                
                // Store window position for coordinate conversion
                _windowLeft = minX;
                _windowTop = minY;
                
                // Check if all screens are 100% DPI (Scaling == 1.0)
                // We only enable background images and darkening if ALL screens are 1.0, to avoid mixed-DPI offsets.
                bool allScreensStandardDpi = true;
                foreach (var screen in Screens.All)
                {
                    if (Math.Abs(screen.Scaling - 1.0) > 0.001)
                    {
                        allScreensStandardDpi = false;
                        break;
                    }
                }
                
                DebugLog("WINDOW", $"DPI Check: All screens standard DPI (1.0)? {allScreensStandardDpi}");

                _useDarkening = allScreensStandardDpi;
                var container = this.FindControl<Canvas>("BackgroundContainer");
                
                if (_useDarkening && container != null && ShareX.Ava.Platform.Abstractions.PlatformServices.IsInitialized)
                {
                    DebugLog("IMAGE", "Enabling background images and darkening (Standard DPI detected)");
                    container.Children.Clear();
                    
                    screenIndex = 0;
                    foreach (var screen in Screens.All)
                    {
                        DebugLog("IMAGE", $"--- Processing screen {screenIndex} for background ---");
                        
                        // 1. Capture screen content (Physical)
                        var screenRect = new System.Drawing.Rectangle(
                            screen.Bounds.X, screen.Bounds.Y, 
                            screen.Bounds.Width, screen.Bounds.Height);
                        DebugLog("IMAGE", $"Screen {screenIndex} capture rect: {screenRect}");
                            
                        var screenshot = await ShareX.Ava.Platform.Abstractions.PlatformServices.ScreenCapture.CaptureRectAsync(screenRect);
                        
                        if (screenshot != null)
                        {
                            var avaloniaBitmap = ConvertToAvaloniaBitmap(screenshot);
                            var imageControl = new Image
                            {
                                Source = avaloniaBitmap,
                                Stretch = Stretch.Fill,
                                Tag = screen
                            };
                            RenderOptions.SetBitmapInterpolationMode(imageControl, BitmapInterpolationMode.HighQuality);

                            // Since we are 1.0 scaling, Logic == Physical
                            // Simple layout:
                            double logLeft = screen.Bounds.X - minX;
                            double logTop = screen.Bounds.Y - minY;
                            double logWidth = screen.Bounds.Width;
                            double logHeight = screen.Bounds.Height;
                            
                            imageControl.Width = logWidth;
                            imageControl.Height = logHeight;
                            Canvas.SetLeft(imageControl, logLeft);
                            Canvas.SetTop(imageControl, logTop);

                            container.Children.Add(imageControl);
                            
                            DebugLog("IMAGE", $"Screen {screenIndex} placed at ({logLeft}, {logTop}) size {logWidth}x{logHeight}");
                            
                            screenshot.Dispose();
                        }
                        
                        screenIndex++;
                    }
                    
                    // Enable darkening
                    InitializeFullScreenDarkening();
                }
                else
                {
                    DebugLog("IMAGE", "Disabling background images and darkening (Mixed/High DPI detected)");
                    if (container != null)
                    {
                        container.Children.Clear();
                    }
                    // Do NOT initialize darkening
                }
            }
        }
        
        private void UpdateWindowSize(int minX, int minY)
        {
            double logicalMinX = double.MaxValue;
            double logicalMinY = double.MaxValue;
            double logicalMaxX = double.MinValue;
            double logicalMaxY = double.MinValue;
            
            var currentScaling = RenderScaling;
            
            foreach (var screen in Screens.All)
            {
                var offsetX = screen.Bounds.X - minX;
                var offsetY = screen.Bounds.Y - minY;
                
                var logLeft = offsetX / currentScaling;
                var logTop = offsetY / currentScaling;
                var logRight = logLeft + (screen.Bounds.Width / currentScaling);
                var logBottom = logTop + (screen.Bounds.Height / currentScaling);
                
                logicalMinX = Math.Min(logicalMinX, logLeft);
                logicalMinY = Math.Min(logicalMinY, logTop);
                logicalMaxX = Math.Max(logicalMaxX, logRight);
                logicalMaxY = Math.Max(logicalMaxY, logBottom);
            }
            
            Width = logicalMaxX - logicalMinX;
            Height = logicalMaxY - logicalMinY;
            DebugLog("WINDOW", $"UpdateWindowSize: Scaling={currentScaling}, Size={Width}x{Height}");
        }

        private void UpdateImagesLayout(int minX, int minY)
        {
            var container = this.FindControl<Canvas>("BackgroundContainer");
            if (container == null) return;
            
            var currentScaling = RenderScaling;
            DebugLog("LAYOUT", $"UpdateImagesLayout: Scaling={currentScaling}");
            
            int idx = 0;
            foreach (var child in container.Children)
            {
                if (child is Image img && img.Tag is Avalonia.Platform.Screen screen)
                {
                    // Calculate based on WINDOW scaling, not Screen scaling
                    // We want 1 image pixel = 1 physical pixel
                    
                    var physicalOffsetX = screen.Bounds.X - minX;
                    var physicalOffsetY = screen.Bounds.Y - minY;
                    
                    var logicalLeft = physicalOffsetX / currentScaling;
                    var logicalTop = physicalOffsetY / currentScaling;
                    var logicalWidth = screen.Bounds.Width / currentScaling;
                    var logicalHeight = screen.Bounds.Height / currentScaling;
                    
                    img.Width = logicalWidth;
                    img.Height = logicalHeight;
                    Canvas.SetLeft(img, logicalLeft);
                    Canvas.SetTop(img, logicalTop);
                    
                    DebugLog("LAYOUT", $"Screen {idx} Layout: Physical {screen.Bounds.Width}x{screen.Bounds.Height} -> Logical {logicalWidth}x{logicalHeight} (@ {currentScaling}x)");
                }
                idx++;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeFullScreenDarkening()
        {
            if (!_useDarkening) return;

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
            if (!_useDarkening) return;

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
                
                DebugLog("MOUSE", $"PointerPressed: Physical={_startPointPhysical}, Logical={_startPointLogical}");
                
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
                
                // Fallback: If Win32 API fails (returns 0,0), calculate from Avalonia position
                if (currentPointPhysical.IsEmpty || (currentPointPhysical.X == 0 && currentPointPhysical.Y == 0))
                {
                    var point = e.GetCurrentPoint(this);
                    var logicalPos = point.Position;
                    
                    // Convert logical to physical using window position and render scaling
                    // Note: This is approximate for mixed-DPI, but better than (0,0)
                    var physicalX = _windowLeft + (int)(logicalPos.X * RenderScaling);
                    var physicalY = _windowTop + (int)(logicalPos.Y * RenderScaling);
                    
                    currentPointPhysical = new System.Drawing.Point(physicalX, physicalY);
                    DebugLog("MOUSE", $"PointerReleased: Win32 API failed, using fallback. Logical={logicalPos}, Calculated Physical={currentPointPhysical}");
                }
                else
                {
                    DebugLog("MOUSE", $"PointerReleased: Physical={currentPointPhysical}");
                }
                
                // Calculate final rect in physical coordinates for screenshot
                var x = Math.Min(_startPointPhysical.X, currentPointPhysical.X);
                var y = Math.Min(_startPointPhysical.Y, currentPointPhysical.Y);
                var width = Math.Abs(_startPointPhysical.X - currentPointPhysical.X);
                var height = Math.Abs(_startPointPhysical.Y - currentPointPhysical.Y);
                
                DebugLog("RESULT", $"Final selection rect: X={x}, Y={y}, W={width}, H={height}");

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
