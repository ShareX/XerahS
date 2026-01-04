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

using System.Collections;
using System.Collections.Generic;
using SkiaSharp;
using ShareX.Ava.Common.GIF;

namespace ShareX.Ava.Common.GIF
{
    /// <summary>
    /// Summary description for PaletteQuantizer.
    /// </summary>
    public class PaletteQuantizer : Quantizer
    {
        /// <summary>
        /// Construct the palette quantizer
        /// </summary>
        /// <param name="palette">The color palette to quantize to</param>
        /// <remarks>
        /// Palette quantization only requires a single quantization step
        /// </remarks>
        public PaletteQuantizer(List<SKColor> palette)
            : base(true)
        {
            _colorMap = new Hashtable();
            _colors = palette.ToArray();
        }

        /// <summary>
        /// Override this to process the pixel in the second pass of the algorithm
        /// </summary>
        /// <param name="pixel">The pixel to quantize</param>
        /// <returns>The quantized value</returns>
        protected override byte QuantizePixel(Color32 pixel)
        {
            byte colorIndex = 0;
            int colorHash = pixel.ARGB;

            // Check if the color is in the lookup table
            if (_colorMap.ContainsKey(colorHash))
            {
                colorIndex = (byte)_colorMap[colorHash];
            }
            else
            {
                // Not found - loop through the palette and find the nearest match.
                // Firstly check the alpha value - if 0, lookup the transparent color
                if (pixel.Alpha == 0)
                {
                    // Transparent. Lookup the first color with an alpha value of 0
                    for (int index = 0; index < _colors.Length; index++)
                    {
                        if (_colors[index].Alpha == 0)
                        {
                            colorIndex = (byte)index;
                            break;
                        }
                    }
                }
                else
                {
                    // Not transparent...
                    int leastDistance = int.MaxValue;
                    int red = pixel.Red;
                    int green = pixel.Green;
                    int blue = pixel.Blue;

                    // Loop through the entire palette, looking for the closest color match
                    for (int index = 0; index < _colors.Length; index++)
                    {
                        SKColor paletteColor = _colors[index];

                        int redDistance = paletteColor.Red - red;
                        int greenDistance = paletteColor.Green - green;
                        int blueDistance = paletteColor.Blue - blue;

                        int distance = (redDistance * redDistance) +
                                       (greenDistance * greenDistance) +
                                       (blueDistance * blueDistance);

                        if (distance < leastDistance)
                        {
                            colorIndex = (byte)index;
                            leastDistance = distance;

                            // And if it's an exact match, exit the loop
                            if (distance == 0)
                            {
                                break;
                            }
                        }
                    }
                }

                // Now I have the color, pop it into the hashtable for next time
                _colorMap.Add(colorHash, colorIndex);
            }

            return colorIndex;
        }

        /// <summary>
        /// Retrieve the palette for the quantized image
        /// </summary>
        /// <returns>The new color palette</returns>
        protected override List<SKColor> GetPalette()
        {
            return new List<SKColor>(_colors);
        }

        /// <summary>
        /// Lookup table for colors
        /// </summary>
        private Hashtable _colorMap;

        /// <summary>
        /// List of all colors in the palette
        /// </summary>
        protected SKColor[] _colors;
    }
}
