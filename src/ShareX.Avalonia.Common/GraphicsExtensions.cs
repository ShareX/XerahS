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

using System.Drawing;

namespace ShareX.Avalonia.Common
{
    public static class GraphicsExtensions
    {
        public static void DrawRectangleProper(this Graphics g, Pen pen, int x, int y, int width, int height)
        {
            if (pen.Width == 1)
            {
                width -= 1;
                height -= 1;
            }

            if (width > 0 && height > 0)
            {
                g.DrawRectangle(pen, x, y, width, height);
            }
        }

        public static void DrawTextWithShadow(this Graphics g, string text, System.Drawing.Point position, Font font, Brush textBrush, Brush shadowBrush)
        {
            System.Drawing.PointF shadowPosition = new System.Drawing.PointF(position.X + 1, position.Y + 1);
            g.DrawString(text, font, shadowBrush, shadowPosition);
            g.DrawString(text, font, textBrush, new System.Drawing.PointF(position.X, position.Y));
        }
    }
}
