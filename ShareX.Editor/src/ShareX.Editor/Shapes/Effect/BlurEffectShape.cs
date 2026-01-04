using SkiaSharp;

namespace ShareX.Editor;

/// <summary>
/// Blur effect shape.
/// </summary>
public class BlurEffectShape : BaseEffectShape
{
    public override ShapeType ShapeType => ShapeType.EffectBlur;

    /// <summary>
    /// Blur radius in pixels.
    /// </summary>
    public float BlurRadius { get; set; } = 15f;

    public override void OnDraw(SKCanvas canvas)
    {
        // Note: Blur effect requires access to the underlying image.
        // This is a placeholder; actual blur is applied during canvas composition.
    }

    /// <summary>
    /// Applies the blur effect to the given image within this shape's bounds.
    /// </summary>
    public void ApplyEffect(SKBitmap bitmap)
    {
        if (!IsValidShape) return;

        var rect = SKRectI.Round(Rectangle);
        rect = SKRectI.Intersect(rect, new SKRectI(0, 0, bitmap.Width, bitmap.Height));
        if (rect.IsEmpty) return;

        using var surface = SKSurface.Create(new SKImageInfo(rect.Width, rect.Height));
        var regionCanvas = surface.Canvas;

        // Draw the region from the bitmap
        regionCanvas.DrawBitmap(bitmap, new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom),
            new SKRect(0, 0, rect.Width, rect.Height));

        // Apply blur filter
        using var blurFilter = SKImageFilter.CreateBlur(BlurRadius, BlurRadius);
        using var paint = new SKPaint { ImageFilter = blurFilter };

        using var snapshot = surface.Snapshot();

        // Draw blurred region back to original
        using var blurSurface = SKSurface.Create(new SKImageInfo(rect.Width, rect.Height));
        blurSurface.Canvas.DrawImage(snapshot, 0, 0, paint);
        using var blurredImage = blurSurface.Snapshot();

        // Copy back to bitmap
        using var blurredBitmap = SKBitmap.FromImage(blurredImage);
        for (int y = 0; y < rect.Height; y++)
        {
            for (int x = 0; x < rect.Width; x++)
            {
                bitmap.SetPixel(rect.Left + x, rect.Top + y, blurredBitmap.GetPixel(x, y));
            }
        }
    }

    public override BaseShape Duplicate()
    {
        return new BlurEffectShape
        {
            Rectangle = this.Rectangle,
            BlurRadius = this.BlurRadius,
            InitialSize = this.InitialSize
        };
    }
}
