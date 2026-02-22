#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

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

using SkiaSharp;
using XerahS.Platform.Abstractions;

namespace XerahS.Media
{
    /// <summary>
    /// Cross-platform gradient information using SkiaSharp types.
    /// </summary>
    public class GradientInfo
    {
        public GradientDirection Type { get; set; }
        public List<GradientStop> Colors { get; set; }

        public GradientInfo()
            : this(GradientDirection.Vertical)
        {
        }

        public GradientInfo(GradientDirection type)
        {
            Type = type;
            Colors = new List<GradientStop>();
        }

        public GradientInfo(GradientDirection type, params SKColor[] colors)
            : this(type)
        {
            if (colors == null || colors.Length == 0)
            {
                return;
            }

            for (int i = 0; i < colors.Length; i++)
            {
                Colors.Add(new GradientStop(colors[i], (int)System.Math.Round(100f / (colors.Length - 1) * i)));
            }
        }
    }

    public class GradientStop
    {
        public SKColor Color { get; set; }
        public float Location { get; set; }

        public GradientStop()
        {
            Color = SKColors.Transparent;
        }

        public GradientStop(SKColor color, float location)
        {
            Color = color;
            Location = location;
        }
    }
}
