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
/// Image encoder that uses FFmpeg CLI for formats not natively supported by SkiaSharp (AVIF and JPEG XL).
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
        return format is EImageFormat.AVIF or EImageFormat.JXL;
    }

    /// <summary>
    /// Maps a 1-100 quality value to a libjxl butteraugli distance. 100 is lossless (0).
    /// Uses the same curve as the reference cjxl tool so quality values mean the same thing.
    /// </summary>
    internal static double JxlDistance(int quality)
    {
        quality = Math.Clamp(quality, 0, 100);
        if (quality >= 100) return 0;
        if (quality >= 30) return Math.Round(0.1 + (100 - quality) * 0.09, 3);
        return Math.Round(53.0 / 3000.0 * quality * quality - 23.0 / 20.0 * quality + 25.0, 3);
    }

    internal static string BuildArguments(EImageFormat format, string inputPng, string outputPath, int quality)
    {
        if (format == EImageFormat.JXL)
        {
            string distance = JxlDistance(quality).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            return $"-y -i \"{inputPng}\" -c:v libjxl -distance {distance} -effort 7 -frames:v 1 \"{outputPath}\"";
        }

        // Convert PNG -> AVIF using libaom-av1. Quality mapping: 100 (best) -> CRF 0, 0 (worst) -> CRF 63
        int crf = Math.Clamp((int)((100 - quality) * 0.63), 0, 63);
        return $"-y -i \"{inputPng}\" -c:v libaom-av1 -crf {crf} -still-picture 1 \"{outputPath}\"";
    }

    /// <inheritdoc />
    public async Task EncodeAsync(SKBitmap bitmap, string filePath, EImageFormat format, int quality)
    {
        if (!CanEncode(format))
        {
            throw new NotSupportedException($"Format {format} is not supported by FFmpegImageEncoder.");
        }

        string ffmpegPath = _ffmpegPathProvider();
        if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
        {
            throw new InvalidOperationException($"FFmpeg is not available. Please download FFmpeg to encode {format} images.");
        }

        // Save as temp PNG first
        string tempPng = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
        try
        {
            await Task.Run(() => ImageHelpers.SaveBitmap(bitmap, tempPng, 100));

            var ffmpegManager = new FFmpegCLIManager(ffmpegPath);
            string args = BuildArguments(format, tempPng, filePath, quality);

            await Task.Run(() => ffmpegManager.Run(args));

            if (!File.Exists(filePath))
            {
                throw new InvalidOperationException($"FFmpeg failed to create {format} file. It may lack the required encoder. Output: {ffmpegManager.Output}");
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
