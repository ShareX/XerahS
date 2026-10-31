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

using NUnit.Framework;
using XerahS.Platform.Linux.Recording;
using XerahS.Platform.Linux.Services;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Tests.Platform;

[TestFixture]
public class WaylandRecordingFallbackTests
{
    private const string Encoders = """
         V....D libx264              libx264 H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10 (codec h264)
         V....D libx265              libx265 H.265 / HEVC (codec hevc)
         V....D libvpx-vp9           libvpx VP9 (codec vp9)
         V..... libsvtav1            SVT-AV1 encoder (codec av1)
         A....D aac                  AAC (Advanced Audio Coding)
         A....D libopus              libopus Opus (codec opus)
        """;

    private const string Devices = """
         DE alsa            ALSA audio output
         DE pulse           Pulse audio output
          E sdl2            SDL2 output device
        """;

    [Test]
    public void ParsesFFmpegEncodersAndPulseInput()
    {
        FFmpegFeatures features = FFmpegFeatures.Parse(Encoders, Devices);
        Assert.Multiple(() =>
        {
            Assert.That(features.Has("libx264"), Is.True);
            Assert.That(features.Has("libopus"), Is.True);
            Assert.That(features.Has("h264_nvenc"), Is.False);
            Assert.That(features.HasPulse, Is.True);
            Assert.That(FFmpegFeatures.Parse(Encoders, " E sdl2 SDL2").HasPulse, Is.False);
        });
    }

    [TestCase(VideoCodec.H264, "libx264", ".mp4", "aac")]
    [TestCase(VideoCodec.HEVC, "libx265", ".mp4", "aac")]
    [TestCase(VideoCodec.VP9, "libvpx-vp9", ".webm", "libopus")]
    [TestCase(VideoCodec.AV1, "libsvtav1", ".webm", "libopus")]
    public void PlansEncodersForEachCodec(VideoCodec codec, string video, string extension, string audio)
    {
        FFmpegEncoderPlan plan = FFmpegEncodingBridge.PlanEncoders(codec, 4000, FFmpegFeatures.Parse(Encoders, Devices))!;
        Assert.That((plan.VideoEncoder, plan.Extension, plan.AudioEncoder), Is.EqualTo((video, extension, audio)));
    }

    [Test]
    public void DegradesToH264AndReportsNothingUsable()
    {
        FFmpegFeatures onlyX264 = FFmpegFeatures.Parse(" V....D libx264  x", "");
        Assert.That(FFmpegEncodingBridge.PlanEncoders(VideoCodec.VP9, 4000, onlyX264)!.VideoEncoder, Is.EqualTo("libx264"));
        Assert.That(FFmpegEncodingBridge.PlanEncoders(VideoCodec.H264, 4000, FFmpegFeatures.Parse("", "")), Is.Null);
    }

    [Test]
    public void ReadsTheNegotiatedSizeWhateverTheFieldOrder()
    {
        // Real gst-launch -v output from GStreamer 1.28: format comes after width/height.
        const string output = """
            /GstPipeline:pipeline0/GstVideoConvert:videoconvert0.GstPad:sink: caps = video/x-raw, format=(string)YUY2, width=(int)1920, height=(int)1080, framerate=(fraction)30/1
            /GstPipeline:pipeline0/GstFakeSink:fakesink0.GstPad:sink: caps = video/x-raw, interlace-mode=(string)progressive, width=(int)1920, height=(int)1080, framerate=(fraction)30/1, format=(string)I420, colorimetry=(string)2:4:5:1
            """;
        Assert.That(FFmpegEncodingBridge.ParseProbedSize(output), Is.EqualTo((1920, 1080)));
        Assert.That(FFmpegEncodingBridge.ParseProbedSize("caps = video/x-raw, format=(string)I420, width=(int)1366, height=(int)768"), Is.EqualTo((1366, 768)));
        Assert.That(FFmpegEncodingBridge.ParseProbedSize("ERROR: not-negotiated"), Is.Null);
    }

    [Test]
    public void CaptureWritesFixedSizeRawFramesToStdout()
    {
        string args = FFmpegEncodingBridge.BuildCaptureArgs("pipewiresrc fd=9 path=106 do-timestamp=true", 1921, 1081, "videocrop left=10 top=20 right=30 bottom=40");
        Assert.That(args, Is.EqualTo(
            "-q -e pipewiresrc fd=9 path=106 do-timestamp=true ! queue max-size-buffers=4 leaky=downstream ! videoconvert ! " +
            "videocrop left=10 top=20 right=30 bottom=40 ! videoconvert ! videoscale ! " +
            "video/x-raw,format=I420,width=1920,height=1080,pixel-aspect-ratio=1/1 ! fdsink fd=1 sync=false"));
    }

    [TestCase(1610, 983, 1608, 982)]
    [TestCase(1366, 768, 1360, 768)]
    [TestCase(1935, 1208, 1928, 1208)]
    [TestCase(1920, 1080, 1920, 1080)]
    [TestCase(1422, 1141, 1416, 1140)]
    [TestCase(3840, 2160, 3840, 2160)]
    [TestCase(1, 1, 8, 2)]
    public void RawFrameWidthHasNoGStreamerRowPadding(int width, int height, int expectedWidth, int expectedHeight)
    {
        // GStreamer pads I420 rows to 4 bytes; ffmpeg rawvideo expects packed rows. Both sides must use
        // a width whose Y (w) and chroma (w/2) strides need no padding, i.e. a multiple of 8.
        string capture = FFmpegEncodingBridge.BuildCaptureArgs("pipewiresrc fd=9 path=106", width, height);
        FFmpegEncoderPlan plan = FFmpegEncodingBridge.PlanEncoders(VideoCodec.H264, 4000, FFmpegFeatures.Parse(Encoders, Devices))!;
        string encode = FFmpegEncodingBridge.BuildEncodeArgs(width, height, 30, plan, null, "/tmp/out.mp4");
        Assert.Multiple(() =>
        {
            Assert.That(capture, Does.Contain($"format=I420,width={expectedWidth},height={expectedHeight},"));
            Assert.That(encode, Does.Contain($"-video_size {expectedWidth}x{expectedHeight} "));
            Assert.That(expectedWidth % 8, Is.EqualTo(0));
        });
    }

    [Test]
    public void CopiesCompleteRawFramesWithoutChangingTheirBytes()
    {
        // 16x4 I420: 64 luma bytes + two 16-byte chroma planes per frame.
        byte[] frames = Enumerable.Range(0, 192).Select(value => (byte)value).ToArray();
        using var source = new MemoryStream(frames);
        using var destination = new MemoryStream();
        Assert.That(FFmpegEncodingBridge.CopyRawFrames(source, destination, 17, 5), Is.EqualTo(2));
        Assert.That(destination.ToArray(), Is.EqualTo(frames));
    }

    [TestCase(0)]
    [TestCase(95)]
    [TestCase(97)]
    public void RejectsEmptyOrIncompleteRawFrames(int byteCount)
    {
        using var source = new MemoryStream(new byte[byteCount]);
        using var destination = new MemoryStream();
        Assert.Throws<InvalidDataException>(() => FFmpegEncodingBridge.CopyRawFrames(source, destination, 16, 4));
        // Never send a partial frame to FFmpeg, even when a full frame preceded it.
        Assert.That(destination.Length, Is.EqualTo(byteCount >= 96 ? 96 : 0));
    }

    [Test]
    public void EncodeUsesWallclockTimestampsConstantRateAndAudio()
    {
        FFmpegEncoderPlan plan = FFmpegEncodingBridge.PlanEncoders(VideoCodec.H264, 4000, FFmpegFeatures.Parse(Encoders, Devices))!;
        string args = FFmpegEncodingBridge.BuildEncodeArgs(1920, 1080, 60, plan, "alsa_output.monitor", "/tmp/out.mp4");
        Assert.Multiple(() =>
        {
            Assert.That(args, Does.Contain("-use_wallclock_as_timestamps 1"));
            Assert.That(args, Does.Contain("-f rawvideo -pix_fmt yuv420p -video_size 1920x1080 -framerate 60 -i pipe:0"));
            Assert.That(args, Does.Contain("-f pulse -i \"alsa_output.monitor\""));
            Assert.That(args, Does.Contain("-map 1:a:0 -c:a aac"));
            Assert.That(args, Does.Contain("-c:v libx264"));
            Assert.That(args, Does.Contain("-fps_mode cfr -r 60"));
            Assert.That(args, Does.Contain("-movflags +faststart"));
            Assert.That(args, Does.EndWith("\"/tmp/out.mp4\""));
        });

        string silent = FFmpegEncodingBridge.BuildEncodeArgs(1920, 1080, 30, plan, null, "/tmp/out.mp4");
        Assert.That(silent, Does.Not.Contain("pulse").And.Not.Contain("-c:a"));
    }

    [TestCase("arch", null, LinuxPackageFamily.Arch)]
    [TestCase("omarchy", "arch", LinuxPackageFamily.Arch)]
    [TestCase("ubuntu", "debian", LinuxPackageFamily.Debian)]
    [TestCase("pop", "ubuntu debian", LinuxPackageFamily.Debian)]
    [TestCase("fedora", null, LinuxPackageFamily.Fedora)]
    [TestCase("bazzite", "fedora", LinuxPackageFamily.Fedora)]
    [TestCase("opensuse-tumbleweed", "opensuse suse", LinuxPackageFamily.OpenSuse)]
    [TestCase("alpine", null, LinuxPackageFamily.Alpine)]
    [TestCase("nixos", null, LinuxPackageFamily.NixOS)]
    [TestCase("gentoo", null, LinuxPackageFamily.Unknown)]
    public void DetectsThePackageFamily(string id, string? idLike, LinuxPackageFamily expected) =>
        Assert.That(GStreamerPluginAdvisor.DetectFamily(id, idLike, isFlatpak: false), Is.EqualTo(expected));

    [Test]
    public void FlatpakWinsOverTheHostDistro() =>
        Assert.That(GStreamerPluginAdvisor.DetectFamily("arch", null, isFlatpak: true), Is.EqualTo(LinuxPackageFamily.Flatpak));

    [TestCase(LinuxPackageFamily.Arch, "sudo pacman -S --needed gst-plugins-good gst-plugins-ugly")]
    [TestCase(LinuxPackageFamily.Debian, "sudo apt install gstreamer1.0-plugins-good gstreamer1.0-plugins-ugly")]
    [TestCase(LinuxPackageFamily.Fedora, "sudo dnf install gstreamer1-plugins-good gstreamer1-plugins-ugly")]
    [TestCase(LinuxPackageFamily.OpenSuse, "sudo zypper install gstreamer-plugins-good gstreamer-plugins-ugly")]
    [TestCase(LinuxPackageFamily.Alpine, "sudo apk add gst-plugins-good gst-plugins-ugly")]
    public void BuildsTheInstallCommandForTheHost(LinuxPackageFamily family, string command)
    {
        GStreamerPluginAdvice advice = GStreamerPluginAdvisor.Advise(["x264enc", "mp4mux"], family, "Host");
        Assert.That(advice.InstallCommand, Is.EqualTo(command));
        Assert.That(advice.HasMissing, Is.True);
    }

    [Test]
    public void ExplainsRepositoriesAndSandboxes()
    {
        Assert.That(GStreamerPluginAdvisor.Advise(["x264enc"], LinuxPackageFamily.Fedora, "Fedora").Note, Does.Contain("RPM Fusion"));
        GStreamerPluginAdvice flatpak = GStreamerPluginAdvisor.Advise(["x264enc"], LinuxPackageFamily.Flatpak, "Arch");
        Assert.That((flatpak.InstallCommand, flatpak.Note), Is.EqualTo(((string?)null, "Update the XerahS Flatpak and its runtime: flatpak update")));
        Assert.That(GStreamerPluginAdvisor.Advise([], LinuxPackageFamily.Arch, "Arch").HasMissing, Is.False);
    }

    [Test]
    public void AudioAddsItsElements() =>
        Assert.That(GStreamerPluginAdvisor.RequiredElements(VideoCodec.H264, withAudio: true), Is.EqualTo(new[] { "x264enc", "mp4mux", "pulsesrc", "avenc_aac" }));

    [Test]
    public void ReadsThePrettyName()
    {
        LinuxOsReleaseInfo info = LinuxOsRelease.Load(_ => ["NAME=\"Arch Linux\"", "PRETTY_NAME=\"Omarchy\"", "ID=arch", "ID_LIKE=\"arch\""]);
        Assert.That((info.DistroId, info.DistroIdLike, info.PrettyName), Is.EqualTo(("arch", "arch", "Omarchy")));
    }

    [Test]
    public void NoNoticeWhenFFmpegEncodes()
    {
        // The FFmpeg bridge records H.264/VP9 fine: no toast, and never a sudo command.
        Assert.That(GStreamerPluginAdvisor.BuildNotice(usedFfmpegFallback: true, hasUsableFFmpegEncoder: true, userFFmpegAlreadyDownloaded: false), Is.Null);
        Assert.That(GStreamerPluginAdvisor.BuildNotice(false, hasUsableFFmpegEncoder: true, userFFmpegAlreadyDownloaded: false), Is.Null);
        Assert.That(GStreamerPluginAdvisor.BuildNotice(false, false, userFFmpegAlreadyDownloaded: true), Is.Null);
    }

    [Test]
    public void DegradedRecordingFetchesFFmpegWithoutAdminRights()
    {
        RecordingEncoderNotice? notice = GStreamerPluginAdvisor.BuildNotice(false, false, false);
        Assert.That(notice, Is.Not.Null);
        Assert.That(notice!.DownloadFFmpeg, Is.True);
        Assert.That(notice.Text, Does.Contain("no administrator rights"));
        Assert.That(notice.Text + notice.Title, Does.Not.Contain("sudo"));
    }
}
