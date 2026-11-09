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
using SkiaSharp;
using XerahS.Common;
using XerahS.Media.Encoders;
using XerahS.Services.Abstractions;

namespace XerahS.Tests.Media;

[TestFixture]
public class JxlImageEncoderTests
{
    [Test]
    public void FFmpegEncoder_HandlesAvifAndJxlOnly()
    {
        var encoder = new FFmpegImageEncoder(() => "ffmpeg");

        Assert.That(encoder.CanEncode(EImageFormat.JXL), Is.True);
        Assert.That(encoder.CanEncode(EImageFormat.AVIF), Is.True);
        Assert.That(encoder.CanEncode(EImageFormat.PNG), Is.False);
        Assert.That(encoder.CanEncode(EImageFormat.WEBP), Is.False);
    }

    [Test]
    public void SkiaEncoder_DoesNotClaimJxl()
    {
        Assert.That(new SkiaImageEncoder().CanEncode(EImageFormat.JXL), Is.False);
    }

    [Test]
    public void ImageFormat_JxlExtensionIsJxl()
    {
        Assert.That(EnumExtensions.GetDescription(EImageFormat.JXL), Is.EqualTo("jxl"));
    }

    [TestCase(100, 0.0)]
    [TestCase(90, 1.0)]
    [TestCase(75, 2.35)]
    [TestCase(30, 6.4)]
    public void JxlDistance_FollowsCjxlQualityCurve(int quality, double expected)
    {
        Assert.That(FFmpegImageEncoder.JxlDistance(quality), Is.EqualTo(expected).Within(0.01));
    }

    [Test]
    public void JxlDistance_GrowsAsQualityDropsAndIsClamped()
    {
        double previous = -1;
        for (int quality = 100; quality >= 0; quality -= 5)
        {
            double distance = FFmpegImageEncoder.JxlDistance(quality);
            Assert.That(distance, Is.GreaterThanOrEqualTo(previous), $"quality {quality}");
            previous = distance;
        }

        Assert.That(FFmpegImageEncoder.JxlDistance(500), Is.EqualTo(0));
        Assert.That(FFmpegImageEncoder.JxlDistance(-5), Is.EqualTo(FFmpegImageEncoder.JxlDistance(0)));
    }

    [Test]
    public void BuildArguments_UsesLibjxlWithInvariantDistance()
    {
        string args = FFmpegImageEncoder.BuildArguments(EImageFormat.JXL, "in.png", "out.jxl", 90);

        Assert.That(args, Does.Contain("-c:v libjxl"));
        Assert.That(args, Does.Contain("-distance 1"));
        Assert.That(args, Does.Contain("\"out.jxl\""));
    }

    [Test]
    public void BuildArguments_KeepsAvifOnLibaom()
    {
        string args = FFmpegImageEncoder.BuildArguments(EImageFormat.AVIF, "in.png", "out.avif", 100);

        Assert.That(args, Does.Contain("-c:v libaom-av1"));
        Assert.That(args, Does.Contain("-crf 0"));
    }

    [Test]
    public async Task Encode_WithRealFfmpeg_WritesJxlFile()
    {
        string? ffmpeg = FindFfmpegWithLibjxl();
        if (ffmpeg == null)
        {
            Assert.Ignore("No ffmpeg with the libjxl encoder is available on this machine.");
        }

        string dir = Path.Combine(Path.GetTempPath(), "xerahs-jxl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var bitmap = new SKBitmap(64, 48, SKColorType.Rgba8888, SKAlphaType.Premul);
            bitmap.Erase(new SKColor(200, 30, 30, 255));
            string output = Path.Combine(dir, "capture.jxl");

            await new FFmpegImageEncoder(() => ffmpeg).EncodeAsync(bitmap, output, EImageFormat.JXL, 100);

            byte[] bytes = File.ReadAllBytes(output);
            Assert.That(bytes.Length, Is.GreaterThan(12));
            bool bareCodestream = bytes[0] == 0xFF && bytes[1] == 0x0A;
            bool isobmffContainer = bytes[4] == (byte)'J' && bytes[5] == (byte)'X' && bytes[6] == (byte)'L';
            Assert.That(bareCodestream || isobmffContainer, Is.True, "file starts with a JPEG XL signature");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Encode_WithoutFfmpeg_ThrowsClearError()
    {
        using var bitmap = new SKBitmap(8, 8);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new FFmpegImageEncoder(() => "/nonexistent/ffmpeg")
                .EncodeAsync(bitmap, Path.Combine(Path.GetTempPath(), "never.jxl"), EImageFormat.JXL, 90));

        Assert.That(ex!.Message, Does.Contain("FFmpeg").And.Contain("JXL"));
    }

    private static string? FindFfmpegWithLibjxl()
    {
        foreach (string candidate in new[] { "/usr/bin/ffmpeg", "/usr/local/bin/ffmpeg", "/opt/homebrew/bin/ffmpeg" })
        {
            if (!File.Exists(candidate)) continue;
            try
            {
                using var process = Process.Start(new ProcessStartInfo(candidate, "-hide_banner -encoders")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                });
                string output = process!.StandardOutput.ReadToEnd();
                process.WaitForExit(10000);
                if (output.Contains("libjxl")) return candidate;
            }
            catch
            {
                // Try the next candidate.
            }
        }

        return null;
    }
}
