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
/// Spotlight annotation - darkens entire image except highlighted area
/// </summary>
public class SpotlightAnnotation : Annotation
{
    /// <summary>
    /// Darkening overlay opacity (0-255)
    /// </summary>
    public byte DarkenOpacity { get; set; } = 180;

    /// <summary>
    /// Size of the canvas (needed for full overlay)
    /// </summary>
    public Size CanvasSize { get; set; }

    public SpotlightAnnotation()
    {
        ToolType = EditorTool.Spotlight;
    }

    public override void Render(DrawingContext context)
    {
        if (CanvasSize.Width <= 0 || CanvasSize.Height <= 0) return;

        // Normalize the spotlight rectangle (ensure it goes from min to max)
        var spotX = Math.Min(StartPoint.X, EndPoint.X);
        var spotY = Math.Min(StartPoint.Y, EndPoint.Y);
        var spotW = Math.Abs(EndPoint.X - StartPoint.X);
        var spotH = Math.Abs(EndPoint.Y - StartPoint.Y);
        var spotlightRect = new Rect(spotX, spotY, spotW, spotH);

        // Create dark overlay brush
        var overlayBrush = new SolidColorBrush(Color.FromArgb(DarkenOpacity, 0, 0, 0));
        
        // Create geometry for the darkening overlay using EvenOdd fill rule
        // This creates a frame around the spotlight rectangle
        var pathGeometry = new PathGeometry { FillRule = FillRule.EvenOdd };
        
        // Outer figure: full canvas
        var outerFigure = new PathFigure { StartPoint = new Point(0, 0), IsClosed = true };
        outerFigure.Segments.Add(new LineSegment { Point = new Point(CanvasSize.Width, 0) });
        outerFigure.Segments.Add(new LineSegment { Point = new Point(CanvasSize.Width, CanvasSize.Height) });
        outerFigure.Segments.Add(new LineSegment { Point = new Point(0, CanvasSize.Height) });
        pathGeometry.Figures.Add(outerFigure);
        
        // Inner figure: spotlight rectangle (hole)
        var innerFigure = new PathFigure { StartPoint = spotlightRect.TopLeft, IsClosed = true };
        innerFigure.Segments.Add(new LineSegment { Point = spotlightRect.TopRight });
        innerFigure.Segments.Add(new LineSegment { Point = spotlightRect.BottomRight });
        innerFigure.Segments.Add(new LineSegment { Point = spotlightRect.BottomLeft });
        pathGeometry.Figures.Add(innerFigure);
        
        // Draw the overlay (darkens everything except the rectangle)
        context.DrawGeometry(overlayBrush, null, pathGeometry);
    }

    public override bool HitTest(Point point, double tolerance = 5)
    {
        var rect = new Rect(StartPoint, EndPoint);
        return rect.Inflate(tolerance).Contains(point);
    }
}
