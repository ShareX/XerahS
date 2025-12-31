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
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;

namespace ShareX.Avalonia.ImageEffects.Manipulations
{
    public class Resize : ImageEffect
    {
        [DefaultValue(250), Description("Use width as 0 to automatically adjust width to maintain aspect ratio.")]
        public int Width { get; set; }

        [DefaultValue(0), Description("Use height as 0 to automatically adjust height to maintain aspect ratio.")]
        public int Height { get; set; }

        [DefaultValue(ResizeMode.ResizeAll)]
        public ResizeMode Mode { get; set; }

        public Resize()
        {
            // this.ApplyDefaultPropertyValues();
            Width = 250;
        }

        public Resize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            if (Width <= 0 && Height <= 0)
            {
                return bmp;
            }

            // TODO: Resize implementation
            return bmp;
        }

        protected override string? GetSummary()
        {
            return Width.ToString();
        }
    }
}

