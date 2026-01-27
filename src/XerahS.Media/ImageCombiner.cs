#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

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

using SkiaSharp;
using XerahS.Common;
using System.Collections.Generic;
using System.Linq;

namespace XerahS.Media
{
    public enum ImageCombinerOrientation
    {
        Vertical,
        Horizontal
    }

    public enum ImageCombinerAlignment
    {
        Near,
        Center,
        Far
    }

    public class ImageCombiner
    {
        public SKBitmap? Combine(IEnumerable<SKBitmap> images, ImageCombinerOrientation orientation, int spacing = 0, ImageCombinerAlignment alignment = ImageCombinerAlignment.Center, SKColor? backgroundColor = null)
        {
            if (images == null || !images.Any())
            {
                return null;
            }

            var imageList = images.ToList();
            if (imageList.Any(i => i == null))
            {
                // Filter out null images or throw? For now filter.
                imageList = imageList.Where(i => i != null).ToList();
                if (!imageList.Any()) return null;
            }

            int maxWidth = 0;
            int maxHeight = 0;
            int totalWidth = 0;
            int totalHeight = 0;

            foreach (var img in imageList)
            {
                if (img.Width > maxWidth) maxWidth = img.Width;
                if (img.Height > maxHeight) maxHeight = img.Height;

                if (orientation == ImageCombinerOrientation.Vertical)
                {
                    totalHeight += img.Height;
                }
                else
                {
                    totalWidth += img.Width;
                }
            }

            // Add spacing
            if (imageList.Count > 1)
            {
                if (orientation == ImageCombinerOrientation.Vertical)
                {
                    totalHeight += spacing * (imageList.Count - 1);
                }
                else
                {
                    totalWidth += spacing * (imageList.Count - 1);
                }
            }

            if (orientation == ImageCombinerOrientation.Vertical)
            {
                totalWidth = maxWidth;
            }
            else
            {
                totalHeight = maxHeight;
            }

            var result = new SKBitmap(totalWidth, totalHeight);
            using (var canvas = new SKCanvas(result))
            {
                canvas.Clear(backgroundColor ?? SKColors.Transparent);

                int currentX = 0;
                int currentY = 0;

                foreach (var img in imageList)
                {
                    int drawX = currentX;
                    int drawY = currentY;

                    if (orientation == ImageCombinerOrientation.Vertical)
                    {
                        // Calculate X based on alignment
                        switch (alignment)
                        {
                            case ImageCombinerAlignment.Near: // Left
                                drawX = 0;
                                break;
                            case ImageCombinerAlignment.Center:
                                drawX = (totalWidth - img.Width) / 2;
                                break;
                            case ImageCombinerAlignment.Far: // Right
                                drawX = totalWidth - img.Width;
                                break;
                        }

                        currentY += img.Height + spacing;
                    }
                    else // Horizontal
                    {
                        // Calculate Y based on alignment
                        switch (alignment)
                        {
                            case ImageCombinerAlignment.Near: // Top
                                drawY = 0;
                                break;
                            case ImageCombinerAlignment.Center:
                                drawY = (totalHeight - img.Height) / 2;
                                break;
                            case ImageCombinerAlignment.Far: // Bottom
                                drawY = totalHeight - img.Height;
                                break;
                        }

                        currentX += img.Width + spacing;
                    }

                    canvas.DrawBitmap(img, drawX, drawY);
                }
            }

            return result;
        }

        public SKBitmap? Combine(IEnumerable<string> filePaths, ImageCombinerOrientation orientation, int spacing = 0, ImageCombinerAlignment alignment = ImageCombinerAlignment.Center, SKColor? backgroundColor = null)
        {
            var bitmaps = new List<SKBitmap>();
            try
            {
                foreach (var path in filePaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        var bmp = SKBitmap.Decode(path);
                        if (bmp != null)
                        {
                            bitmaps.Add(bmp);
                        }
                    }
                }

                return Combine(bitmaps, orientation, spacing, alignment, backgroundColor);
            }
            finally
            {
                // Cleanup loaded bitmaps
                foreach (var bmp in bitmaps)
                {
                    bmp.Dispose();
                }
            }
        }
    }
}
