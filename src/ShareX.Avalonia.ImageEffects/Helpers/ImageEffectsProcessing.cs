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
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using DrawingPoint = System.Drawing.Point;

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

        public static Bitmap Slice(Bitmap bmp, int minSliceHeight, int maxSliceHeight, int minSliceShift, int maxSliceShift)
        {
            if (minSliceHeight < 1 || maxSliceHeight < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(minSliceHeight));
            }

            if (minSliceShift < 0 || maxSliceShift < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minSliceShift));
            }

            if (maxSliceHeight < minSliceHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSliceHeight));
            }

            Bitmap bmpResult = bmp.CreateEmptyBitmap();

            using (Graphics g = Graphics.FromImage(bmpResult))
            {
                int y = 0;

                while (y < bmp.Height)
                {
                    Rectangle sourceRect = new Rectangle(0, y, bmp.Width, RandomFast.Next(minSliceHeight, maxSliceHeight));
                    Rectangle destRect = sourceRect;

                    if (RandomFast.Next(1) == 0)
                    {
                        destRect.X = RandomFast.Next(-maxSliceShift, -minSliceShift);
                    }
                    else
                    {
                        destRect.X = RandomFast.Next(minSliceShift, maxSliceShift);
                    }

                    g.DrawImage(bmp, destRect, sourceRect, GraphicsUnit.Pixel);

                    y += sourceRect.Height;
                }
            }

            return bmpResult;
        }

        public static Bitmap AddCanvas(Image img, CanvasMargin margin, Color canvasColor)
        {
            if (margin.Horizontal == 0 && margin.Vertical == 0)
            {
                return null;
            }

            int width = img.Width + margin.Horizontal;
            int height = img.Height + margin.Vertical;

            if (width < 1 || height < 1)
            {
                return null;
            }

            Bitmap bmp = img.CreateEmptyBitmap(margin.Horizontal, margin.Vertical);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.DrawImage(img, margin.Left, margin.Top, img.Width, img.Height);

                if (canvasColor.A > 0)
                {
                    g.CompositingMode = CompositingMode.SourceCopy;

                    using (Brush brush = new SolidBrush(canvasColor))
                    {
                        if (margin.Left > 0)
                        {
                            g.FillRectangle(brush, 0, 0, margin.Left, bmp.Height);
                        }

                        if (margin.Top > 0)
                        {
                            g.FillRectangle(brush, 0, 0, bmp.Width, margin.Top);
                        }

                        if (margin.Right > 0)
                        {
                            g.FillRectangle(brush, bmp.Width - margin.Right, 0, margin.Right, bmp.Height);
                        }

                        if (margin.Bottom > 0)
                        {
                            g.FillRectangle(brush, 0, bmp.Height - margin.Bottom, bmp.Width, margin.Bottom);
                        }
                    }
                }
            }

            return bmp;
        }

        public static Bitmap CreateGradientMask(Bitmap bmp, GradientInfo gradient, float opacity = 1f)
        {
            Bitmap mask = (Bitmap)bmp.Clone();

            if (opacity <= 0 || gradient == null || !gradient.IsValid)
            {
                return mask;
            }

            gradient.Draw(mask);

            using (UnsafeBitmap bmpSource = new UnsafeBitmap(bmp, true, ImageLockMode.ReadOnly))
            using (UnsafeBitmap bmpMask = new UnsafeBitmap(mask, true, ImageLockMode.ReadWrite))
            {
                int pixelCount = bmpSource.PixelCount;

                for (int i = 0; i < pixelCount; i++)
                {
                    ColorBgra sourceColor = bmpSource.GetPixel(i);
                    ColorBgra maskColor = bmpMask.GetPixel(i);
                    maskColor.Alpha = (byte)Math.Min(255, sourceColor.Alpha * (maskColor.Alpha / 255f) * opacity);
                    bmpMask.SetPixel(i, maskColor);
                }
            }

            return mask;
        }

        public static Bitmap AddGlow(Bitmap bmp, int size, float strength, Color color, DrawingPoint offset, GradientInfo? gradient = null)
        {
            if (size < 0 || strength < 0.1f)
            {
                return bmp;
            }

            Bitmap bmpBlur = null;
            Bitmap bmpMask = null;

            try
            {
                if (size > 0)
                {
                    bmpBlur = AddCanvas(bmp, new CanvasMargin(size), Color.Transparent) ?? bmp;
                    BoxBlur(bmpBlur, size);
                }
                else
                {
                    bmpBlur = bmp;
                }

                if (gradient != null && gradient.IsValid)
                {
                    bmpMask = CreateGradientMask(bmpBlur, gradient, strength);
                }
                else
                {
                    bmpMask = ColorMatrixManager.Mask(strength, color).Apply(bmpBlur);
                }

                Bitmap bmpResult = bmpMask.CreateEmptyBitmap(Math.Abs(offset.X), Math.Abs(offset.Y));

                using (Graphics g = Graphics.FromImage(bmpResult))
                {
                    g.DrawImage(bmpMask, Math.Max(0, offset.X), Math.Max(0, offset.Y), bmpMask.Width, bmpMask.Height);
                    g.DrawImage(bmp, Math.Max(size, -offset.X + size), Math.Max(size, -offset.Y + size), bmp.Width, bmp.Height);
                }

                return bmpResult;
            }
            finally
            {
                bmp?.Dispose();
                bmpBlur?.Dispose();
                bmpMask?.Dispose();
            }
        }

        public static Bitmap AddShadow(Bitmap bmp, float opacity, int size)
        {
            return AddShadow(bmp, opacity, size, 1, Color.Black, new DrawingPoint(0, 0));
        }

        public static Bitmap AddShadow(Bitmap bmp, float opacity, int size, float darkness, Color color, DrawingPoint offset, bool autoResize = true)
        {
            Bitmap bmpShadow = null;

            try
            {
                bmpShadow = bmp.CreateEmptyBitmap(size * 2, size * 2);
                Rectangle shadowRectangle = new Rectangle(size, size, bmp.Width, bmp.Height);
                ColorMatrixManager.Mask(opacity, color).Apply(bmp, bmpShadow, shadowRectangle);

                if (size > 0)
                {
                    BoxBlur(bmpShadow, size);
                }

                if (darkness > 1)
                {
                    Bitmap shadowImage2 = ColorMatrixManager.Alpha(darkness).Apply(bmpShadow);
                    bmpShadow.Dispose();
                    bmpShadow = shadowImage2;
                }

                Bitmap bmpResult;

                if (autoResize)
                {
                    bmpResult = bmpShadow.CreateEmptyBitmap(Math.Abs(offset.X), Math.Abs(offset.Y));

                    using (Graphics g = Graphics.FromImage(bmpResult))
                    {
                        g.DrawImage(bmpShadow, Math.Max(0, offset.X), Math.Max(0, offset.Y), bmpShadow.Width, bmpShadow.Height);
                        g.DrawImage(bmp, Math.Max(size, -offset.X + size), Math.Max(size, -offset.Y + size), bmp.Width, bmp.Height);
                    }
                }
                else
                {
                    bmpResult = bmp.CreateEmptyBitmap();

                    using (Graphics g = Graphics.FromImage(bmpResult))
                    {
                        g.DrawImage(bmpShadow, -size + offset.X, -size + offset.Y, bmpShadow.Width, bmpShadow.Height);
                        g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
                    }
                }

                return bmpResult;
            }
            finally
            {
                bmp?.Dispose();
                bmpShadow?.Dispose();
            }
        }

        [Flags]
        public enum AnchorSides
        {
            None = 0,
            Left = 1,
            Top = 2,
            Right = 4,
            Bottom = 8,
            All = Left | Top | Right | Bottom
        }

        public static Rectangle FindAutoCropRectangle(Bitmap bmp, bool sameColorCrop = false, AnchorSides sides = AnchorSides.All)
        {
            Rectangle source = new Rectangle(0, 0, bmp.Width, bmp.Height);

            if (sides == AnchorSides.None)
            {
                return source;
            }

            Rectangle crop = source;

            using (UnsafeBitmap unsafeBitmap = new UnsafeBitmap(bmp, true, ImageLockMode.ReadOnly))
            {
                bool leave = false;

                ColorBgra checkColor = unsafeBitmap.GetPixel(0, 0);
                uint mask = checkColor.Alpha == 0 ? 0xFF000000 : 0xFFFFFFFF;
                uint check = checkColor.Bgra & mask;

                if (sides.HasFlag(AnchorSides.Left))
                {
                    for (int x = 0; x < bmp.Width && !leave; x++)
                    {
                        for (int y = 0; y < bmp.Height; y++)
                        {
                            if ((unsafeBitmap.GetPixel(x, y).Bgra & mask) != check)
                            {
                                crop.X = x;
                                crop.Width -= x;
                                leave = true;
                                break;
                            }
                        }
                    }

                    if (!leave)
                    {
                        return crop;
                    }

                    leave = false;
                }

                if (sides.HasFlag(AnchorSides.Top))
                {
                    for (int y = 0; y < bmp.Height && !leave; y++)
                    {
                        for (int x = 0; x < bmp.Width; x++)
                        {
                            if ((unsafeBitmap.GetPixel(x, y).Bgra & mask) != check)
                            {
                                crop.Y = y;
                                crop.Height -= y;
                                leave = true;
                                break;
                            }
                        }
                    }
                }

                if (!sameColorCrop)
                {
                    checkColor = unsafeBitmap.GetPixel(bmp.Width - 1, bmp.Height - 1);
                    mask = checkColor.Alpha == 0 ? 0xFF000000 : 0xFFFFFFFF;
                    check = checkColor.Bgra & mask;
                }

                if (sides.HasFlag(AnchorSides.Right))
                {
                    leave = false;
                    for (int x = bmp.Width - 1; x >= 0 && !leave; x--)
                    {
                        for (int y = 0; y < bmp.Height; y++)
                        {
                            if ((unsafeBitmap.GetPixel(x, y).Bgra & mask) != check)
                            {
                                crop.Width = x - crop.X + 1;
                                leave = true;
                                break;
                            }
                        }
                    }
                }

                if (sides.HasFlag(AnchorSides.Bottom))
                {
                    leave = false;
                    for (int y = bmp.Height - 1; y >= 0 && !leave; y--)
                    {
                        for (int x = 0; x < bmp.Width; x++)
                        {
                            if ((unsafeBitmap.GetPixel(x, y).Bgra & mask) != check)
                            {
                                crop.Height = y - crop.Y + 1;
                                leave = true;
                                break;
                            }
                        }
                    }
                }
            }

            return crop;
        }

        public static Bitmap AutoCropImage(Bitmap bmp, bool sameColorCrop, AnchorSides sides, int padding)
        {
            Rectangle source = new Rectangle(0, 0, bmp.Width, bmp.Height);
            Rectangle crop = FindAutoCropRectangle(bmp, sameColorCrop, sides);

            if (source == crop)
            {
                return bmp;
            }

            Bitmap croppedBitmap = CropBitmap(bmp, crop);

            if (croppedBitmap == null)
            {
                return bmp;
            }

            using (bmp)
            {
                if (padding > 0)
                {
                    using (croppedBitmap)
                    {
                        Color color = bmp.GetPixel(0, 0);
                        Bitmap padded = AddCanvas(croppedBitmap, new CanvasMargin(padding), color);
                        return padded ?? bmp;
                    }
                }

                return croppedBitmap;
            }
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

        public static Bitmap AddReflection(Image img, int percentage, int maxAlpha, int minAlpha)
        {
            percentage = MathHelpers.Clamp(percentage, 1, 100);
            maxAlpha = MathHelpers.Clamp(maxAlpha, 0, 255);
            minAlpha = MathHelpers.Clamp(minAlpha, 0, 255);

            Bitmap reflection;

            using (Bitmap bitmapRotate = (Bitmap)img.Clone())
            {
                bitmapRotate.RotateFlip(RotateFlipType.RotateNoneFlipY);
                reflection = bitmapRotate.Clone(new Rectangle(0, 0, bitmapRotate.Width, (int)(bitmapRotate.Height * ((float)percentage / 100))), PixelFormat.Format32bppArgb);
            }

            using (reflection)
            {
                Bitmap result = new Bitmap(reflection.Width, reflection.Height, PixelFormat.Format32bppArgb);
                ColorMatrix opacityMatrix = new ColorMatrix
                {
                    Matrix33 = 0
                };

                float alpha = maxAlpha - ((float)(maxAlpha - minAlpha) / reflection.Height);
                float addValue = (float)(maxAlpha - minAlpha) / reflection.Height;

                using (Graphics g = Graphics.FromImage(result))
                {
                    g.SetHighQuality();
                    g.Clear(Color.Transparent);
                    for (int i = 0; i < reflection.Height; i++)
                    {
                        opacityMatrix.Matrix33 = alpha / 255;

                        alpha -= addValue;

                        ImageAttributes ia = new ImageAttributes();
                        ia.SetColorMatrix(opacityMatrix);
                        g.DrawImage(reflection, new Rectangle(0, i, reflection.Width, 1), 0, i, reflection.Width, 1, GraphicsUnit.Pixel, ia);
                    }
                }

                return result;
            }
        }

        public static Bitmap DrawReflection(Bitmap bmp, int percentage, int maxAlpha, int minAlpha, int offset, bool skew, int skewSize)
        {
            Bitmap reflection = AddReflection(bmp, percentage, maxAlpha, minAlpha);

            if (skew)
            {
                reflection = AddSkew(reflection, skewSize, 0);
            }

            Bitmap bmpResult = new Bitmap(reflection.Width, bmp.Height + reflection.Height + offset);

            using (bmp)
            using (reflection)
            using (Graphics g = Graphics.FromImage(bmpResult))
            {
                g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
                g.DrawImage(reflection, 0, bmp.Height + offset, reflection.Width, reflection.Height);
            }

            return bmpResult;
        }

        public static Bitmap WavyEdges(Bitmap bmp, int waveDepth, int waveRange, AnchorSides sides, Color backgroundColor)
        {
            if (waveDepth < 1 || waveRange < 1 || sides == AnchorSides.None)
            {
                return bmp;
            }

            List<DrawingPoint> points = new List<DrawingPoint>();

            int horizontalWaveCount = Math.Max(2, (bmp.Width / waveRange + 1) / 2 * 2) - 1;
            int verticalWaveCount = Math.Max(2, (bmp.Height / waveRange + 1) / 2 * 2) - 1;
            int horizontalWaveRange = bmp.Width / horizontalWaveCount;
            int verticalWaveRange = bmp.Height / verticalWaveCount;

            int step = Math.Min(Math.Max(1, waveRange / waveDepth), 10);

            int WaveFunc(int t, int max, int depth) => (int)((1 - Math.Cos(t * Math.PI / max)) * depth / 2);

            if (sides.HasFlag(AnchorSides.Top))
            {
                int startX = sides.HasFlag(AnchorSides.Left) ? waveDepth : 0;
                int endX = sides.HasFlag(AnchorSides.Right) ? bmp.Width - waveDepth : bmp.Width;
                for (int x = startX; x < endX; x += step)
                {
                    points.Add(new DrawingPoint(x, WaveFunc(x, horizontalWaveRange, waveDepth)));
                }
                points.Add(new DrawingPoint(endX, WaveFunc(endX, horizontalWaveRange, waveDepth)));
            }
            else
            {
                points.Add(new DrawingPoint(0, 0));
            }

            if (sides.HasFlag(AnchorSides.Right))
            {
                int startY = sides.HasFlag(AnchorSides.Top) ? waveDepth : 0;
                int endY = sides.HasFlag(AnchorSides.Bottom) ? bmp.Height - waveDepth : bmp.Height;
                for (int y = startY; y < endY; y += step)
                {
                    points.Add(new DrawingPoint(bmp.Width - waveDepth + WaveFunc(y, verticalWaveRange, waveDepth), y));
                }
                points.Add(new DrawingPoint(bmp.Width - waveDepth + WaveFunc(endY, verticalWaveRange, waveDepth), endY));
            }
            else
            {
                points.Add(new DrawingPoint(bmp.Width, points[^1].Y));
            }

            if (sides.HasFlag(AnchorSides.Bottom))
            {
                int startX = sides.HasFlag(AnchorSides.Right) ? bmp.Width - waveDepth : bmp.Width;
                int endX = sides.HasFlag(AnchorSides.Left) ? waveDepth : 0;
                for (int x = startX; x >= endX; x -= step)
                {
                    points.Add(new DrawingPoint(x, bmp.Height - waveDepth + WaveFunc(x, horizontalWaveRange, waveDepth)));
                }
            }
            else
            {
                points.Add(new DrawingPoint(points[^1].X, bmp.Height));
            }

            if (sides.HasFlag(AnchorSides.Left))
            {
                int startY = sides.HasFlag(AnchorSides.Bottom) ? bmp.Height - waveDepth : bmp.Height;
                int endY = sides.HasFlag(AnchorSides.Top) ? waveDepth : 0;
                for (int y = startY; y >= endY; y -= step)
                {
                    points.Add(new DrawingPoint(waveDepth - WaveFunc(y, verticalWaveRange, waveDepth), y));
                }
            }
            else
            {
                points.Add(new DrawingPoint(0, points[^1].Y));
            }

            if (!sides.HasFlag(AnchorSides.Top))
            {
                points[0] = new DrawingPoint(points[^1].X, 0);
            }

            Bitmap bmpResult = bmp.CreateEmptyBitmap();

            using (bmp)
            using (Graphics g = Graphics.FromImage(bmpResult))
            using (TextureBrush brush = new TextureBrush(bmp))
            {
                if (backgroundColor.A > 0)
                {
                    g.Clear(backgroundColor);
                }

                g.SetHighQuality();
                g.PixelOffsetMode = PixelOffsetMode.Half;

                g.FillPolygon(brush, points.ToArray());
            }

            return bmpResult;
        }

        public static Bitmap TornEdges(Bitmap bmp, int tornDepth, int tornRange, AnchorSides sides, bool curvedEdges, bool random, Color backgroundColor)
        {
            if (tornDepth < 1 || tornRange < 1 || sides == AnchorSides.None)
            {
                return bmp;
            }

            List<DrawingPoint> points = new List<DrawingPoint>();

            int horizontalTornCount = bmp.Width / tornRange;
            int verticalTornCount = bmp.Height / tornRange;

            if (horizontalTornCount < 2 && verticalTornCount < 2)
            {
                points.Add(new DrawingPoint(0, 0));
                points.Add(new DrawingPoint(bmp.Width, 0));
            }

            if (sides.HasFlag(AnchorSides.Right) && verticalTornCount > 1)
            {
                int startY = (sides.HasFlag(AnchorSides.Top) && horizontalTornCount > 1) ? tornDepth : 0;
                int endY = (sides.HasFlag(AnchorSides.Bottom) && horizontalTornCount > 1) ? bmp.Height - tornDepth : bmp.Height;
                for (int y = startY; y < endY; y += tornRange)
                {
                    int x = random ? RandomFast.Next(0, tornDepth) : ((y / tornRange) & 1) * tornDepth;
                    points.Add(new DrawingPoint(bmp.Width - tornDepth + x, y));
                }
            }
            else
            {
                points.Add(new DrawingPoint(bmp.Width, 0));
                points.Add(new DrawingPoint(bmp.Width, bmp.Height));
            }

            if (sides.HasFlag(AnchorSides.Bottom) && horizontalTornCount > 1)
            {
                int startX = (sides.HasFlag(AnchorSides.Right) && verticalTornCount > 1) ? bmp.Width - tornDepth : bmp.Width;
                int endX = (sides.HasFlag(AnchorSides.Left) && verticalTornCount > 1) ? tornDepth : 0;
                for (int x = startX; x >= endX; x = (x / tornRange - 1) * tornRange)
                {
                    int y = random ? RandomFast.Next(0, tornDepth) : ((x / tornRange) & 1) * tornDepth;
                    points.Add(new DrawingPoint(x, bmp.Height - tornDepth + y));
                }
            }
            else
            {
                points.Add(new DrawingPoint(bmp.Width, bmp.Height));
                points.Add(new DrawingPoint(0, bmp.Height));
            }

            if (sides.HasFlag(AnchorSides.Left) && verticalTornCount > 1)
            {
                int startY = (sides.HasFlag(AnchorSides.Bottom) && horizontalTornCount > 1) ? bmp.Height - tornDepth : bmp.Height;
                int endY = (sides.HasFlag(AnchorSides.Top) && horizontalTornCount > 1) ? tornDepth : 0;
                for (int y = startY; y >= endY; y = (y / tornRange - 1) * tornRange)
                {
                    int x = random ? RandomFast.Next(0, tornDepth) : ((y / tornRange) & 1) * tornDepth;
                    points.Add(new DrawingPoint(x, y));
                }
            }
            else
            {
                points.Add(new DrawingPoint(0, bmp.Height));
                points.Add(new DrawingPoint(0, 0));
            }

            Bitmap bmpResult = bmp.CreateEmptyBitmap();

            using (bmp)
            using (Graphics g = Graphics.FromImage(bmpResult))
            using (TextureBrush brush = new TextureBrush(bmp))
            {
                if (backgroundColor.A > 0)
                {
                    g.Clear(backgroundColor);
                }

                if (curvedEdges)
                {
                    g.SetHighQuality();
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.FillClosedCurve(brush, points.ToArray());
                }
                else
                {
                    g.FillPolygon(brush, points.ToArray());
                }
            }

            return bmpResult;
        }

        public static Bitmap AddSkew(Image img, int x, int y)
        {
            Bitmap result = img.CreateEmptyBitmap(Math.Abs(x), Math.Abs(y));

            using (img)
            using (Graphics g = Graphics.FromImage(result))
            {
                g.SetHighQuality();
                int startX = -Math.Min(0, x);
                int startY = -Math.Min(0, y);
                int endX = Math.Max(0, x);
                int endY = Math.Max(0, y);
                System.Drawing.Point[] destinationPoints =
                {
                    new System.Drawing.Point(startX, startY),
                    new System.Drawing.Point(startX + img.Width - 1, endY),
                    new System.Drawing.Point(endX, startY + img.Height - 1)
                };
                g.DrawImage(img, destinationPoints);
            }

            return result;
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

        public static Bitmap ResizeImage(Bitmap bmp, int width, int height, bool allowEnlarge, bool centerImage, Color backColor)
        {
            if (!allowEnlarge && bmp.Width <= width && bmp.Height <= height)
            {
                return bmp;
            }

            double ratioX = (double)width / bmp.Width;
            double ratioY = (double)height / bmp.Height;
            double ratio = Math.Min(ratioX, ratioY);
            int newWidth = (int)(bmp.Width * ratio);
            int newHeight = (int)(bmp.Height * ratio);

            int offsetX = centerImage ? (width - newWidth) / 2 : 0;
            int offsetY = centerImage ? (height - newHeight) / 2 : 0;

            Bitmap bmpResult = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            bmpResult.SetResolution(bmp.HorizontalResolution, bmp.VerticalResolution);

            using (Graphics g = Graphics.FromImage(bmpResult))
            {
                if (backColor.A > 0)
                {
                    g.Clear(backColor);
                }

                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;

                g.DrawImage(bmp, offsetX, offsetY, newWidth, newHeight);
            }

            return bmpResult;
        }

        public static Bitmap Outline(Bitmap bmp, int borderSize, Color borderColor, int padding = 0, bool outlineOnly = false)
        {
            Bitmap outline = MakeOutline(bmp, padding, padding + borderSize + 1, borderColor);

            if (outlineOnly)
            {
                bmp.Dispose();
                return outline;
            }

            using (outline)
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.DrawImage(outline, 0, 0, outline.Width, outline.Height);
            }

            return bmp;
        }

        public static Bitmap MakeOutline(Bitmap bmp, int minRadius, int maxRadius, Color color)
        {
            Bitmap bmpResult = bmp.CreateEmptyBitmap();

            using (UnsafeBitmap source = new UnsafeBitmap(bmp, true, ImageLockMode.ReadOnly))
            using (UnsafeBitmap dest = new UnsafeBitmap(bmpResult, true, ImageLockMode.WriteOnly))
            {
                for (int x = 0; x < source.Width; x++)
                {
                    for (int y = 0; y < source.Height; y++)
                    {
                        float dist = DistanceToThreshold(source, x, y, maxRadius, 255);

                        if (dist > minRadius && dist < maxRadius)
                        {
                            byte alpha = 255;

                            if (dist - minRadius < 1)
                            {
                                alpha = (byte)(255 * (dist - minRadius));
                            }
                            else if (maxRadius - dist < 1)
                            {
                                alpha = (byte)(255 * (maxRadius - dist));
                            }

                            ColorBgra bgra = new ColorBgra(color.B, color.G, color.R, alpha);
                            dest.SetPixel(x, y, bgra);
                        }
                    }
                }
            }

            return bmpResult;
        }

        public static Bitmap RoundedCorners(Bitmap bmp, int cornerRadius)
        {
            Bitmap bmpResult = bmp.CreateEmptyBitmap();

            using (bmp)
            using (Graphics g = Graphics.FromImage(bmpResult))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.Half;

                using (GraphicsPath gp = new GraphicsPath())
                {
                    AddRoundedRectangle(gp, new RectangleF(0, 0, bmp.Width, bmp.Height), cornerRadius);

                    using (TextureBrush brush = new TextureBrush(bmp))
                    {
                        g.FillPath(brush, gp);
                    }
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

        private static float DistanceToThreshold(UnsafeBitmap unsafeBitmap, int x, int y, int radius, int threshold)
        {
            int minx = Math.Max(x - radius, 0);
            int maxx = Math.Min(x + radius, unsafeBitmap.Width - 1);
            int miny = Math.Max(y - radius, 0);
            int maxy = Math.Min(y + radius, unsafeBitmap.Height - 1);
            int dist2 = (radius * radius) + 1;

            for (int tx = minx; tx <= maxx; tx++)
            {
                for (int ty = miny; ty <= maxy; ty++)
                {
                    ColorBgra color = unsafeBitmap.GetPixel(tx, ty);

                    if (color.Alpha >= threshold)
                    {
                        int dx = tx - x;
                        int dy = ty - y;
                        int testDist2 = (dx * dx) + (dy * dy);
                        if (testDist2 < dist2)
                        {
                            dist2 = testDist2;
                        }
                    }
                }
            }

            return (float)Math.Sqrt(dist2);
        }

        public static Bitmap DrawBorder(Bitmap bmp, Color borderColor, int borderSize, BorderType borderType, DashStyle dashStyle = DashStyle.Solid)
        {
            using (Pen borderPen = new Pen(borderColor, borderSize) { Alignment = PenAlignment.Inset, DashStyle = dashStyle })
            {
                return DrawBorder(bmp, borderPen, borderType);
            }
        }

        public static Bitmap DrawBorder(Bitmap bmp, GradientInfo gradientInfo, int borderSize, BorderType borderType, DashStyle dashStyle = DashStyle.Solid)
        {
            int width = bmp.Width;
            int height = bmp.Height;

            if (borderType == BorderType.Outside)
            {
                width += borderSize * 2;
                height += borderSize * 2;
            }

            using (LinearGradientBrush brush = gradientInfo.GetGradientBrush(new Rectangle(0, 0, width, height)))
            using (Pen borderPen = new Pen(brush, borderSize) { Alignment = PenAlignment.Inset, DashStyle = dashStyle })
            {
                return DrawBorder(bmp, borderPen, borderType);
            }
        }

        public static Bitmap DrawBorder(Bitmap bmp, Pen borderPen, BorderType borderType)
        {
            Bitmap bmpResult;

            if (borderType == BorderType.Inside)
            {
                bmpResult = bmp;

                using (Graphics g = Graphics.FromImage(bmpResult))
                {
                    g.DrawRectangle(borderPen, 0, 0, bmp.Width, bmp.Height);
                }
            }
            else
            {
                int borderSize = (int)borderPen.Width;
                bmpResult = bmp.CreateEmptyBitmap(borderSize * 2, borderSize * 2);

                using (bmp)
                using (Graphics g = Graphics.FromImage(bmpResult))
                {
                    g.DrawRectangle(borderPen, 0, 0, bmpResult.Width, bmpResult.Height);
                    g.DrawImage(bmp, borderSize, borderSize, bmp.Width, bmp.Height);
                }
            }

            return bmpResult;
        }

        public static Bitmap FillBackground(Image img, Color color)
        {
            using (Brush brush = new SolidBrush(color))
            {
                return FillBackground(img, brush);
            }
        }

        public static Bitmap FillBackground(Image img, GradientInfo gradientInfo)
        {
            using (LinearGradientBrush brush = gradientInfo.GetGradientBrush(new Rectangle(0, 0, img.Width, img.Height)))
            {
                return FillBackground(img, brush);
            }
        }

        public static Bitmap FillBackground(Image img, Brush brush)
        {
            Bitmap result = img.CreateEmptyBitmap();

            using (Graphics g = Graphics.FromImage(result))
            {
                g.FillRectangle(brush, 0, 0, result.Width, result.Height);
                g.DrawImage(img, 0, 0, result.Width, result.Height);
            }

            return result;
        }

        public static Bitmap DrawBackgroundImage(Bitmap bmp, string backgroundImageFilePath, bool center = true, bool tile = false)
        {
            if (string.IsNullOrEmpty(backgroundImageFilePath) || !File.Exists(backgroundImageFilePath))
            {
                return bmp;
            }

            Bitmap? backgroundImage = null;

            try
            {
                backgroundImage = (Bitmap)Image.FromFile(backgroundImageFilePath);
                return DrawBackgroundImage(bmp, backgroundImage, center, tile);
            }
            catch
            {
                return bmp;
            }
        }

        public static Bitmap DrawBackgroundImage(Bitmap bmp, Bitmap backgroundImage, bool center = true, bool tile = false)
        {
            if (bmp == null || backgroundImage == null)
            {
                return bmp;
            }

            using (bmp)
            using (backgroundImage)
            {
                Bitmap bmpResult = bmp.CreateEmptyBitmap();

                using (Graphics g = Graphics.FromImage(bmpResult))
                {
                    if (tile)
                    {
                        using (TextureBrush tb = new TextureBrush(backgroundImage, WrapMode.Tile))
                        {
                            g.FillRectangle(tb, new Rectangle(0, 0, bmpResult.Width, bmpResult.Height));
                        }
                    }
                    else
                    {
                        int x = 0;
                        int y = 0;
                        int width = backgroundImage.Width;
                        int height = backgroundImage.Height;

                        if (center)
                        {
                            x = (bmpResult.Width - width) / 2;
                            y = (bmpResult.Height - height) / 2;
                        }

                        g.DrawImage(backgroundImage, x, y, width, height);
                    }

                    g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
                }

                return bmpResult;
            }
        }

        public static Bitmap DrawCheckers(Image img)
        {
            return DrawCheckers(img, 10, SystemColors.ControlLight, SystemColors.ControlLightLight);
        }

        public static Bitmap DrawCheckers(Image img, int checkerSize, Color checkerColor1, Color checkerColor2)
        {
            Bitmap bmpResult = img.CreateEmptyBitmap();

            using (img)
            using (Graphics g = Graphics.FromImage(bmpResult))
            using (Image checker = CreateCheckerPattern(checkerSize, checkerSize, checkerColor1, checkerColor2))
            using (Brush checkerBrush = new TextureBrush(checker, WrapMode.Tile))
            {
                g.FillRectangle(checkerBrush, new Rectangle(0, 0, bmpResult.Width, bmpResult.Height));
                g.DrawImage(img, 0, 0, img.Width, img.Height);
            }

            return bmpResult;
        }

        public static Bitmap DrawCheckers(int width, int height, int checkerSize, Color checkerColor1, Color checkerColor2)
        {
            Bitmap bmp = new Bitmap(width, height);

            using (Graphics g = Graphics.FromImage(bmp))
            using (Image checker = CreateCheckerPattern(checkerSize, checkerSize, checkerColor1, checkerColor2))
            using (Brush checkerBrush = new TextureBrush(checker, WrapMode.Tile))
            {
                g.FillRectangle(checkerBrush, new Rectangle(0, 0, bmp.Width, bmp.Height));
            }

            return bmp;
        }

        public static Bitmap CreateCheckerPattern(int width, int height, Color checkerColor1, Color checkerColor2)
        {
            Bitmap bmp = new Bitmap(width * 2, height * 2);

            using (Graphics g = Graphics.FromImage(bmp))
            using (Brush brush1 = new SolidBrush(checkerColor1))
            using (Brush brush2 = new SolidBrush(checkerColor2))
            {
                g.FillRectangle(brush1, 0, 0, width, height);
                g.FillRectangle(brush1, width, height, width, height);
                g.FillRectangle(brush2, width, 0, width, height);
                g.FillRectangle(brush2, 0, height, width, height);
            }

            return bmp;
        }

        private static void AddRoundedRectangle(GraphicsPath graphicsPath, RectangleF rect, float radius)
        {
            if (radius <= 0f)
            {
                graphicsPath.AddRectangle(rect);
                return;
            }

            if (radius >= Math.Min(rect.Width, rect.Height) / 2f)
            {
                graphicsPath.AddEllipse(rect);
                return;
            }

            float diameter = radius * 2f;
            SizeF size = new SizeF(diameter, diameter);
            RectangleF arc = new RectangleF(rect.Location, size);

            graphicsPath.AddArc(arc, 180, 90);
            arc.X = rect.Right - diameter;
            graphicsPath.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - diameter;
            graphicsPath.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            graphicsPath.AddArc(arc, 90, 90);
            graphicsPath.CloseFigure();
        }
    }
}
