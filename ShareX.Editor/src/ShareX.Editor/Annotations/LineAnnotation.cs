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
/// Line annotation
/// </summary>
public class LineAnnotation : Annotation
{
    public LineAnnotation()
    {
        ToolType = EditorTool.Line;
    }

    public override void Render(DrawingContext context)
    {
        var pen = CreatePen();
        context.DrawLine(pen, StartPoint, EndPoint);
    }

    public override bool HitTest(Point point, double tolerance = 5)
    {
        // Calculate distance from point to line segment
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
