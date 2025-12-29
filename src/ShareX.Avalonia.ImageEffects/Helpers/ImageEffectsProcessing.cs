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
using System.Drawing.Drawing2D;
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

        public static Bitmap CropBitmap(Bitmap bmp, Rectangle rect)
        {
            Rectangle bounds = new Rectangle(0, 0, bmp.Width, bmp.Height);
            if (rect.Width <= 0 || rect.Height <= 0 || !bounds.Contains(rect))
            {
                return bmp;
            }

            return bmp.Clone(rect, PixelFormat.Format32bppArgb);
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

        public static Bitmap Pixelate(Bitmap bmp, int size, int borderSize = 0, Color? borderColor = null)
        {
            size = Math.Max(1, size);
            Bitmap result = (Bitmap)bmp.Clone();

            using (UnsafeBitmap source = new UnsafeBitmap(bmp, true, ImageLockMode.ReadOnly))
            using (UnsafeBitmap dest = new UnsafeBitmap(result, true, ImageLockMode.WriteOnly))
            {
                for (int y = 0; y < source.Height; y += size)
                {
                    for (int x = 0; x < source.Width; x += size)
                    {
                        int maxX = Math.Min(x + size, source.Width);
                        int maxY = Math.Min(y + size, source.Height);
                        int r = 0, g = 0, b = 0, a = 0, count = 0;

                        for (int yy = y; yy < maxY; yy++)
                        {
                            for (int xx = x; xx < maxX; xx++)
                            {
                                ColorBgra c = source.GetPixel(xx, yy);
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
                        ColorBgra avg = new ColorBgra(bb, gb, rb, ab);

                        for (int yy = y; yy < maxY; yy++)
                        {
                            for (int xx = x; xx < maxX; xx++)
                            {
                                dest.SetPixel(xx, yy, avg);
                            }
                        }
                    }
                }
            }

            if (borderSize > 0 && borderColor.HasValue)
            {
                using (Graphics g = Graphics.FromImage(result))
                using (Pen pen = new Pen(borderColor.Value, borderSize) { Alignment = PenAlignment.Inset })
                {
                    g.DrawRectangle(pen, 0, 0, result.Width, result.Height);
                }
            }

            return result;
        }

        public static Bitmap RotateImage(Bitmap bmp, float angle, bool upsize, bool clip)
        {
            if (angle == 0)
            {
                return bmp;
            }

            float rad = angle * (float)(Math.PI / 180.0);
            float cos = Math.Abs((float)Math.Cos(rad));
            float sin = Math.Abs((float)Math.Sin(rad));

            int newWidth = bmp.Width;
            int newHeight = bmp.Height;

            if (upsize)
            {
                newWidth = (int)Math.Ceiling((bmp.Width * cos) + (bmp.Height * sin));
                newHeight = (int)Math.Ceiling((bmp.Width * sin) + (bmp.Height * cos));
            }

            Bitmap rotated = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);
            rotated.SetResolution(bmp.HorizontalResolution, bmp.VerticalResolution);

            using (Graphics g = Graphics.FromImage(rotated))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;

                g.TranslateTransform(newWidth / 2f, newHeight / 2f);
                g.RotateTransform(angle);
                g.TranslateTransform(-bmp.Width / 2f, -bmp.Height / 2f);

                Rectangle destRect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                g.DrawImage(bmp, destRect, new Rectangle(0, 0, bmp.Width, bmp.Height), GraphicsUnit.Pixel);
            }

            if (!upsize && clip)
            {
                int x = (rotated.Width - bmp.Width) / 2;
                int y = (rotated.Height - bmp.Height) / 2;
                Rectangle crop = new Rectangle(Math.Max(0, x), Math.Max(0, y), Math.Min(bmp.Width, rotated.Width), Math.Min(bmp.Height, rotated.Height));
                Bitmap clipped = rotated.Clone(crop, PixelFormat.Format32bppArgb);
                rotated.Dispose();
                return clipped;
            }

            return rotated;
        }

        public static Bitmap ResizeImage(Bitmap bmp, Size size)
        {
            if (size.Width < 1 || size.Height < 1 || (bmp.Width == size.Width && bmp.Height == size.Height))
            {
                return bmp;
            }

            Bitmap bmpResult = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            bmpResult.SetResolution(bmp.HorizontalResolution, bmp.VerticalResolution);

            using (Graphics g = Graphics.FromImage(bmpResult))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;

                using (ImageAttributes ia = new ImageAttributes())
                {
                    ia.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(bmp, new Rectangle(0, 0, size.Width, size.Height), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, ia);
                }
            }

            return bmpResult;
        }

        public static Size ApplyAspectRatio(int width, int height, Bitmap bmp)
        {
            int newWidth;
            int newHeight;

            if (width == 0 && height == 0)
            {
                return new Size(bmp.Width, bmp.Height);
            }
            else if (width == 0)
            {
                newWidth = (int)Math.Round((float)height / bmp.Height * bmp.Width);
                newHeight = height;
            }
            else if (height == 0)
            {
                newWidth = width;
                newHeight = (int)Math.Round((float)width / bmp.Width * bmp.Height);
            }
            else
            {
                newWidth = width;
                newHeight = height;
            }

            return new Size(newWidth, newHeight);
        }
    }
}
