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
using ShareX.Avalonia.Common.Colors;
using ShareX.Avalonia.ImageEffects.Helpers;
using System;
using System.ComponentModel;
using SkiaSharp;
// using ShareX.Avalonia.Common.Drawing; // Check if GradientInfo exists

namespace ShareX.Avalonia.ImageEffects
{
    [Description("Glow")]
    public class Glow : ImageEffect
    {
        private static readonly Random Random = new Random();
        private int size;
        private float strength;

        [DefaultValue(20)]
        public int Size
        {
            get => size;
            set => size = Math.Max(0, value);
        }

        [DefaultValue(1f)]
        public float Strength
        {
            get => strength;
            set => strength = Math.Max(0.1f, value);
        }

        // [DefaultValue(typeof(Color), "White")]
        public SKColor Color { get; set; }

        [DefaultValue(false)]
        public bool UseGradient { get; set; }

        // public GradientInfo Gradient { get; set; }

        // [DefaultValue(typeof(DrawingPoint), "0, 0")]
        public SKPoint Offset { get; set; }

        public Glow()
        {
            // this.ApplyDefaultPropertyValues();
            // Gradient = CreateDefaultGradient();
            Size = 20;
            Strength = 1f;
            Color = SKColors.White;
            Offset = new SKPoint(0,0);
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            return ImageEffectsProcessing.AddGlow(bmp, Size, Strength, Color, Offset, UseGradient);
        }

        protected override string? GetSummary()
        {
            return Size.ToString();
        }
    }
}
