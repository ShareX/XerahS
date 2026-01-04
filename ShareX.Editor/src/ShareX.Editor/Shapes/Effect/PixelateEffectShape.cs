using SkiaSharp;

namespace ShareX.Editor;

/// <summary>
/// Pixelate effect shape.
/// </summary>
public class PixelateEffectShape : BaseEffectShape
{
    public override ShapeType ShapeType => ShapeType.EffectPixelate;

    /// <summary>
    /// Pixel block size.
    /// </summary>
    public int PixelSize { get; set; } = 10;

    public override void OnDraw(SKCanvas canvas)
    {
        // Placeholder; pixelation is applied during canvas composition.
    }

    /// <summary>
    /// Applies the pixelate effect to the given image within this shape's bounds.
    /// </summary>
    public void ApplyEffect(SKBitmap bitmap)
    {
        if (!IsValidShape || PixelSize < 2) return;

        var rect = SKRectI.Round(Rectangle);
        rect = SKRectI.Intersect(rect, new SKRectI(0, 0, bitmap.Width, bitmap.Height));
        if (rect.IsEmpty) return;

        for (int y = rect.Top; y < rect.Bottom; y += PixelSize)
        {
            for (int x = rect.Left; x < rect.Right; x += PixelSize)
            {
                // Sample center pixel of block
                int sampleX = Math.Min(x + PixelSize / 2, rect.Right - 1);
                int sampleY = Math.Min(y + PixelSize / 2, rect.Bottom - 1);
                SKColor color = bitmap.GetPixel(sampleX, sampleY);

                // Fill block with sampled color
                for (int py = y; py < Math.Min(y + PixelSize, rect.Bottom); py++)
                {
                    for (int px = x; px < Math.Min(x + PixelSize, rect.Right); px++)
                    {
                        bitmap.SetPixel(px, py, color);
                    }
                }
            }
        }
    }

    public override BaseShape Duplicate()
    {
        return new PixelateEffectShape
        {
            Rectangle = this.Rectangle,
            PixelSize = this.PixelSize,
            InitialSize = this.InitialSize
        };
    }
}
