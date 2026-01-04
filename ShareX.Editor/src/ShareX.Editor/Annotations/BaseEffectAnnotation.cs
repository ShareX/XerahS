using global::Avalonia;
using global::Avalonia.Media;
using SkiaSharp;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Base class for effect annotations (Blur, Pixelate, Highlight)
/// </summary>
public abstract class BaseEffectAnnotation : Annotation
{
    /// <summary>
    /// Effect radius / strength
    /// </summary>
    public double Amount { get; set; } = 10;

    /// <summary>
    /// Whether the effect is applied as a region (rectangle) or freehand
    /// </summary>
    public bool IsFreehand { get; set; }
    
    /// <summary>
    /// The generated bitmap for the effect (optional)
    /// </summary>
    public global::Avalonia.Media.Imaging.Bitmap? EffectBitmap { get; protected set; }

    public override Rect GetBounds()
    {
        return new Rect(StartPoint, EndPoint);
    }

    public override bool HitTest(Point point, double tolerance = 5)
    {
        return GetBounds().Inflate(tolerance).Contains(point);
    }
    
    /// <summary>
    /// Updates the effect bitmap based on the source image
    /// </summary>
    public virtual void UpdateEffect(SKBitmap source) { }
}
