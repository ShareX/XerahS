using SkiaSharp;

namespace ShareX.Editor;

/// <summary>
/// A rectangle drawing shape.
/// </summary>
public class RectangleDrawingShape : BaseDrawingShape
{
    public override ShapeType ShapeType => ShapeType.DrawingRectangle;

    /// <summary>
    /// Corner radius for rounded rectangles.
    /// </summary>
    public float CornerRadius { get; set; } = 0f;

    public override void OnDraw(SKCanvas canvas)
    {
        if (!IsValidShape) return;

        // Draw fill
        if (IsFilled)
        {
            using var fillPaint = CreateFillPaint();
            if (CornerRadius > 0)
                canvas.DrawRoundRect(Rectangle, CornerRadius, CornerRadius, fillPaint);
            else
                canvas.DrawRect(Rectangle, fillPaint);
        }

        // Draw border
        using var borderPaint = CreateBorderPaint();
        if (CornerRadius > 0)
            canvas.DrawRoundRect(Rectangle, CornerRadius, CornerRadius, borderPaint);
        else
            canvas.DrawRect(Rectangle, borderPaint);
    }

    public override BaseShape Duplicate()
    {
        return new RectangleDrawingShape
        {
            Rectangle = this.Rectangle,
            BorderColor = this.BorderColor,
            BorderThickness = this.BorderThickness,
            FillColor = this.FillColor,
            CornerRadius = this.CornerRadius,
            InitialSize = this.InitialSize
        };
    }
}
