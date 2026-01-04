using SkiaSharp;

namespace ShareX.Editor;

/// <summary>
/// An arrow drawing shape.
/// </summary>
public class ArrowDrawingShape : LineDrawingShape
{
    public override ShapeType ShapeType => ShapeType.DrawingArrow;

    /// <summary>
    /// Size of the arrowhead in pixels.
    /// </summary>
    public float ArrowHeadSize { get; set; } = 15f;

    public override void OnDraw(SKCanvas canvas)
    {
        if (!IsValidShape) return;

        using var paint = CreateBorderPaint();
        paint.StrokeCap = SKStrokeCap.Round;
        paint.StrokeJoin = SKStrokeJoin.Round;

        // Draw line
        canvas.DrawLine(StartPosition, EndPosition, paint);

        // Draw arrowhead
        DrawArrowHead(canvas, paint);
    }

    private void DrawArrowHead(SKCanvas canvas, SKPaint paint)
    {
        float angle = (float)Math.Atan2(EndPosition.Y - StartPosition.Y, EndPosition.X - StartPosition.X);
        float arrowAngle = (float)(Math.PI / 6); // 30 degrees

        SKPoint arrowPoint1 = new SKPoint(
            EndPosition.X - ArrowHeadSize * (float)Math.Cos(angle - arrowAngle),
            EndPosition.Y - ArrowHeadSize * (float)Math.Sin(angle - arrowAngle)
        );

        SKPoint arrowPoint2 = new SKPoint(
            EndPosition.X - ArrowHeadSize * (float)Math.Cos(angle + arrowAngle),
            EndPosition.Y - ArrowHeadSize * (float)Math.Sin(angle + arrowAngle)
        );

        using var path = new SKPath();
        path.MoveTo(EndPosition);
        path.LineTo(arrowPoint1);
        path.MoveTo(EndPosition);
        path.LineTo(arrowPoint2);

        canvas.DrawPath(path, paint);
    }

    public override BaseShape Duplicate()
    {
        return new ArrowDrawingShape
        {
            Rectangle = this.Rectangle,
            BorderColor = this.BorderColor,
            BorderThickness = this.BorderThickness,
            ArrowHeadSize = this.ArrowHeadSize,
            InitialSize = this.InitialSize
        };
    }
}
