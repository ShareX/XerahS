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


namespace ShareX.Avalonia.ImageEffects.Adjustments
{
    [Description("Alpha")]
    public class Alpha : ImageEffect
    {
        private float alpha;

        [DefaultValue(1f)]
        public float Opacity
        {
            get => alpha;
            set => alpha = MathHelpers.Clamp(value, 0f, 1f);
        }

        [DefaultValue(false)]
        public bool SetAlpha { get; set; }

        public Alpha()
        {
            // this.ApplyDefaultPropertyValues();
            Opacity = 1f;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            return ImageEffectsProcessing.ApplyOpacity(bmp, Opacity);
        }

        protected override string? GetSummary()
        {
            return Opacity.ToString();
        }
    }
}
