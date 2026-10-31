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

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Platform.Linux.Recording;

/// <summary>ffmpeg encoders and input devices available on this machine.</summary>
internal sealed record FFmpegFeatures(IReadOnlySet<string> Encoders, bool HasPulse)
{
    public bool Has(string encoder) => Encoders.Contains(encoder);

    public static FFmpegFeatures Parse(string encodersListing, string devicesListing)
    {
        var encoders = new HashSet<string>(StringComparer.Ordinal);
        foreach (string raw in encodersListing.Split('\n'))
        {
            string[] parts = raw.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0].Length == 6 && "VAS".Contains(parts[0][0]) && parts[1] != "=")
            {
                encoders.Add(parts[1]);
            }
        }

        bool pulse = devicesListing.Split('\n').Any(line =>
        {
            string[] parts = line.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 && parts[0].Contains('D') && parts[1] == "pulse";
        });

        return new FFmpegFeatures(encoders, pulse);
    }
}

/// <summary>The ffmpeg video/audio encoder choice for a codec request.</summary>
internal sealed record FFmpegEncoderPlan(string VideoEncoder, string VideoArgs, string AudioEncoder, string Extension);

/// <summary>
/// Capture with GStreamer, encode with FFmpeg. GStreamer only reads the PipeWire portal stream
/// (which ffmpeg cannot) and writes fixed-size raw I420 frames to stdout; ffmpeg timestamps them
/// by wall clock and encodes a constant-rate file. That also fixes variable-frame-rate sources
/// (Hyprland only sends frames when a window changes), which CFR-only containers such as
/// Ogg/Theora otherwise play back sped up.
/// </summary>
internal static class FFmpegEncodingBridge
{
    private static readonly ConcurrentDictionary<string, FFmpegFeatures> FeatureCache = new(StringComparer.Ordinal);
    private static readonly Regex CapsWidth = new(@"width=\(int\)(\d+)", RegexOptions.CultureInvariant);
    private static readonly Regex CapsHeight = new(@"height=\(int\)(\d+)", RegexOptions.CultureInvariant);

    public static FFmpegFeatures GetFeatures(string ffmpegPath) => FeatureCache.GetOrAdd(ffmpegPath, path =>
    {
        try
        {
            return FFmpegFeatures.Parse(Run(path, "-hide_banner -encoders"), Run(path, "-hide_banner -devices"));
        }
        catch
        {
            return new FFmpegFeatures(new HashSet<string>(), false);
        }
    });

    private static string Run(string fileName, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("Could not start " + fileName);
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        string stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);
        return stdout + "\n" + stderr.GetAwaiter().GetResult();
    }

    /// <summary>Picks encoders the local ffmpeg has, degrading to H.264 (then MPEG-4) when needed.</summary>
    public static FFmpegEncoderPlan? PlanEncoders(VideoCodec codec, int bitrateKbps, FFmpegFeatures features)
    {
        int bitrate = bitrateKbps > 0 ? bitrateKbps : 4000;
        string rate = string.Create(CultureInfo.InvariantCulture, $"-b:v {bitrate}k -maxrate {bitrate * 3 / 2}k -bufsize {bitrate * 2}k");
        string mp4Audio = features.Has("aac") ? "aac" : features.Has("libopus") ? "libopus" : string.Empty;
        string webmAudio = features.Has("libopus") ? "libopus" : string.Empty;

        FFmpegEncoderPlan? H264()
        {
            if (features.Has("libx264")) return new("libx264", $"-preset veryfast -tune zerolatency {rate} -pix_fmt yuv420p", mp4Audio, ".mp4");
            if (features.Has("libopenh264")) return new("libopenh264", $"{rate} -pix_fmt yuv420p", mp4Audio, ".mp4");
            if (features.Has("mpeg4")) return new("mpeg4", $"-q:v 3 -pix_fmt yuv420p", mp4Audio, ".mp4");
            return null;
        }

        return codec switch
        {
            VideoCodec.HEVC when features.Has("libx265") => new("libx265", $"-preset veryfast {rate} -pix_fmt yuv420p -tag:v hvc1", mp4Audio, ".mp4"),
            VideoCodec.VP9 when features.Has("libvpx-vp9") => new("libvpx-vp9", $"-deadline realtime -cpu-used 8 -row-mt 1 {rate} -pix_fmt yuv420p", webmAudio, ".webm"),
            VideoCodec.AV1 when features.Has("libsvtav1") => new("libsvtav1", $"-preset 10 {rate} -pix_fmt yuv420p", webmAudio, ".webm"),
            VideoCodec.AV1 when features.Has("libaom-av1") => new("libaom-av1", $"-cpu-used 8 -usage realtime {rate} -pix_fmt yuv420p", webmAudio, ".webm"),
            _ => H264(),
        };
    }

    /// <summary>Reads the negotiated I420 frame size from "gst-launch-1.0 -v" output.</summary>
    public static (int Width, int Height)? ParseProbedSize(string verboseOutput)
    {
        // Field order varies between GStreamer versions, so read width and height separately from
        // the last caps line that negotiated I420.
        foreach (string line in verboseOutput.Split('\n').Reverse())
        {
            if (!line.Contains("caps = video/x-raw", StringComparison.Ordinal) || !line.Contains("format=(string)I420", StringComparison.Ordinal))
            {
                continue;
            }

            Match width = CapsWidth.Match(line);
            Match height = CapsHeight.Match(line);
            if (width.Success && height.Success)
            {
                return (int.Parse(width.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(height.Groups[1].Value, CultureInfo.InvariantCulture));
            }
        }

        return null;
    }

    public static string BuildProbeArgs(string sourceElement) =>
        $"-v {sourceElement} num-buffers=1 ! videoconvert ! video/x-raw,format=I420 ! fakesink";

    /// <summary>
    /// GStreamer side: portal stream → (crop) → fixed, tightly packed I420 on stdout. Align the
    /// width to eight pixels and the height to two so GStreamer's plane strides match FFmpeg's
    /// rawvideo layout. videoscale handles alignment and window resizes while keeping the frames
    /// at the fixed size FFmpeg expects.
    /// </summary>
    public static string BuildCaptureArgs(string sourceElement, int width, int height, string? cropElement = null)
    {
        var parts = new List<string>
        {
            "-q", "-e", sourceElement,
            "!", "queue max-size-buffers=4 leaky=downstream",
            "!", "videoconvert",
        };

        if (!string.IsNullOrEmpty(cropElement))
        {
            parts.AddRange(["!", cropElement, "!", "videoconvert"]);
        }

        parts.AddRange([
            "!", "videoscale",
            "!", string.Create(CultureInfo.InvariantCulture, $"video/x-raw,format=I420,width={RawWidth(width)},height={Even(height)},pixel-aspect-ratio=1/1"),
            "!", "fdsink fd=1 sync=false",
        ]);
        return string.Join(" ", parts);
    }

    /// <summary>FFmpeg side: raw I420 on stdin (+ optional PulseAudio) → constant-rate file.</summary>
    public static string BuildEncodeArgs(int width, int height, int fps, FFmpegEncoderPlan plan, string? pulseDevice, string outputPath)
    {
        int rate = fps is > 0 and <= 240 ? fps : 30;
        var args = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-y",
            "-use_wallclock_as_timestamps", "1",
            "-thread_queue_size", "1024",
            "-f", "rawvideo", "-pix_fmt", "yuv420p",
            "-video_size", string.Create(CultureInfo.InvariantCulture, $"{RawWidth(width)}x{Even(height)}"),
            "-framerate", rate.ToString(CultureInfo.InvariantCulture),
            "-i", "pipe:0",
        };

        bool audio = !string.IsNullOrEmpty(pulseDevice) && !string.IsNullOrEmpty(plan.AudioEncoder);
        if (audio)
        {
            args.AddRange(["-thread_queue_size", "1024", "-f", "pulse", "-i", Quote(pulseDevice!)]);
        }

        args.AddRange(["-map", "0:v:0"]);
        if (audio)
        {
            args.AddRange(["-map", "1:a:0", "-c:a", plan.AudioEncoder, "-b:a", "160k"]);
        }

        args.AddRange(["-c:v", plan.VideoEncoder, plan.VideoArgs, "-fps_mode", "cfr", "-r", rate.ToString(CultureInfo.InvariantCulture)]);
        if (plan.Extension == ".mp4")
        {
            args.AddRange(["-movflags", "+faststart"]);
        }

        args.Add(Quote(outputPath));
        return string.Join(" ", args);
    }

    internal static int Even(int value) => Math.Max(2, value & ~1);

    /// <summary>
    /// Width of the raw I420 frames piped from GStreamer to ffmpeg. GStreamer pads every I420 plane
    /// row to a multiple of 4 bytes (Y stride = round_up_4(width), U/V stride = round_up_4(width / 2)),
    /// while ffmpeg's rawvideo demuxer expects tightly packed rows. Any width that is not a multiple
    /// of 8 therefore made ffmpeg read rows at the wrong stride (diagonally sheared picture) and frames
    /// of the wrong size (planes drifting into green/magenta garbage), e.g. a 1610 px wide window.
    /// A multiple of 8 keeps both strides unpadded, so both sides agree on the frame layout.
    /// </summary>
    internal static int RawWidth(int value) => Math.Max(8, value & ~7);

    /// <summary>
    /// Forward complete I420 frames only. A raw pipe carries no size or stride metadata, so an
    /// empty or incomplete stream must fail instead of producing an apparently successful file.
    /// </summary>
    internal static long CopyRawFrames(Stream source, Stream destination, int width, int height)
    {
        int frameBytes = checked(RawWidth(width) * Even(height) * 3 / 2);
        byte[] frame = new byte[frameBytes];
        long frames = 0;
        int read;
        while ((read = source.ReadAtLeast(frame, frameBytes, throwOnEndOfStream: false)) != 0)
        {
            if (read != frameBytes)
            {
                throw new InvalidDataException($"Incomplete I420 frame: received {read} of {frameBytes} bytes. Check the capture size and plane strides.");
            }

            destination.Write(frame);
            frames++;
        }

        if (frames == 0)
        {
            throw new InvalidDataException("The selected screen or window did not deliver any video frames.");
        }

        return frames;
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
