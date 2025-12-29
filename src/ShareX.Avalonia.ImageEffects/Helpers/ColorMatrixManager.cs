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

namespace ShareX.Avalonia.ImageEffects
{
    public static class ColorMatrixManager
    {
        private const float Rw = 0.212671f;
        private const float Gw = 0.715160f;
        private const float Bw = 0.072169f;

        public static Bitmap Apply(this ColorMatrix matrix, Bitmap bmp)
        {
            Bitmap dest = bmp.CreateEmptyBitmap();
            Rectangle destRect = new Rectangle(0, 0, dest.Width, dest.Height);
            Apply(matrix, bmp, dest, destRect);
            return dest;
        }

        public static void Apply(this ColorMatrix matrix, Bitmap src, Bitmap dest, Rectangle destRect)
        {
            using (Graphics g = Graphics.FromImage(dest))
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.ClearColorMatrix();
                ia.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                g.SetHighQuality();
                g.DrawImage(src, destRect, 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
            }
        }

        public static Bitmap ChangeGamma(Bitmap bmp, float value)
        {
            value = value.Clamp(0.1f, 5.0f);

            Bitmap bmpResult = bmp.CreateEmptyBitmap();

            using (Graphics g = Graphics.FromImage(bmpResult))
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.ClearColorMatrix();
                ia.SetGamma(value, ColorAdjustType.Bitmap);
                g.SetHighQuality();
                g.DrawImage(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, ia);
            }

            return bmpResult;
        }

        public static ColorMatrix Inverse()
        {
            return new ColorMatrix(new[]
            {
                new float[] { -1, 0, 0, 0, 0 },
                new float[] { 0, -1, 0, 0, 0 },
                new float[] { 0, 0, -1, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 1, 1, 1, 0, 1 }
            });
        }

        public static ColorMatrix Alpha(float value, float add = 0f)
        {
            return new ColorMatrix(new[]
            {
                new float[] { 1, 0, 0, 0, 0 },
                new float[] { 0, 1, 0, 0, 0 },
                new float[] { 0, 0, 1, 0, 0 },
                new float[] { 0, 0, 0, value, 0 },
                new float[] { 0, 0, 0, add, 1 }
            });
        }

        public static ColorMatrix Brightness(float value)
        {
            return new ColorMatrix(new[]
            {
                new float[] { 1, 0, 0, 0, 0 },
                new float[] { 0, 1, 0, 0, 0 },
                new float[] { 0, 0, 1, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { value, value, value, 0, 1 }
            });
        }

        public static ColorMatrix Contrast(float value)
        {
            return new ColorMatrix(new[]
            {
                new float[] { value, 0, 0, 0, 0 },
                new float[] { 0, value, 0, 0, 0 },
                new float[] { 0, 0, value, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 0, 0, 0, 0, 1 }
            });
        }

        public static ColorMatrix BlackWhite()
        {
            return new ColorMatrix(new[]
            {
                new float[] { 1.5f, 1.5f, 1.5f, 0, 0 },
                new float[] { 1.5f, 1.5f, 1.5f, 0, 0 },
                new float[] { 1.5f, 1.5f, 1.5f, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { -1, -1, -1, 0, 1 }
            });
        }

        public static ColorMatrix Polaroid()
        {
            return new ColorMatrix(new[]
            {
                new float[] { 1.438f, -0.062f, -0.062f, 0, 0 },
                new float[] { -0.122f, 1.378f, -0.122f, 0, 0 },
                new float[] { -0.016f, -0.016f, 1.483f, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { -0.03f, 0.05f, -0.02f, 0, 1 }
            });
        }

        public static ColorMatrix Grayscale(float value = 1)
        {
            return new ColorMatrix(new[]
            {
                new float[] { Rw * value, Rw * value, Rw * value, 0, 0 },
                new float[] { Gw * value, Gw * value, Gw * value, 0, 0 },
                new float[] { Bw * value, Bw * value, Bw * value, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 0, 0, 0, 0, 1 }
            });
        }

        public static ColorMatrix Sepia(float value = 1)
        {
            return new ColorMatrix(new[]
            {
                new float[] { 0.393f * value, 0.349f * value, 0.272f * value, 0, 0 },
                new float[] { 0.769f * value, 0.686f * value, 0.534f * value, 0, 0 },
                new float[] { 0.189f * value, 0.168f * value, 0.131f * value, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 0, 0, 0, 0, 1 }
            });
        }

        public static ColorMatrix Hue(float angle)
        {
            float a = angle * (float)(Math.PI / 180);
            float c = (float)Math.Cos(a);
            float s = (float)Math.Sin(a);

            return new ColorMatrix(new[]
            {
                new float[] { (Rw + (c * (1 - Rw))) + (s * -Rw), (Rw + (c * -Rw)) + (s * 0.143f), (Rw + (c * -Rw)) + (s * -(1 - Rw)), 0, 0 },
                new float[] { (Gw + (c * -Gw)) + (s * -Gw), (Gw + (c * (1 - Gw))) + (s * 0.14f), (Gw + (c * -Gw)) + (s * Gw), 0, 0 },
                new float[] { (Bw + (c * -Bw)) + (s * (1 - Bw)), (Bw + (c * -Bw)) + (s * -0.283f), (Bw + (c * (1 - Bw))) + (s * Bw), 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 0, 0, 0, 0, 1 }
            });
        }

        public static ColorMatrix Saturation(float value)
        {
            return new ColorMatrix(new[]
            {
                new float[] { ((1.0f - value) * Rw) + value, (1.0f - value) * Rw, (1.0f - value) * Rw, 0, 0 },
                new float[] { (1.0f - value) * Gw, ((1.0f - value) * Gw) + value, (1.0f - value) * Gw, 0, 0 },
                new float[] { (1.0f - value) * Bw, (1.0f - value) * Bw, ((1.0f - value) * Bw) + value, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 0, 0, 0, 0, 1 }
            });
        }

        public static ColorMatrix Colorize(Color color, float value)
        {
            float r = color.R / 255f;
            float g = color.G / 255f;
            float b = color.B / 255f;
            float invAmount = 1 - value;

            return new ColorMatrix(new[]
            {
                new float[] { invAmount + (value * r * Rw), value * g * Rw, value * b * Rw, 0, 0 },
                new float[] { value * r * Gw, invAmount + (value * g * Gw), value * b * Gw, 0, 0 },
                new float[] { value * r * Bw, value * g * Bw, invAmount + (value * b * Bw), 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 0, 0, 0, 0, 1 }
            });
        }

        public static ColorMatrix Mask(float opacity, Color color)
        {
            return new ColorMatrix(new[]
            {
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, color.A / 255f * opacity, 0 },
                new float[] { ((float)color.R).Remap(0, 255, 0, 1), ((float)color.G).Remap(0, 255, 0, 1), ((float)color.B).Remap(0, 255, 0, 1), 0, 1 }
            });
        }
    }
}

