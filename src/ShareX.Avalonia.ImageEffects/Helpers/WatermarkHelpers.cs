#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
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

using ShareX.Avalonia.Common;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ShareX.Avalonia.ImageEffects.Helpers
{
    public static class WatermarkHelpers
    {
        public static Size MeasureText(string text, Font font, TextRenderingHint renderingHint = TextRenderingHint.SystemDefault)
        {
            if (string.IsNullOrEmpty(text) || font == null)
            {
                return Size.Empty;
            }

            using Bitmap bmp = new Bitmap(1, 1);
            using Graphics g = Graphics.FromImage(bmp);
            g.TextRenderingHint = renderingHint;

            using StringFormat format = new StringFormat(StringFormat.GenericTypographic)
            {
                FormatFlags = StringFormatFlags.MeasureTrailingSpaces
            };

            SizeF sizeF = g.MeasureString(text, font, int.MaxValue, format);
            return Size.Ceiling(sizeF);
        }

        public static System.Drawing.Point GetPosition(ContentAlignment placement, System.Drawing.Point offset, Size canvasSize, Size elementSize)
        {
            int x = placement switch
            {
                ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft => 0,
                ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => (canvasSize.Width - elementSize.Width) / 2,
                _ => canvasSize.Width - elementSize.Width
            };

            int y = placement switch
            {
                ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight => 0,
                ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight => (canvasSize.Height - elementSize.Height) / 2,
                _ => canvasSize.Height - elementSize.Height
            };

            return new System.Drawing.Point(x + offset.X, y + offset.Y);
        }

        public static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();

            if (radius <= 0 || rect.Width <= 0 || rect.Height <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            int diameter = radius * 2;
            int adjustedDiameter = System.Math.Min(diameter, System.Math.Min(rect.Width, rect.Height));

            Rectangle arc = new Rectangle(rect.Location, new Size(adjustedDiameter, adjustedDiameter));

            // Top left
            path.AddArc(arc, 180, 90);

            // Top right
            arc.X = rect.Right - adjustedDiameter;
            path.AddArc(arc, 270, 90);

            // Bottom right
            arc.Y = rect.Bottom - adjustedDiameter;
            path.AddArc(arc, 0, 90);

            // Bottom left
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
