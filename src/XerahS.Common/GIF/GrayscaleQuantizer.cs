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

using SkiaSharp;

namespace XerahS.Common.GIF
{
    /// <summary>
    /// Summary description for PaletteQuantizer.
    /// </summary>
    public class GrayscaleQuantizer : PaletteQuantizer
    {
        /// <summary>
        /// Construct the palette quantizer
        /// </summary>
        /// <remarks>
        /// Palette quantization only requires a single quantization step
        /// </remarks>
        public GrayscaleQuantizer()
            : base(new List<SKColor>())
        {
            _colors = new SKColor[256];

            int nColors = 256;

            // Initialize a new color table with entries that are determined
            // by some optimal palette-finding algorithm; for demonstration
            // purposes, use a grayscale.
            for (uint i = 0; i < nColors; i++)
            {
                byte Alpha = 0xFF; // Colors are opaque.
                byte Intensity = (byte)(i * 0xFF / (nColors - 1)); // Even distribution.

                _colors[i] = new SKColor(Intensity, Intensity, Intensity, Alpha);
            }
        }

        /// <summary>
        /// Override this to process the pixel in the second pass of the algorithm
        /// </summary>
        /// <param name="pixel">The pixel to quantize</param>
        /// <returns>The quantized value</returns>
        protected override byte QuantizePixel(Color32 pixel)
        {
            double luminance = (pixel.Red * 0.299) + (pixel.Green * 0.587) + (pixel.Blue * 0.114);

            // Gray scale is an intensity map from black to white.
            // Compute the index to the grayscale entry that
            // approximates the luminance, and then round the index.
            // Also, constrain the index choices by the number of
            // colors to do, and then set that pixel's index to the
            // byte value.
            byte colorIndex = (byte)(luminance + 0.5);

            return colorIndex;
        }
    }
}
