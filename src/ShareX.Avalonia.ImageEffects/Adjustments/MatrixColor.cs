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
    [Description("Color matrix")]
    public class MatrixColor : ImageEffect
    {
        [DefaultValue(1f)]
        public float Matrix00 { get; set; }
        [DefaultValue(0f)]
        public float Matrix01 { get; set; }
        [DefaultValue(0f)]
        public float Matrix02 { get; set; }
        [DefaultValue(0f)]
        public float Matrix03 { get; set; }
        [DefaultValue(0f)]
        public float Matrix04 { get; set; }

        [DefaultValue(0f)]
        public float Matrix10 { get; set; }
        [DefaultValue(1f)]
        public float Matrix11 { get; set; }
        [DefaultValue(0f)]
        public float Matrix12 { get; set; }
        [DefaultValue(0f)]
        public float Matrix13 { get; set; }
        [DefaultValue(0f)]
        public float Matrix14 { get; set; }

        [DefaultValue(0f)]
        public float Matrix20 { get; set; }
        [DefaultValue(0f)]
        public float Matrix21 { get; set; }
        [DefaultValue(1f)]
        public float Matrix22 { get; set; }
        [DefaultValue(0f)]
        public float Matrix23 { get; set; }
        [DefaultValue(0f)]
        public float Matrix24 { get; set; }

        [DefaultValue(0f)]
        public float Matrix30 { get; set; }
        [DefaultValue(0f)]
        public float Matrix31 { get; set; }
        [DefaultValue(0f)]
        public float Matrix32 { get; set; }
        [DefaultValue(1f)]
        public float Matrix33 { get; set; }
        [DefaultValue(0f)]
        public float Matrix34 { get; set; }

        [DefaultValue(0f)]
        public float Matrix40 { get; set; }
        [DefaultValue(0f)]
        public float Matrix41 { get; set; }
        [DefaultValue(0f)]
        public float Matrix42 { get; set; }
        [DefaultValue(0f)]
        public float Matrix43 { get; set; }
        [DefaultValue(1f)]
        public float Matrix44 { get; set; }

        public MatrixColor()
        {
            // this.ApplyDefaultPropertyValues();
            Matrix00 = 1f; Matrix11 = 1f; Matrix22 = 1f; Matrix33 = 1f; Matrix44 = 1f;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             float[] matrix = new float[] {
                 Matrix00, Matrix01, Matrix02, Matrix03, Matrix04,
                 Matrix10, Matrix11, Matrix12, Matrix13, Matrix14,
                 Matrix20, Matrix21, Matrix22, Matrix23, Matrix24,
                 Matrix30, Matrix31, Matrix32, Matrix33, Matrix34
             };
             
             return ImageEffectsProcessing.ApplyColorMatrix(bmp, matrix);
        }
    }
}
