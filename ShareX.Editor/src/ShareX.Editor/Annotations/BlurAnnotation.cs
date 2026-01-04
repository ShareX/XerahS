using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using System.IO;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Blur annotation - applies blur to the region
/// </summary>
public class BlurAnnotation : BaseEffectAnnotation
{
    // EffectBitmap inherited from Base
    
    public BlurAnnotation()
    {
        ToolType = EditorTool.Blur;
        StrokeColor = "#00000000"; // Transparent border
        StrokeWidth = 0;
        Amount = 10; // Default blur radius
    }

    public override void Render(DrawingContext context)
    {
        var rect = GetBounds();

        if (EffectBitmap != null)
        {
            // Draw the pre-calculated blurred image
            context.DrawImage(EffectBitmap, rect);
        }
        else
        {
            // Fallback: draw translucent placeholder if effect not generated yet
            context.DrawRectangle(new SolidColorBrush(Colors.Gray, 0.3), null, rect);
        }

        // Draw selection border if selected
        if (IsSelected)
        {
            var pen = new Pen(new SolidColorBrush(Colors.DodgerBlue), 2);
            context.DrawRectangle(null, pen, rect);
        }
    }

    /// <summary>
    /// Update the internal blurred bitmap based on the source image
    /// </summary>
    /// <param name="source">The full source image (SKBitmap)</param>
    public override void UpdateEffect(SKBitmap source)
    {
        if (source == null) return;
        
        var rect = GetBounds();
        if (rect.Width <= 0 || rect.Height <= 0) return;

        // Convert Rect to SKRectI (integers)
        var skRect = new SKRectI((int)rect.X, (int)rect.Y, (int)rect.Right, (int)rect.Bottom);
        
        // Ensure bounds are valid
        skRect.Intersect(new SKRectI(0, 0, source.Width, source.Height));
        
        if (skRect.Width <= 0 || skRect.Height <= 0) return;

        // Crop the region
        using var crop = new SKBitmap(skRect.Width, skRect.Height);
        source.ExtractSubset(crop, skRect);

        // Apply Blur
        // Note: ImageEffectsProcessing.ApplyBlur usually takes the whole image. 
        // We can just blur this crop.
        var blurRadius = (int)Amount;
        
        // Use SkiaSharp directly for the crop since helper might assume full image context
        using var surface = SKSurface.Create(new SKImageInfo(crop.Width, crop.Height));
        using var canvas = surface.Canvas;
        using var paint = new SKPaint();
        paint.ImageFilter = SKImageFilter.CreateBlur(blurRadius, blurRadius);
        
        canvas.DrawBitmap(crop, 0, 0, paint);
        
        using var blurredImage = surface.Snapshot();
        using var resultBitmap = SKBitmap.FromImage(blurredImage);

        // Convert to Avalonia Bitmap
        EffectBitmap?.Dispose();
        EffectBitmap = ToAvaloniaBitmap(resultBitmap);
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
