#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using Avalonia;
using Avalonia.Media;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Arrow annotation (line with arrowhead)
/// </summary>
public class ArrowAnnotation : Annotation
{
    /// <summary>
    /// Arrow head size in pixels
    /// </summary>
    public double ArrowHeadSize { get; set; } = 12;

    public ArrowAnnotation()
    {
        ToolType = EditorTool.Arrow;
    }

    public override void Render(DrawingContext context)
    {
        var pen = CreatePen();
        
        // Calculate arrow head
        var dx = EndPoint.X - StartPoint.X;
        var dy = EndPoint.Y - StartPoint.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        
        if (length > 0)
        {
            var ux = dx / length;
            var uy = dy / length;
            
            // Modern arrow: narrower angle (20 degrees instead of 30)
            var arrowAngle = Math.PI / 9; // 20 degrees for sleeker look
            var angle = Math.Atan2(dy, dx);
            
            // Calculate arrowhead base point
            var arrowBase = new Point(
                EndPoint.X - ArrowHeadSize * ux,
                EndPoint.Y - ArrowHeadSize * uy);
            
            // Draw line from start to arrow base
            context.DrawLine(pen, StartPoint, arrowBase);
            
            // Arrow head wing points
            var point1 = new Point(
                EndPoint.X - ArrowHeadSize * Math.Cos(angle - arrowAngle),
                EndPoint.Y - ArrowHeadSize * Math.Sin(angle - arrowAngle));
            
            var point2 = new Point(
                EndPoint.X - ArrowHeadSize * Math.Cos(angle + arrowAngle),
                EndPoint.Y - ArrowHeadSize * Math.Sin(angle + arrowAngle));
            
            // Draw filled arrow head triangle
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(EndPoint, true);
                ctx.LineTo(point1);
                ctx.LineTo(point2);
                ctx.EndFigure(true);
            }
            
            context.DrawGeometry(CreateStrokeBrush(), pen, geometry);
        }
        else
        {
            // Fallback for zero-length arrow
            context.DrawLine(pen, StartPoint, EndPoint);
        }
    }

    public override bool HitTest(Point point, double tolerance = 5)
    {
        // Reuse line hit test logic
        var dx = EndPoint.X - StartPoint.X;
        var dy = EndPoint.Y - StartPoint.Y;
        var lineLength = Math.Sqrt(dx * dx + dy * dy);
        if (lineLength < 0.001) return false;
        
        var t = Math.Max(0, Math.Min(1, 
            ((point.X - StartPoint.X) * (EndPoint.X - StartPoint.X) + 
             (point.Y - StartPoint.Y) * (EndPoint.Y - StartPoint.Y)) / (lineLength * lineLength)));
        
        var projection = new Point(
            StartPoint.X + t * (EndPoint.X - StartPoint.X),
            StartPoint.Y + t * (EndPoint.Y - StartPoint.Y));
        
        var pdx = point.X - projection.X;
        var pdy = point.Y - projection.Y;
        var distance = Math.Sqrt(pdx * pdx + pdy * pdy);
        return distance <= tolerance;
    }
}
