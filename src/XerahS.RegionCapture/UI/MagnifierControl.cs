using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using XerahS.RegionCapture.Models;
using AvPixelRect = Avalonia.PixelRect;
using AvPixelPoint = Avalonia.PixelPoint;
using PixelRect = XerahS.RegionCapture.Models.PixelRect;
using PixelPoint = XerahS.RegionCapture.Models.PixelPoint;

namespace XerahS.RegionCapture.UI;

/// <summary>
/// A magnifying glass control that shows a zoomed view of the area around the cursor
/// for pixel-perfect precision during region selection.
/// </summary>
public sealed class MagnifierControl : Control
{
    private const int DefaultZoomLevel = 4;
    private const int MagnifierSize = 120; // Size in logical pixels
    private const int PixelGridSize = 15; // Number of pixels to show (odd for center pixel)
    private const int CrosshairOffset = 1;

    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 0.5);
    private static readonly IPen CrosshairPen = new Pen(Brushes.Red, 1.5);
    private static readonly IPen BorderPen = new Pen(Brushes.White, 2);
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.FromArgb(230, 30, 30, 30));

    private WriteableBitmap? _capturedPixels;
    private PixelPoint _cursorPosition;
    private Color _centerPixelColor = Colors.Transparent;
    private readonly int _zoomLevel;

    public MagnifierControl(int zoomLevel = DefaultZoomLevel)
    {
        _zoomLevel = Math.Max(1, zoomLevel);
        Width = MagnifierSize;
        Height = MagnifierSize + 25; // Extra space for color info
        IsHitTestVisible = false;
    }

    /// <summary>
    /// Updates the magnifier with the current cursor position.
    /// </summary>
    public void UpdatePosition(PixelPoint physicalCursorPos)
    {
        _cursorPosition = physicalCursorPos;
        InvalidateVisual();
    }

    /// <summary>
    /// Updates the captured pixel data for the magnifier.
    /// </summary>
    public void UpdatePixels(WriteableBitmap? bitmap, Color centerColor)
    {
        _capturedPixels = bitmap;
        _centerPixelColor = centerColor;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var magnifierRect = new Rect(0, 0, MagnifierSize, MagnifierSize);

        // Draw background
        context.DrawRectangle(BackgroundBrush, BorderPen, magnifierRect, 4, 4);

        // Draw captured pixels (scaled up)
        if (_capturedPixels is not null)
        {
            var sourceRect = new Rect(0, 0, _capturedPixels.PixelSize.Width, _capturedPixels.PixelSize.Height);
            var destRect = new Rect(2, 2, MagnifierSize - 4, MagnifierSize - 4);

            // Use nearest-neighbor scaling for crisp pixels
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
            {
                context.DrawImage(_capturedPixels, sourceRect, destRect);
            }
        }

        // Draw pixel grid
        DrawPixelGrid(context, magnifierRect);

        // Draw crosshair at center
        DrawCrosshair(context, magnifierRect);

        // Draw color info below magnifier
        DrawColorInfo(context, MagnifierSize);
    }

    private void DrawPixelGrid(DrawingContext context, Rect bounds)
    {
        var pixelSize = (bounds.Width - 4) / PixelGridSize;
        var startX = 2.0;
        var startY = 2.0;

        // Vertical lines
        for (int i = 0; i <= PixelGridSize; i++)
        {
            var x = startX + i * pixelSize;
            context.DrawLine(GridPen, new Point(x, startY), new Point(x, bounds.Height - 2));
        }

        // Horizontal lines
        for (int i = 0; i <= PixelGridSize; i++)
        {
            var y = startY + i * pixelSize;
            context.DrawLine(GridPen, new Point(startX, y), new Point(bounds.Width - 2, y));
        }
    }

    private void DrawCrosshair(DrawingContext context, Rect bounds)
    {
        var pixelSize = (bounds.Width - 4) / PixelGridSize;
        var centerIndex = PixelGridSize / 2;

        var centerX = 2 + (centerIndex + 0.5) * pixelSize;
        var centerY = 2 + (centerIndex + 0.5) * pixelSize;

        // Highlight center pixel with a box
        var highlightRect = new Rect(
            2 + centerIndex * pixelSize,
            2 + centerIndex * pixelSize,
            pixelSize,
            pixelSize);

        context.DrawRectangle(null, CrosshairPen, highlightRect);

        // Draw crosshair lines extending from center
        var extension = pixelSize * 2;

        // Left
        context.DrawLine(CrosshairPen,
            new Point(highlightRect.Left - extension, centerY),
            new Point(highlightRect.Left - 2, centerY));

        // Right
        context.DrawLine(CrosshairPen,
            new Point(highlightRect.Right + 2, centerY),
            new Point(highlightRect.Right + extension, centerY));

        // Top
        context.DrawLine(CrosshairPen,
            new Point(centerX, highlightRect.Top - extension),
            new Point(centerX, highlightRect.Top - 2));

        // Bottom
        context.DrawLine(CrosshairPen,
            new Point(centerX, highlightRect.Bottom + 2),
            new Point(centerX, highlightRect.Bottom + extension));
    }

    private void DrawColorInfo(DrawingContext context, double yOffset)
    {
        // Draw color swatch
        var swatchRect = new Rect(4, yOffset + 4, 16, 16);
        var swatchBrush = new SolidColorBrush(_centerPixelColor);
        context.DrawRectangle(swatchBrush, new Pen(Brushes.White, 1), swatchRect);

        // Draw hex color value
        var hexColor = $"#{_centerPixelColor.R:X2}{_centerPixelColor.G:X2}{_centerPixelColor.B:X2}";
        var formattedText = new FormattedText(
            hexColor,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas", FontStyle.Normal, FontWeight.Normal),
            11,
            Brushes.White);

        context.DrawText(formattedText, new Point(24, yOffset + 6));

        // Draw RGB values
        var rgbText = $"R:{_centerPixelColor.R} G:{_centerPixelColor.G} B:{_centerPixelColor.B}";
        var rgbFormatted = new FormattedText(
            rgbText,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas", FontStyle.Normal, FontWeight.Normal),
            9,
            new SolidColorBrush(Color.FromRgb(180, 180, 180)));

        context.DrawText(rgbFormatted, new Point(4, yOffset + 6 + formattedText.Height));
    }
}
