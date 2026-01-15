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
        // State Machine
        public enum RegionCaptureState { Idle, DragSelecting, Selected, Adjusting }
        private RegionCaptureState _state = RegionCaptureState.Idle;

        // UI Cache
        private Avalonia.Controls.Shapes.Line? _crosshairH;
        private Avalonia.Controls.Shapes.Line? _crosshairV;
        private Canvas? _resizeHandlesCanvas;
        private StackPanel? _infoStack;
        private Image? _magnifierImage;
        private TextBlock? _infoText;

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

        // Current physical selection
        private SKRectI _currentSelectionPhysical;

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

        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Escape)
            {
                OnCancel();
            }
            else if (e.Key == Key.Enter)
            {
                ConfirmSelection();
            }
            else if (e.Key >= Key.D1 && e.Key <= Key.D9)
            {
                SelectMonitor(e.Key - Key.D1);
            }
            else if (e.Key == Key.D0)
            {
                SelectMonitor(9); // 0 = 10th monitor
            }
            else if (e.Key == Key.OemTilde)
            {
                SelectActiveMonitor();
            }
            
            // Nudge logic can differ based on modifiers
        }

        private void OnCancel()
        {
            TroubleshootingHelper.Log("RegionCapture", "INPUT", "Cancelled via Keyboard/Mouse");
            _tcs.TrySetResult(SKRectI.Empty);
            Close();
        }

        private void OnRightClick()
        {
            if (_state == RegionCaptureState.DragSelecting || _state == RegionCaptureState.Selected)
            {
                // Reset to Idle
                _state = RegionCaptureState.Idle;
                
                // Clear Visuals
                if (_resizeHandlesCanvas != null) _resizeHandlesCanvas.IsVisible = false;
                CancelSelection(); // Existing method clearing border/darkening
                
                // Update text
                if (_infoText != null) _infoText.IsVisible = false;
                if (_infoStack != null) _infoStack.IsVisible = false; // magnifying glass
                
                TroubleshootingHelper.Log("RegionCapture", "STATE", "Right click -> Reset to Idle");
            }
            else
            {
                // If Idle, exit
                OnCancel();
            }
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
        private Bitmap? ConvertToAvaloniaBitmap(SkiaSharp.SKBitmap source)
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

            // Cache UI elements
            _crosshairH = this.FindControl<Avalonia.Controls.Shapes.Line>("CrosshairHorizontal");
            _crosshairV = this.FindControl<Avalonia.Controls.Shapes.Line>("CrosshairVertical");
            _resizeHandlesCanvas = this.FindControl<Canvas>("ResizeHandlesCanvas");
            _infoStack = this.FindControl<StackPanel>("InfoStack");
            _magnifierImage = this.FindControl<Image>("MagnifierImage");
            _infoText = this.FindControl<TextBlock>("InfoText");

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
            UpdateWindowSelectionNew(mousePos);
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
            fullScreenFigure.Segments ??= new PathSegments();
            fullScreenFigure.Segments.Add(new LineSegment { Point = new Point(Width, 0) });
            fullScreenFigure.Segments.Add(new LineSegment { Point = new Point(Width, Height) });
            fullScreenFigure.Segments.Add(new LineSegment { Point = new Point(0, Height) });
            darkeningGeometry.Figures ??= new PathFigures();
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
            outerFigure.Segments ??= new PathSegments();
            outerFigure.Segments.Add(new LineSegment { Point = new Point(Width, 0) });
            outerFigure.Segments.Add(new LineSegment { Point = new Point(Width, Height) });
            outerFigure.Segments.Add(new LineSegment { Point = new Point(0, Height) });
            darkeningGeometry.Figures ??= new PathFigures();
            darkeningGeometry.Figures.Add(outerFigure);

            // Inner rectangle: selection area (this creates the "hole")
            var innerFigure = new PathFigure
            {
                StartPoint = new Point(selX, selY),
                IsClosed = true
            };
            innerFigure.Segments ??= new PathSegments();
            innerFigure.Segments.Add(new LineSegment { Point = new Point(selX + selWidth, selY) });
            innerFigure.Segments.Add(new LineSegment { Point = new Point(selX + selWidth, selY + selHeight) });
            innerFigure.Segments.Add(new LineSegment { Point = new Point(selX, selY + selHeight) });
            darkeningGeometry.Figures.Add(innerFigure);

            overlay.Data = darkeningGeometry;
        }

        private void CancelSelection()
        {
            // Reset canvas
            var captureCanvas = this.FindControl<RegionCaptureCanvas>("CaptureCanvas");
            if (captureCanvas != null)
            {
                captureCanvas.UpdateSelection(SKRectI.Empty, false);
                captureCanvas.SetDarkening(_useDarkening);
            }

            var infoText = this.FindControl<TextBlock>("InfoText");
            if (infoText != null)
            {
                infoText.IsVisible = false;
            }

            // Reset selection state
            _hoveredWindow = null;
        }

        /* Obsolete: Handled by RegionCaptureCanvas
        private void InitializeFullScreenDarkening() { ... }
        private void UpdateDarkeningOverlay(...) { ... }
        */


        private void OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                OnRightClick();
                return;
            }
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
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Dispose new backend if initialized
            DisposeNewBackend();
        }
        
        private void UpdateCrosshair(Point p)
        {
            if (_crosshairH == null || _crosshairV == null) return;
            
            _crosshairH.StartPoint = new Point(0, p.Y);
            _crosshairH.EndPoint = new Point(Width, p.Y);
            _crosshairH.IsVisible = true;

            _crosshairV.StartPoint = new Point(p.X, 0);
            _crosshairV.EndPoint = new Point(p.X, Height);
            _crosshairV.IsVisible = true;
        }

        private void UpdateMagnifierPosition(Point p)
        {
            if (_infoStack == null) return;
            
            _infoStack.IsVisible = true;
            
            // Offset from cursor
            double x = p.X + 25;
            double y = p.Y + 25;

            // Boundary check
            if (x + _infoStack.Bounds.Width > Width) x = p.X - _infoStack.Bounds.Width - 25;
            if (y + _infoStack.Bounds.Height > Height) y = p.Y - _infoStack.Bounds.Height - 25;
            
            // Fallback if top/left is negative
            if (x < 0) x = 0;
            if (y < 0) y = 0;

            Canvas.SetLeft(_infoStack, x);
            Canvas.SetTop(_infoStack, y);
        }
    }
}
