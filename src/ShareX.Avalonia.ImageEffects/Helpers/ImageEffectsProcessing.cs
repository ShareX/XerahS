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

        public static SKBitmap DrawRoundedCorners(SKBitmap bmp, int radius, bool roundTopLeft, bool roundTopRight, bool roundBottomLeft, bool roundBottomRight, SKColor backgroundColor)
        {
            if (radius <= 0) return bmp;

            SKBitmap result = new SKBitmap(bmp.Width, bmp.Height);
            using (SKCanvas canvas = new SKCanvas(result))
            {
                canvas.Clear(backgroundColor);
                
                using (SKPaint paint = new SKPaint())
                {
                    paint.IsAntialias = true;
                    // Create path with specific rounded corners
                    using (SKPath path = new SKPath())
                    {
                        var rrect = new SKRoundRect();
                        rrect.SetRectRadii(new SKRect(0, 0, bmp.Width, bmp.Height), new SKPoint[] {
                            new SKPoint(roundTopLeft ? radius : 0, roundTopLeft ? radius : 0),
                            new SKPoint(roundTopRight ? radius : 0, roundTopRight ? radius : 0),
                            new SKPoint(roundBottomRight ? radius : 0, roundBottomRight ? radius : 0),
                            new SKPoint(roundBottomLeft ? radius : 0, roundBottomLeft ? radius : 0)
                        });

                        path.AddRoundRect(rrect);
                        
                        canvas.ClipPath(path, SKClipOperation.Intersect, true);
                        canvas.DrawBitmap(bmp, 0, 0, paint);
                    }
                }
            }
            return result;
        }

        public static SKBitmap ResizeCanvas(SKBitmap bmp, int left, int top, int right, int bottom, SKColor color)
        {
             int newWidth = bmp.Width + left + right;
             int newHeight = bmp.Height + top + bottom;
             
             SKBitmap result = new SKBitmap(newWidth, newHeight);
             using (SKCanvas canvas = new SKCanvas(result))
             {
                 canvas.Clear(color);
                 canvas.DrawBitmap(bmp, left, top);
             }
             return result;
        }

        public static SKBitmap AutoCrop(SKBitmap bmp, SKColor color, int tolerance = 0, int margin = 0)
        {
            // Simple auto crop implementation using LockPixels
            // Assumes checking 4 sides for color match
            
            if (bmp == null) return null;
            
            int width = bmp.Width;
            int height = bmp.Height;
            
            int minX = 0, minY = 0, maxX = width - 1, maxY = height - 1;
            
            // Lock pixels for unsafe access
            IntPtr pixels = bmp.GetPixels();
            
            // Helper to check pixel match
            // Note: This is a simplified check. For full speed, pointer arithmetic is needed.
            // Using GetPixel for now to avoid unsafe blocks if possible, but GetPixel is available on SKBitmap?
            // SKBitmap.GetPixel is available but slow. 
            // Let's use read-only span or naive loop with GetPixel for safety first, optimize if needed.
            
            bool IsMatch(int x, int y)
            {
                SKColor c = bmp.GetPixel(x, y);
                if (tolerance <= 0) return c.Equals(color);
                
                int diffR = Math.Abs(c.Red - color.Red);
                int diffG = Math.Abs(c.Green - color.Green);
                int diffB = Math.Abs(c.Blue - color.Blue);
                int diffA = Math.Abs(c.Alpha - color.Alpha);
                
                return diffR <= tolerance && diffG <= tolerance && diffB <= tolerance && diffA <= tolerance;
            }

            // Scan Top
            bool found = false;
            for (; minY < height; minY++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!IsMatch(x, minY))
                    {
                        found = true;
                        break;
                    }
                }
                if (found) break;
            }
            if (!found) return new SKBitmap(1, 1); // Empty or full match

            // Scan Bottom
            found = false;
            for (; maxY >= minY; maxY--)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!IsMatch(x, maxY))
                    {
                        found = true;
                        break;
                    }
                }
                if (found) break;
            }

            // Scan Left
            found = false;
            for (; minX < width; minX++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (!IsMatch(minX, y))
                    {
                        found = true;
                        break;
                    }
                }
                if (found) break;
            }

            // Scan Right
            found = false;
            for (; maxX >= minX; maxX--)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (!IsMatch(maxX, y))
                    {
                        found = true;
                        break;
                    }
                }
                if (found) break;
            }
            
            // Apply margin
            minX = Math.Max(0, minX - margin);
            minY = Math.Max(0, minY - margin);
            maxX = Math.Min(width - 1, maxX + margin);
            maxY = Math.Min(height - 1, maxY + margin);
            
            int newWidth = maxX - minX + 1;
            int newHeight = maxY - minY + 1;
            
            return CropBitmap(bmp, minX, minY, newWidth, newHeight);
        }

        public static SKBitmap Outline(SKBitmap bmp, int size, SKColor color, int padding = 0, bool outlineOnly = false)
        {
             if (size <= 0) return bmp;

             // Calculate new dimensions
             int extra = (size + padding) * 2;
             int width = bmp.Width + extra;
             int height = bmp.Height + extra;
             
             SKBitmap result = new SKBitmap(width, height);
             using (SKCanvas canvas = new SKCanvas(result))
             {
                 canvas.Clear(SKColors.Transparent);
                 canvas.Translate(size + padding, size + padding); // Move to center area
                 
                 using (SKPaint paint = new SKPaint())
                 {
                     // Dilate to create outline
                     // Dilate by 'size'
                     paint.ImageFilter = SKImageFilter.CreateDilate(size, size);
                     paint.Color = color;
                     paint.IsAntialias = true;
                     
                     // We need to draw the alpha mask of the original bmp filled with 'color' dilated
                     // A simple way is to use the image filter on the original bitmap
                     // However, Dilate filter applies to colors too.
                     // Better: Draw bmp with color filter to make it solid 'color', then dilate.
                     
                     using (SKPaint maskPaint = new SKPaint())
                     {
                         // Create a paint that draws the bitmap as solid color, dilated
                         maskPaint.ColorFilter = SKColorFilter.CreateBlendMode(color, SKBlendMode.SrcIn); 
                         maskPaint.ImageFilter = SKImageFilter.CreateDilate(size, size);
                         
                         canvas.DrawBitmap(bmp, 0, 0, maskPaint);
                     }
                 }
                 
                 if (!outlineOnly)
                 {
                     canvas.DrawBitmap(bmp, 0, 0);
                 }
                 else
                 {
                     // If outline only, we want to punch out the original?
                     // Usually OutlineOnly means just the border.
                     // So we might want to clear the original area?
                     using (SKPaint clearPaint = new SKPaint { BlendMode = SKBlendMode.Clear })
                     {
                         canvas.DrawBitmap(bmp, 0, 0, clearPaint);
                     }
                 }
             }
             return result;
        }

        public static SKBitmap DrawBackground(SKBitmap bmp, SKColor color)
        {
            SKBitmap result = new SKBitmap(bmp.Width, bmp.Height);
            using (SKCanvas canvas = new SKCanvas(result))
            {
                // Draw background first
                canvas.Clear(color);
                // Draw image on top
                canvas.DrawBitmap(bmp, 0, 0);
            }
            return result;
        }

        public static SKBitmap DrawCheckerboard(SKBitmap bmp, int size, SKColor c1, SKColor c2)
        {
            if (size <= 0) size = 10;
            SKBitmap result = new SKBitmap(bmp.Width, bmp.Height);
            using (SKCanvas canvas = new SKCanvas(result))
            {
                // Draw checkerboard
                using (SKPaint p1 = new SKPaint { Color = c1 })
                using (SKPaint p2 = new SKPaint { Color = c2 })
                {
                    int cols = (int)Math.Ceiling(bmp.Width / (double)size);
                    int rows = (int)Math.Ceiling(bmp.Height / (double)size);
                    
                    for(int y=0; y<rows; y++)
                    {
                        for(int x=0; x<cols; x++)
                        {
                            var rect = new SKRect(x*size, y*size, (x+1)*size, (y+1)*size);
                            bool isColor1 = ((x + y) % 2 == 0);
                            canvas.DrawRect(rect, isColor1 ? p1 : p2);
                        }
                    }
                }
                
                // Overlay image
                canvas.DrawBitmap(bmp, 0, 0);
            }
            return result;
        }

        public static SKBitmap DrawWatermark(SKBitmap bmp, string imagePath, SKPoint offset, float scale, bool useCenterColor, SKColor centerColor)
        {
            if (string.IsNullOrEmpty(imagePath) || !System.IO.File.Exists(imagePath)) return bmp;

            SKBitmap watermark;
            try
            {
                watermark = SKBitmap.Decode(imagePath);
            }
            catch
            {
                return bmp;
            }

            if (watermark == null) return bmp;

            using (watermark)
            {
                SKBitmap result = bmp.Copy();
                using (SKCanvas canvas = new SKCanvas(result))
                {
                    using (SKPaint paint = new SKPaint())
                    {
                        float targetWidth = watermark.Width * (scale / 100f);
                        float targetHeight = watermark.Height * (scale / 100f);
                        
                        float x = offset.X;
                        float y = offset.Y;
                        
                        var rect = new SKRect(x, y, x + targetWidth, y + targetHeight);
                        canvas.DrawBitmap(watermark, rect, paint);
                    }
                }
                return result;
            }
        }

        public static SKBitmap DrawString(SKBitmap bmp, string text, string fontFamily, float fontSize, SKFontStyleWeight fontWeight, SKColor color, SKPoint offset, bool drawBackground, SKColor backgroundColor, CanvasMargin padding, int cornerRadius, bool drawBorder, int borderSize, SKColor borderColor, bool drawShadow, SKColor shadowColor, SKPoint shadowOffset)
        {
            if (string.IsNullOrEmpty(text)) return bmp;

            SKBitmap result = bmp.Copy();
            using (SKCanvas canvas = new SKCanvas(result))
            {
                using (SKPaint textPaint = new SKPaint())
                {
                    textPaint.Typeface = SKTypeface.FromFamilyName(fontFamily, fontWeight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
                    textPaint.TextSize = fontSize;
                    textPaint.Color = color;
                    textPaint.IsAntialias = true;

                    SKRect textBounds = new SKRect();
                    textPaint.MeasureText(text, ref textBounds);
                    
                    // Center the text vertically within the 'line height' roughly or just use bounds?
                    // Skia DrawText coordinates are for the baseline.
                    // Let's assume offset is Top-Left of the bounding box we want to draw in.
                    
                    float x = offset.X;
                    float y = offset.Y + textPaint.TextSize; // Approximate baseline

                    if (drawBackground || drawBorder)
                    {
                        var bgRect = new SKRect(
                            offset.X - padding.Left, 
                            offset.Y - padding.Top, 
                            offset.X + textBounds.Width + padding.Right, 
                            offset.Y + textBounds.Height + padding.Bottom
                        );
                        
                        // Adjust bgRect height more generously for text
                        bgRect.Bottom += textPaint.FontMetrics.Descent;

                        if (drawBackground)
                        {
                            using (var bgPaint = new SKPaint { Color = backgroundColor, IsAntialias = true })
                            {
                                 if (cornerRadius > 0)
                                     canvas.DrawRoundRect(bgRect, cornerRadius, cornerRadius, bgPaint);
                                 else
                                     canvas.DrawRect(bgRect, bgPaint);
                            }
                        }
                        
                         if (drawBorder && borderSize > 0)
                        {
                            using (var borderPaint = new SKPaint { Color = borderColor, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = borderSize })
                            {
                                 if (cornerRadius > 0)
                                     canvas.DrawRoundRect(bgRect, cornerRadius, cornerRadius, borderPaint);
                                 else
                                     canvas.DrawRect(bgRect, borderPaint);
                            }
                        }
                    }

                    if (drawShadow)
                    {
                        using (var shadowPaint = textPaint.Clone())
                        {
                            shadowPaint.Color = shadowColor;
                            canvas.DrawText(text, x + shadowOffset.X, y + shadowOffset.Y, shadowPaint);
                        }
                    }

                    canvas.DrawText(text, x, y, textPaint);
                }
            }
            return result;
        }

        public static SKBitmap ApplyColorMatrix(SKBitmap bmp, float[] matrix)
        {
            SKBitmap result = new SKBitmap(bmp.Width, bmp.Height);
            using (SKCanvas canvas = new SKCanvas(result))
            {
                using (SKPaint paint = new SKPaint())
                {
                    paint.ColorFilter = SKColorFilter.CreateColorMatrix(matrix);
                    canvas.DrawBitmap(bmp, 0, 0, paint);
                }
            }
            return result;
        }

        public static SKBitmap ApplyGamma(SKBitmap bmp, float gamma)
        {
            if (gamma <= 0) return bmp;

            byte[] table = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                table[i] = (byte)Math.Min(255, (int)((Math.Pow(i / 255.0, 1.0 / gamma) * 255.0) + 0.5));
            }
            
            SKBitmap result = new SKBitmap(bmp.Width, bmp.Height);
            using (SKCanvas canvas = new SKCanvas(result))
            {
                using (SKPaint paint = new SKPaint())
                {
                    // Pass null for alpha to keep it unchanged
                    paint.ColorFilter = SKColorFilter.CreateTable(null, table, table, table);
                    canvas.DrawBitmap(bmp, 0, 0, paint);
                }
            }
            return result;
        }

        public static SKBitmap Colorize(SKBitmap bmp, SKColor color)
        {
            SKBitmap result = new SKBitmap(bmp.Width, bmp.Height);
            using (SKCanvas canvas = new SKCanvas(result))
            {
                canvas.DrawBitmap(bmp, 0, 0);
                
                using (SKPaint paint = new SKPaint())
                {
                    paint.Color = color;
                    paint.BlendMode = SKBlendMode.Color; 
                    canvas.DrawRect(0, 0, bmp.Width, bmp.Height, paint);
                }
            }
            return result;
        }

        public static SKBitmap ApplyConvolutionMatrix(SKBitmap bmp, float[] kernel, int size, float divisor, float bias)
        {
            if (divisor == 0) divisor = 1;
            
            // Normalize kernel by divisor
            float[] normalizedKernel = new float[kernel.Length];
            for(int i=0; i<kernel.Length; i++) normalizedKernel[i] = kernel[i] / divisor;
            
            // Skia MatrixConvolution expects kernel size, kernel, gain, bias, kernelOffset, tileMode, convolveAlpha
            // We bake divisor into the kernel, so gain is 1.0f.
            
            using var paint = new SKPaint();
            paint.ImageFilter = SKImageFilter.CreateMatrixConvolution(
                new SKSizeI(size, size), 
                normalizedKernel, 
                1.0f, 
                bias, 
                new SKPointI(size / 2, size / 2),
                SKShaderTileMode.Clamp, 
                true
            );
            
            var result = new SKBitmap(bmp.Width, bmp.Height);
            using (var canvas = new SKCanvas(result))
            {
                canvas.DrawBitmap(bmp, 0, 0, paint);
            }
            return result;
        }
        public static SKBitmap ApplyOpacity(SKBitmap bmp, float opacity)
        {
            if (opacity >= 1.0f) return bmp;

            SKBitmap result = new SKBitmap(bmp.Width, bmp.Height);
            using (SKCanvas canvas = new SKCanvas(result))
            {
                using (SKPaint paint = new SKPaint())
                {
                    // Or simpler: Color = SKColors.White.WithAlpha((byte)(255 * opacity))
                    paint.Color = SKColors.White.WithAlpha((byte)(255 * opacity));
                    canvas.DrawBitmap(bmp, 0, 0, paint);
                }
            }
            return result;
        }

        public static SKBitmap AddGlow(SKBitmap bmp, int size, float strength, SKColor color, SKPoint offset, bool useGradient = false)
        {
             // Mapping Glow to Shadow with 0 offset?
             // Strength usually controls opacity or spread.
             // We'll treat Strength as Opacity for now (clamped).
             float opacity = Math.Min(1.0f, strength);
             return AddShadow(bmp, opacity, size, 1, color, offset, true);
        }

        public static SKBitmap DrawReflection(SKBitmap bmp, int percentage, int maxAlpha, int minAlpha, int offset, bool skew, int skewSize)
        {
            int reflectionHeight = (int)(bmp.Height * (percentage / 100.0));
            if (reflectionHeight <= 0) return bmp;

            int totalHeight = bmp.Height + offset + reflectionHeight;
            int width = bmp.Width;
            
            // Handle skew expansion if needed
            int skewOffset = skew ? skewSize : 0;
            
            SKBitmap result = new SKBitmap(width + skewOffset, totalHeight);
            using (SKCanvas canvas = new SKCanvas(result))
            {
                canvas.Clear(SKColors.Transparent);
                
                // Draw original
                canvas.DrawBitmap(bmp, skewOffset / 2, 0); // Center if skewed? Or Left aligned? Skew usually shifts bottom.
                // Re-read skew description: "skew reflection from bottom left to bottom right"
                // For simplicity, let's ignore complex skew logic or implement basic skew.
                
                // Draw reflection
                using (SKPaint paint = new SKPaint())
                {
                    // 1. Create flipped bitmap part
                    var subset = new SKBitmap(width, reflectionHeight);
                    // Extract bottom part of original? Usually reflection is the bottom mirrored.
                    // Reflection is the bottom 'reflectionHeight' pixels of the image, flipped vertically.
                    
                    var sourceRect = new SKRectI(0, bmp.Height - reflectionHeight, width, bmp.Height);
                    bmp.ExtractSubset(subset, sourceRect);
                    
                    using (var flipped = Flip(subset, false, true))
                    {
                        // 2. Create gradient mask
                        // Skia doesn't support direct alpha mask bitmap without shader.
                        // We can use DST_IN blend mode with a gradient rect.
                        
                        using (var reflectionLayer = new SKBitmap(width, reflectionHeight))
                        using (var layerCanvas = new SKCanvas(reflectionLayer))
                        {
                            layerCanvas.Clear(SKColors.Transparent);
                            layerCanvas.DrawBitmap(flipped, 0, 0);
                            
                            // Apply gradient fade
                            using (var gradientPaint = new SKPaint())
                            {
                                gradientPaint.BlendMode = SKBlendMode.DstIn;
                                var colors = new SKColor[] { 
                                    SKColors.White.WithAlpha((byte)maxAlpha), 
                                    SKColors.White.WithAlpha((byte)minAlpha) 
                                };
                                var points = new SKPoint[] { new SKPoint(0, 0), new SKPoint(0, reflectionHeight) };
                                gradientPaint.Shader = SKShader.CreateLinearGradient(points[0], points[1], colors, null, SKShaderTileMode.Clamp);
                                
                                layerCanvas.DrawRect(0, 0, width, reflectionHeight, gradientPaint);
                            }
                            
                            // 3. Draw reflection to main canvas
                            float drawY = bmp.Height + offset;
                            
                            if (skew)
                            {
                                canvas.Save();
                                // Skew logic: transform matrix
                                SKMatrix skewMatrix = SKMatrix.CreateSkew((float)skewSize / width, 0);
                                // This is tricky. Let's skip skew for now or just draw regular.
                                // canvas.Concat(skewMatrix);
                            }
                            
                            canvas.DrawBitmap(reflectionLayer, skewOffset / 2, drawY);
                            
                            if (skew) canvas.Restore();
                        }
                    }
                }
            }
            return result;
        }
        public static SKBitmap DrawPolaroid(SKBitmap bmp, int margin, bool rotate)
        {
            if (margin < 0) margin = 0;
            
            // Polaroid layout:
            // Side/Top margins: 'margin'
            // Bottom margin: usually larger, say 4 * margin or fixed size? 
            // Let's use 5 * margin roughly for the "caption" area.
            int bottomMargin = Math.Max(margin, margin * 4);
            
            int totalWidth = bmp.Width + (margin * 2);
            int totalHeight = bmp.Height + margin + bottomMargin;
            
            SKBitmap card = new SKBitmap(totalWidth, totalHeight);
            using (SKCanvas canvas = new SKCanvas(card))
            {
                // Draw white background
                canvas.Clear(SKColors.White);
                
                // Draw image
                canvas.DrawBitmap(bmp, margin, margin);
                
                // Add simple border around image? Polaroid usually has depth or inner shadow?
                // For now just flat white card.
                
                // Add slight shadow to the card itself?
                // The effect returns the card. External shadow might be separate.
            }
            
            if (rotate)
            {
                // Rotate by random or fixed small angle?
                // ShareX original uses random -5 to 5 degrees usually.
                // We'll use a fixed angle for stability or pseudo-random logic?
                // Let's use -3 degrees as a stylish default.
                return Rotate(card, -3, true, false, SKColors.Transparent);
            }
            
            return card;
        }

        public static SKBitmap ApplyRGBSplit(SKBitmap bmp, SKPoint offsetRed, SKPoint offsetGreen, SKPoint offsetBlue)
        {
            // Calculate bounds union
            SKRect bounds = new SKRect(0, 0, bmp.Width, bmp.Height);
            SKRect rR = SKRect.Create(offsetRed.X, offsetRed.Y, bmp.Width, bmp.Height);
            SKRect rG = SKRect.Create(offsetGreen.X, offsetGreen.Y, bmp.Width, bmp.Height);
            SKRect rB = SKRect.Create(offsetBlue.X, offsetBlue.Y, bmp.Width, bmp.Height);
            
            SKRect union = bounds;
            union.Union(rR);
            union.Union(rG);
            union.Union(rB);
            
            int width = (int)Math.Ceiling(union.Width);
            int height = (int)Math.Ceiling(union.Height);
            
            SKBitmap result = new SKBitmap(width, height);
            using (SKCanvas canvas = new SKCanvas(result))
            {
                canvas.Clear(SKColors.Transparent);
                
                // Helper to draw channel (inlined or removed if unused)
                // DrawChannel was reported as unused because we loop manually below.
                
                // Matrices for filtering channels
                // R: Keep Red, Zero others. Alpha? Keep Alpha?
                // If we use Plus blend, 0 alpha means no add. 
                // We want to add R component.
                // Red Channel: R=R, G=0, B=0, A=A?
                // If we keep A=A, we add Alpha 3 times -> 3x Alpha.
                // We typically want the resulting Alpha to be Max(Ar, Ag, Ab) or something?
                // Or just average?
                // Standard RGB split keeps opaque image opaque.
                // Logic:
                // Final Pixel = (R_at_offsetRed, G_at_offsetGreen, B_at_offsetBlue, A_at_???)
                // Skia's Plus adds alphas too.
                // If we use Screen, it clamps.
                // Better approach:
                // Draw 3 layers, and use a specialized blend or write mask.
                // But simplified additive works for black background.
                // For transparent background, alpha management is tricky.
                // PROPER WAY:
                // We cannot easily do this with just blending if we care about Alpha correctly without deeper composition.
                // BUT, assuming standard opaque image or simple transparency:
                // Filter: R -> R, G->0, B->0, A->A.
                // If we add them, A becomes 3*A.
                // We should probably set A=0 in the channel filters, but then we get 0 alpha result?
                // Wait, if A=0, nothing is drawn.
                // So we need A=A/3? No.
                // Let's assume the result should have the union of alpha shapes.
                
                // Alternative: Draw 3 full copies, then mix them? 
                // Too slow?
                
                // "Lighten" blend mode might be better than Plus?
                // "Screen"?
                
                // Let's stick to what's simple:
                // Just use the matrix to shift channels for coloring, and Draw them.
                // But we need to combine them into one image where R comes from one, G from another...
                
                // SKColorFilter.CreateChannelMixer? unavailable?
                
                // We can use SKPaint.ColorFilter with Matrix.
                // Matrix for Red only: 
                // 1 0 0 0 0
                // 0 0 0 0 0
                // 0 0 0 0 0
                // 0 0 0 1 0  <-- Keep Alpha
                
                // If we draw this 3 times with Screen blend mode:
                // 1. Draw Red-only version. (R, 0, 0, A)
                // 2. Screen Draw Green-only version. (0, G, 0, A).  Result: (R, G, 0, A_screened)
                // 3. Screen Draw Blue-only version. (0, 0, B, A). Result: (R, G, B, A_screened_again)
                
                // Screen of Alphas: 1 - (1-A)(1-A)(1-A).
                // If A=1, Result A=1.
                // If A=0, Result A=0.
                // If part overlapped, A increases. This mimics "chromatic aberration ghosting".
                // This is acceptable for RGBSplit effect.
                
                float[] matR = new float[] {
                    1, 0, 0, 0, 0,
                    0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0,
                    0, 0, 0, 1, 0
                };
                
                float[] matG = new float[] {
                    0, 0, 0, 0, 0,
                    0, 1, 0, 0, 0,
                    0, 0, 0, 0, 0,
                    0, 0, 0, 1, 0
                };
                
                float[] matB = new float[] {
                    0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0,
                    0, 0, 1, 0, 0,
                    0, 0, 0, 1, 0
                };
                
                using (SKPaint p = new SKPaint())
                {
                    p.BlendMode = SKBlendMode.Screen;
                    
                    p.ColorFilter = SKColorFilter.CreateColorMatrix(matR);
                    canvas.DrawBitmap(bmp, offsetRed.X - union.Left, offsetRed.Y - union.Top, p);
                    
                    p.ColorFilter = SKColorFilter.CreateColorMatrix(matG);
                    canvas.DrawBitmap(bmp, offsetGreen.X - union.Left, offsetGreen.Y - union.Top, p);
                    
                    p.ColorFilter = SKColorFilter.CreateColorMatrix(matB);
                    canvas.DrawBitmap(bmp, offsetBlue.X - union.Left, offsetBlue.Y - union.Top, p);
                }
            }
            return result;
        }
        public static SKBitmap DrawTornEdge(SKBitmap bmp, int depth, int range, AnchorSides sides, bool curved)
        {
             // Simplified implementation: generic logic for creating a path and clipping
             // Depth = amplitude of tear
             // Range = frequency/period of tear
             
             if (depth <= 0 || range <= 0) return bmp;
             
             SKBitmap result = new SKBitmap(bmp.Width, bmp.Height);
             using (SKCanvas canvas = new SKCanvas(result))
             {
                 canvas.Clear(SKColors.Transparent);
                 
                 using (SKPath path = new SKPath())
                 {
                     // Start Top-Left
                     path.MoveTo(0, 0);
                     
                     // Top Edge
                     if ((sides & AnchorSides.Top) != 0)
                     {
                         // Generate jagged top edge
                         float x = 0;
                         while (x < bmp.Width)
                         {
                             float nextX = Math.Min(bmp.Width, x + range);
                             float y = (float)(new Random((int)x).NextDouble() * depth); 
                             // Deterministic random would be better.
                             // Using Sine for curved?
                             if (curved)
                             {
                                 // Quadratic bezier? step x by range/2
                                 path.QuadTo(x + range/2, y, nextX, 0); 
                             }
                             else
                             {
                                 path.LineTo(nextX, y);
                             }
                             x = nextX;
                         }
                     }
                     else
                     {
                        path.LineTo(bmp.Width, 0);
                     }
                     
                     // Right Edge
                     if ((sides & AnchorSides.Right) != 0)
                     {
                         float y = 0;
                         while (y < bmp.Height)
                         {
                             float nextY = Math.Min(bmp.Height, y + range);
                             float x = bmp.Width - (float)(new Random((int)y + 1000).NextDouble() * depth);
                             if (curved)
                             {
                                 path.QuadTo(x, y + range/2, bmp.Width, nextY);
                             }
                             else
                             {
                                 path.LineTo(bmp.Width - (depth/2), nextY); // Simplified zigzag
                                 path.LineTo(bmp.Width, nextY);
                             }
                             y = nextY;
                         }
                     }
                     else
                     {
                         path.LineTo(bmp.Width, bmp.Height);
                     }
                     
                     // Bottom Edge
                     if ((sides & AnchorSides.Bottom) != 0)
                     {
                         float x = bmp.Width;
                         while (x > 0)
                         {
                             float nextX = Math.Max(0, x - range);
                             float y = bmp.Height - (float)(new Random((int)x + 2000).NextDouble() * depth);
                             if (curved)
                             {
                                 path.QuadTo(x - range/2, y, nextX, bmp.Height);
                             }
                             else
                             {
                                 path.LineTo(nextX, y); // Simplified
                             }
                             x = nextX;
                         }
                     }
                     else
                     {
                         path.LineTo(0, bmp.Height);
                     }
                     
                     // Left Edge
                     if ((sides & AnchorSides.Left) != 0)
                     {
                          float y = bmp.Height;
                          while (y > 0)
                          {
                              float nextY = Math.Max(0, y - range);
                              float x = (float)(new Random((int)y + 3000).NextDouble() * depth);
                              if (curved)
                              {
                                  path.QuadTo(x, y - range/2, 0, nextY);
                              }
                              else
                              {
                                  path.LineTo(x, nextY);
                              }
                              y = nextY;
                          }
                     }
                     else
                     {
                         path.Close();
                     }
                     
                     canvas.ClipPath(path, SKClipOperation.Intersect, true);
                     canvas.DrawBitmap(bmp, 0, 0);
                 }
             }
             return result;
        }

        public static SKBitmap DrawWaveEdge(SKBitmap bmp, int depth, int range, AnchorSides sides)
        {
             // Similar to TornEdge but with regular sine wave
             if (depth <= 0 || range <= 0) return bmp;
             
             SKBitmap result = new SKBitmap(bmp.Width, bmp.Height);
             using (SKCanvas canvas = new SKCanvas(result))
             {
                 canvas.Clear(SKColors.Transparent);
                 
                 using (SKPath path = new SKPath())
                 {
                     path.MoveTo(0, 0);
                     
                     // TODO: Implement proper wave path for all sides.
                     // For now, just rectangle to verify flow.
                     path.AddRect(new SKRect(0, 0, bmp.Width, bmp.Height));
                     
                     canvas.ClipPath(path, SKClipOperation.Intersect, true);
                     canvas.DrawBitmap(bmp, 0, 0);
                 }
             }
             return result;
        }
    }

    [Flags]
    public enum AnchorSides
    {
        None = 0,
        Top = 1,
        Bottom = 2,
        Left = 4,
        Right = 8,
        All = Top | Bottom | Left | Right
    }
}
