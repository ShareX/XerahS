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
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using XerahS.Core.Helpers;
using SkiaSharp;
using System.Diagnostics;
using Path = Avalonia.Controls.Shapes.Path;
using Point = Avalonia.Point;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;

namespace XerahS.UI.Views.RegionCapture
{
    public partial class RegionCaptureWindow : Window
    {
        private SKPointI GetGlobalMousePosition()
        {
            if (XerahS.Platform.Abstractions.PlatformServices.IsInitialized)
            {
                var p = XerahS.Platform.Abstractions.PlatformServices.Input.GetCursorPosition();
                return new SKPointI(p.X, p.Y);
            }
            return new SKPointI(0, 0);
        }

        // Start point in physical screen coordinates (for final screenshot region)
        private SKPointI _startPointPhysical;
        // Start point in logical window coordinates (for visual rendering)
        private Point _startPointLogical;
        private bool _isSelecting;

        // Store window position for coordinate conversion
        private int _windowLeft = 0;
        private int _windowTop = 0;
        private double _capturedScaling = 1.0; // Captured at window open to ensure consistent coordinate conversion
        private bool _usePerScreenScalingForLayout;
        private bool _useWindowPositionForFallback;
        private bool _useLogicalCoordinatesForCapture;
        // _loggedPointerMoveFallback removed: Unused warning fix

        // Result task completion source to return value to caller
        private readonly System.Threading.Tasks.TaskCompletionSource<SKRectI> _tcs;

        // Darkening overlay settings (configurable from RegionCaptureOptions)
        private const byte DarkenOpacity = 128; // 0-255, where 128 is 50% opacity (can be calculated from BackgroundDimStrength)
        private bool _useDarkening = true;

        private readonly Stopwatch _openStopwatch = Stopwatch.StartNew();

        // Window detection
        private XerahS.Platform.Abstractions.WindowInfo[]? _windows;
        private XerahS.Platform.Abstractions.WindowInfo? _hoveredWindow;
        private bool _dragStarted;
        private const int DragThreshold = 5;

        // Debug logging is now handled by TroubleshootingHelper

        public RegionCaptureWindow()
        {
            InitializeComponent();
            _tcs = new System.Threading.Tasks.TaskCompletionSource<SKRectI>();

            DebugLog("INIT", "RegionCaptureWindow created");
            DebugLog("INIT", $"Initial state: RenderScaling={RenderScaling}, Position={Position}, Bounds={Bounds}, ClientSize={ClientSize}");

            // Close on Escape key
            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    DebugLog("INPUT", "Escape key pressed - cancelling");
                    _tcs.TrySetResult(SKRectI.Empty);
                    Close();
                }
            };
        }

        [Conditional("DEBUG")]
        private void DebugLog(string category, string message)
        {
            TroubleshootingHelper.Log("RegionCapture", category, message);
        }

        [Conditional("DEBUG")]
        private void DebugLogLayout(string reason)
        {
            var selectionCanvas = this.FindControl<Canvas>("SelectionCanvas");
            var backgroundContainer = this.FindControl<Canvas>("BackgroundContainer");
            var overlay = this.FindControl<Path>("DarkeningOverlay");

            DebugLog("LAYOUT", $"[{reason}] Window: Bounds={Bounds}, ClientSize={ClientSize}, Width={Width} Height={Height}, RenderScaling={RenderScaling}, Position={Position}");

            if (selectionCanvas != null)
            {
                DebugLog("LAYOUT", $"[{reason}] SelectionCanvas: Bounds={selectionCanvas.Bounds}, DesiredSize={selectionCanvas.DesiredSize}");
            }

            if (backgroundContainer != null)
            {
                DebugLog("LAYOUT", $"[{reason}] BackgroundContainer: Bounds={backgroundContainer.Bounds}, DesiredSize={backgroundContainer.DesiredSize}");
            }

            if (overlay != null)
            {
                DebugLog("LAYOUT", $"[{reason}] DarkeningOverlay: Bounds={overlay.Bounds}, IsVisible={overlay.IsVisible}");
            }
        }

        public System.Threading.Tasks.Task<SKRectI> GetResultAsync()
        {
            return _tcs.Task;
        }

        // Helper to convert SkiaSharp.SKBitmap to Avalonia Bitmap
        private Bitmap ConvertToAvaloniaBitmap(SkiaSharp.SKBitmap source)
        {
            if (source == null) return null;
            using var image = source.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            using var stream = image.AsStream();
            return new Bitmap(stream);
        }

        protected override async void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            DebugLog("LIFECYCLE", $"OnOpened started (elapsed {_openStopwatch.ElapsedMilliseconds}ms since ctor)");
            DebugLog("WINDOW", $"OnOpened state: RenderScaling={RenderScaling}, Position={Position}, Bounds={Bounds}, ClientSize={ClientSize}");

            if (XerahS.Platform.Abstractions.PlatformServices.IsInitialized)
            {
                var screenService = XerahS.Platform.Abstractions.PlatformServices.Screen;
                _usePerScreenScalingForLayout = screenService?.UsePerScreenScalingForRegionCaptureLayout ?? false;
                _useWindowPositionForFallback = screenService?.UseWindowPositionForRegionCaptureFallback ?? false;
                _useLogicalCoordinatesForCapture = screenService?.UseLogicalCoordinatesForRegionCapture ?? false;

                // Get our own handle to exclude from detection
                _myHandle = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

                // Initialize window list for detection
                if (XerahS.Platform.Abstractions.PlatformServices.Window != null)
                {
                    try
                    {
                        // Fetch windows (Z-ordered) and filter visible ones
                        _windows = XerahS.Platform.Abstractions.PlatformServices.Window.GetAllWindows()
                            .Where(w => w.IsVisible && !IsMyWindow(w))
                            .ToArray();
                        DebugLog("WINDOW", $"Fetched {_windows.Length} visible windows for detection");
                    }
                    catch (Exception ex)
                    {
                        DebugLog("ERROR", $"Failed to fetch windows: {ex.Message}");
                    }
                }
            }

            DebugLog("WINDOW", $"Region capture policy: PerScreenScaling={_usePerScreenScalingForLayout}, UseWindowPositionFallback={_useWindowPositionForFallback}, UseLogicalCoords={_useLogicalCoordinatesForCapture}");

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
                    DebugLog("SCREEN", $"Screen {screenIndex}: RenderScaling={RenderScaling}, ScreenScaling={screen.Scaling}");

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
                DebugLog("WINDOW", $"Post-UpdateWindowSize: Width={Width}, Height={Height}, ClientSize={ClientSize}, RenderScaling={RenderScaling}");

                // Position window at absolute top-left of virtual screen
                Position = new PixelPoint(minX, minY);
                DebugLog("WINDOW", $"Set Position to: {Position}");
                DebugLog("WINDOW", $"Actual Position after set: {Position}, RenderScaling: {RenderScaling}");
                DebugLogLayout("AfterPosition");

                // Store window position for coordinate conversion (platform policy)
                if (_useWindowPositionForFallback)
                {
                    _windowLeft = Position.X;
                    _windowTop = Position.Y;
                }
                else
                {
                    _windowLeft = minX;
                    _windowTop = minY;
                }
                DebugLog("WINDOW", $"Stored window origin for fallback: {_windowLeft},{_windowTop} (Position={Position})");

                // Capture scaling at this point - use 1.0 to match the physical pixel coordinate system
                // used for the overlay layout. RenderScaling can change as the mouse moves between
                // monitors, so we need a stable value.
                _capturedScaling = 1.0;
                DebugLog("WINDOW", $"Captured scaling for coordinate conversion: {_capturedScaling}");

                // Comprehensive DPI troubleshooting logging
                LogEnvironment("RegionCapture");
                LogMonitorInfo("RegionCapture", Screens.All);
                LogVirtualScreenBounds("RegionCapture", minX, minY, maxX, maxY, Width, Height, RenderScaling);

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

                if (_useDarkening && container != null && XerahS.Platform.Abstractions.PlatformServices.IsInitialized)
                {
                    DebugLog("IMAGE", "Enabling background images and darkening (Standard DPI detected)");
                    container.Children.Clear();

                    var backgroundStopwatch = Stopwatch.StartNew();

                    screenIndex = 0;
                    foreach (var screen in Screens.All)
                    {
                        DebugLog("IMAGE", $"--- Processing screen {screenIndex} for background ---");

                        // 1. Capture screen content (Physical)
                        var skScreenRect = new SKRectI(
                            screen.Bounds.X, screen.Bounds.Y,
                            screen.Bounds.X + screen.Bounds.Width, screen.Bounds.Y + screen.Bounds.Height);
                        DebugLog("IMAGE", $"Screen {screenIndex} capture rect: {skScreenRect}");

                        var captureStopwatch = Stopwatch.StartNew();
                        var screenshot = await XerahS.Platform.Abstractions.PlatformServices.ScreenCapture.CaptureRectAsync(skScreenRect);
                        captureStopwatch.Stop();
                        DebugLog("IMAGE", $"Screen {screenIndex} capture duration: {captureStopwatch.ElapsedMilliseconds}ms");

                        if (screenshot != null)
                        {
                            DebugLog("IMAGE", $"Screen {screenIndex} capture result: {screenshot.Width}x{screenshot.Height}");
                            var convertStopwatch = Stopwatch.StartNew();
                            var avaloniaBitmap = ConvertToAvaloniaBitmap(screenshot);
                            convertStopwatch.Stop();
                            DebugLog("IMAGE", $"Screen {screenIndex} bitmap conversion: {convertStopwatch.ElapsedMilliseconds}ms");

                            var imageControl = new Image
                            {
                                Source = avaloniaBitmap,
                                Stretch = Stretch.Fill,
                                Tag = screen
                            };
                            RenderOptions.SetBitmapInterpolationMode(imageControl, BitmapInterpolationMode.HighQuality);

                            double logLeft;
                            double logTop;
                            double logWidth;
                            double logHeight;

                            if (_usePerScreenScalingForLayout)
                            {
                                // Use per-screen scaling for logical sizing on macOS.
                                var layoutScale = Math.Abs(screen.Scaling) < 0.001 ? 1.0 : screen.Scaling;
                                logLeft = (screen.Bounds.X - minX) / layoutScale;
                                logTop = (screen.Bounds.Y - minY) / layoutScale;
                                logWidth = screen.Bounds.Width / layoutScale;
                                logHeight = screen.Bounds.Height / layoutScale;
                                DebugLog("IMAGE", $"Screen {screenIndex} logical target (ScreenScaling {layoutScale}): {logWidth}x{logHeight}");
                            }
                            else
                            {
                                // Default layout uses logical bounds directly.
                                logLeft = screen.Bounds.X - minX;
                                logTop = screen.Bounds.Y - minY;
                                logWidth = screen.Bounds.Width;
                                logHeight = screen.Bounds.Height;
                                DebugLog("IMAGE", $"Screen {screenIndex} logical target (Default): {logWidth}x{logHeight}");
                            }

                            imageControl.Width = logWidth;
                            imageControl.Height = logHeight;
                            Canvas.SetLeft(imageControl, logLeft);
                            Canvas.SetTop(imageControl, logTop);

                            container.Children.Add(imageControl);

                            DebugLog("IMAGE", $"Screen {screenIndex} placed at ({logLeft}, {logTop}) size {logWidth}x{logHeight}");
                            if (!double.IsNaN(Width) && !double.IsNaN(Height) && (logWidth > Width || logHeight > Height))
                            {
                                DebugLog("IMAGE", $"WARNING: Image size exceeds window size. Image={logWidth}x{logHeight}, Window={Width}x{Height}");
                            }

                            screenshot.Dispose();
                        }
                        else
                        {
                            DebugLog("IMAGE", $"Screen {screenIndex} capture returned null");
                        }

                        screenIndex++;
                    }

                    backgroundStopwatch.Stop();
                    DebugLog("IMAGE", $"Background capture total: {backgroundStopwatch.ElapsedMilliseconds}ms");
                    DebugLogLayout("AfterBackgroundCapture");

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

            // Initial pointer move check to highlight window under cursor immediately
            var mousePos = GetGlobalMousePosition();
            UpdateWindowSelection(mousePos);
        }

        private void UpdateWindowSize(int minX, int minY)
        {
            double logicalMinX = double.MaxValue;
            double logicalMinY = double.MaxValue;
            double logicalMaxX = double.MinValue;
            double logicalMaxY = double.MinValue;

            foreach (var screen in Screens.All)
            {
                var offsetX = screen.Bounds.X - minX;
                var offsetY = screen.Bounds.Y - minY;

                double logLeft;
                double logTop;
                double logRight;
                double logBottom;

                if (_usePerScreenScalingForLayout)
                {
                    var scale = Math.Abs(screen.Scaling) < 0.001 ? 1.0 : screen.Scaling;
                    logLeft = offsetX / scale;
                    logTop = offsetY / scale;
                    logRight = logLeft + (screen.Bounds.Width / scale);
                    logBottom = logTop + (screen.Bounds.Height / scale);
                    DebugLog("WINDOW", $"UpdateWindowSize screen: Bounds={screen.Bounds}, ScreenScaling={scale}, LogicalRect=({logLeft},{logTop}) to ({logRight},{logBottom})");
                }
                else
                {
                    logLeft = offsetX;
                    logTop = offsetY;
                    logRight = logLeft + screen.Bounds.Width;
                    logBottom = logTop + screen.Bounds.Height;
                    DebugLog("WINDOW", $"UpdateWindowSize screen: Bounds={screen.Bounds}, LogicalRect=({logLeft},{logTop}) to ({logRight},{logBottom})");
                }

                logicalMinX = Math.Min(logicalMinX, logLeft);
                logicalMinY = Math.Min(logicalMinY, logTop);
                logicalMaxX = Math.Max(logicalMaxX, logRight);
                logicalMaxY = Math.Max(logicalMaxY, logBottom);
            }

            var targetWidth = logicalMaxX - logicalMinX;
            var targetHeight = logicalMaxY - logicalMinY;

            // Set both Width/Height and ClientSize to force proper sizing
            // 2026-01-09 21:50: ClientSize was not updating to match Width/Height on MIKE-NB
            Width = targetWidth;
            Height = targetHeight;
            ClientSize = new Avalonia.Size(targetWidth, targetHeight);

            DebugLog("WINDOW", $"UpdateWindowSize: LogicalBounds=({logicalMinX},{logicalMinY}) to ({logicalMaxX},{logicalMaxY}), Size={Width}x{Height}");
            DebugLog("WINDOW", $"UpdateWindowSize: ClientSize set to {ClientSize.Width}x{ClientSize.Height}");

            // Also explicitly size the SelectionCanvas to ensure it covers the entire virtual screen
            // 2026-01-09 22:30: ClientSize is logical (scaled), so Canvas must also be logical size.
            // If targetWidth is physical (2496), and RenderScaling is 1.25, ClientSize becomes ~2012.
            // We must size Canvas to ~2012 too, otherwise it will be larger than window content area
            // and might be clipped or offset if alignment is Center (default).
            // We forced Top/Left alignment in XAML, but correct sizing is safer.
            
            var selectionCanvas = this.FindControl<Canvas>("SelectionCanvas");
            if (selectionCanvas != null)
            {
                var canvasScale = RenderScaling;
                if (canvasScale < 0.1) canvasScale = 1.0;

                selectionCanvas.Width = targetWidth / canvasScale;
                selectionCanvas.Height = targetHeight / canvasScale;
                DebugLog("CANVAS", $"SelectionCanvas sized to {selectionCanvas.Width}x{selectionCanvas.Height} (Target={targetWidth}x{targetHeight} / Scale={canvasScale})");
            }
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
            _hoveredWindow = null;
        }

        private SKPointI ConvertLogicalToPhysical(Point logicalPos)
        {
            var physicalX = _windowLeft + (int)Math.Round(logicalPos.X * RenderScaling);
            var physicalY = _windowTop + (int)Math.Round(logicalPos.Y * RenderScaling);
            return new SKPointI(physicalX, physicalY);
        }

        private SKPointI ConvertLogicalToScreen(Point logicalPos)
        {
            if (_useLogicalCoordinatesForCapture)
            {
                var screenX = (int)Math.Round(logicalPos.X + Position.X);
                var screenY = (int)Math.Round(logicalPos.Y + Position.Y);
                return new SKPointI(screenX, screenY);
            }

            return ConvertLogicalToPhysical(logicalPos);
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
                    _tcs.TrySetResult(SKRectI.Empty);
                    Close();
                    e.Handled = true;
                }
                return;
            }

            // Handle left-click to start selection
            if (point.Properties.IsLeftButtonPressed)
            {
                // Store logical coordinates for visual rendering (from Avalonia - already correct)
                _startPointLogical = point.Position;
                _dragStarted = false;

                // Store screen coordinates for final screenshot region
                _startPointPhysical = _useLogicalCoordinatesForCapture
                    ? ConvertLogicalToScreen(_startPointLogical)
                    : GetGlobalMousePosition();
                _isSelecting = true;

                DebugLog("MOUSE", $"PointerPressed: Physical={_startPointPhysical}, Logical={_startPointLogical}");
                if (!_useLogicalCoordinatesForCapture && _startPointPhysical.X == 0 && _startPointPhysical.Y == 0)
                {
                    _startPointPhysical = ConvertLogicalToScreen(_startPointLogical);
                    DebugLog("MOUSE", $"PointerPressed: Physical position is (0,0); using fallback from logical -> {_startPointPhysical}");
                }

                // If we are hovering a window, we don't clear visuals yet.
                // We only clear/update visuals if drag exceeds threshold.
                // But if we weren't hovering, we should init selection border (0size).
                if (_hoveredWindow == null)
                {
                    UpdateSelectionVisuals(_startPointLogical, _startPointLogical, _startPointPhysical, _startPointPhysical);
                }
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

            var currentPointLogical = point.Position;
            var currentPointPhysical = _useLogicalCoordinatesForCapture
                ? ConvertLogicalToScreen(currentPointLogical)
                : GetGlobalMousePosition();

            // Handle selection dragging
            if (_isSelecting)
            {
                // Check if drag threshold exceeded
                if (!_dragStarted)
                {
                    var dist = Math.Sqrt(Math.Pow(currentPointLogical.X - _startPointLogical.X, 2) + Math.Pow(currentPointLogical.Y - _startPointLogical.Y, 2));
                    if (dist > DragThreshold)
                    {
                        _dragStarted = true;
                        _hoveredWindow = null; // Drag overrides window selection
                    }
                    else
                    {
                        // Stick to window selection if we haven't dragged far enough
                        return;
                    }
                }

                UpdateSelectionVisuals(_startPointLogical, currentPointLogical, _startPointPhysical, currentPointPhysical);
            }
            else
            {
                // Not selecting - check for window under cursor (Active Window detection)
                UpdateWindowSelection(currentPointPhysical);
            }
        }

        private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (_isSelecting)
            {
                _isSelecting = false;

                // If we didn't drag and we have a hovered window, use that window
                if (!_dragStarted && _hoveredWindow != null)
                {
                    var rect = _hoveredWindow.Bounds;
                    DebugLog("RESULT", $"Selected Window: {rect}");
                    _tcs.TrySetResult(new SKRectI(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height));
                    Close();
                    return;
                }

                // Get final position in physical screen coordinates (from Win32 API)
                var currentPointPhysical = _useLogicalCoordinatesForCapture
                    ? ConvertLogicalToScreen(e.GetCurrentPoint(this).Position)
                    : GetGlobalMousePosition();

                // Fallback: If Win32 API fails (returns 0,0), calculate from Avalonia position
                if (!_useLogicalCoordinatesForCapture && currentPointPhysical.X == 0 && currentPointPhysical.Y == 0)
                {
                    var point = e.GetCurrentPoint(this);
                    var logicalPos = point.Position;

                    // Convert logical to physical using window position and render scaling
                    // Note: This is approximate for mixed-DPI, but better than (0,0)
                    currentPointPhysical = ConvertLogicalToScreen(logicalPos);
                    DebugLog("MOUSE", $"PointerReleased: Win32 API failed, using fallback. Logical={logicalPos}, RenderScaling={RenderScaling}, WindowPos={_windowLeft},{_windowTop}, WindowPosition={Position}, Calculated Physical={currentPointPhysical}");
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
                var resultRect = new SKRectI(x, y, x + width, y + height);

                _tcs.TrySetResult(resultRect);
                Close();
            }
        }

        private IntPtr _myHandle;

        private bool IsMyWindow(XerahS.Platform.Abstractions.WindowInfo w)
        {
            if (_myHandle != IntPtr.Zero && w.Handle == _myHandle) return true;

            // Also exclude windows with empty title and small size (tooltips etc) if desired, 
            // but for now just safely exclude self.
            return false;
        }

        /// <summary>
        /// Gets the DPI scaling factor for the screen containing the specified physical point.
        /// </summary>
        private double GetScalingForPhysicalPoint(int physicalX, int physicalY)
        {
            foreach (var screen in Screens.All)
            {
                // Screen.Bounds is in physical pixels
                if (screen.Bounds.Contains(new PixelPoint(physicalX, physicalY)))
                {
                    return screen.Scaling > 0 ? screen.Scaling : 1.0;
                }
            }

            // Fallback to window's RenderScaling if no screen found
            return RenderScaling;
        }

        private void UpdateWindowSelection(SKPointI mousePos)
        {
            if (_windows == null) return;

            var window = _windows.FirstOrDefault(w => w.Bounds.Contains(mousePos.X, mousePos.Y));

            if (window != null && window != _hoveredWindow)
            {
                _hoveredWindow = window;

                // Convert window bounds (physical) to logical for rendering
                double logicalX, logicalY, logicalW, logicalH;

                // Find containing screen to determine layout offset
                Avalonia.Platform.Screen? containingScreen = null;
                double screenScaling = 1.0;
                int screenIndex = 0;
                int currentIdx = 0;
                
                // Use global RenderScaling for the counter-scaling, checking validity
                var scaling = RenderScaling;
                if (scaling < 0.1) scaling = 1.0;
                
                foreach (var screen in Screens.All)
                {
                    if (screen.Bounds.Contains(new Avalonia.PixelPoint(window.Bounds.X, window.Bounds.Y)))
                    {
                        containingScreen = screen;
                        screenScaling = screen.Scaling;
                        screenIndex = currentIdx;
                        break;
                    }
                    currentIdx++;
                }

                // Match the window layout logic first (Physical Layout)
                if (containingScreen != null)
                {
                    // _windowLeft was initialized to minX in OnOpened (unless fallback used)
                    var screenLayoutOffsetX = containingScreen.Bounds.X - _windowLeft;
                    var screenLayoutOffsetY = containingScreen.Bounds.Y - _windowTop;

                    // Position within the screen
                    var withinScreenX = window.Bounds.X - containingScreen.Bounds.X;
                    var withinScreenY = window.Bounds.Y - containingScreen.Bounds.Y;

                    // "Ideal" layout coordinates (Physical pixels)
                    var idealX = screenLayoutOffsetX + withinScreenX;
                    var idealY = screenLayoutOffsetY + withinScreenY;
                    var idealW = window.Bounds.Width;
                    var idealH = window.Bounds.Height;

                    // CRITICAL FIX (2026-01-09 22:30):
                    // Reverted Hybrid Scaling. Now that SelectionCanvas alignment (Top/Left) and sizing (Logical) 
                    // are fixed, we should go back to using RenderScaling for both Position and Size.
                    
                    logicalX = idealX / scaling;
                    logicalY = idealY / scaling;
                    logicalW = idealW / scaling;
                    logicalH = idealH / scaling;

                    DebugLog("SELECTION", $"Counter-scaling (Retry): Ideal=({idealX},{idealY}) {idealW}x{idealH} / Scaling {scaling} -> Logical=({logicalX},{logicalY})");
                }
                else
                {
                    // Fallback
                    var relativeX = window.Bounds.X - _windowLeft;
                    var relativeY = window.Bounds.Y - _windowTop;
                    
                    logicalX = relativeX / scaling;
                    logicalY = relativeY / scaling;
                    logicalW = window.Bounds.Width / scaling;
                    logicalH = window.Bounds.Height / scaling;
                }

                // Screen index and scaling already determined above for layout


                // Comprehensive selection logging for DPI troubleshooting
                int processId = 0;
                if (XerahS.Platform.Abstractions.PlatformServices.IsInitialized)
                {
                    try { processId = (int)(XerahS.Platform.Abstractions.PlatformServices.Window?.GetWindowProcessId(window.Handle) ?? 0); }
                    catch { }
                }
                LogWindowSelection("RegionCapture",
                    window.Title ?? "",
                    processId,
                    new System.Drawing.Rectangle(window.Bounds.X, window.Bounds.Y, window.Bounds.Width, window.Bounds.Height),
                    scaling,
                    logicalX, logicalY, logicalW, logicalH,
                    screenIndex, screenScaling);

                // Update visuals to match window
                var border = this.FindControl<Rectangle>("SelectionBorder");
                var borderInner = this.FindControl<Rectangle>("SelectionBorderInner");
                var infoText = this.FindControl<TextBlock>("InfoText");
                var overlay = this.FindControl<Path>("DarkeningOverlay");

                if (border != null)
                {
                    border.IsVisible = true;
                    Canvas.SetLeft(border, logicalX);
                    Canvas.SetTop(border, logicalY);
                    border.Width = logicalW;
                    border.Height = logicalH;
                }

                if (borderInner != null)
                {
                    borderInner.IsVisible = true;
                    Canvas.SetLeft(borderInner, logicalX);
                    Canvas.SetTop(borderInner, logicalY);
                    borderInner.Width = logicalW;
                    borderInner.Height = logicalH;
                }

                UpdateDarkeningOverlay(logicalX, logicalY, logicalW, logicalH);

                if (infoText != null)
                {
                    infoText.IsVisible = true;
                    // Format: Title | X: ...
                    var title = !string.IsNullOrEmpty(window.Title) ? window.Title + "\n" : "";
                    infoText.Text = $"{title}X: {window.Bounds.X} Y: {window.Bounds.Y} W: {window.Bounds.Width} H: {window.Bounds.Height}";

                    Canvas.SetLeft(infoText, logicalX);

                    var labelHeight = 45; // slightly larger for title
                    var topPadding = 5;
                    var labelY = logicalY - labelHeight - topPadding;
                    if (labelY < 5) labelY = 5;

                    Canvas.SetTop(infoText, labelY);
                }
            }
            else if (window == null && _hoveredWindow != null)
            {
                // Lost window hover
                _hoveredWindow = null;
                CancelSelection(); // Clears visuals
            }
        }

        private void UpdateSelectionVisuals(Point logicalStart, Point logicalEnd, SKPointI physicalStart, SKPointI physicalEnd)
        {
            var border = this.FindControl<Rectangle>("SelectionBorder");
            var borderInner = this.FindControl<Rectangle>("SelectionBorderInner");
            var infoText = this.FindControl<TextBlock>("InfoText");

            if (border != null)
            {
                // Calculate rect in logical coordinates for visual rendering
                var x = Math.Min(logicalStart.X, logicalEnd.X);
                var y = Math.Min(logicalStart.Y, logicalEnd.Y);
                var width = Math.Abs(logicalStart.X - logicalEnd.X);
                var height = Math.Abs(logicalStart.Y - logicalEnd.Y);

                // Update outer border (white solid)
                border.IsVisible = true;
                Canvas.SetLeft(border, x);
                Canvas.SetTop(border, y);
                border.Width = width;
                border.Height = height;

                // Update inner border (black dashed - static marching ants pattern)
                if (borderInner != null)
                {
                    borderInner.IsVisible = true;
                    Canvas.SetLeft(borderInner, x);
                    Canvas.SetTop(borderInner, y);
                    borderInner.Width = width;
                    borderInner.Height = height;
                }

                // Update darkening overlay to cut out the selection area
                UpdateDarkeningOverlay(x, y, width, height);

                var physicalX = Math.Min(physicalStart.X, physicalEnd.X);
                var physicalY = Math.Min(physicalStart.Y, physicalEnd.Y);
                var physicalWidth = Math.Abs(physicalStart.X - physicalEnd.X);
                var physicalHeight = Math.Abs(physicalStart.Y - physicalEnd.Y);

                if (infoText != null)
                {
                    infoText.IsVisible = true;
                    infoText.Text = $"X: {physicalX} Y: {physicalY} W: {physicalWidth} H: {physicalHeight}";

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
        // Local Logging Helpers (Moved from TroubleshootingHelper to avoid Core->Avalonia dependency)
        
        [Conditional("DEBUG")]
        private void LogWindowSelection(string category, string windowTitle, int processId, System.Drawing.Rectangle physicalBounds, double renderScaling, double logicalX, double logicalY, double logicalW, double logicalH, int screenIndex, double screenScaling)
        {
            TroubleshootingHelper.Log(category, "SELECTION", $"Window: \"{TruncateString(windowTitle, 30)}\" (PID={processId})");
            TroubleshootingHelper.Log(category, "SELECTION", $"  Physical: ({physicalBounds.X},{physicalBounds.Y}) {physicalBounds.Width}x{physicalBounds.Height}");
            TroubleshootingHelper.Log(category, "SELECTION", $"  Overlay RenderScaling: {renderScaling:F3}");
            TroubleshootingHelper.Log(category, "SELECTION", $"  Screen[{screenIndex}] Scaling: {screenScaling:F3}");
            
            if (Math.Abs(renderScaling - screenScaling) > 0.001)
            {
                 TroubleshootingHelper.Log(category, "WARNING", $"  ** SCALING MISMATCH: Overlay={renderScaling:F3} vs PerMonitor={screenScaling:F3} **");
            }
            else
            {
                 TroubleshootingHelper.Log(category, "SELECTION", $"  Per-monitor DPI scale: {screenScaling:F3}");
            }

            TroubleshootingHelper.Log(category, "SELECTION", $"  Computed logical: ({logicalX:F1},{logicalY:F1}) {logicalW:F1}x{logicalH:F1}");
            TroubleshootingHelper.Log(category, "SELECTION", $"  Alt (per-monitor): ({physicalBounds.X / screenScaling:F1},?) {physicalBounds.Width / screenScaling:F1}x?");
        }

        [Conditional("DEBUG")]
        private void LogEnvironment(string category)
        {
            TroubleshootingHelper.Log(category, "ENVIRONMENT", "=== Environment Details ===");
            TroubleshootingHelper.Log(category, "ENVIRONMENT", $"Machine: {Environment.MachineName}");
            TroubleshootingHelper.Log(category, "ENVIRONMENT", $"User: {Environment.UserName}");
            TroubleshootingHelper.Log(category, "ENVIRONMENT", $"OS: {Environment.OSVersion}");
            TroubleshootingHelper.Log(category, "ENVIRONMENT", $".NET: {Environment.Version}");
            TroubleshootingHelper.Log(category, "ENVIRONMENT", $"Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
            TroubleshootingHelper.Log(category, "ENVIRONMENT", $"Process Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
            TroubleshootingHelper.Log(category, "ENVIRONMENT", "DPI Awareness: (check app manifest for dpiAwareness setting)");
            TroubleshootingHelper.Log(category, "ENVIRONMENT", $"Log time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff zzz}");
        }

        [Conditional("DEBUG")]
        private void LogMonitorInfo(string category, System.Collections.Generic.IEnumerable<Avalonia.Platform.Screen> screens)
        {
            TroubleshootingHelper.Log(category, "MONITORS", "=== Monitor Configuration ===");
            int i = 0;
            foreach (var s in screens)
            {
                var dpi = s.Scaling * 96.0; 
                TroubleshootingHelper.Log(category, "MONITORS", $"Screen {i}: Bounds={s.Bounds}, IsPrimary={s.IsPrimary}, Avalonia.Scaling={s.Scaling:F3}, Win32.DPI={dpi:F0}x{dpi:F0} (Scale={s.Scaling:F3})");
                i++;
            }
            TroubleshootingHelper.Log(category, "MONITORS", $"Total monitors: {i}");
        }
        
        [Conditional("DEBUG")]
        private void LogVirtualScreenBounds(string category, int minX, int minY, int maxX, int maxY, double overlayWidth, double overlayHeight, double renderScaling)
        {
            TroubleshootingHelper.Log(category, "VIRTUAL", "=== Virtual Screen Bounds ===");
            TroubleshootingHelper.Log(category, "VIRTUAL", $"Virtual screen: ({minX},{minY}) to ({maxX},{maxY})");
            TroubleshootingHelper.Log(category, "VIRTUAL", $"Virtual size: {maxX - minX}x{maxY - minY} px");
            TroubleshootingHelper.Log(category, "VIRTUAL", $"Overlay size: {overlayWidth:F0}x{overlayHeight:F0} logical");
            TroubleshootingHelper.Log(category, "VIRTUAL", $"Overlay RenderScaling: {renderScaling:F3}");
            
            var expectedW = (maxX - minX) / renderScaling;
            var expectedH = (maxY - minY) / renderScaling;
            if (Math.Abs(expectedW - overlayWidth) > 2 || Math.Abs(expectedH - overlayHeight) > 2)
            {
                TroubleshootingHelper.Log(category, "WARNING", $"  ** OVERLAY SIZE MISMATCH: Expected {expectedW:F0}x{expectedH:F0}, Got {overlayWidth:F0}x{overlayHeight:F0} (diff: {overlayWidth-expectedW:F0}x{overlayHeight-expectedH:F0}) **");
            }
        }

        private static string TruncateString(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }
    }
}
