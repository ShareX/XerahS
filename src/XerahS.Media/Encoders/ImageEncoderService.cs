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
/// Composite image encoder service that routes encoding requests to appropriate encoders based on format.
/// </summary>
public class ImageEncoderService : IImageEncoderService
{
    private readonly IReadOnlyList<IImageEncoder> _encoders;

    /// <summary>
    /// Creates a new ImageEncoderService with the specified encoders.
    /// </summary>
    /// <param name="encoders">The encoders to use, in priority order.</param>
    public ImageEncoderService(IEnumerable<IImageEncoder> encoders)
    {
        _encoders = encoders?.ToList() ?? throw new ArgumentNullException(nameof(encoders));
    }

    /// <summary>
    /// Creates a default ImageEncoderService with Skia and FFmpeg encoders.
    /// </summary>
    /// <param name="ffmpegPathProvider">Function that returns the path to ffmpeg.exe.</param>
    /// <returns>A configured ImageEncoderService.</returns>
    public static ImageEncoderService CreateDefault(Func<string> ffmpegPathProvider)
    {
        var encoders = new List<IImageEncoder>
        {
            new SkiaImageEncoder(),
            new FFmpegImageEncoder(ffmpegPathProvider)
        };
        return new ImageEncoderService(encoders);
    }

    /// <inheritdoc />
    public async Task EncodeAsync(SKBitmap bitmap, string filePath, EImageFormat format, int quality = 100)
    {
        if (bitmap == null)
        {
            throw new ArgumentNullException(nameof(bitmap));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));
        }

        var encoder = _encoders.FirstOrDefault(e => e.CanEncode(format));
        if (encoder == null)
        {
            throw new NotSupportedException($"No encoder found for format: {format}");
        }

        await encoder.EncodeAsync(bitmap, filePath, format, quality);
    }
}
