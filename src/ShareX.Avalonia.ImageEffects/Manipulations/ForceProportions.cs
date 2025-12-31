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
// using ShareX.Avalonia.ImageEffects.Enums;

namespace ShareX.Avalonia.ImageEffects.Manipulations
{
    [Description("Force proportions")]
    public class ForceProportions : ImageEffect
    {
        [DefaultValue(16f)]
        public float RatioX { get; set; }

        [DefaultValue(9f)]
        public float RatioY { get; set; }

        // [DefaultValue(typeof(Color), "Black")]
        public SKColor Color { get; set; }

        [DefaultValue(true)]
        public bool AutoResize { get; set; }

        // [DefaultValue(ContentAlignment.MiddleCenter)]
        // public ContentAlignment Anchor { get; set; }

        public ForceProportions()
        {
            // this.ApplyDefaultPropertyValues();
            RatioX = 16f;
            RatioY = 9f;
            Color = SKColors.Black;
            AutoResize = true;
            // Anchor = ContentAlignment.MiddleCenter;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             // TODO: Implementation
             return bmp;
        }

        protected override string? GetSummary()
        {
            return $"{RatioX}:{RatioY}";
        }
    }
}

