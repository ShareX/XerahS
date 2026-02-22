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
using XerahS.Services.Abstractions;

namespace XerahS.Media.Encoders;

/// <summary>
/// Image encoder that uses SkiaSharp for native format support (PNG, JPEG, BMP, GIF, WEBP, TIFF).
/// </summary>
public class SkiaImageEncoder : IImageEncoder
{
    /// <inheritdoc />
    public bool CanEncode(EImageFormat format)
    {
        return format == EImageFormat.PNG ||
               format == EImageFormat.JPEG ||
               format == EImageFormat.BMP ||
               format == EImageFormat.TIFF ||
               format == EImageFormat.GIF ||
               format == EImageFormat.WEBP;
    }

    /// <inheritdoc />
    public Task EncodeAsync(SKBitmap bitmap, string filePath, EImageFormat format, int quality)
    {
        return Task.Run(() =>
        {
            ImageHelpers.SaveBitmap(bitmap, filePath, quality);
        });
    }
}
