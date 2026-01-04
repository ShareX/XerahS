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
/// Number annotation - auto-incrementing numbered circle markers
/// </summary>
public class NumberAnnotation : Annotation
{
    /// <summary>
    /// Number to display (typically auto-incremented)
    /// </summary>
    public int Number { get; set; } = 1;

    /// <summary>
    /// Font size for the number
    /// </summary>
    public double FontSize { get; set; } = 32;

    /// <summary>
    /// Circle radius
    /// </summary>
    public double Radius { get; set; } = 25;

    public NumberAnnotation()
    {
        ToolType = EditorTool.Number;
    }

    public override void Render(DrawingContext context)
    {
        var center = StartPoint;
        var brush = CreateStrokeBrush();
        var pen = CreatePen();
        
        // Draw filled circle
        context.DrawEllipse(brush, pen, center, Radius, Radius);
        
        // Draw number text
        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
        var text = Number.ToString();
        var formattedText = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            Brushes.White);

        var textPos = new Point(
            center.X - formattedText.Width / 2,
            center.Y - formattedText.Height / 2);

        context.DrawText(formattedText, textPos);
    }

    public override bool HitTest(Point point, double tolerance = 5)
    {
        var dx = point.X - StartPoint.X;
        var dy = point.Y - StartPoint.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        return distance <= (Radius + tolerance);
    }

    public override Rect GetBounds()
    {
        return new Rect(
            StartPoint.X - Radius,
            StartPoint.Y - Radius,
            Radius * 2,
            Radius * 2);
    }
}
