using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using System.IO;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Pixelate annotation - applies pixelation to the region
/// </summary>
public class PixelateAnnotation : BaseEffectAnnotation
{
    // EffectBitmap inherited from Base

    public PixelateAnnotation()
    {
        ToolType = EditorTool.Pixelate;
        StrokeColor = "#00000000";
        StrokeWidth = 0;
        Amount = 10; // Default pixel size
    }

    public override void Render(DrawingContext context)
    {
        var rect = GetBounds();

        if (EffectBitmap != null)
        {
            context.DrawImage(EffectBitmap, rect);
        }
        else
        {
            context.DrawRectangle(new SolidColorBrush(Colors.Gray, 0.3), null, rect);
        }

        if (IsSelected)
        {
            var pen = new Pen(new SolidColorBrush(Colors.DodgerBlue), 2);
            context.DrawRectangle(null, pen, rect);
        }
    }

    public override void UpdateEffect(SKBitmap source)
    {
        if (source == null) return;
        
        var rect = GetBounds();
        var skRect = new SKRectI((int)rect.X, (int)rect.Y, (int)rect.Right, (int)rect.Bottom);
        skRect.Intersect(new SKRectI(0, 0, source.Width, source.Height));
        
        if (skRect.Width <= 0 || skRect.Height <= 0) return;

        using var crop = new SKBitmap(skRect.Width, skRect.Height);
        source.ExtractSubset(crop, skRect);

        // Pixelate logic: Downscale then upscale
        var pixelSize = (int)Math.Max(1, Amount);
        int w = Math.Max(1, crop.Width / pixelSize);
        int h = Math.Max(1, crop.Height / pixelSize);

        var info = new SKImageInfo(w, h);
        using var small = crop.Resize(info, SKFilterQuality.None);
        
        info = new SKImageInfo(crop.Width, crop.Height);
        using var result = small.Resize(info, SKFilterQuality.None); // Nearest neighbor upscale

        EffectBitmap?.Dispose();
        EffectBitmap = ToAvaloniaBitmap(result);
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
