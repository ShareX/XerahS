using SkiaSharp;

namespace ShareX.Editor;

/// <summary>
/// Base class for effect shapes (blur, pixelate, highlight).
/// </summary>
public abstract class BaseEffectShape : BaseShape
{
    public override ShapeCategory ShapeCategory => ShapeCategory.Effect;
}
