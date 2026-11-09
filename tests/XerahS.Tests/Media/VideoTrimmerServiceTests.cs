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
using NUnit.Framework;
using XerahS.Media;

namespace XerahS.Tests.Media;

[TestFixture]
public class VideoTrimmerServiceTests
{
    [TestCase("12", 12)]
    [TestCase("1:05.5", 65.5)]
    [TestCase("01:02:03.250", 3723.25)]
    public void TryParseTimestamp_AcceptsCommonForms(string text, double expected)
    {
        Assert.That(VideoTrimmerService.TryParseTimestamp(text, out double seconds), Is.True);
        Assert.That(seconds, Is.EqualTo(expected).Within(0.0001));
    }

    [TestCase("")]
    [TestCase("abc")]
    [TestCase("1.5:00")]
    [TestCase("1:2:3:4")]
    [TestCase("-3")]
    public void TryParseTimestamp_RejectsInvalid(string text)
    {
        Assert.That(VideoTrimmerService.TryParseTimestamp(text, out _), Is.False);
    }

    [Test]
    public void BuildTrimArguments_FastCopiesStreams_PreciseReencodes()
    {
        string[] fast = VideoTrimmerService.BuildTrimArguments("in.mkv", "out.mkv", 1.5, 4, precise: false);
        Assert.That(string.Join(' ', fast), Does.Contain("-ss 00:00:01.500 -i in.mkv -t 00:00:02.500"));
        Assert.That(fast, Does.Contain("copy"));
        Assert.That(fast, Does.Not.Contain("libx264"));

        string[] precise = VideoTrimmerService.BuildTrimArguments("in.mkv", "out.mp4", 1.5, 4, precise: true);
        Assert.That(precise, Does.Contain("libx264"));
        Assert.That(precise, Does.Not.Contain("copy"));
        Assert.That(precise[^1], Is.EqualTo("out.mp4"));
    }

    [Test]
    public void GetDefaultOutputPath_KeepsExtensionForFastAndNumbersCollisions()
    {
        string dir = Path.Combine(Path.GetTempPath(), "xerahs-trim-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string input = Path.Combine(dir, "clip.mkv");
            Assert.That(Path.GetFileName(VideoTrimmerService.GetDefaultOutputPath(input, precise: false)), Is.EqualTo("clip-trimmed.mkv"));
            Assert.That(Path.GetFileName(VideoTrimmerService.GetDefaultOutputPath(input, precise: true)), Is.EqualTo("clip-trimmed.mp4"));

            File.WriteAllText(Path.Combine(dir, "clip-trimmed.mkv"), "x");
            Assert.That(Path.GetFileName(VideoTrimmerService.GetDefaultOutputPath(input, precise: false)), Is.EqualTo("clip-trimmed-2.mkv"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task TrimAsync_ProducesShorterVideoWithRealFFmpeg()
    {
        string? ffmpeg = new[] { "/usr/bin/ffmpeg", "/usr/local/bin/ffmpeg" }.FirstOrDefault(File.Exists);
        if (ffmpeg == null)
        {
            Assert.Ignore("FFmpeg is not installed.");
        }

        string dir = Path.Combine(Path.GetTempPath(), "xerahs-trim-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string input = Path.Combine(dir, "source.mp4");
            using (var generate = Process.Start(new ProcessStartInfo(ffmpeg!,
                       $"-hide_banner -v error -f lavfi -i testsrc=duration=4:size=160x120:rate=10 -c:v libx264 -g 5 -pix_fmt yuv420p \"{input}\"")
                   { UseShellExecute = false }))
            {
                await generate!.WaitForExitAsync();
                Assert.That(generate.ExitCode, Is.EqualTo(0));
            }

            var service = new VideoTrimmerService(ffmpeg!);
            double? duration = await service.ProbeDurationAsync(input);
            Assert.That(duration, Is.EqualTo(4).Within(0.2));

            foreach (bool precise in new[] { false, true })
            {
                string output = VideoTrimmerService.GetDefaultOutputPath(input, precise);
                await service.TrimAsync(input, output, 1, 3, duration!.Value, precise, null, CancellationToken.None);
                double? trimmed = await service.ProbeDurationAsync(output);
                Assert.That(trimmed, Is.EqualTo(2).Within(0.6), precise ? "precise" : "fast");
                Assert.That(Directory.GetFiles(dir, ".xerahs-trim-*"), Is.Empty, "staging file cleaned up");
            }

            byte[]? frame = await service.GetFrameAsync(input, 2);
            Assert.That(frame, Is.Not.Null.And.Length.GreaterThan(8));
            Assert.That(frame![..4], Is.EqualTo(new byte[] { 0x89, 0x50, 0x4E, 0x47 }), "PNG signature");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.TrimAsync(input, input, 1, 3, duration!.Value, false, null, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
