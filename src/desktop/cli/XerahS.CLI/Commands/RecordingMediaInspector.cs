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
using System.Text.Json;
using XerahS.Common;

namespace XerahS.CLI.Commands;

internal sealed record RecordingMediaStream(string Type, string Codec, double? Duration, int Width, int Height,
    double? FramesPerSecond, int? SampleRate, int? Channels);

internal sealed record RecordingMediaReport(double? Duration, IReadOnlyList<RecordingMediaStream> Streams)
{
    public IReadOnlyList<string> Validate(bool native, int? expectedAudioStreams = null, double? expectedDuration = null)
    {
        List<string> errors = [];
        var video = Streams.Where(stream => stream.Type == "video").ToList();
        var audio = Streams.Where(stream => stream.Type == "audio").ToList();
        if (video.Count != 1) errors.Add($"Expected one video stream; found {video.Count}.");
        if (expectedAudioStreams is int count && audio.Count != count)
            errors.Add($"Expected {count} audio streams; found {audio.Count}.");
        if (Duration is not > 0) errors.Add("The recording has no positive container duration.");
        if (expectedDuration is double seconds && Duration is double actual && Math.Abs(actual - seconds) > 1)
            errors.Add($"Duration {actual:F3}s differs from expected active time {seconds:F3}s by more than 1s.");
        if (native)
        {
            if (audio.Count > 1) errors.Add("Native recording must mix audio into at most one stream.");
            foreach (var stream in video)
            {
                if (stream.Codec != "h264") errors.Add($"Expected native H.264 video; found {stream.Codec}.");
                if (stream.Width < 2 || stream.Height < 2 || stream.Width % 2 != 0 || stream.Height % 2 != 0)
                    errors.Add("Native video dimensions must be positive and even.");
            }
            foreach (var stream in audio)
                if (stream.Codec != "aac" || stream.SampleRate != 48000 || stream.Channels != 2)
                    errors.Add("Expected native AAC, 48 kHz, stereo audio.");
        }
        if (video.Count == 1 && audio.Count == 1 && video[0].Duration is double videoDuration && audio[0].Duration is double audioDuration
            && Math.Abs(videoDuration - audioDuration) > 0.1)
            errors.Add($"Audio/video stream durations differ by {Math.Abs(videoDuration - audioDuration):F3}s (limit 0.100s).");
        return errors;
    }
}

internal static class RecordingMediaInspector
{
    public static async Task<RecordingMediaReport> InspectAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Recording not found.", path);
        var start = new ProcessStartInfo(PathsManager.GetFFprobePath())
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        foreach (string argument in new[] { "-v", "error", "-show_streams", "-show_format", "-of", "json", Path.GetFullPath(path) })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Could not start ffprobe.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            string json = await output;
            string stderr = await error;
            if (process.ExitCode != 0) throw new IOException($"ffprobe failed ({process.ExitCode}): {stderr.Trim()}");
            return Parse(json);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    internal static RecordingMediaReport Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        List<RecordingMediaStream> streams = [];
        if (root.TryGetProperty("streams", out var array))
            foreach (var stream in array.EnumerateArray())
                streams.Add(new RecordingMediaStream(Text(stream, "codec_type"), Text(stream, "codec_name"),
                    Number(stream, "duration"), (int)(Number(stream, "width") ?? 0), (int)(Number(stream, "height") ?? 0),
                    Fraction(Text(stream, "avg_frame_rate")), (int?)Number(stream, "sample_rate"), (int?)Number(stream, "channels")));
        double? duration = root.TryGetProperty("format", out var format) ? Number(format, "duration") : null;
        return new RecordingMediaReport(duration, streams);
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.ToString() : string.Empty;

    private static double? Number(JsonElement element, string name) =>
        double.TryParse(Text(element, name), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value) ? value : null;

    private static double? Fraction(string text)
    {
        string[] parts = text.Split('/');
        return parts.Length == 2 && double.TryParse(parts[0], CultureInfo.InvariantCulture, out double numerator)
            && double.TryParse(parts[1], CultureInfo.InvariantCulture, out double denominator) && denominator != 0
            ? numerator / denominator : null;
    }
}
