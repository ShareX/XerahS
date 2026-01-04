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
/// Text annotation
/// </summary>
public class TextAnnotation : Annotation
{
    /// <summary>
    /// Text content
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Font size in pixels
    /// </summary>
    public double FontSize { get; set; } = 48;

    /// <summary>
    /// Font family
    /// </summary>
    public string FontFamily { get; set; } = "Segoe UI";

    /// <summary>
    /// Bold style
    /// </summary>
    public bool IsBold { get; set; }

    /// <summary>
    /// Italic style
    /// </summary>
    public bool IsItalic { get; set; }

    public TextAnnotation()
    {
        ToolType = EditorTool.Text;
    }

    public override void Render(DrawingContext context)
    {
        if (string.IsNullOrEmpty(Text)) return;

        var typeface = new Typeface(
            FontFamily,
            IsItalic ? FontStyle.Italic : FontStyle.Normal,  
            IsBold ? FontWeight.Bold : FontWeight.Normal);

        var formattedText = new FormattedText(
            Text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            CreateStrokeBrush());

        context.DrawText(formattedText, StartPoint);
    }

    public override bool HitTest(Point point, double tolerance = 5)
    {
        if (string.IsNullOrEmpty(Text)) return false;

        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight.Normal);
        var formattedText = new FormattedText(
            Text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            Brushes.Black);

        var textBounds = new Rect(
            StartPoint.X,
            StartPoint.Y,
            formattedText.Width,
            formattedText.Height);

        return textBounds.Inflate(tolerance).Contains(point);
    }

    public override Rect GetBounds()
    {
        if (string.IsNullOrEmpty(Text)) return new Rect(StartPoint, new Size(10, 10));

        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight.Normal);
        var formattedText = new FormattedText(
            Text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            Brushes.Black);

        return new Rect(StartPoint.X, StartPoint.Y, formattedText.Width, formattedText.Height);
    }
}
