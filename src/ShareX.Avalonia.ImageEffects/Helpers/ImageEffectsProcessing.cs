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
using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace ShareX.Avalonia.ImageEffects.Helpers
{
    public static class ImageEffectsProcessing
    {
        public static Bitmap BoxBlur(Bitmap bmp, int radius)
        {
            radius = Math.Max(1, radius);
            Bitmap result = (Bitmap)bmp.Clone();

            int diameter = (radius * 2) + 1;

            using (UnsafeBitmap source = new UnsafeBitmap(bmp, true, ImageLockMode.ReadOnly))
            using (UnsafeBitmap dest = new UnsafeBitmap(result, true, ImageLockMode.WriteOnly))
            {
                for (int y = 0; y < source.Height; y++)
                {
                    for (int x = 0; x < source.Width; x++)
                    {
                        int r = 0, g = 0, b = 0, a = 0, count = 0;

                        for (int ky = -radius; ky <= radius; ky++)
                        {
                            int py = (y + ky).Clamp(0, source.Height - 1);

                            for (int kx = -radius; kx <= radius; kx++)
                            {
                                int px = (x + kx).Clamp(0, source.Width - 1);
                                ColorBgra c = source.GetPixel(px, py);
                                r += c.Red;
                                g += c.Green;
                                b += c.Blue;
                                a += c.Alpha;
                                count++;
                            }
                        }

                        byte rb = (byte)(r / count);
                        byte gb = (byte)(g / count);
                        byte bb = (byte)(b / count);
                        byte ab = (byte)(a / count);

                        dest.SetPixel(x, y, new ColorBgra(bb, gb, rb, ab));
                    }
                }
            }

            return result;
        }

        public static Bitmap GaussianBlur(Bitmap bmp, int radius)
        {
            radius = Math.Max(1, radius);
            int size = (radius * 2) + 1;
            double sigma = Math.Max(1.0, radius / 3.0);
            var kernel = ConvolutionMatrixManager.GaussianBlur(size, size, sigma);
            return kernel.Apply(bmp);
        }

        public static void ColorDepth(Bitmap bmp, int bitsPerChannel)
        {
            bitsPerChannel = MathHelpers.Clamp(bitsPerChannel, 1, 8);
            int levels = 1 << bitsPerChannel;
            float step = 255f / (levels - 1);

            using (UnsafeBitmap unsafeBitmap = new UnsafeBitmap(bmp, true, ImageLockMode.ReadWrite))
            {
                for (int i = 0; i < unsafeBitmap.PixelCount; i++)
                {
                    ColorBgra color = unsafeBitmap.GetPixel(i);
                    color.Red = Quantize(color.Red);
                    color.Green = Quantize(color.Green);
                    color.Blue = Quantize(color.Blue);
                    unsafeBitmap.SetPixel(i, color);
                }
            }

            byte Quantize(byte value)
            {
                int level = (int)Math.Round(value / step);
                return (byte)MathHelpers.Clamp((int)Math.Round(level * step), 0, 255);
            }
        }
    }
}
