using Avalonia;
using Avalonia.Media;
using System.Collections.Generic;
using System.Linq;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Freehand pen/drawing annotation
/// </summary>
public class FreehandAnnotation : Annotation
{
    public List<Point> Points { get; set; } = new List<Point>();
    
    /// <summary>
    /// Simplification tolerance for smoothing
    /// </summary>
    public double SmoothingTolerance { get; set; } = 2.0;

    public FreehandAnnotation()
    {
        ToolType = EditorTool.Pen;
    }

    public override void Render(DrawingContext context)
    {
        if (Points.Count < 2) return;

        var pen = CreatePen();
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(Points[0], false);
            for (int i = 1; i < Points.Count; i++)
            {
                ctx.LineTo(Points[i]);
            }
            ctx.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    public override bool HitTest(Point point, double tolerance = 5)
    {
        // Simple bounding box check first
        if (!GetBounds().Inflate(tolerance).Contains(point)) return false;

        // Detailed point check
        // Optimization: Check segments
        for (int i = 0; i < Points.Count - 1; i++)
        {
            if (DistanceToSegment(point, Points[i], Points[i + 1]) <= tolerance)
                return true;
        }

        return false;
    }

    public override Rect GetBounds()
    {
        if (Points.Count == 0) return new Rect(0, 0, 0, 0);
        
        double minX = Points.Min(p => p.X);
        double minY = Points.Min(p => p.Y);
        double maxX = Points.Max(p => p.X);
        double maxY = Points.Max(p => p.Y);

        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }

    private double DistanceToSegment(Point p, Point v, Point w)
    {
        double l2 = (v.X - w.X) * (v.X - w.X) + (v.Y - w.Y) * (v.Y - w.Y);
        if (l2 == 0) return Distance(p, v);

        double t = ((p.X - v.X) * (w.X - v.X) + (p.Y - v.Y) * (w.Y - v.Y)) / l2;
        t = Math.Max(0, Math.Min(1, t));

        Point projection = new Point(v.X + t * (w.X - v.X), v.Y + t * (w.Y - v.Y));
        return Distance(p, projection);
    }

    private double Distance(Point p1, Point p2)
    {
        return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
    }
}
