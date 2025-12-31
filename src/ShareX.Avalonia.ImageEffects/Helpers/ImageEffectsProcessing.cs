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

            // Box blur approximation or Gaussian blur
            // Skia's CreateBlur is Gaussian. Visual difference usually acceptable.
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
            // Re-route to standard blur for Skia simplicity
             return ApplyBlur(bmp, radius);
        }

        public static SKBitmap AddShadow(SKBitmap bmp, float opacity, int size)
        {
            return AddShadow(bmp, opacity, size, 1, SKColors.Black, new SKPoint(0,0));
        }

        public static SKBitmap AddShadow(SKBitmap bmp, float opacity, int size, float darkness, SkiaSharp.SKColor color, SkiaSharp.SKPoint offset, bool autoResize = true)
        {
             // Create Drop Shadow Filter
             // Note: Skia's DropShadow draws BOTH the shadow and the input.
             float sigmaX = size / 3.0f; // Approximate sigma from size
             float sigmaY = size / 3.0f;

             // Color with opacity
             var shadowColor = color.WithAlpha((byte)(opacity * 255));

             using var filter = SKImageFilter.CreateDropShadow(
                 offset.X, offset.Y,
                 sigmaX, sigmaY,
                 shadowColor);

             using var paint = new SKPaint();
             paint.ImageFilter = filter;

             // Calculate new bounds if autoResize is true
             // For simplify, we draw on a larger canvas if needed, or same canvas.
             // This is a complex logic to port fully 1:1, but this is the Skia way.
             
             // Simplest implementation: Draw centered on a canvas big enough
             // For now, let's just return the blurred/shadowed image on a new canvas
             var result = new SKBitmap(bmp.Width + size * 2, bmp.Height + size * 2);
             using (var canvas = new SKCanvas(result))
             {
                 canvas.Clear(SKColors.Transparent);
                 // Translate to make room for shadow (naive implementation)
                 canvas.Translate(size, size);
                 canvas.DrawBitmap(bmp, 0, 0, paint);
             }
             return result;
        }
        
        public static void Pixelate(SKBitmap bmp, int pixelSize)
        {
            // TODO: Implement via downscale/upscale or shader
        }

        public static void ColorDepth(SKBitmap bmp, int bits)
        {
           // TODO: Implement via loop and quantization
        }

        public static SKBitmap Rotate(SKBitmap bmp, float angle, bool upsize = true, bool clip = true, SKColor? backgroundColor = null)
        {
            // TODO: Matrix transform
            return bmp;
        }
    }
}
