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


using System;
using SkiaSharp;
using ShareX.Avalonia.Common;

namespace ShareX.Avalonia.ImageEffects.Helpers
{
    public static class ImageEffectsProcessing
    {
        public static SKBitmap ApplyBlur(SKBitmap bmp, int radius)
        {
            if (radius <= 0) return bmp;

            using var paint = new SKPaint();
            paint.ImageFilter = SKImageFilter.CreateBlur(radius, radius);

            var result = new SKBitmap(bmp.Width, bmp.Height);
            using (var canvas = new SKCanvas(result))
            {
                canvas.DrawBitmap(bmp, 0, 0, paint);
            }
            return result;
        }

        public static SKBitmap BoxBlur(SKBitmap bmp, int radius)
        {
             return ApplyBlur(bmp, radius);
        }

        public static SKBitmap AddShadow(SKBitmap bmp, float opacity, int size)
        {
            return AddShadow(bmp, opacity, size, 1, SKColors.Black, new SKPoint(0,0));
        }

        public static SKBitmap AddShadow(SKBitmap bmp, float opacity, int size, float darkness, SkiaSharp.SKColor color, SkiaSharp.SKPoint offset, bool autoResize = true)
        {
             float sigmaX = size / 3.0f;
             float sigmaY = size / 3.0f;

             var shadowColor = color.WithAlpha((byte)(opacity * 255));

             using var filter = SKImageFilter.CreateDropShadow(
                 offset.X, offset.Y,
                 sigmaX, sigmaY,
                 shadowColor);

             using var paint = new SKPaint();
             paint.ImageFilter = filter;

             // Calculate proper bounds manually since ComputeFastBounds is missing/incompatible
             SKRect original = new SKRect(0, 0, bmp.Width, bmp.Height);
             
             // Shadow approximate bounds: source offset by (dx, dy) expanded by 3*sigma
             SKRect shadow = new SKRect(
                 offset.X - sigmaX * 3, 
                 offset.Y - sigmaY * 3, 
                 bmp.Width + offset.X + sigmaX * 3, 
                 bmp.Height + offset.Y + sigmaY * 3);

             SKRect bounds = original;
             bounds.Union(shadow);

             if (!autoResize)
             {
                 bounds = original;
             }

             int width = (int)Math.Ceiling(bounds.Width);
             int height = (int)Math.Ceiling(bounds.Height);

             var result = new SKBitmap(width, height);
             using (var canvas = new SKCanvas(result))
             {
                 canvas.Clear(SKColors.Transparent);
                 // Translate so the bounding box fits in (0,0) to (width,height)
                 canvas.Translate(-bounds.Left, -bounds.Top);
                 canvas.DrawBitmap(bmp, 0, 0, paint);
             }
             return result;
        }
        
        public static void Pixelate(SKBitmap bmp, int pixelSize)
        {
            if (pixelSize <= 1) return;

            int width = bmp.Width;
            int height = bmp.Height;
            int smallWidth = (width + pixelSize - 1) / pixelSize;
            int smallHeight = (height + pixelSize - 1) / pixelSize;

            using var smallBmp = bmp.Resize(new SKImageInfo(smallWidth, smallHeight), SKFilterQuality.Low);
            using var canvas = new SKCanvas(bmp);
            
            // Draw scaled back up with Nearest Neighbor to get pixelated look
            using var paint = new SKPaint { FilterQuality = SKFilterQuality.None };
            var rect = new SKRect(0, 0, width, height);
            canvas.DrawBitmap(smallBmp, rect, paint);
        }

        public static void ColorDepth(SKBitmap bmp, int bits)
        {
           // TODO: Implement color quantization
        }

        public static SKBitmap Rotate(SKBitmap bmp, float angle, bool upsize = true, bool clip = true, SKColor? backgroundColor = null)
        {
            if (angle % 360 == 0) return bmp;

            SKMatrix matrix = SKMatrix.CreateRotationDegrees(angle, bmp.Width / 2f, bmp.Height / 2f);
            SKRect rect = new SKRect(0, 0, bmp.Width, bmp.Height);
            
            if (upsize)
            {
                rect = matrix.MapRect(rect);
            }

            int newWidth = (int)Math.Ceiling(rect.Width);
            int newHeight = (int)Math.Ceiling(rect.Height);

            SKBitmap result = new SKBitmap(newWidth, newHeight);
            using (SKCanvas canvas = new SKCanvas(result))
            {
                canvas.Clear(backgroundColor ?? SKColors.Transparent);
                if (upsize)
                {
                    canvas.Translate(-rect.Left, -rect.Top);
                }
                canvas.Translate(result.Width / 2f, result.Height / 2f);
                canvas.RotateDegrees(angle);
                canvas.Translate(-bmp.Width / 2f, -bmp.Height / 2f);
                canvas.DrawBitmap(bmp, 0, 0);
            }

            return result;
        }

        public static SKBitmap Flip(SKBitmap bmp, bool horizontal, bool vertical)
        {
            if (!horizontal && !vertical) return bmp;

            SKBitmap result = new SKBitmap(bmp.Width, bmp.Height);
            using (SKCanvas canvas = new SKCanvas(result))
            {
                canvas.Clear();
                canvas.Scale(horizontal ? -1 : 1, vertical ? -1 : 1, bmp.Width / 2f, bmp.Height / 2f);
                canvas.DrawBitmap(bmp, 0, 0);
            }
            return result;
        }

        public static SKBitmap ResizeImage(SKBitmap bmp, int width, int height)
        {
            return bmp.Resize(new SKImageInfo(width, height), SKFilterQuality.High);
        }

        public static SKBitmap ResizeImage(SKBitmap bmp, System.Drawing.Size size)
        {
            return ResizeImage(bmp, size.Width, size.Height);
        }

        public static SKBitmap CropBitmap(SKBitmap bmp, int x, int y, int width, int height)
        {
            var subset = new SKBitmap(width, height);
            bool success = bmp.ExtractSubset(subset, new SKRectI(x, y, x + width, y + height));
            if (!success)
            {
                 // Fallback if extract fails (e.g. out of bounds), create blank
                 return new SKBitmap(width, height);
            }
            return subset;
        }

        public static SKBitmap CropBitmap(SKBitmap bmp, System.Drawing.Rectangle rect)
        {
            return CropBitmap(bmp, rect.X, rect.Y, rect.Width, rect.Height);
        }

        public static System.Drawing.Size ApplyAspectRatio(int width, int height, SKBitmap bmp)
        {
            if (width == 0 && height == 0)
            {
                return new System.Drawing.Size(bmp.Width, bmp.Height);
            }
            else if (width > 0 && height == 0)
            {
                return new System.Drawing.Size(width, (int)(width / (double)bmp.Width * bmp.Height));
            }
            else if (width == 0 && height > 0)
            {
                return new System.Drawing.Size((int)(height / (double)bmp.Height * bmp.Width), height);
            }
            
            return new System.Drawing.Size(width, height);
        }
    }
}
