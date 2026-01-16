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

namespace XerahS.Common
{
    public readonly struct PointF
    {
        public static readonly PointF Empty = new PointF(0, 0);

        public float X { get; }
        public float Y { get; }

        public PointF(float x, float y)
        {
            X = x;
            Y = y;
        }

        public PointF Add(PointF other)
        {
            return new PointF(X + other.X, Y + other.Y);
        }

        public override string ToString()
        {
            return $"X={X}, Y={Y}";
        }

        public static implicit operator System.Drawing.PointF(PointF p) => new System.Drawing.PointF(p.X, p.Y);
        public static implicit operator PointF(System.Drawing.PointF p) => new PointF(p.X, p.Y);
    }
}
