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
    public class FFmpegUpdateChecker : GitHubUpdateChecker
    {
        public FFmpegArchitecture Architecture { get; private set; }

        public FFmpegUpdateChecker(string owner, string repo) : base(owner, repo)
        {
            Architecture = ResolveArchitecture();
        }

        public FFmpegUpdateChecker(string owner, string repo, FFmpegArchitecture architecture) : base(owner, repo)
        {
            Architecture = architecture;
        }

        protected override bool UpdateReleaseInfo(GitHubRelease? release, bool isPortable, bool isBrowserDownloadURL)
        {
            if (release != null)
            {
                string? tagName = release.tag_name;

                if (!string.IsNullOrEmpty(tagName))
                {
                    string actualTagName = tagName!;

                    if (actualTagName.Length > 1 && actualTagName.StartsWith("v", StringComparison.Ordinal))
                    {
                        if (Version.TryParse(actualTagName.Substring(1), out Version? version))
                        {
                            LatestVersion = version;
                        }

                        if (release.assets != null && release.assets.Length > 0)
                        {
                            string endsWith = GetExpectedAssetSuffix();

                            foreach (GitHubAsset asset in release.assets)
                            {
                                if (asset != null && !string.IsNullOrEmpty(asset.name) &&
                                    asset.name.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase))
                                {
                                    FileName = asset.name;

                                    if (isBrowserDownloadURL)
                                    {
                                        DownloadURL = asset.browser_download_url;
                                    }
                                    else
                                    {
                                        DownloadURL = asset.url;
                                    }

                                    IsPreRelease = release.prerelease;

                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        public static FFmpegArchitecture ResolveArchitecture()
        {
            if (OperatingSystem.IsWindows())
            {
                return RuntimeInformation.OSArchitecture switch
                {
                    System.Runtime.InteropServices.Architecture.Arm64 => FFmpegArchitecture.winArm64,
                    System.Runtime.InteropServices.Architecture.X64 => FFmpegArchitecture.win64,
                    _ => FFmpegArchitecture.win32
                };
            }

            if (OperatingSystem.IsMacOS())
            {
                return FFmpegArchitecture.macos64;
            }

            if (OperatingSystem.IsLinux())
            {
                return FFmpegArchitecture.linux64;
            }

            return FFmpegArchitecture.win64;
        }

        public string GetExpectedAssetSuffix()
        {
            return Architecture switch
            {
                FFmpegArchitecture.win64 => "win-x64.zip",
                FFmpegArchitecture.win32 => "win-x86.zip",
                FFmpegArchitecture.winArm64 => "win-arm64.zip",
                FFmpegArchitecture.macos64 => "macos64.zip",
                FFmpegArchitecture.linux64 => "linux64.zip",
                _ => "win-x64.zip"
            };
        }
    }
}
