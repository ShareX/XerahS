#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
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

using SkiaSharp;
using System.Drawing;
using System.Drawing.Imaging;

namespace XerahS.Common
{
    public class ImageFilesCache : IDisposable
    {
        private Dictionary<string, SKBitmap> images = new Dictionary<string, SKBitmap>();

        public SKBitmap? GetImage(string filePath)
        {
            SKBitmap? bmp = null;

            if (!string.IsNullOrEmpty(filePath))
            {
                if (images.ContainsKey(filePath))
                {
                    return images[filePath];
                }

                bmp = ImageHelpers.LoadBitmap(filePath);

                if (bmp != null)
                {
                    images.Add(filePath, bmp);
                }
            }

            return bmp;
        }

        public SKBitmap? GetFileIconAsImage(string filePath, bool isSmallIcon = true)
        {
            SKBitmap? bmp = null;

            if (!string.IsNullOrEmpty(filePath))
            {
                if (images.ContainsKey(filePath))
                {
                    return images[filePath];
                }

                // NativeMethods.GetFileIcon returns System.Drawing.Icon (Windows only)
                // We keep this for now as NativeMethods wasn't fully refactored.
                // Assuming Windows platform for this specific method call or it returns null on others.

                using (Icon? icon = NativeMethods.GetFileIcon(filePath, isSmallIcon))
                {
                    if (icon != null && icon.Width > 0 && icon.Height > 0)
                    {
                        using (Bitmap sysBmp = icon.ToBitmap())
                        {
                            if (sysBmp != null)
                            {
                                using (MemoryStream ms = new MemoryStream())
                                {
                                    sysBmp.Save(ms, ImageFormat.Png);
                                    ms.Seek(0, SeekOrigin.Begin);
                                    bmp = SKBitmap.Decode(ms);
                                }

                                if (bmp != null)
                                {
                                    images.Add(filePath, bmp);
                                }
                            }
                        }
                    }
                }
            }

            return bmp;
        }

        public void Clear()
        {
            if (images != null)
            {
                Dispose();

                images.Clear();
            }
        }

        public void Dispose()
        {
            if (images != null)
            {
                foreach (SKBitmap bmp in images.Values)
                {
                    if (bmp != null)
                    {
                        bmp.Dispose();
                    }
                }
            }
        }
    }
}
