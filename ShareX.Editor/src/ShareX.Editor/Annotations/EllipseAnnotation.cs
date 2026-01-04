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
/// Ellipse/circle annotation
/// </summary>
public class EllipseAnnotation : Annotation
{
    public EllipseAnnotation()
    {
        ToolType = EditorTool.Ellipse;
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(StartPoint, EndPoint);
        var center = rect.Center;
        var radiusX = rect.Width / 2;
        var radiusY = rect.Height / 2;
        
        var pen = CreatePen();
        var geometry = new EllipseGeometry(rect);
        
        context.DrawGeometry(null, pen, geometry);
    }

    public override bool HitTest(Point point, double tolerance = 5)
    {
        var rect = new Rect(StartPoint, EndPoint);
        var center = rect.Center;
        var radiusX = rect.Width / 2;
        var radiusY = rect.Height / 2;
        
        // Normalize point relative to ellipse center
        var dx = (point.X - center.X) / radiusX;
        var dy = (point.Y - center.Y) / radiusY;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        
        // Check if point is on the ellipse border (within tolerance)
        var toleranceNormalized = tolerance / Math.Min(radiusX, radiusY);
        return Math.Abs(distance - 1.0) <= toleranceNormalized;
    }
}
