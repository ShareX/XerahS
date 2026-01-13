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

            // Write comprehensive diagnostics log for multi-monitor/DPI troubleshooting
            try
            {
                var diagnosticsPath = CaptureDebugHelper.WriteRegionCaptureDiagnostics(XerahS.Common.PathsManager.PersonalFolder);
                if (!string.IsNullOrEmpty(diagnosticsPath))
                {
                    TroubleshootingHelper.Log("RegionCapture", "DIAGNOSTICS", $"Capture diagnostics written to: {diagnosticsPath}");
                }
            }
            catch
            {
                // Never crash Region Capture due to diagnostics
            }

            TroubleshootingHelper.Log("RegionCapture", "INIT", "RegionCaptureWindow created");
            TroubleshootingHelper.Log("RegionCapture", "INIT", $"Initial state: RenderScaling={RenderScaling}, Position={Position}, Bounds={Bounds}, ClientSize={ClientSize}");

            // Try to initialize new backend
            if (TryInitializeNewBackend())
            {
                TroubleshootingHelper.Log("RegionCapture", "INIT", "New capture backend enabled");
            }
            else
            {
                TroubleshootingHelper.Log("RegionCapture", "INIT", "Using legacy capture backend");
            }

            // Close on Escape key
            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    TroubleshootingHelper.Log("RegionCapture", "INPUT", "Escape key pressed - cancelling");
                    _tcs.TrySetResult(SKRectI.Empty);
                    Close();
                }
            };

            // Also handle right-click to force close for debugging
            this.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
                {
                    TroubleshootingHelper.Log("RegionCapture", "INPUT", "Right-click detected - force closing for debug");
                    _tcs.TrySetResult(SKRectI.Empty);
                    Close();
                }
            };
        }

        private void DebugLogLayout(string reason)
        {
            var selectionCanvas = this.FindControl<Canvas>("SelectionCanvas");
            var backgroundContainer = this.FindControl<Canvas>("BackgroundContainer");
            var overlay = this.FindControl<Path>("DarkeningOverlay");

            TroubleshootingHelper.Log("RegionCapture", "LAYOUT", $"[{reason}] Window: Bounds={Bounds}, ClientSize={ClientSize}, Width={Width} Height={Height}, RenderScaling={RenderScaling}, Position={Position}");

            if (selectionCanvas != null)
            {
                TroubleshootingHelper.Log("RegionCapture", "LAYOUT", $"[{reason}] SelectionCanvas: Bounds={selectionCanvas.Bounds}, DesiredSize={selectionCanvas.DesiredSize}");
            }

            if (backgroundContainer != null)
            {
                TroubleshootingHelper.Log("RegionCapture", "LAYOUT", $"[{reason}] BackgroundContainer: Bounds={backgroundContainer.Bounds}, DesiredSize={backgroundContainer.DesiredSize}");
            }

            if (overlay != null)
            {
                TroubleshootingHelper.Log("RegionCapture", "LAYOUT", $"[{reason}] DarkeningOverlay: Bounds={overlay.Bounds}, IsVisible={overlay.IsVisible}");
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

            TroubleshootingHelper.Log("RegionCapture", "LIFECYCLE", $"OnOpened started (elapsed {_openStopwatch.ElapsedMilliseconds}ms since ctor)");
            TroubleshootingHelper.Log("RegionCapture", "WINDOW", $"OnOpened state: RenderScaling={RenderScaling}, Position={Position}, Bounds={Bounds}, ClientSize={ClientSize}");

            // Get our own handle to exclude from window detection
            _myHandle = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

            // Initialize window list for detection
            if (XerahS.Platform.Abstractions.PlatformServices.IsInitialized &&
                XerahS.Platform.Abstractions.PlatformServices.Window != null)
            {
                try
                {
                    _windows = XerahS.Platform.Abstractions.PlatformServices.Window.GetAllWindows()
                        .Where(w => w.IsVisible && !IsMyWindow(w))
                        .ToArray();
                    TroubleshootingHelper.Log("RegionCapture", "WINDOW", $"Fetched {_windows.Length} visible windows for detection");
                }
                catch (Exception ex)
                {
                    TroubleshootingHelper.Log("RegionCapture", "ERROR", $"Failed to fetch windows: {ex.Message}");
                }
            }

            // Use new backend for positioning
            PositionWindowWithNewBackend();

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
                    TroubleshootingHelper.Log("RegionCapture", "WINDOW", $"UpdateWindowSize screen: Bounds={screen.Bounds}, ScreenScaling={scale}, LogicalRect=({logLeft},{logTop}) to ({logRight},{logBottom})");
                }
                else
                {
                    logLeft = offsetX;
                    logTop = offsetY;
                    logRight = logLeft + screen.Bounds.Width;
                    logBottom = logTop + screen.Bounds.Height;
                    TroubleshootingHelper.Log("RegionCapture", "WINDOW", $"UpdateWindowSize screen: Bounds={screen.Bounds}, LogicalRect=({logLeft},{logTop}) to ({logRight},{logBottom})");
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

            TroubleshootingHelper.Log("RegionCapture", "WINDOW", $"UpdateWindowSize: LogicalBounds=({logicalMinX},{logicalMinY}) to ({logicalMaxX},{logicalMaxY}), Size={Width}x{Height}");
            TroubleshootingHelper.Log("RegionCapture", "WINDOW", $"UpdateWindowSize: ClientSize set to {ClientSize.Width}x{ClientSize.Height}");

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
                TroubleshootingHelper.Log("RegionCapture", "CANVAS", $"SelectionCanvas sized to {selectionCanvas.Width}x{selectionCanvas.Height} (Target={targetWidth}x{targetHeight} / Scale={canvasScale})");
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
            OnPointerPressedNew(e);
        }

        private void OnPointerMoved(object sender, PointerEventArgs e)
        {
            OnPointerMovedNew(e);
        }

        private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            OnPointerReleasedNew(e);
        }

        private IntPtr _myHandle;

        private bool IsMyWindow(XerahS.Platform.Abstractions.WindowInfo w)
        {
            if (_myHandle != IntPtr.Zero && w.Handle == _myHandle) return true;

            // Also exclude windows with empty title and small size (tooltips etc) if desired,
            // but for now just safely exclude self.
            return false;
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

                    TroubleshootingHelper.Log("RegionCapture", "SELECTION", $"Counter-scaling (Retry): Ideal=({idealX},{idealY}) {idealW}x{idealH} / Scaling {scaling} -> Logical=({logicalX},{logicalY})");
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
                TroubleshootingHelper.LogWindowSelection("RegionCapture",
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

        private static string TruncateString(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Dispose new backend if initialized
            DisposeNewBackend();
        }
    }
}
