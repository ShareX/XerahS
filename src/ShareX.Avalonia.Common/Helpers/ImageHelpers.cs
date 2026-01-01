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

#nullable enable

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using ShareX.Ava.Common.GIF;
using SkiaSharp;

namespace ShareX.Ava.Common;

public static class ImageHelpers
{
    public static SKBitmap? LoadBitmap(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            return SKBitmap.Decode(filePath);
        }
        catch
        {
            return null;
        }
    }

    public static void SaveBitmap(SKBitmap bitmap, string filePath, int quality = 100)
    {
        if (bitmap is null) throw new ArgumentNullException(nameof(bitmap));
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path cannot be empty.", nameof(filePath));

        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);

        SKEncodedImageFormat format = GetEncodedFormat(filePath);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(format, quality);
        using FileStream stream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }

    public static SKBitmap ResizeImage(SKBitmap bitmap, int width, int height, SKFilterQuality quality = SKFilterQuality.High)
    {
        if (bitmap is null) throw new ArgumentNullException(nameof(bitmap));
        if (width <= 0 && height <= 0) return bitmap;

        (int targetWidth, int targetHeight) = ApplyAspectRatio(width, height, bitmap.Width, bitmap.Height);
        SKImageInfo info = new SKImageInfo(targetWidth, targetHeight, bitmap.ColorType, bitmap.AlphaType, bitmap.ColorSpace);
        SKBitmap? resized = bitmap.Resize(info, quality);
        return resized ?? new SKBitmap(info);
    }

    public static SKBitmap ResizeImage(SKBitmap bitmap, System.Drawing.Size size, SKFilterQuality quality = SKFilterQuality.High)
    {
        return ResizeImage(bitmap, size.Width, size.Height, quality);
    }

    public static SKBitmap Crop(SKBitmap bitmap, SKRectI rect)
    {
        if (bitmap is null) throw new ArgumentNullException(nameof(bitmap));

        SKRectI bounded = new SKRectI(
            Math.Max(0, rect.Left),
            Math.Max(0, rect.Top),
            Math.Min(bitmap.Width, rect.Right),
            Math.Min(bitmap.Height, rect.Bottom));

        if (bounded.Width <= 0 || bounded.Height <= 0)
        {
            return new SKBitmap();
        }

        SKBitmap subset = new SKBitmap(bounded.Width, bounded.Height);
        return bitmap.ExtractSubset(subset, bounded) ? subset : new SKBitmap();
    }

    public static SKBitmap Crop(SKBitmap bitmap, int x, int y, int width, int height)
    {
        return Crop(bitmap, new SKRectI(x, y, x + width, y + height));
    }

    public static SKBitmap Rotate(SKBitmap bitmap, float angle, bool expand = true, SKColor? backgroundColor = null)
    {
        if (bitmap is null) throw new ArgumentNullException(nameof(bitmap));
        if (Math.Abs(angle % 360) < float.Epsilon) return bitmap;

        SKMatrix matrix = SKMatrix.CreateRotationDegrees(angle, bitmap.Width / 2f, bitmap.Height / 2f);
        SKRect bounds = new SKRect(0, 0, bitmap.Width, bitmap.Height);
        if (expand)
        {
            bounds = matrix.MapRect(bounds);
        }

        int newWidth = (int)Math.Ceiling(bounds.Width);
        int newHeight = (int)Math.Ceiling(bounds.Height);
        SKBitmap rotated = new SKBitmap(new SKImageInfo(newWidth, newHeight, bitmap.ColorType, bitmap.AlphaType, bitmap.ColorSpace));

        using SKCanvas canvas = new SKCanvas(rotated);
        canvas.Clear(backgroundColor ?? SKColors.Transparent);

        if (expand)
        {
            canvas.Translate(-bounds.Left, -bounds.Top);
        }

        canvas.Translate(rotated.Width / 2f, rotated.Height / 2f);
        canvas.RotateDegrees(angle);
        canvas.Translate(-bitmap.Width / 2f, -bitmap.Height / 2f);
        canvas.DrawBitmap(bitmap, 0, 0);

        return rotated;
    }

    public static SKBitmap RemoveMetadata(SKBitmap bitmap, SKEncodedImageFormat? format = null, int quality = 100)
    {
        if (bitmap is null) throw new ArgumentNullException(nameof(bitmap));

        SKEncodedImageFormat targetFormat = format ?? SKEncodedImageFormat.Png;
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(targetFormat, quality);
        return SKBitmap.Decode(data);
    }

    public static Bitmap LoadImage(string filePath)
    {
        using Image image = Image.FromFile(filePath);
        return new Bitmap(image);
    }

    public static void SaveImage(Image image, string filePath)
    {
        ImageFormat format = GetImageFormat(filePath);
        image.Save(filePath, format);
    }

    public static Bitmap ResizeImage(Image image, int width, int height)
    {
        Bitmap result = new Bitmap(width, height);
        using Graphics g = Graphics.FromImage(result);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(image, 0, 0, width, height);
        return result;
    }

    public static MemoryStream SaveGIF(Image img, GIFQuality quality)
    {
        MemoryStream ms = new MemoryStream();
        SaveGIF(img, ms, quality);
        return ms;
    }

    public static void SaveGIF(Image img, Stream stream, GIFQuality quality)
    {
        if (quality == GIFQuality.Default)
        {
            img.Save(stream, ImageFormat.Gif);
            return;
        }

        Quantizer quantizer = quality switch
        {
            GIFQuality.Grayscale => new GrayscaleQuantizer(),
            GIFQuality.Bit4 => new OctreeQuantizer(15, 4),
            _ => new OctreeQuantizer(255, 4)
        };

        using Bitmap quantized = quantizer.Quantize(img);
        quantized.Save(stream, ImageFormat.Gif);
    }

    public static Bitmap CreateCheckerPattern()
    {
        return CreateCheckerPattern(10, 10);
    }

    public static Bitmap CreateCheckerPattern(int width, int height)
    {
        return CreateCheckerPattern(width, height, SystemColors.ControlLight, SystemColors.ControlLightLight);
    }

    public static Bitmap CreateCheckerPattern(int width, int height, Color checkerColor1, Color checkerColor2)
    {
        Bitmap bmp = new Bitmap(width * 2, height * 2);

        using Graphics g = Graphics.FromImage(bmp);
        using Brush brush1 = new SolidBrush(checkerColor1);
        using Brush brush2 = new SolidBrush(checkerColor2);

        g.FillRectangle(brush1, 0, 0, width, height);
        g.FillRectangle(brush1, width, height, width, height);
        g.FillRectangle(brush2, width, 0, width, height);
        g.FillRectangle(brush2, 0, height, width, height);

        return bmp;
    }

    private static (int Width, int Height) ApplyAspectRatio(int width, int height, int originalWidth, int originalHeight)
    {
        if (width == 0 && height == 0)
        {
            return (originalWidth, originalHeight);
        }

        if (width > 0 && height == 0)
        {
            int computedHeight = (int)Math.Round(width / (double)originalWidth * originalHeight);
            return (width, Math.Max(1, computedHeight));
        }

        if (width == 0 && height > 0)
        {
            int computedWidth = (int)Math.Round(height / (double)originalHeight * originalWidth);
            return (Math.Max(1, computedWidth), height);
        }

        return (width, height);
    }

    private static ImageFormat GetImageFormat(string filePath)
    {
        string extension = Path.GetExtension(filePath)?.TrimStart('.').ToLowerInvariant() ?? string.Empty;
        return extension switch
        {
            "jpg" or "jpeg" => ImageFormat.Jpeg,
            "bmp" => ImageFormat.Bmp,
            "gif" => ImageFormat.Gif,
            "tif" or "tiff" => ImageFormat.Tiff,
            _ => ImageFormat.Png
        };
    }

    private static SKEncodedImageFormat GetEncodedFormat(string filePath)
    {
        string extension = Path.GetExtension(filePath)?.TrimStart('.').ToLowerInvariant() ?? string.Empty;
        return extension switch
        {
            "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
            "bmp" => SKEncodedImageFormat.Bmp,
            "gif" => SKEncodedImageFormat.Gif,
            "webp" => SKEncodedImageFormat.Webp,
            "tif" or "tiff" => SKEncodedImageFormat.Png, // SkiaSharp lacks TIFF encoding, fall back to PNG
            _ => SKEncodedImageFormat.Png
        };
    }
}
