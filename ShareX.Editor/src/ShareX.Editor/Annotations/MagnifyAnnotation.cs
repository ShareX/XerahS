using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using System.IO;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Magnify annotation - zooms into the area
/// </summary>
public class MagnifyAnnotation : BaseEffectAnnotation
{
    // EffectBitmap inherited from Base

    public MagnifyAnnotation()
    {
        ToolType = EditorTool.Magnify;
        StrokeColor = "#FF000000"; // Black border
        StrokeWidth = 2;
        Amount = 2.0; // Zoom level (2x)
    }

    public override void Render(DrawingContext context)
    {
        var rect = GetBounds();

        if (EffectBitmap != null)
        {
            // Draw magnified content
            context.DrawImage(EffectBitmap, rect);
        }
        else
        {
            // Fallback
            context.DrawRectangle(new SolidColorBrush(Colors.LightGray, 0.5), null, rect);
        }

        // Always draw border for magnifier
        var pen = CreatePen();
        context.DrawRectangle(null, pen, rect);

        if (IsSelected)
        {
            var selectionPen = new Pen(new SolidColorBrush(Colors.DodgerBlue), 2);
            context.DrawRectangle(null, selectionPen, rect);
        }
    }

    public override void UpdateEffect(SKBitmap source)
    {
        if (source == null) return;
        
        var rect = GetBounds();
        if (rect.Width <= 0 || rect.Height <= 0) return;

        // For magnification, we capture a SMALLER area from the center of the rect
        // and scale it UP to fill the rect.
        
        double zoom = Math.Max(1.0, Amount);
        double captureWidth = rect.Width / zoom;
        double captureHeight = rect.Height / zoom;
        
        double centerX = rect.Center.X;
        double centerY = rect.Center.Y;
        
        double captureX = centerX - (captureWidth / 2);
        double captureY = centerY - (captureHeight / 2);
        
        var captureRect = new SKRectI((int)captureX, (int)captureY, (int)(captureX + captureWidth), (int)(captureY + captureHeight));
        
        // Ensure bounds validation
        // We probably want to clamp to image bounds
        var sourceBounds = new SKRectI(0, 0, source.Width, source.Height);
        
        // If capture rect is outside, intersection handles it
        var actualCapture = captureRect;
        actualCapture.Intersect(sourceBounds);
        
        if (actualCapture.Width <= 0 || actualCapture.Height <= 0) return;

        using var crop = new SKBitmap(actualCapture.Width, actualCapture.Height);
        source.ExtractSubset(crop, actualCapture);
        
        // Now scale it up to fill the full rect
        var info = new SKImageInfo((int)rect.Width, (int)rect.Height);
        using var scaled = crop.Resize(info, SKFilterQuality.High);

        EffectBitmap?.Dispose();
        EffectBitmap = ToAvaloniaBitmap(scaled);
    }

    private Bitmap ToAvaloniaBitmap(SKBitmap skBitmap)
    {
        using var image = SKImage.FromBitmap(skBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var memoryStream = new MemoryStream();
        data.SaveTo(memoryStream);
        memoryStream.Position = 0;
        return new Bitmap(memoryStream);
    }
}
