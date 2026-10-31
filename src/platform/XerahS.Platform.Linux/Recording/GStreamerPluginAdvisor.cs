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

using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Services;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Platform.Linux.Recording;

/// <summary>Linux package families that ship GStreamer plugins under different names.</summary>
public enum LinuxPackageFamily
{
    Unknown,
    Arch,
    Debian,
    Fedora,
    OpenSuse,
    Alpine,
    NixOS,
    Flatpak,
}

/// <summary>What the user should install to record natively with GStreamer.</summary>
internal sealed record GStreamerPluginAdvice(
    IReadOnlyList<string> MissingElements,
    IReadOnlyList<string> MissingPackages,
    string? InstallCommand,
    string HostName,
    string? Note)
{
    public bool HasMissing => MissingElements.Count > 0;
}

/// <summary>What the user is told about a recording's encoder path, if anything.</summary>
internal sealed record RecordingEncoderNotice(string Title, string Text, bool DownloadFFmpeg);

/// <summary>
/// Works out which GStreamer elements are missing for a recording codec and decides what, if
/// anything, the user needs to hear. Nothing here ever asks for sudo: GStreamer only has to
/// capture (pipewiresrc ships with PipeWire) and FFmpeg does the encoding. When no usable FFmpeg
/// encoder exists, XerahS downloads a static FFmpeg into the user's own tools folder.
/// Package names per distribution are still worked out for the debug log (support).
/// </summary>
internal static class GStreamerPluginAdvisor
{
    // element -> upstream plugin set it ships in
    private static readonly Dictionary<string, string> ElementPluginSet = new(StringComparer.Ordinal)
    {
        ["x264enc"] = "ugly",
        ["mp4mux"] = "good",
        ["h264parse"] = "bad",
        ["vp9enc"] = "good",
        ["vp8enc"] = "good",
        ["webmmux"] = "good",
        ["matroskamux"] = "good",
        ["opusenc"] = "base",
        ["pulsesrc"] = "good",
        ["avenc_aac"] = "libav",
    };

    private static int _notified;

    /// <summary>Elements a good native recording for <paramref name="codec"/> needs.</summary>
    internal static IReadOnlyList<string> RequiredElements(VideoCodec codec, bool withAudio)
    {
        var elements = codec switch
        {
            VideoCodec.VP9 or VideoCodec.AV1 => new List<string> { "vp9enc", "webmmux" },
            _ => new List<string> { "x264enc", "mp4mux" },
        };

        if (withAudio)
        {
            elements.Add("pulsesrc");
            elements.Add(codec is VideoCodec.VP9 or VideoCodec.AV1 ? "opusenc" : "avenc_aac");
        }

        return elements;
    }

    internal static LinuxPackageFamily DetectFamily(string? id, string? idLike, bool isFlatpak)
    {
        if (isFlatpak)
        {
            return LinuxPackageFamily.Flatpak;
        }

        string all = ((id ?? string.Empty) + " " + (idLike ?? string.Empty)).ToLowerInvariant();
        bool Has(params string[] tokens) => tokens.Any(t => all.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(t));

        if (Has("nixos")) return LinuxPackageFamily.NixOS;
        if (Has("arch", "manjaro", "endeavouros", "cachyos", "garuda", "artix", "omarchy")) return LinuxPackageFamily.Arch;
        if (Has("debian", "ubuntu", "linuxmint", "pop", "elementary", "zorin", "kali", "raspbian")) return LinuxPackageFamily.Debian;
        if (Has("fedora", "rhel", "centos", "rocky", "almalinux", "nobara", "bazzite")) return LinuxPackageFamily.Fedora;
        if (Has("opensuse", "suse", "opensuse-tumbleweed", "opensuse-leap", "sles")) return LinuxPackageFamily.OpenSuse;
        if (Has("alpine", "postmarketos")) return LinuxPackageFamily.Alpine;
        return LinuxPackageFamily.Unknown;
    }

    internal static GStreamerPluginAdvice Advise(IReadOnlyList<string> missingElements, LinuxPackageFamily family, string? hostName)
    {
        var sets = missingElements
            .Select(e => ElementPluginSet.TryGetValue(e, out string? set) ? set : "bad")
            .Distinct()
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        (IReadOnlyList<string> packages, string? command, string? note) = family switch
        {
            LinuxPackageFamily.Arch => Packages(sets, s => s == "libav" ? "gst-libav" : $"gst-plugins-{s}", p => $"sudo pacman -S --needed {p}"),
            LinuxPackageFamily.Debian => Packages(sets, s => s == "libav" ? "gstreamer1.0-libav" : $"gstreamer1.0-plugins-{s}", p => $"sudo apt install {p}"),
            LinuxPackageFamily.Fedora => Packages(sets,
                s => s switch { "libav" => "gstreamer1-plugin-libav", "ugly" => "gstreamer1-plugins-ugly", "bad" => "gstreamer1-plugins-bad-free", _ => $"gstreamer1-plugins-{s}" },
                p => $"sudo dnf install {p}",
                sets.Contains("ugly") ? "x264enc comes from RPM Fusion (enable the free and nonfree repositories first)." : null),
            LinuxPackageFamily.OpenSuse => Packages(sets, s => s == "libav" ? "gstreamer-plugins-libav" : $"gstreamer-plugins-{s}", p => $"sudo zypper install {p}",
                sets.Contains("ugly") ? "x264enc comes from the Packman repository." : null),
            LinuxPackageFamily.Alpine => Packages(sets, s => s == "libav" ? "gst-libav" : $"gst-plugins-{s}", p => $"sudo apk add {p}"),
            LinuxPackageFamily.NixOS => Packages(sets, s => s == "libav" ? "gst_all_1.gst-libav" : $"gst_all_1.gst-plugins-{s}", _ => null,
                "Add these to environment.systemPackages (or your XerahS package's GStreamer inputs)."),
            LinuxPackageFamily.Flatpak => ((IReadOnlyList<string>)[], null, "Update the XerahS Flatpak and its runtime: flatpak update"),
            _ => Packages(sets, s => s == "libav" ? "gst-libav" : $"gst-plugins-{s}", _ => null,
                "Install your distribution's GStreamer plugin packages for these sets."),
        };

        return new GStreamerPluginAdvice(missingElements, packages, command, string.IsNullOrWhiteSpace(hostName) ? "Linux" : hostName!, note);
    }

    private static (IReadOnlyList<string>, string?, string?) Packages(
        List<string> sets, Func<string, string> name, Func<string, string?> command, string? note = null)
    {
        List<string> packages = sets.Select(name).ToList();
        return (packages, packages.Count == 0 ? null : command(string.Join(' ', packages)), note);
    }

    /// <summary>Missing elements for the codec on this machine, with install advice for the host.</summary>
    internal static GStreamerPluginAdvice Check(VideoCodec codec, bool withAudio, Func<string, bool> hasElement)
    {
        List<string> missing = RequiredElements(codec, withAudio).Where(e => !hasElement(e)).ToList();
        LinuxRuntimeEnvironment environment = LinuxRuntimeEnvironment.Detect();
        LinuxPackageFamily family = DetectFamily(LinuxOsRelease.DistroId, LinuxOsRelease.DistroIdLike, environment.IsFlatpak);
        return Advise(missing, family, LinuxOsRelease.PrettyName);
    }

    /// <summary>
    /// What to tell the user. Null when recording works as intended (FFmpeg encoding, or native
    /// GStreamer encoders), or when nothing the user can do without admin rights would help.
    /// </summary>
    internal static RecordingEncoderNotice? BuildNotice(bool usedFfmpegFallback, bool hasUsableFFmpegEncoder, bool userFFmpegAlreadyDownloaded)
    {
        if (usedFfmpegFallback || hasUsableFFmpegEncoder || userFFmpegAlreadyDownloaded)
        {
            return null;
        }

        return new RecordingEncoderNotice(
            "Getting FFmpeg for screen recording",
            "This recording uses a basic fallback encoder because no FFmpeg with H.264 or VP9 was found. " +
            "XerahS is downloading FFmpeg into its own tools folder (one time, about 150 MB, no administrator " +
            "rights needed). Your next recording will use it.",
            DownloadFFmpeg: true);
    }

    /// <summary>
    /// Logs the missing GStreamer elements (with the host's package names, for support) and, when
    /// recording is degraded, fetches FFmpeg in the background. At most once per app session.
    /// </summary>
    internal static void Notify(GStreamerPluginAdvice advice, bool usedFfmpegFallback, bool hasUsableFFmpegEncoder)
    {
        if (advice.HasMissing)
        {
            string packages = advice.MissingPackages.Count > 0 ? $" (packages: {string.Join(' ', advice.MissingPackages)})" : string.Empty;
            DebugHelper.WriteLine(
                $"[GStreamerPluginAdvisor] GStreamer lacks {string.Join(", ", advice.MissingElements)} on {advice.HostName}{packages}; " +
                (usedFfmpegFallback ? "FFmpeg encodes instead." : "using the fallback path."));
        }

        bool userFFmpegPresent = File.Exists(Path.Combine(PathsManager.ToolsArchitectureFolder, "ffmpeg"));
        RecordingEncoderNotice? notice = BuildNotice(usedFfmpegFallback, hasUsableFFmpegEncoder, userFFmpegPresent);
        if (notice == null || Interlocked.Exchange(ref _notified, 1) == 1)
        {
            return;
        }

        DebugHelper.WriteLine("[GStreamerPluginAdvisor] " + notice.Text);
        ShowToast(notice.Title, notice.Text);

        if (notice.DownloadFFmpeg)
        {
            _ = Task.Run(async () =>
            {
                FFmpegDownloadResult result = await FFmpegDownloader.DownloadLatestToToolsAsync().ConfigureAwait(false);
                DebugHelper.WriteLine(result.Success
                    ? $"[GStreamerPluginAdvisor] FFmpeg downloaded to {result.FFmpegPath}"
                    : $"[GStreamerPluginAdvisor] FFmpeg download failed: {result.ErrorMessage}");
                ShowToast(
                    result.Success ? "FFmpeg is ready" : "FFmpeg download failed",
                    result.Success
                        ? "Your next screen recording will use FFmpeg."
                        : $"{result.ErrorMessage} You can retry from the recording's Video Settings (Download FFmpeg).");
            });
        }
    }

    private static void ShowToast(string title, string text)
    {
        try
        {
            if (PlatformServices.IsToastServiceInitialized)
            {
                PlatformServices.Toast.ShowToast(new ToastConfig
                {
                    Title = title,
                    Text = text,
                    LeftClickAction = ToastClickAction.CloseNotification,
                    Duration = 10f,
                    Size = new SizeI(560, 180),
                    AutoHide = true,
                });
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Could not show the recording notice");
        }
    }
}
