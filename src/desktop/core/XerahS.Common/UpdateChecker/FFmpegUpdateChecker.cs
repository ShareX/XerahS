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

using System.Runtime.InteropServices;

namespace XerahS.Common
{
    /// <summary>Selects BtbN's static GPL release-branch builds, never master or shared builds.</summary>
    public class FFmpegUpdateChecker : GitHubUpdateChecker
    {
        // Follow the selected stable release branch, independently of master auto-builds.
        public const string ReleaseBranch = "9.0";
        public FFmpegArchitecture Architecture { get; }
        public string? ChecksumsURL { get; private set; }

        public FFmpegUpdateChecker() : this(FFmpegDownloader.DefaultOwner, FFmpegDownloader.DefaultRepo) { }
        public FFmpegUpdateChecker(string owner, string repo) : this(owner, repo, ResolveArchitecture()) { }
        public FFmpegUpdateChecker(string owner, string repo, FFmpegArchitecture architecture) : base(owner, repo)
        {
            Architecture = architecture;
        }

        protected override bool UpdateReleaseInfo(GitHubRelease? release, bool isPortable, bool isBrowserDownloadURL)
        {
            FileName = string.Empty;
            DownloadURL = ChecksumsURL = null;
            LatestVersion = null;
            IsPreRelease = false;
            string? expectedName = GetExpectedAssetName();
            if (expectedName == null || release == null || release.draft || release.prerelease || release.assets == null)
                return false;

            // BtbN's floating tag is "latest", with dated releases named "autobuild-...".
            if (release.tag_name != "latest" && release.tag_name?.StartsWith("autobuild-", StringComparison.Ordinal) != true)
                return false;

            var archive = release.assets.FirstOrDefault(a => a.name == expectedName);
            var checksums = release.assets.FirstOrDefault(a => a.name == FFmpegReleaseVerifier.ChecksumsFileName);
            string? url = isBrowserDownloadURL ? archive?.browser_download_url : archive?.url;
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(checksums?.browser_download_url))
                return false;

            FileName = expectedName;
            DownloadURL = url;
            ChecksumsURL = checksums.browser_download_url;
            LatestVersion = Version.Parse(ReleaseBranch + ".0");
            return true;
        }

        public static FFmpegArchitecture ResolveArchitecture() => ResolveArchitecture(
            OperatingSystem.IsWindows() ? OSPlatform.Windows : OperatingSystem.IsLinux() ? OSPlatform.Linux : OSPlatform.OSX,
            RuntimeInformation.OSArchitecture);

        internal static FFmpegArchitecture ResolveArchitecture(OSPlatform platform, System.Runtime.InteropServices.Architecture architecture)
        {
            if (platform == OSPlatform.Windows)
                return architecture switch
                {
                    System.Runtime.InteropServices.Architecture.X64 => FFmpegArchitecture.win64,
                    System.Runtime.InteropServices.Architecture.Arm64 => FFmpegArchitecture.winArm64,
                    System.Runtime.InteropServices.Architecture.X86 => FFmpegArchitecture.win32,
                    _ => FFmpegArchitecture.unsupported
                };
            if (platform == OSPlatform.Linux)
                return architecture switch
                {
                    System.Runtime.InteropServices.Architecture.X64 => FFmpegArchitecture.linux64,
                    System.Runtime.InteropServices.Architecture.Arm64 => FFmpegArchitecture.linuxArm64,
                    _ => FFmpegArchitecture.unsupported
                };
            return platform == OSPlatform.OSX ? FFmpegArchitecture.macos64 : FFmpegArchitecture.unsupported;
        }

        public string? GetExpectedAssetSuffix() => Architecture switch
        {
            FFmpegArchitecture.win64 => $"win64-gpl-{ReleaseBranch}.zip",
            FFmpegArchitecture.winArm64 => $"winarm64-gpl-{ReleaseBranch}.zip",
            FFmpegArchitecture.linux64 => $"linux64-gpl-{ReleaseBranch}.tar.xz",
            FFmpegArchitecture.linuxArm64 => $"linuxarm64-gpl-{ReleaseBranch}.tar.xz",
            _ => null
        };

        public string? GetExpectedAssetName() => GetExpectedAssetSuffix() is { } suffix
            ? $"ffmpeg-n{ReleaseBranch}-latest-{suffix}" : null;

        public string GetExpectedDownloadUrl() => GetExpectedAssetName() is { } name
            ? $"https://github.com/{Owner}/{Repo}/releases/download/latest/{name}"
            : $"https://github.com/{Owner}/{Repo}/releases";
    }
}
