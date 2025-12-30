using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System.Drawing; // For Rectangle return type
using Point = Avalonia.Point;

namespace ShareX.Avalonia.UI.Views.RegionCapture
{
    public partial class RegionCaptureWindow : Window
    {
        private Point _startPoint;
        private bool _isSelecting;
        
        // Result task completion source to return value to caller
        private readonly System.Threading.Tasks.TaskCompletionSource<System.Drawing.Rectangle> _tcs;

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

                // Position and Size are in physical pixels (?) or logical?
                // Window.Position is PixelPoint (physical).
                // Window.Width/Height are logical (double).
                
                // Set Position (Physical)
                Position = new PixelPoint(minX, minY);

                // Set Size (Logical)
                // We need to convert physical size to logical size based on scaling.
                // Assuming uniform scaling or taking scaling of primary? 
                // This is complex with mixed DPI. 
                // For now, let's try setting Width/Height based on RenderScaling (approximated).
                // Or use PlatformImpl to set bounds?
                
                // For a safe start, we just use the raw pixels divided by current scaling
                // This might be slightly off on mixed DPI, but better than single monitor.
                Width = totalWidth / RenderScaling;
                Height = totalHeight / RenderScaling;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (point.Properties.IsLeftButtonPressed)
            {
                _startPoint = point.Position;
                _isSelecting = true;
                
                var border = this.FindControl<Border>("SelectionBorder");
                if (border != null)
                {
                    border.IsVisible = true;
                    Canvas.SetLeft(border, _startPoint.X);
                    Canvas.SetTop(border, _startPoint.Y);
                    border.Width = 0;
                    border.Height = 0;
                }
            }
        }

        private void OnPointerMoved(object sender, PointerEventArgs e)
        {
            if (!_isSelecting) return;

            var currentPoint = e.GetCurrentPoint(this).Position;
            var border = this.FindControl<Border>("SelectionBorder");
            var infoText = this.FindControl<TextBlock>("InfoText");

            if (border != null)
            {
                // Calculate rect
                var x = Math.Min(_startPoint.X, currentPoint.X);
                var y = Math.Min(_startPoint.Y, currentPoint.Y);
                var width = Math.Abs(_startPoint.X - currentPoint.X);
                var height = Math.Abs(_startPoint.Y - currentPoint.Y);

                Canvas.SetLeft(border, x);
                Canvas.SetTop(border, y);
                border.Width = width;
                border.Height = height;
                
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
