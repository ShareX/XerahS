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
using System.Runtime.InteropServices;
using System.Text;
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
            var inv = CultureInfo.InvariantCulture;

            string sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "unknown";
            string currentDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "unknown";
            string desktopSession = Environment.GetEnvironmentVariable("DESKTOP_SESSION") ?? "unknown";
            bool isWayland = sessionType.Equals("wayland", StringComparison.OrdinalIgnoreCase);

            bool hasScreenCastPortal = PortalInterfaceChecker.HasInterface("org.freedesktop.portal.ScreenCast");
            bool hasScreenshotPortal = PortalInterfaceChecker.HasInterface("org.freedesktop.portal.Screenshot");
            bool hasGlobalShortcutsPortal = PortalInterfaceChecker.HasInterface("org.freedesktop.portal.GlobalShortcuts");
            bool hasInputCapturePortal = PortalInterfaceChecker.HasInterface("org.freedesktop.portal.InputCapture");

            bool hasFfmpeg = CommandExists("ffmpeg");
            bool hasWfRecorder = CommandExists("wf-recorder");
            bool hasSlurp = CommandExists("slurp");
            bool hasGrim = CommandExists("grim");
            bool hasGstLaunch = CommandExists("gst-launch-1.0");
            bool hasGstInspect = CommandExists("gst-inspect-1.0");
            bool hasBusctl = CommandExists("busctl");
            bool hasPwCli = CommandExists("pw-cli");

            bool ffmpegPipewire = hasFfmpeg && CommandOutputContains("ffmpeg", "-hide_banner -devices", "pipewire");
            bool ffmpegX11Grab = hasFfmpeg && CommandOutputContains("ffmpeg", "-hide_banner -devices", "x11grab");
            bool gstreamerPipewire = hasGstInspect && CommandExitCodeIsZero("gst-inspect-1.0", "pipewiresrc");
            bool pipewireRunning = CommandExitCodeIsZero("pgrep", "-x pipewire");

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

            sb.AppendLine("[SESSION]");
            sb.AppendLine($"XDG_SESSION_TYPE: {sessionType}");
            sb.AppendLine($"XDG_CURRENT_DESKTOP: {currentDesktop}");
            sb.AppendLine($"DESKTOP_SESSION: {desktopSession}");
            sb.AppendLine($"Wayland: {isWayland}");
            sb.AppendLine();

            sb.AppendLine("[PORTAL INTERFACES]");
            sb.AppendLine($"ScreenCast: {ToStatus(hasScreenCastPortal)}");
            sb.AppendLine($"Screenshot: {ToStatus(hasScreenshotPortal)}");
            sb.AppendLine($"GlobalShortcuts: {ToStatus(hasGlobalShortcutsPortal)}");
            sb.AppendLine($"InputCapture: {ToStatus(hasInputCapturePortal)}");
            sb.AppendLine();

            sb.AppendLine("[TOOLS]");
            sb.AppendLine($"ffmpeg: {ToStatus(hasFfmpeg)}");
            sb.AppendLine($"wf-recorder: {ToStatus(hasWfRecorder)}");
            sb.AppendLine($"slurp: {ToStatus(hasSlurp)}");
            sb.AppendLine($"grim: {ToStatus(hasGrim)}");
            sb.AppendLine($"gst-launch-1.0: {ToStatus(hasGstLaunch)}");
            sb.AppendLine($"gst-inspect-1.0: {ToStatus(hasGstInspect)}");
            sb.AppendLine($"busctl: {ToStatus(hasBusctl)}");
            sb.AppendLine($"pw-cli: {ToStatus(hasPwCli)}");
            sb.AppendLine($"pipewire process running: {ToStatus(pipewireRunning)}");
            sb.AppendLine();

            sb.AppendLine("[CAPABILITIES]");
            sb.AppendLine($"FFmpeg pipewire input: {ToStatus(ffmpegPipewire)}");
            sb.AppendLine($"FFmpeg x11grab input: {ToStatus(ffmpegX11Grab)}");
            sb.AppendLine($"GStreamer pipewiresrc: {ToStatus(gstreamerPipewire)}");
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
            else
            {
                if (!ffmpegX11Grab)
                {
                    sb.AppendLine("- Install FFmpeg build with x11grab support for X11 recording fallback.");
                }
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

        private static bool CommandExists(string command)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return false;
                }

                process.WaitForExit(2000);
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool CommandExitCodeIsZero(string fileName, string arguments)
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
                    return false;
                }

                process.WaitForExit(5000);
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool CommandOutputContains(string fileName, string arguments, string text)
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
                    return false;
                }

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit(5000);

                return stdout.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                       stderr.Contains(text, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
