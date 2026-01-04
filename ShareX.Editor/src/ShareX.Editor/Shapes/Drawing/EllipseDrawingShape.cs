using SkiaSharp;

namespace ShareX.Editor;

/// <summary>
/// An ellipse drawing shape.
/// </summary>
public class EllipseDrawingShape : BaseDrawingShape
{
    public override ShapeType ShapeType => ShapeType.DrawingEllipse;

    public override void OnDraw(SKCanvas canvas)
    {
        if (!IsValidShape) return;

        // Draw fill
        if (IsFilled)
        {
            using var fillPaint = CreateFillPaint();
            canvas.DrawOval(Rectangle, fillPaint);
        }

        // Draw border
        using var borderPaint = CreateBorderPaint();
        canvas.DrawOval(Rectangle, borderPaint);
    }

    public override BaseShape Duplicate()
    {
        return new EllipseDrawingShape
        {
            Rectangle = this.Rectangle,
            BorderColor = this.BorderColor,
            BorderThickness = this.BorderThickness,
            FillColor = this.FillColor,
            InitialSize = this.InitialSize
        };
    }
}
