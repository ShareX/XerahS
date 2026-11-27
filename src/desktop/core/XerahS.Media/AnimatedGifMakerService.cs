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

using System.Diagnostics;
using System.Globalization;
using SkiaSharp;

namespace XerahS.Media;

public sealed record AnimatedGifOptions
{
    /// <summary>Delay per frame in milliseconds (GIF resolution is 10 ms).</summary>
    public int DelayMilliseconds { get; init; } = 500;
    /// <summary>Loop forever when true; otherwise play <see cref="RepeatCount"/> extra times.</summary>
    public bool Loop { get; init; } = true;
    public int RepeatCount { get; init; }
    /// <summary>Longest side of the GIF; 0 keeps the first image's size.</summary>
    public int MaxDimension { get; init; }
}

/// <summary>
/// Animated GIF from still images (ShareX Animated GIF Maker). Every frame is fitted to the first
/// image's size, then FFmpeg builds one optimized palette for the whole animation
/// (palettegen/paletteuse, the same approach XerahS uses for recording GIFs).
/// </summary>
public sealed class AnimatedGifMakerService
{
    private readonly string _ffmpegPath;

    public AnimatedGifMakerService(string ffmpegPath)
    {
        _ffmpegPath = ffmpegPath ?? throw new ArgumentNullException(nameof(ffmpegPath));
    }

    /// <summary>FFmpeg -loop value: 0 = forever, -1 = play once, N = repeat N times.</summary>
    public static int GetLoopArgument(AnimatedGifOptions options) =>
        options.Loop ? 0 : options.RepeatCount <= 0 ? -1 : Math.Min(options.RepeatCount, ushort.MaxValue);

    /// <summary>Frame rate expressed as a rational so arbitrary delays survive (e.g. 1000/333).</summary>
    public static string GetFrameRate(int delayMilliseconds) =>
        $"1000/{Math.Clamp(delayMilliseconds, 10, 655350).ToString(CultureInfo.InvariantCulture)}";

    public static (int Width, int Height) GetCanvasSize(int width, int height, int maxDimension)
    {
        int largest = Math.Max(width, height);
        if (maxDimension <= 0 || largest <= maxDimension)
        {
            return (width, height);
        }

        double scale = maxDimension / (double)largest;
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    public async Task CreateAsync(IReadOnlyList<string> imageFiles, string outputPath, AnimatedGifOptions options,
        IProgress<double>? progress = null, CancellationToken token = default)
    {
        if (imageFiles == null || imageFiles.Count < 2)
        {
            throw new ArgumentException("Choose at least two images.", nameof(imageFiles));
        }

        if (options.DelayMilliseconds < 10)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The frame delay must be at least 10 ms.");
        }

        string outputFolder = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? throw new ArgumentException("An output folder is required.", nameof(outputPath));
        Directory.CreateDirectory(outputFolder);
        string workFolder = Path.Combine(Path.GetTempPath(), "xerahs-gif-" + Guid.NewGuid().ToString("N"));
        string staging = Path.Combine(outputFolder, $".xerahs-gif-{Guid.NewGuid():N}.gif");
        Directory.CreateDirectory(workFolder);

        try
        {
            int width, height;
            using (SKBitmap first = ImageBatchService.Load(imageFiles[0]))
            {
                (width, height) = GetCanvasSize(first.Width, first.Height, options.MaxDimension);
            }

            for (int i = 0; i < imageFiles.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                using SKBitmap source = ImageBatchService.Load(imageFiles[i]);
                using SKBitmap frame = ImageBatchService.Resize(source, width, height, ImageResizeMode.Fit);
                byte[] png = ImageBatchService.Encode(frame, new ImageBatchOptions { Format = ImageBatchOutputFormat.Png });
                await File.WriteAllBytesAsync(Path.Combine(workFolder, $"frame{i:D5}.png"), png, token);
                progress?.Report((i + 1) * 70.0 / imageFiles.Count);
            }

            string[] args =
            [
                "-hide_banner", "-nostdin", "-v", "error",
                "-framerate", GetFrameRate(options.DelayMilliseconds),
                "-i", Path.Combine(workFolder, "frame%05d.png"),
                "-filter_complex", "split[a][b];[a]palettegen=stats_mode=full:reserve_transparent=1[p];[b][p]paletteuse=dither=sierra2_4a:alpha_threshold=128",
                "-loop", GetLoopArgument(options).ToString(CultureInfo.InvariantCulture),
                "-f", "gif", "-y", staging
            ];
            await RunFFmpegAsync(args, token);
            if (!File.Exists(staging) || new FileInfo(staging).Length == 0)
            {
                throw new InvalidOperationException("FFmpeg produced an empty GIF.");
            }

            File.Move(staging, outputPath, true);
            progress?.Report(100);
        }
        finally
        {
            if (File.Exists(staging)) File.Delete(staging);
            try { Directory.Delete(workFolder, recursive: true); } catch (IOException) { }
        }
    }

    private async Task RunFFmpegAsync(IEnumerable<string> arguments, CancellationToken token)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(_ffmpegPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };
        foreach (string arg in arguments)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();
        using CancellationTokenRegistration registration = token.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); }
            catch (Exception ex) when (ex is InvalidOperationException
                                          or ObjectDisposedException
                                          or System.ComponentModel.Win32Exception
                                          or AggregateException) { }
        });
        Task<string> errors = process.StandardError.ReadToEndAsync();
        Task drain = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        await Task.WhenAll(errors, drain, process.WaitForExitAsync());
        token.ThrowIfCancellationRequested();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException((await errors).Trim());
        }
    }
}
