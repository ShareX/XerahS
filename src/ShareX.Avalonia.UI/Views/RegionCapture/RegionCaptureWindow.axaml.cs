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
                
                // Calculate final rect in logical pixels
                var x = Math.Min(_startPoint.X, currentPoint.X);
                var y = Math.Min(_startPoint.Y, currentPoint.Y);
                var width = Math.Abs(_startPoint.X - currentPoint.X);
                var height = Math.Abs(_startPoint.Y - currentPoint.Y);

                // Get render scaling (DPI)
                var scaling = this.RenderScaling;

                // Convert to physical pixels for screen capture
                var physX = (int)(x * scaling);
                var physY = (int)(y * scaling);
                var physWidth = (int)(width * scaling);
                var physHeight = (int)(height * scaling);

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
