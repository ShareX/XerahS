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
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services
{
    public class LinuxDiagnosticService : IDiagnosticService
    {
        private const string FolderName = "CaptureTroubleshooting";

        public string WriteRegionCaptureDiagnostics(string personalFolder)
        {
            // Not implemented for Linux yet
            return string.Empty;
        }

        public string WriteRecordingDiagnostics(string personalFolder)
        {
            if (!OperatingSystem.IsLinux())
            {
                return string.Empty;
            }

            try
            {
                var folder = Path.Combine(personalFolder, FolderName);
                Directory.CreateDirectory(folder);

                string fileName = $"linux-recording-diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                string filePath = Path.Combine(folder, fileName);
                string report = BuildRecordingReport();

                File.WriteAllText(filePath, report, Encoding.UTF8);
                return filePath;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string BuildRecordingReport()
        {
            var sb = new StringBuilder();

            string sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "unknown";
            string currentDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "unknown";
            string desktopSession = Environment.GetEnvironmentVariable("DESKTOP_SESSION") ?? "unknown";
            bool isWayland = sessionType.Equals("wayland", StringComparison.OrdinalIgnoreCase);

            string currentDirectory = Environment.CurrentDirectory;
            string baseDirectory = AppContext.BaseDirectory;
            string processPath = Environment.ProcessPath ?? "unknown";
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "<not set>";
            string xdgRuntimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? "<not set>";
            string flatpakId = Environment.GetEnvironmentVariable("FLATPAK_ID") ?? "<not set>";
            string snap = Environment.GetEnvironmentVariable("SNAP") ?? "<not set>";
            string container = Environment.GetEnvironmentVariable("container") ?? "<not set>";
            bool hasFlatpakInfo = File.Exists("/.flatpak-info");
            bool hasDockerEnv = File.Exists("/.dockerenv");
            bool hasContainerEnv = File.Exists("/run/.containerenv");

            bool hasScreenCastPortal = PortalInterfaceChecker.HasInterface("org.freedesktop.portal.ScreenCast");
            bool hasScreenshotPortal = PortalInterfaceChecker.HasInterface("org.freedesktop.portal.Screenshot");
            bool hasGlobalShortcutsPortal = PortalInterfaceChecker.HasInterface("org.freedesktop.portal.GlobalShortcuts");
            bool hasInputCapturePortal = PortalInterfaceChecker.HasInterface("org.freedesktop.portal.InputCapture");

            string screenCastProbe = PortalInterfaceChecker.GetDiagnosticSummary("org.freedesktop.portal.ScreenCast");
            string screenshotProbe = PortalInterfaceChecker.GetDiagnosticSummary("org.freedesktop.portal.Screenshot");
            string globalShortcutsProbe = PortalInterfaceChecker.GetDiagnosticSummary("org.freedesktop.portal.GlobalShortcuts");
            string inputCaptureProbe = PortalInterfaceChecker.GetDiagnosticSummary("org.freedesktop.portal.InputCapture");

            var ffmpegProbe = ProbeCommand("ffmpeg");
            var wfRecorderProbe = ProbeCommand("wf-recorder");
            var slurpProbe = ProbeCommand("slurp");
            var grimProbe = ProbeCommand("grim");
            var gstLaunchProbe = ProbeCommand("gst-launch-1.0");
            var gstInspectProbe = ProbeCommand("gst-inspect-1.0");
            var busctlProbe = ProbeCommand("busctl");
            var pwCliProbe = ProbeCommand("pw-cli");

            bool hasFfmpeg = ffmpegProbe.Exists;
            bool hasWfRecorder = wfRecorderProbe.Exists;
            bool hasSlurp = slurpProbe.Exists;
            bool hasGrim = grimProbe.Exists;
            bool hasGstLaunch = gstLaunchProbe.Exists;
            bool hasGstInspect = gstInspectProbe.Exists;
            bool hasBusctl = busctlProbe.Exists;
            bool hasPwCli = pwCliProbe.Exists;

            bool ffmpegPipewire = hasFfmpeg && CommandOutputContains("ffmpeg", "-hide_banner -devices", "pipewire");
            bool ffmpegX11Grab = hasFfmpeg && CommandOutputContains("ffmpeg", "-hide_banner -devices", "x11grab");
            bool gstreamerPipewire = hasGstInspect && CommandExitCodeIsZero("gst-inspect-1.0", "pipewiresrc");
            bool pipewireRunning = CommandExitCodeIsZero("pgrep", "-x pipewire");

            string vp9CapabilitySnapshot = hasGstInspect
                ? GetVp9EncoderCapabilitySnapshot()
                : "gst-inspect-1.0 is missing; vp9enc capability snapshot unavailable.";

            string recommendedBackend = GetRecommendedBackend(
                isWayland,
                hasScreenCastPortal,
                ffmpegPipewire,
                gstreamerPipewire,
                hasWfRecorder);

            sb.AppendLine("================================================================================");
            sb.AppendLine("                      LINUX RECORDING DIAGNOSTICS");
            sb.AppendLine("================================================================================");
            sb.AppendLine();
            sb.AppendLine("[SYSTEM]");
            sb.AppendLine($"TimestampLocal: {DateTime.Now:O}");
            sb.AppendLine($"TimestampUtc: {DateTime.UtcNow:O}");
            sb.AppendLine($"OSDescription: {RuntimeInformation.OSDescription}");
            sb.AppendLine($"OSArchitecture: {RuntimeInformation.OSArchitecture}");
            sb.AppendLine($"ProcessArchitecture: {RuntimeInformation.ProcessArchitecture}");
            sb.AppendLine($"FrameworkDescription: {RuntimeInformation.FrameworkDescription}");
            sb.AppendLine($"CurrentCulture: {CultureInfo.CurrentCulture.Name}");
            sb.AppendLine();

            sb.AppendLine("[PROCESS EXECUTION CONTEXT]");
            sb.AppendLine($"CurrentDirectory: {currentDirectory}");
            sb.AppendLine($"AppContext.BaseDirectory: {baseDirectory}");
            sb.AppendLine($"ProcessPath: {processPath}");
            sb.AppendLine($"PATH: {pathEnv}");
            sb.AppendLine($"XDG_RUNTIME_DIR: {xdgRuntimeDir}");
            sb.AppendLine($"Sandbox.FlatpakId: {flatpakId}");
            sb.AppendLine($"Sandbox.FlatpakInfoFile: {hasFlatpakInfo}");
            sb.AppendLine($"Sandbox.Snap: {snap}");
            sb.AppendLine($"Sandbox.ContainerEnv: {container}");
            sb.AppendLine($"Sandbox.DockerMarker: {hasDockerEnv}");
            sb.AppendLine($"Sandbox.ContainerMarker: {hasContainerEnv}");
            sb.AppendLine();

            sb.AppendLine("[SESSION]");
            sb.AppendLine($"XDG_SESSION_TYPE: {sessionType}");
            sb.AppendLine($"XDG_CURRENT_DESKTOP: {currentDesktop}");
            sb.AppendLine($"DESKTOP_SESSION: {desktopSession}");
            sb.AppendLine($"Wayland: {isWayland}");
            sb.AppendLine();

            sb.AppendLine("[PORTAL INTERFACES]");
            sb.AppendLine($"ScreenCast: {ToStatus(hasScreenCastPortal)}");
            sb.AppendLine($"ScreenCastProbe: {screenCastProbe}");
            sb.AppendLine($"Screenshot: {ToStatus(hasScreenshotPortal)}");
            sb.AppendLine($"ScreenshotProbe: {screenshotProbe}");
            sb.AppendLine($"GlobalShortcuts: {ToStatus(hasGlobalShortcutsPortal)}");
            sb.AppendLine($"GlobalShortcutsProbe: {globalShortcutsProbe}");
            sb.AppendLine($"InputCapture: {ToStatus(hasInputCapturePortal)}");
            sb.AppendLine($"InputCaptureProbe: {inputCaptureProbe}");
            sb.AppendLine();

            sb.AppendLine("[COMMAND RESOLUTION]");
            sb.AppendLine($"ffmpeg: {ToStatus(hasFfmpeg)} ({ffmpegProbe.Resolution})");
            sb.AppendLine($"wf-recorder: {ToStatus(hasWfRecorder)} ({wfRecorderProbe.Resolution})");
            sb.AppendLine($"slurp: {ToStatus(hasSlurp)} ({slurpProbe.Resolution})");
            sb.AppendLine($"grim: {ToStatus(hasGrim)} ({grimProbe.Resolution})");
            sb.AppendLine($"gst-launch-1.0: {ToStatus(hasGstLaunch)} ({gstLaunchProbe.Resolution})");
            sb.AppendLine($"gst-inspect-1.0: {ToStatus(hasGstInspect)} ({gstInspectProbe.Resolution})");
            sb.AppendLine($"busctl: {ToStatus(hasBusctl)} ({busctlProbe.Resolution})");
            sb.AppendLine($"pw-cli: {ToStatus(hasPwCli)} ({pwCliProbe.Resolution})");
            sb.AppendLine();

            sb.AppendLine("[CAPABILITIES]");
            sb.AppendLine($"FFmpeg pipewire input: {ToStatus(ffmpegPipewire)}");
            sb.AppendLine($"FFmpeg x11grab input: {ToStatus(ffmpegX11Grab)}");
            sb.AppendLine($"GStreamer pipewiresrc: {ToStatus(gstreamerPipewire)}");
            sb.AppendLine($"pipewire process running: {ToStatus(pipewireRunning)}");
            sb.AppendLine();

            sb.AppendLine("[VP9 ENCODER CAPABILITIES]");
            sb.AppendLine(vp9CapabilitySnapshot);
            sb.AppendLine();

            sb.AppendLine("[BACKEND DECISION]");
            sb.AppendLine($"RecommendedBackend: {recommendedBackend}");
            sb.AppendLine();

            sb.AppendLine("[RECOMMENDATIONS]");
            if (isWayland)
            {
                if (!hasScreenCastPortal)
                {
                    sb.AppendLine("- Install xdg-desktop-portal backend for your DE/WM (gnome/kde/wlr/hyprland).");
                }
                if (!pipewireRunning)
                {
                    sb.AppendLine("- Ensure PipeWire is installed and running.");
                }
                if (!ffmpegPipewire && !gstreamerPipewire)
                {
                    sb.AppendLine("- Install FFmpeg with pipewire input or GStreamer pipewire plugins.");
                }
                if (!hasSlurp)
                {
                    sb.AppendLine("- Install slurp for native region selection on wlroots compositors.");
                }
            }
            else if (!ffmpegX11Grab)
            {
                sb.AppendLine("- Install FFmpeg build with x11grab support for X11 recording fallback.");
            }

            return sb.ToString();
        }

        private static string ToStatus(bool value) => value ? "OK" : "Missing";

        private static string GetRecommendedBackend(bool isWayland, bool hasScreenCastPortal, bool ffmpegPipewire, bool gstreamerPipewire, bool hasWfRecorder)
        {
            if (isWayland)
            {
                if (hasScreenCastPortal)
                {
                    if (ffmpegPipewire)
                    {
                        return "Wayland Portal + FFmpeg(pipewire)";
                    }

                    if (gstreamerPipewire)
                    {
                        return "Wayland Portal + GStreamer(pipewiresrc)";
                    }

                    if (hasWfRecorder)
                    {
                        return "wf-recorder";
                    }

                    return "Wayland Portal (missing encoder input integration)";
                }

                if (hasWfRecorder)
                {
                    return "wf-recorder (no ScreenCast portal detected)";
                }

                return "Unsupported Wayland setup";
            }

            return "FFmpeg x11grab";
        }

        private static CommandProbe ProbeCommand(string command)
        {
            var result = RunCommand("which", command, 3000);
            if (result.Started && !result.TimedOut && result.ExitCode == 0)
            {
                string output = !string.IsNullOrWhiteSpace(result.StandardOutput)
                    ? result.StandardOutput.Trim()
                    : result.StandardError.Trim();

                if (string.IsNullOrWhiteSpace(output))
                {
                    output = "<resolved but no output>";
                }

                return new CommandProbe(true, ToSingleLine(output));
            }

            string detail =
                !string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardError.Trim() :
                !string.IsNullOrWhiteSpace(result.StandardOutput) ? result.StandardOutput.Trim() :
                !string.IsNullOrWhiteSpace(result.Error) ? result.Error :
                result.TimedOut ? "which timed out" :
                $"which exited with code {result.ExitCode?.ToString() ?? "unknown"}";

            return new CommandProbe(false, ToSingleLine(detail));
        }

        private static bool CommandExitCodeIsZero(string fileName, string arguments)
        {
            var result = RunCommand(fileName, arguments, 5000);
            return result.Started && !result.TimedOut && result.ExitCode == 0;
        }

        private static bool CommandOutputContains(string fileName, string arguments, string text)
        {
            var result = RunCommand(fileName, arguments, 5000);
            if (!result.Started || result.TimedOut)
            {
                return false;
            }

            return result.StandardOutput.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                   result.StandardError.Contains(text, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetVp9EncoderCapabilitySnapshot()
        {
            var result = RunCommand("gst-inspect-1.0", "vp9enc", 8000);
            if (!result.Started)
            {
                return $"Unable to start gst-inspect-1.0 vp9enc: {result.Error}";
            }

            if (result.TimedOut)
            {
                return "gst-inspect-1.0 vp9enc timed out after 8000ms.";
            }

            if (result.ExitCode != 0)
            {
                string message = !string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardError.Trim() : result.StandardOutput.Trim();
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = $"exit code {result.ExitCode}";
                }

                return $"gst-inspect-1.0 vp9enc failed: {ToSingleLine(message)}";
            }

            var combined = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput
                : result.StandardOutput + Environment.NewLine + result.StandardError;

            var trackedLines = ExtractVp9TrackedLines(combined);
            if (trackedLines.Count == 0)
            {
                return "vp9enc detected, but tracked properties were not found in gst-inspect output.";
            }

            return string.Join(Environment.NewLine, trackedLines.Select(line => "- " + line));
        }

        private static List<string> ExtractVp9TrackedLines(string output)
        {
            string[] trackedKeywords =
            {
                "Long-name",
                "Description",
                "deadline",
                "target-bitrate",
                "cpu-used",
                "end-usage",
                "row-mt",
                "threads"
            };

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var matches = new List<string>();

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                bool containsTrackedKeyword = false;
                foreach (string keyword in trackedKeywords)
                {
                    if (line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        containsTrackedKeyword = true;
                        break;
                    }
                }

                if (!containsTrackedKeyword)
                {
                    continue;
                }

                matches.Add(line);
                if (matches.Count >= 24)
                {
                    break;
                }
            }

            return matches;
        }

        private static CommandRunResult RunCommand(string fileName, string arguments, int timeoutMs)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return new CommandRunResult(false, false, null, string.Empty, string.Empty, "failed to start process");
                }

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                bool exited = process.WaitForExit(timeoutMs);
                if (!exited)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Best effort kill for timed-out diagnostics commands.
                    }

                    return new CommandRunResult(true, true, null, stdout, stderr, $"timed out after {timeoutMs}ms");
                }

                return new CommandRunResult(true, false, process.ExitCode, stdout, stderr, string.Empty);
            }
            catch (Exception ex)
            {
                return new CommandRunResult(false, false, null, string.Empty, string.Empty, $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string ToSingleLine(string value)
        {
            return value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
        }

        private readonly record struct CommandProbe(bool Exists, string Resolution);

        private readonly record struct CommandRunResult(
            bool Started,
            bool TimedOut,
            int? ExitCode,
            string StandardOutput,
            string StandardError,
            string Error);
    }
}
