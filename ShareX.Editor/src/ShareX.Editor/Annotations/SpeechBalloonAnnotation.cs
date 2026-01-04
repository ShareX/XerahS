using Avalonia;
using Avalonia.Media;
using System;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Speech Balloon annotation with tail
/// </summary>
public class SpeechBalloonAnnotation : Annotation
{
    // Tail control point relative to bounding box? Or absolute?
    // Let's make it absolute for dragging.
    public Point TailPoint { get; set; }
    
    public string Text { get; set; } = "";
    
    // Background color
    public string FillColor { get; set; } = "#FFFFFFFF"; // White
    
    public SpeechBalloonAnnotation()
    {
        ToolType = EditorTool.SpeechBalloon;
        StrokeWidth = 2;
        StrokeColor = "#FF000000";
    }

    public override void Render(DrawingContext context)
    {
        var rect = GetBounds();
        if (rect.Width < 5 || rect.Height < 5) return;

        // Default tail point if not set (e.g. at creation)
        if (TailPoint == default)
        {
            TailPoint = new Point(rect.Right, rect.Bottom + 20);
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // Simple rounded rectangle with a tail triangle merged
            // We can use a path.
            // 1. Draw rounded rect
            // 2. Add tail
            
            double radius = 10;
            
            // Start Top-Left
            ctx.BeginFigure(new Point(rect.Left + radius, rect.Top), true);
            
            // Top edge
            ctx.LineTo(new Point(rect.Right - radius, rect.Top));
            ctx.ArcTo(new Point(rect.Right, rect.Top + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            
            // Right edge
            ctx.LineTo(new Point(rect.Right, rect.Bottom - radius));
            ctx.ArcTo(new Point(rect.Right - radius, rect.Bottom), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            
            // Bottom edge (with tail)
            // Determine where the tail should attach. 
            // Simple heuristic: Attach to the side closest to TailPoint.
            // For now, simpler: Attach to closest point on perimeter.
            // Or Fixed: Just draw a path that includes the tail point at the bottom for now.
             
            // Simplified Bubble: Tail always at bottom for MVP
            double midBottom = rect.X + rect.Width / 2;
            double tailBaseWidth = 20;

            // To Tail
            ctx.LineTo(new Point(midBottom + tailBaseWidth/2, rect.Bottom));
            ctx.LineTo(TailPoint);
            ctx.LineTo(new Point(midBottom - tailBaseWidth/2, rect.Bottom));

            // To Left
            ctx.LineTo(new Point(rect.Left + radius, rect.Bottom));
            ctx.ArcTo(new Point(rect.Left, rect.Bottom - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            
            // Left edge
            ctx.LineTo(new Point(rect.Left, rect.Top + radius));
            ctx.ArcTo(new Point(rect.Left + radius, rect.Top), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            
            ctx.EndFigure(true);
        }

        var fillBrush = new SolidColorBrush(ParseColor(FillColor));
        var pen = CreatePen();
        
        context.DrawGeometry(fillBrush, pen, geometry);
    }
    
    public override Rect GetBounds()
    {
        var r = new Rect(StartPoint, EndPoint);
        // Include tail in bounds?
        // Usually bounds is just the body for resizing. Tail is a handle.
        return r;
    }

    public override bool HitTest(Point point, double tolerance = 5)
    {
        // Hit test box + tail
        // Simplified: just box for now
        return GetBounds().Inflate(tolerance).Contains(point);
    }
}
