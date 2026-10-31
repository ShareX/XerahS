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
using System.Reflection;
using NUnit.Framework;
using XerahS.Common;
using XerahS.Media;

namespace XerahS.Tests.Tools;

[TestFixture]
public sealed class FFmpegCLIManagerTests
{
    [Test]
    [Platform(Exclude = "Win")]
    public void Close_AfterGracefulQuitAttempts_KillsEntireProcessTree()
    {
        const string shellPath = "/bin/sh";
        if (!File.Exists(shellPath))
        {
            Assert.Ignore("/bin/sh is required for the process-tree regression test.");
        }

        string tempDirectory = Path.Combine(Path.GetTempPath(), "XerahS-FFmpegCLIManagerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string childPidPath = Path.Combine(tempDirectory, "child.pid");
        int childPid = 0;

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = shellPath,
                Arguments = $"-c \"sleep 60 & echo $! > {QuoteForShell(childPidPath)}; wait\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        try
        {
            Assert.That(process.Start(), Is.True);
            Assert.That(WaitUntil(() => TryReadPid(childPidPath, out childPid), TimeSpan.FromSeconds(5)), Is.True,
                "Timed out waiting for child process PID.");

            var manager = new FFmpegCLIManager(shellPath);
            SetPrivateField(typeof(ExternalCLIManager), manager, "process", process);
            SetPrivateField(typeof(ExternalCLIManager), manager, "<IsProcessRunning>k__BackingField", true);
            SetPrivateField(typeof(FFmpegCLIManager), manager, "closeTryCount", 2);

            manager.Close();

            Assert.Multiple(() =>
            {
                Assert.That(process.WaitForExit(5000), Is.True, "Parent process should be killed on the forced close attempt.");
                Assert.That(WaitUntil(() => ProcessHasExited(childPid), TimeSpan.FromSeconds(5)), Is.True,
                    "Forced FFmpeg close should terminate child processes as well as the parent process.");
            });
        }
        finally
        {
            TryKillProcessTree(process);
            if (childPid > 0)
            {
                TryKillProcess(childPid);
            }

            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void IsAllowedExecutablePath_RejectsWindowsNonExeFiles()
    {
        Assert.That(FFmpegCLIManager.IsAllowedExecutablePath("ffmpeg.exe"), Is.True);
        Assert.That(FFmpegCLIManager.IsAllowedExecutablePath("ffmpeg"), Is.True);
        Assert.That(FFmpegCLIManager.IsAllowedExecutablePath(""), Is.False);

        if (OperatingSystem.IsWindows())
        {
            Assert.That(FFmpegCLIManager.IsAllowedExecutablePath(@"C:\tools\ffmpeg.dll"), Is.False);
            Assert.That(FFmpegCLIManager.IsAllowedExecutablePath(@"C:\video.mp4"), Is.False);
        }
    }

    [Test]
    public void GetVideoInfo_EscapesEmbeddedQuotesInInputPath()
    {
        var manager = new CapturingFFmpegCLIManager();
        string videoPath = Path.Combine(Path.GetTempPath(), "capture \"quoted\" name.mp4");

        VideoInfo? info = manager.GetVideoInfo(videoPath);

        Assert.Multiple(() =>
        {
            Assert.That(info, Is.Not.Null);
            Assert.That(manager.CapturedArgs, Is.EqualTo("-i \"" + videoPath.Replace("\"", "\\\"") + "\""));
        });
    }

    [Test]
    public void ConcatenateVideos_EscapesApostrophesInConcatListPaths()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "XerahS-FFmpegCLIManagerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var manager = new CapturingFFmpegCLIManager();
            string inputPath = Path.Combine(tempDirectory, "clip'segment.mp4");
            string outputPath = Path.Combine(tempDirectory, "combined.mp4");

            manager.ConcatenateVideos(new[] { inputPath }, outputPath);

            Assert.Multiple(() =>
            {
                Assert.That(manager.CapturedArgs, Is.EqualTo($"-f concat -safe 0 -i {outputPath}.txt -c copy {outputPath}"));
                Assert.That(manager.CapturedConcatList, Is.EqualTo($"file '{inputPath.Replace("'", "'\\''")}'"));
            });
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private sealed class CapturingFFmpegCLIManager : FFmpegCLIManager
    {
        public CapturingFFmpegCLIManager() : base("ffmpeg")
        {
        }

        public string? CapturedArgs { get; private set; }
        public string? CapturedConcatList { get; private set; }

        public override int Open(string path, string? args = null)
        {
            CapturedArgs = args;

            if (args?.StartsWith("-f concat ", StringComparison.Ordinal) == true)
            {
                string listFile = ExtractConcatListFilePath(args);
                CapturedConcatList = File.ReadAllText(listFile);
                return 0;
            }

            Output.AppendLine("Input #0, mov,mp4,m4a,3gp,3g2,mj2, from 'capture.mp4':");
            Output.AppendLine("  Duration: 00:00:01.00, start: 0.000000, bitrate: 512 kb/s");
            Output.AppendLine("    Stream #0:0: Video: h264 (High), yuv420p, 1920x1080, 30 fps");
            return 0;
        }

        private static string ExtractConcatListFilePath(string args)
        {
            const string prefix = " -i ";
            int start = args.IndexOf(prefix, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            start += prefix.Length;
            int end = args.IndexOf(" -c copy ", start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start));
            return args[start..end].Trim('"');
        }
    }

    private static string QuoteForShell(string value) => "'" + value.Replace("'", "'\\''") + "'";

    private static bool TryReadPid(string path, out int pid)
    {
        pid = 0;

        if (!File.Exists(path))
        {
            return false;
        }

        return int.TryParse(File.ReadAllText(path).Trim(), out pid);
    }

    private static bool ProcessHasExited(int pid)
    {
        // On Linux, when a parent and its child are both killed in quick succession (as
        // FFmpegCLIManager.Close() does), the child becomes a zombie because the parent
        // is dead and cannot wait() to reap it. Process.GetProcessById() returns zombies
        // and reports HasExited == false, which would race against init's reaper and
        // flake the test. Read /proc/<pid>/status directly: State "Z" (zombie) or "X"
        // (dead) both mean the process has terminated, even if the PID is still in the
        // table. Falls back to the managed Process API on non-Linux or if /proc is
        // unavailable.
        if (OperatingSystem.IsLinux())
        {
            string statusPath = $"/proc/{pid}/status";
            try
            {
                foreach (string line in File.ReadAllLines(statusPath))
                {
                    if (line.StartsWith("State:", StringComparison.Ordinal))
                    {
                        // Format: "State:\t<letter> (<name>)"
                        // We only care about the first letter: R/S/D/Z/T/X/I/K/P
                        // Z = zombie, X = dead. Both mean the process has terminated.
                        int letterIndex = line.IndexOf('\t') + 1;
                        if (letterIndex > 0 && letterIndex < line.Length)
                        {
                            char state = line[letterIndex];
                            return state == 'Z' || state == 'X' || state == 'K';
                        }
                        return false;
                    }
                }
            }
            catch (FileNotFoundException)
            {
                return true;
            }
            catch (DirectoryNotFoundException)
            {
                return true;
            }
        }

        try
        {
            using Process process = Process.GetProcessById(pid);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(50);
        }

        return condition();
    }

    private static void SetPrivateField(Type declaringType, object instance, string fieldName, object? value)
    {
        FieldInfo? field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found on {declaringType.FullName}.");
        field!.SetValue(instance, value);
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void TryKillProcess(int pid)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
