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
/// Image encoder that uses FFmpeg CLI for formats not natively supported by SkiaSharp (e.g., AVIF).
/// </summary>
public class FFmpegImageEncoder : IImageEncoder
{
    private readonly Func<string> _ffmpegPathProvider;

    /// <summary>
    /// Creates a new FFmpegImageEncoder with a factory function to get the FFmpeg path.
    /// </summary>
    /// <param name="ffmpegPathProvider">Function that returns the path to ffmpeg.exe.</param>
    public FFmpegImageEncoder(Func<string> ffmpegPathProvider)
    {
        _ffmpegPathProvider = ffmpegPathProvider ?? throw new ArgumentNullException(nameof(ffmpegPathProvider));
    }

    /// <inheritdoc />
    public bool CanEncode(EImageFormat format)
    {
        return format == EImageFormat.AVIF;
    }

    /// <inheritdoc />
    public async Task EncodeAsync(SKBitmap bitmap, string filePath, EImageFormat format, int quality)
    {
        if (format != EImageFormat.AVIF)
        {
            throw new NotSupportedException($"Format {format} is not supported by FFmpegImageEncoder.");
        }

        string ffmpegPath = _ffmpegPathProvider();
        if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
        {
            throw new InvalidOperationException("FFmpeg is not available. Please download FFmpeg to encode AVIF images.");
        }

        // Save as temp PNG first
        string tempPng = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
        try
        {
            await Task.Run(() => ImageHelpers.SaveBitmap(bitmap, tempPng, 100));

            // Convert PNG -> AVIF using FFmpeg with libaom-av1
            // Quality mapping: 100 (best) -> CRF 0, 0 (worst) -> CRF 63
            int crf = (int)((100 - quality) * 0.63);
            crf = Math.Clamp(crf, 0, 63);

            var ffmpegManager = new FFmpegCLIManager(ffmpegPath);
            string args = $"-y -i \"{tempPng}\" -c:v libaom-av1 -crf {crf} -still-picture 1 \"{filePath}\"";

            await Task.Run(() => ffmpegManager.Run(args));

            if (!File.Exists(filePath))
            {
                throw new InvalidOperationException($"FFmpeg failed to create AVIF file. Output: {ffmpegManager.Output}");
            }
        }
        finally
        {
            if (File.Exists(tempPng))
            {
                try
                {
                    File.Delete(tempPng);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
