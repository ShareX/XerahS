using SkiaSharp;

namespace ShareX.Editor;

/// <summary>
/// A line drawing shape.
/// </summary>
public class LineDrawingShape : BaseDrawingShape
{
    public override ShapeType ShapeType => ShapeType.DrawingLine;

    /// <summary>
    /// Whether to center the line on the start position.
    /// </summary>
    public bool CenterOnStart { get; set; } = false;

    public override void OnDraw(SKCanvas canvas)
    {
        if (!IsValidShape) return;

        using var paint = CreateBorderPaint();
        paint.StrokeCap = SKStrokeCap.Round;

        canvas.DrawLine(StartPosition, EndPosition, paint);
    }

    public override void OnCreatingUpdate(SKPoint currentPos)
    {
        EndPosition = currentPos;
        // For lines, Rectangle is just used for hit-testing bounds
        Rectangle = CreateRectangle(StartPosition, EndPosition);
    }

    public override bool Contains(SKPoint point)
    {
        // Line hit-testing: check distance from point to line segment
        float threshold = Math.Max(BorderThickness, 5f);
        return DistanceToLineSegment(point, StartPosition, EndPosition) <= threshold;
    }

    private static float DistanceToLineSegment(SKPoint p, SKPoint a, SKPoint b)
    {
        float dx = b.X - a.X;
        float dy = b.Y - a.Y;
        float lengthSquared = dx * dx + dy * dy;

        if (lengthSquared == 0)
            return SKPoint.Distance(p, a);

        float t = Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared));
        SKPoint projection = new SKPoint(a.X + t * dx, a.Y + t * dy);
        return SKPoint.Distance(p, projection);
    }

    public override BaseShape Duplicate()
    {
        return new LineDrawingShape
        {
            Rectangle = this.Rectangle,
            BorderColor = this.BorderColor,
            BorderThickness = this.BorderThickness,
            InitialSize = this.InitialSize
        };
    }
}
