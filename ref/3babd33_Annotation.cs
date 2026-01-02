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
using System.Text.Json.Serialization;

namespace ShareX.Ava.Annotations.Models;

/// <summary>
/// Base class for all annotation types
/// </summary>
[JsonDerivedType(typeof(ArrowAnnotation), typeDiscriminator: "Arrow")]
[JsonDerivedType(typeof(BlurAnnotation), typeDiscriminator: "Blur")]
[JsonDerivedType(typeof(CropAnnotation), typeDiscriminator: "Crop")]
[JsonDerivedType(typeof(EllipseAnnotation), typeDiscriminator: "Ellipse")]
[JsonDerivedType(typeof(FreehandAnnotation), typeDiscriminator: "Freehand")]
[JsonDerivedType(typeof(HighlightAnnotation), typeDiscriminator: "Highlight")]
[JsonDerivedType(typeof(ImageAnnotation), typeDiscriminator: "Image")]
[JsonDerivedType(typeof(LineAnnotation), typeDiscriminator: "Line")]
[JsonDerivedType(typeof(MagnifyAnnotation), typeDiscriminator: "Magnify")]
[JsonDerivedType(typeof(NumberAnnotation), typeDiscriminator: "Number")]
[JsonDerivedType(typeof(PixelateAnnotation), typeDiscriminator: "Pixelate")]
[JsonDerivedType(typeof(RectangleAnnotation), typeDiscriminator: "Rectangle")]
[JsonDerivedType(typeof(SmartEraserAnnotation), typeDiscriminator: "SmartEraser")]
[JsonDerivedType(typeof(SpeechBalloonAnnotation), typeDiscriminator: "SpeechBalloon")]
[JsonDerivedType(typeof(SpotlightAnnotation), typeDiscriminator: "Spotlight")]
[JsonDerivedType(typeof(TextAnnotation), typeDiscriminator: "Text")]
public abstract class Annotation
{
    /// <summary>
    /// Unique identifier for this annotation
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Tool type that created this annotation
    /// </summary>
    public EditorTool ToolType { get; set; }

    /// <summary>
    /// Stroke/border color (hex color string)
    /// </summary>
    public string StrokeColor { get; set; } = "#ef4444";

    /// <summary>
    /// Stroke width in pixels
    /// </summary>
    public double StrokeWidth { get; set; } = 4;

    /// <summary>
    /// Starting point (top-left for rectangles, start for lines/arrows)
    /// </summary>
    public Point StartPoint { get; set; }

    /// <summary>
    /// Ending point (bottom-right for rectangles, end for lines/arrows)
    /// </summary>
    public Point EndPoint { get; set; }

    /// <summary>
    /// Whether this annotation is currently selected
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Z-order for rendering (higher = on top)
    /// </summary>
    public int ZIndex { get; set; }

    /// <summary>
    /// Render this annotation to the drawing context
    /// </summary>
    /// <param name="context">Drawing context to render to</param>
    public abstract void Render(DrawingContext context);

    /// <summary>
    /// Hit test to determine if a point intersects this annotation
    /// </summary>
    /// <param name="point">Point to test</param>
    /// <param name="tolerance">Hit test tolerance in pixels</param>
    /// <returns>True if the point hits this annotation</returns>
    public abstract bool HitTest(Point point, double tolerance = 5);

    /// <summary>
    /// Get the bounding rectangle for this annotation
    /// </summary>
    public virtual Rect GetBounds()
    {
        return new Rect(StartPoint, EndPoint);
    }

    /// <summary>
    /// Parse hex color string to Avalonia Color
    /// </summary>
    protected Color ParseColor(string hexColor)
    {
        return Color.Parse(hexColor);
    }

    /// <summary>
    /// Create a brush from the stroke color
    /// </summary>
    protected IBrush CreateStrokeBrush()
    {
        return new SolidColorBrush(ParseColor(StrokeColor));
    }

    /// <summary>
    /// Create a pen for drawing outlines
    /// </summary>
    protected Pen CreatePen()
    {
        return new Pen(CreateStrokeBrush(), StrokeWidth);
    }
}
