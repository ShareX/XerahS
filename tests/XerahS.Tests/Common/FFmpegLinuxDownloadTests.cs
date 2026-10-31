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
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Common;

public class FFmpegLinuxDownloadTests
{
    private const string LinuxArchive = "/Td6WFoAAATm1rRGAgAhARYAAAB0L+Wj4Cf/AMJdADNhsOBmbmIE9QcryQD4J4pFCQH12WNC2ozc6DBAdcUdTpqG4z5Ozy2JD8VMb83eoahb3Ftetw5XuuDcNJclxmdGYi5xY9Fdff4dcFaNaRxJyWAnFEMrrB2c4sOy+XcevIiMr82SesDA6YMWhg9MxiOXLmoFhJ7dSw/MP2EDpftKUC3GYTYlP2xmj61UO/O3LpVI2zKvj8bb+qVG/57b5lkAwU232rw5YOw7sY1jBc+EnCmHMFC70HsQP+0omsNU7SegAAAAVe/uByrtED0AAd4BgFAAALkY5FexxGf7AgAAAAAEWVo=";
    private string _folder = null!;

    [SetUp] public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), "xerahs-btbn-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }
    [TearDown] public void Cleanup() => Directory.Delete(_folder, recursive: true);

    [TestCase(FFmpegArchitecture.win64, "win64", ".zip")]
    [TestCase(FFmpegArchitecture.winArm64, "winarm64", ".zip")]
    [TestCase(FFmpegArchitecture.linux64, "linux64", ".tar.xz")]
    [TestCase(FFmpegArchitecture.linuxArm64, "linuxarm64", ".tar.xz")]
    public void SelectsOnlyTheStableStaticGplBuildAndRequiresChecksums(FFmpegArchitecture architecture, string target, string extension)
    {
        var checker = new TestChecker(architecture);
        string name = $"ffmpeg-n9.0-latest-{target}-gpl-9.0{extension}";
        string[] assets = [$"ffmpeg-master-latest-{target}-gpl{extension}", $"ffmpeg-n9.0-latest-{target}-gpl-shared-9.0{extension}",
            $"ffmpeg-n9.0-latest-{target}-lgpl-9.0{extension}", name, "checksums.sha256"];
        Assert.That(checker.Apply(assets), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(checker.Owner, Is.EqualTo("BtbN"));
            Assert.That(checker.Repo, Is.EqualTo("FFmpeg-Builds"));
            Assert.That(checker.FileName, Is.EqualTo(name));
            Assert.That(checker.DownloadURL, Is.EqualTo($"https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/{name}"));
            Assert.That(checker.ChecksumsURL, Does.EndWith("/checksums.sha256"));
        });
        Assert.That(checker.Apply(assets.Where(n => n != name).ToArray()), Is.False);
        Assert.That(checker.DownloadURL, Is.Null, "A failed lookup must not retain a previous download URL.");
        Assert.That(checker.Apply([name]), Is.False, "The checksum manifest is required on Windows and Linux.");
    }

    [TestCase(FFmpegArchitecture.win32)]
    [TestCase(FFmpegArchitecture.macos64)]
    [TestCase(FFmpegArchitecture.unsupported)]
    public void UnsupportedPlatformsNeverFallBackToAnotherCpu(FFmpegArchitecture architecture)
    {
        var checker = new TestChecker(architecture);
        Assert.That(checker.GetExpectedAssetName(), Is.Null);
        Assert.That(checker.Apply(["ffmpeg-n9.0-latest-win64-gpl-9.0.zip", "checksums.sha256"]), Is.False);
        Assert.That(FFmpegUpdateChecker.ResolveArchitecture(OSPlatform.Linux, Architecture.X86), Is.EqualTo(FFmpegArchitecture.unsupported));
        Assert.That(FFmpegUpdateChecker.ResolveArchitecture(OSPlatform.Windows, Architecture.Arm64), Is.EqualTo(FFmpegArchitecture.winArm64));
    }

    [TestCase("v9.0", false, false)]
    [TestCase("latest", true, false)]
    [TestCase("latest", false, true)]
    public void RejectsRetiredTagsDraftsAndPrereleases(string tag, bool draft, bool prerelease)
    {
        var checker = new TestChecker(FFmpegArchitecture.linux64);
        Assert.That(checker.Apply([checker.GetExpectedAssetName()!, "checksums.sha256"], tag, draft, prerelease), Is.False);
    }

    [Test]
    public void AcceptsDatedBtbNReleases()
    {
        var checker = new TestChecker(FFmpegArchitecture.linux64);
        Assert.That(checker.Apply([checker.GetExpectedAssetName()!, "checksums.sha256"], "autobuild-2026-10-17-20-51"), Is.True);
    }

    [Test]
    public void FindsOnlyTheExactUnambiguousChecksum()
    {
        string hash = new('A', 64);
        byte[] sums = Encoding.UTF8.GetBytes($"{hash}  ffmpeg.tar.xz\r\n{new string('b', 64)} *other.tar.xz\n");
        Assert.That(FFmpegReleaseVerifier.FindHash(sums, "ffmpeg.tar.xz"), Is.EqualTo(hash.ToLowerInvariant()));
        Assert.That(FFmpegReleaseVerifier.FindHash(sums, "ffmpeg"), Is.Null);
        Assert.That(FFmpegReleaseVerifier.FindHash(sums.Concat(sums).ToArray(), "ffmpeg.tar.xz"), Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task InstallsNestedToolPairAndLicenseWithoutUnpackingOtherFiles(bool linux)
    {
        string archive = linux ? WriteLinuxArchive() : WriteZip();
        string destination = Path.Combine(_folder, "tools");
        var architecture = linux ? FFmpegArchitecture.linux64 : FFmpegArchitecture.win64;
        string path = await FFmpegDownloader.InstallArchiveAsync(archive, Hash(archive), destination, architecture);
        Assert.That(File.ReadAllText(path), Is.EqualTo("synthetic ffmpeg"));
        Assert.That(File.ReadAllText(Path.Combine(destination, linux ? "ffprobe" : "ffprobe.exe")), Is.EqualTo("synthetic ffprobe"));
        Assert.That(File.ReadAllText(Path.Combine(destination, "LICENSE.txt")), Is.EqualTo("GPL test license"));
        Assert.That(Directory.GetFileSystemEntries(destination), Has.Length.EqualTo(3));
        if (!OperatingSystem.IsWindows()) Assert.That(File.GetUnixFileMode(path).HasFlag(UnixFileMode.UserExecute), Is.True);
    }

    [Test]
    public void TamperedArchiveNeverTouchesExistingTools()
    {
        string archive = WriteZip();
        string destination = ExistingTools();
        Assert.ThrowsAsync<InvalidDataException>(() => FFmpegDownloader.InstallArchiveAsync(archive, new string('0', 64), destination, FFmpegArchitecture.win64));
        AssertOldTools(destination);
    }

    [TestCase("missing")]
    [TestCase("duplicate")]
    [TestCase("symlink")]
    [TestCase("traversal")]
    public void MalformedArchiveNeverMixesOldAndNewBinaries(string problem)
    {
        string archive = WriteZip(problem);
        string destination = ExistingTools();
        Assert.ThrowsAsync<InvalidDataException>(() => FFmpegDownloader.InstallArchiveAsync(archive, Hash(archive), destination, FFmpegArchitecture.win64));
        AssertOldTools(destination);
    }

    [Test]
    public void FailedReplacementRollsBackTheFirstBinary()
    {
        string archive = WriteZip();
        string destination = ExistingTools();
        File.Delete(Path.Combine(destination, "ffprobe.exe"));
        Directory.CreateDirectory(Path.Combine(destination, "ffprobe.exe"));
        Assert.ThrowsAsync<IOException>(() => FFmpegDownloader.InstallArchiveAsync(archive, Hash(archive), destination, FFmpegArchitecture.win64));
        Assert.That(File.ReadAllText(Path.Combine(destination, "ffmpeg.exe")), Is.EqualTo("old ffmpeg"));
        Assert.That(Directory.GetDirectories(destination, ".ffmpeg-install-*"), Is.Empty);
    }

    [Test]
    public void CancellationDoesNotReplaceExistingTools()
    {
        string archive = WriteLinuxArchive();
        string destination = ExistingTools();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.That(async () => await FFmpegDownloader.InstallArchiveAsync(archive, Hash(archive), destination, FFmpegArchitecture.linux64, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
        AssertOldTools(destination);
    }

    [Test]
    public void LinuxToolsFolderKeepsExistingInstallLocations()
    {
        if (!OperatingSystem.IsLinux()) Assert.Ignore("Linux only.");
        Assert.That(PathsManager.GetToolsArchitectureFolderName(Architecture.X64), Is.EqualTo("linux-x64"));
        Assert.That(PathsManager.GetToolsArchitectureFolderName(Architecture.Arm64), Is.EqualTo("linux-arm64"));
    }

    [Test, Explicit("Downloads approximately 150 MB from BtbN, validates it, and runs a synthetic recording."), Category("Network")]
    public async Task DownloadsVerifiedToolsAndEncodesPlayableVideo()
    {
        if (!OperatingSystem.IsLinux()) Assert.Ignore("Linux executable smoke test.");
        string destination = Path.Combine(_folder, "downloaded-tools");
        FFmpegDownloadResult result = await FFmpegDownloader.DownloadLatestAsync(destination);
        Assert.That(result.Success, Is.True, result.ErrorMessage);
        Assert.That(result.FFmpegPath, Is.EqualTo(Path.Combine(destination, "ffmpeg")));
        string version = await Run(result.FFmpegPath!, ["-version"]);
        Assert.That(version, Does.Contain($"ffmpeg version n{FFmpegUpdateChecker.ReleaseBranch}"));
        TestContext.Out.WriteLine(version.Split('\n')[0]);
        string video = Path.Combine(_folder, "synthetic.mp4");
        await Run(result.FFmpegPath!, ["-hide_banner", "-f", "lavfi", "-i", "testsrc2=size=64x64:rate=10", "-t", "0.3", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-y", video]);
        string output = await Run(Path.Combine(destination, "ffprobe"), ["-v", "error", "-show_entries", "stream=codec_name,width,height", "-of", "json", video]);
        Assert.That(output, Does.Contain("h264").And.Contain("64"));
    }

    private static async Task<string> Run(string executable, string[] arguments)
    {
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.That(process.ExitCode, Is.Zero, await error);
        return await output;
    }

    private string WriteLinuxArchive()
    {
        string path = Path.Combine(_folder, "fixture.tar.xz");
        File.WriteAllBytes(path, Convert.FromBase64String(LinuxArchive));
        return path;
    }
    private string WriteZip(string? problem = null)
    {
        string path = Path.Combine(_folder, "fixture.zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Add(string name, string text, bool link = false)
        {
            var entry = zip.CreateEntry(name);
            if (link) entry.ExternalAttributes = 0xA1FF << 16;
            using var writer = new StreamWriter(entry.Open());
            writer.Write(text);
        }
        Add("build/bin/ffmpeg.exe", "synthetic ffmpeg", problem == "symlink");
        if (problem != "missing") Add(problem == "traversal" ? "../ffprobe.exe" : "build/bin/ffprobe.exe", "synthetic ffprobe");
        if (problem == "duplicate") Add("build/bin/ffmpeg.exe", "duplicate");
        Add("build/LICENSE.txt", "GPL test license");
        Add("build/doc/ignored.txt", "ignored");
        return path;
    }
    private string ExistingTools()
    {
        string folder = Path.Combine(_folder, "tools");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "ffmpeg.exe"), "old ffmpeg");
        File.WriteAllText(Path.Combine(folder, "ffprobe.exe"), "old ffprobe");
        return folder;
    }
    private static void AssertOldTools(string folder)
    {
        Assert.That(File.ReadAllText(Path.Combine(folder, "ffmpeg.exe")), Is.EqualTo("old ffmpeg"));
        Assert.That(File.ReadAllText(Path.Combine(folder, "ffprobe.exe")), Is.EqualTo("old ffprobe"));
    }
    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private sealed class TestChecker(FFmpegArchitecture architecture)
        : FFmpegUpdateChecker(FFmpegDownloader.DefaultOwner, FFmpegDownloader.DefaultRepo, architecture)
    {
        public bool Apply(string[] names, string tag = "latest", bool draft = false, bool prerelease = false) =>
            UpdateReleaseInfo(JsonSerializer.Deserialize<GitHubRelease>(JsonSerializer.Serialize(new
            {
                tag_name = tag, draft, prerelease,
                assets = names.Select(name => new { name, browser_download_url = $"https://github.com/BtbN/FFmpeg-Builds/releases/download/{tag}/{name}" })
            })), false, true);
    }
}
