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


namespace ShareX.Avalonia.ImageEffects
{
    [Description("Outline")]
    public class Outline : ImageEffect
    {
        private int size;
        private int padding;

        [DefaultValue(1)]
        public int Size
        {
            get => size;
            set => size = Math.Max(value, 1);
        }

        [DefaultValue(0)]
        public int Padding
        {
            get => padding;
            set => padding = Math.Max(value, 0);
        }

        // [DefaultValue(typeof(Color), "Black")]
        public SKColor Color { get; set; }

        [DefaultValue(false)]
        public bool OutlineOnly { get; set; }

        public Outline()
        {
            // this.ApplyDefaultPropertyValues();
            Size = 1;
            Padding = 0;
            Color = SKColors.Black;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            // return ImageEffectsProcessing.Outline(bmp, Size, Color, Padding, OutlineOnly);
            return bmp;
        }

        protected override string? GetSummary()
        {
            return Size.ToString();
        }
    }
}

