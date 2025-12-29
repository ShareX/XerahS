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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using ShareX.Avalonia.Common.GIF;

namespace ShareX.Avalonia.Common
{
    public static class ImageHelpers
    {
        public static Bitmap LoadImage(string filePath)
        {
            using (Image image = Image.FromFile(filePath))
            {
                return new Bitmap(image);
            }
        }

        public static void SaveImage(Image image, string filePath)
        {
            ImageFormat format = GetImageFormat(filePath);
            image.Save(filePath, format);
        }

        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            Bitmap result = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(image, 0, 0, width, height);
            }

            return result;
        }

        private static ImageFormat GetImageFormat(string filePath)
        {
            string extension = Path.GetExtension(filePath)?.TrimStart('.').ToLowerInvariant();
            return extension switch
            {
                "jpg" => ImageFormat.Jpeg,
                "jpeg" => ImageFormat.Jpeg,
                "bmp" => ImageFormat.Bmp,
                "gif" => ImageFormat.Gif,
                "tif" => ImageFormat.Tiff,
                "tiff" => ImageFormat.Tiff,
                _ => ImageFormat.Png
            };
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
            }
            else
            {
                Quantizer quantizer;

                switch (quality)
                {
                    case GIFQuality.Grayscale:
                        quantizer = new GrayscaleQuantizer();
                        break;
                    case GIFQuality.Bit4:
                        quantizer = new OctreeQuantizer(15, 4);
                        break;
                    default:
                    case GIFQuality.Bit8:
                        quantizer = new OctreeQuantizer(255, 4);
                        break;
                }

                using (Bitmap quantized = quantizer.Quantize(img))
                {
                    quantized.Save(stream, ImageFormat.Gif);
                }
            }
        }
    }
}
