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
    [Description("Convolution matrix")]
    internal class MatrixConvolution : ImageEffect
    {
        [DefaultValue(0)]
        public int X0Y0 { get; set; }
        [DefaultValue(0)]
        public int X1Y0 { get; set; }
        [DefaultValue(0)]
        public int X2Y0 { get; set; }

        [DefaultValue(0)]
        public int X0Y1 { get; set; }
        [DefaultValue(1)]
        public int X1Y1 { get; set; }
        [DefaultValue(0)]
        public int X2Y1 { get; set; }

        [DefaultValue(0)]
        public int X0Y2 { get; set; }
        [DefaultValue(0)]
        public int X1Y2 { get; set; }
        [DefaultValue(0)]
        public int X2Y2 { get; set; }

        [DefaultValue(1.0)]
        public double Factor { get; set; }

        [DefaultValue((byte)0)]
        public byte Offset { get; set; }

        public MatrixConvolution()
        {
            // this.ApplyDefaultPropertyValues();
            X1Y1 = 1;
            Factor = 1.0;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            float[] kernel = new float[] {
                X0Y0, X1Y0, X2Y0,
                X0Y1, X1Y1, X2Y1,
                X0Y2, X1Y2, X2Y2
            };
            
            return ImageEffectsProcessing.ApplyConvolutionMatrix(bmp, kernel, 3, (float)Factor, Offset);
        }
    }
}
