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

namespace ShareX.Avalonia.ImageEffects.Drawings
{
    [Description("Draw checkerboard")]
    internal class DrawCheckerboard : ImageEffect
    {
        [DefaultValue(10)]
        public int Size { get; set; }

        // [DefaultValue(typeof(Color), "LightGray")]
        public SKColor Color1 { get; set; }

        // [DefaultValue(typeof(Color), "White")]
        public SKColor Color2 { get; set; }

        public DrawCheckerboard()
        {
            // this.ApplyDefaultPropertyValues();
            Size = 10;
            Color1 = SKColors.LightGray;
            Color2 = SKColors.White;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             // TODO: Draw checkerboard
             return bmp;
        }

        protected override string? GetSummary()
        {
            return Size.ToString();
        }
    }
}

