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
using System.Text;
using System.Text.RegularExpressions;

namespace XerahS.Media;

/// <summary>
/// FFmpeg-based video trimming (ported from ShareX's Video Trimmer). Fast mode stream-copies the
/// source (instant, lossless, cuts snap to keyframes); precise mode re-encodes H.264/AAC MP4 at
/// the exact boundaries. Output is staged next to the destination and moved into place only
/// after FFmpeg succeeds, so a failure or cancel never clobbers an existing file.
/// </summary>
public sealed partial class VideoTrimmerService
{
    [GeneratedRegex(@"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex ProbeDurationAsyncRegex();

    private readonly string _ffmpegPath;

    public VideoTrimmerService(string ffmpegPath)
    {
        _ffmpegPath = ffmpegPath ?? throw new ArgumentNullException(nameof(ffmpegPath));
    }

    public static string Timestamp(double seconds)
    {
        return TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
    }

    /// <summary>Parses "ss", "mm:ss", or "hh:mm:ss" with optional fractional seconds.</summary>
    public static bool TryParseTimestamp(string? text, out double seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] parts = text.Trim().Split(':');
        if (parts.Length > 3)
        {
            return false;
        }

        double total = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            bool isLast = i == parts.Length - 1;
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                value < 0 || (!isLast && value != Math.Floor(value)))
            {
                return false;
            }

            total = total * 60 + value;
        }

        seconds = total;
        return true;
    }

    public static string[] BuildTrimArguments(string input, string output, double start, double end, bool precise)
    {
        List<string> args = ["-v", "error", "-ss", Timestamp(start), "-i", input, "-t", Timestamp(end - start),
            "-map", "0:V:0", "-map", "0:a?", "-map_chapters", "-1"];
        if (precise)
        {
            args.AddRange(["-c:v", "libx264", "-preset", "fast", "-crf", "18", "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2",
                "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart"]);
        }
        else
        {
            args.AddRange(["-map", "0:s?", "-c", "copy", "-avoid_negative_ts", "make_zero"]);
        }

        args.AddRange(["-progress", "pipe:1", "-nostats", "-n", output]);
        return args.ToArray();
    }

    /// <summary>Extension of the trimmed file: the source's for fast mode, .mp4 for precise mode.</summary>
    public static string GetOutputExtension(string input, bool precise)
    {
        string extension = Path.GetExtension(input);
        return precise || string.IsNullOrEmpty(extension) ? ".mp4" : extension;
    }

    /// <summary>"name-trimmed.ext" next to the source, numbered if that file already exists.</summary>
    public static string GetDefaultOutputPath(string input, bool precise)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(input)) ?? string.Empty;
        string name = Path.GetFileNameWithoutExtension(input) + "-trimmed";
        string extension = GetOutputExtension(input, precise);
        string candidate = Path.Combine(directory, name + extension);
        for (int i = 2; File.Exists(candidate); i++)
        {
            candidate = Path.Combine(directory, $"{name}-{i}{extension}");
        }

        return candidate;
    }

    public async Task<double?> ProbeDurationAsync(string input, CancellationToken token = default)
    {
        var (_, log) = await RunAsync(["-i", input], token, allowFailure: true);
        Match match = ProbeDurationAsyncRegex().Match(log);
        if (!match.Success)
        {
            return null;
        }

        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 3600 +
            int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 60 +
            double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>One PNG frame near <paramref name="position"/>, scaled to at most <paramref name="maxWidth"/> pixels wide.</summary>
    public async Task<byte[]?> GetFrameAsync(string input, double position, int maxWidth = 480, CancellationToken token = default)
    {
        var (data, _) = await RunAsync(
            ["-v", "error", "-ss", Timestamp(position), "-i", input, "-frames:v", "1",
             "-vf", $"scale='min({maxWidth},iw)':-2", "-f", "image2pipe", "-c:v", "png", "pipe:1"],
            token, allowFailure: true);
        return data.Length > 0 ? data : null;
    }

    public async Task TrimAsync(string input, string output, double start, double end, double duration,
        bool precise, IProgress<double>? progress, CancellationToken token)
    {
        if (!double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end <= start || end > duration + 0.001)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "The selection must start before it ends and stay within the video.");
        }

        if (string.Equals(Path.GetFullPath(input), Path.GetFullPath(output), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Choose a different output file; the source video cannot be overwritten.");
        }

        string extension = GetOutputExtension(input, precise);
        if (!string.Equals(Path.GetExtension(output), extension, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The output file must use the {extension} extension for this trim mode.");
        }

        string temporary = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, $".xerahs-trim-{Guid.NewGuid():N}{extension}");
        try
        {
            await RunAsync(BuildTrimArguments(input, temporary, start, end, precise), token, progress: progress, duration: end - start);
            token.ThrowIfCancellationRequested();
            if (!File.Exists(temporary) || new FileInfo(temporary).Length == 0)
            {
                throw new InvalidOperationException("FFmpeg produced an empty file.");
            }

            File.Move(temporary, output, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private async Task<(byte[] Data, string Log)> RunAsync(IEnumerable<string> arguments, CancellationToken token,
        bool allowFailure = false, IProgress<double>? progress = null, double duration = 0)
    {
        token.ThrowIfCancellationRequested();
        using Process process = new();
        process.StartInfo = new ProcessStartInfo(_ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string arg in new[] { "-hide_banner", "-nostdin" }.Concat(arguments))
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

        StringBuilder log = new();
        async Task ReadErrorsAsync()
        {
            while (await process.StandardError.ReadLineAsync() is { } line)
            {
                log.AppendLine(line);
                if (log.Length > 32768) log.Remove(0, log.Length - 32768);
            }
        }

        using MemoryStream data = new();
        async Task ReadOutputAsync()
        {
            if (progress == null)
            {
                await process.StandardOutput.BaseStream.CopyToAsync(data);
                return;
            }

            while (await process.StandardOutput.ReadLineAsync() is { } line)
            {
                if (line.StartsWith("out_time_us=", StringComparison.Ordinal) && duration > 0 &&
                    long.TryParse(line.AsSpan(12), NumberStyles.Integer, CultureInfo.InvariantCulture, out long microseconds))
                {
                    progress.Report(Math.Clamp(microseconds / 1000000d / duration * 100, 0, 100));
                }
            }
        }

        await Task.WhenAll(ReadErrorsAsync(), ReadOutputAsync(), process.WaitForExitAsync());
        token.ThrowIfCancellationRequested();
        if (!allowFailure && process.ExitCode != 0)
        {
            throw new InvalidOperationException(log.ToString().Trim());
        }

        return (data.ToArray(), log.ToString());
    }
}
