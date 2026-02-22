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

using System;
using System.IO;
using System.Runtime.Versioning;
using SkiaSharp;

namespace XerahS.Uploaders
{
    public abstract class ImageUploader : FileUploader
    {
        /// <summary>
        /// Uploads an image from an SKBitmap. This is the cross-platform method.
        /// </summary>
        /// <param name="bitmap">The SKBitmap to upload</param>
        /// <param name="fileName">The filename for the upload</param>
        /// <param name="format">The image format to encode as (default PNG)</param>
        /// <param name="quality">Encoding quality 0-100 (default 100)</param>
        /// <returns>Upload result with URL on success</returns>
        public UploadResult UploadImage(SKBitmap bitmap, string fileName, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100)
        {
            ArgumentNullException.ThrowIfNull(bitmap);
            ArgumentNullException.ThrowIfNull(fileName);

            using var data = bitmap.Encode(format, quality);
            if (data == null)
            {
                var result = new UploadResult();
                result.Errors.Add("Failed to encode bitmap");
                return result;
            }

            using var stream = new MemoryStream();
            data.SaveTo(stream);
            stream.Position = 0;
            return Upload(stream, fileName);
        }

        /// <summary>
        /// Uploads an image from an SKImage. This is the cross-platform method.
        /// </summary>
        public UploadResult UploadImage(SKImage image, string fileName, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100)
        {
            ArgumentNullException.ThrowIfNull(image);
            ArgumentNullException.ThrowIfNull(fileName);

            using var data = image.Encode(format, quality);
            if (data == null)
            {
                var result = new UploadResult();
                result.Errors.Add("Failed to encode image");
                return result;
            }

            using var stream = new MemoryStream();
            data.SaveTo(stream);
            stream.Position = 0;
            return Upload(stream, fileName);
        }

#if WINDOWS
        /// <summary>
        /// Uploads an image from a System.Drawing.Image. Windows-only, deprecated.
        /// Use UploadImage(SKBitmap, ...) instead.
        /// </summary>
        [Obsolete("Use UploadImage(SKBitmap, string) for cross-platform support")]
        [SupportedOSPlatform("windows")]
        public UploadResult UploadImage(System.Drawing.Image image, string fileName)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("System.Drawing image upload is only supported on Windows.");
            }

            using MemoryStream stream = new MemoryStream();
            image.Save(stream, image.RawFormat);
            stream.Position = 0;
            return Upload(stream, fileName);
        }
#endif
    }
}
