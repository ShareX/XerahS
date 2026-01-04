using SkiaSharp;

namespace ShareX.Editor;

/// <summary>
/// Base class for all drawing shapes (rectangles, arrows, text, etc.).
/// </summary>
public abstract class BaseDrawingShape : BaseShape
{
    public override ShapeCategory ShapeCategory => ShapeCategory.Drawing;

    /// <summary>
    /// Border color of the shape.
    /// </summary>
    public SKColor BorderColor { get; set; } = SKColors.Red;

    /// <summary>
    /// Border thickness in pixels.
    /// </summary>
    public float BorderThickness { get; set; } = 2f;

    /// <summary>
    /// Fill color of the shape (if applicable).
    /// </summary>
    public SKColor FillColor { get; set; } = SKColors.Transparent;

    /// <summary>
    /// Whether the shape should be filled.
    /// </summary>
    public bool IsFilled => FillColor.Alpha > 0;

    /// <summary>
    /// Creates a paint object for the border.
    /// </summary>
    protected SKPaint CreateBorderPaint()
    {
        return new SKPaint
        {
            Color = BorderColor,
            StrokeWidth = BorderThickness,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };
    }

    /// <summary>
    /// Creates a paint object for the fill.
    /// </summary>
    protected SKPaint CreateFillPaint()
    {
        return new SKPaint
        {
            Color = FillColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
    }
}
