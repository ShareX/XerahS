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
/// Crop annotation - modifies the image dimensions
/// Note: This is a special annotation that triggers actual image modification
/// </summary>
public class CropAnnotation : Annotation
{
    public CropAnnotation()
    {
        ToolType = EditorTool.Crop;
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(StartPoint, EndPoint);
        
        // Draw crop overlay rectangle with handles
        var pen = new Pen(new SolidColorBrush(Colors.White), 2);
        var dashPen = new Pen(new SolidColorBrush(Colors.Black), 2)
        {
            DashStyle = new DashStyle(new[] { 4.0, 4.0 }, 0)
        };
        
        // Draw dashed border
        context.DrawRectangle(null, dashPen, rect);
        
        // Draw resize handles at corners and edges
        DrawHandle(context, new Point(rect.Left, rect.Top));
        DrawHandle(context, new Point(rect.Right, rect.Top));
        DrawHandle(context, new Point(rect.Left, rect.Bottom));
        DrawHandle(context, new Point(rect.Right, rect.Bottom));
        DrawHandle(context, new Point(rect.Center.X, rect.Top));
        DrawHandle(context, new Point(rect.Center.X, rect.Bottom));
        DrawHandle(context, new Point(rect.Left, rect.Center.Y));
        DrawHandle(context, new Point(rect.Right, rect.Center.Y));
    }

    private void DrawHandle(DrawingContext context, Point center)
    {
        const double handleSize = 8;
        var rect = new Rect(
            center.X - handleSize / 2,
            center.Y - handleSize / 2,
            handleSize,
            handleSize);
        
        context.DrawRectangle(Brushes.White, new Pen(Brushes.Black, 1), rect);
    }

    public override bool HitTest(Point point, double tolerance = 5)
    {
        var rect = new Rect(StartPoint, EndPoint);
        
        // Check if point is on the crop rectangle border (within tolerance)
        var outerRect = rect.Inflate(tolerance);
        var innerRect = rect.Deflate(tolerance);
        
        return outerRect.Contains(point) && !innerRect.Contains(point);
    }
}
