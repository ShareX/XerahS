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

using Newtonsoft.Json;

namespace XerahS.Common
{
    public class GitHubUpdateChecker : UpdateChecker
    {
        public string Owner { get; private set; }
        public string Repo { get; private set; }
        public bool IncludePreRelease { get; set; }
        public bool IsPreRelease { get; protected set; }

        private const string APIURL = "https://api.github.com";

        private string ReleasesURL => $"{APIURL}/repos/{Owner}/{Repo}/releases";
        private string LatestReleaseURL => $"{ReleasesURL}/latest";

        public GitHubUpdateChecker(string owner, string repo)
        {
            Owner = owner;
            Repo = repo;
        }

        public override async Task CheckUpdateAsync()
        {
            try
            {
                string url = IncludePreRelease ? ReleasesURL : LatestReleaseURL;
                DebugHelper.WriteLine($"Checking for updates at: {url}");

                GitHubRelease? latestRelease = await GetLatestRelease(IncludePreRelease);

                if (latestRelease == null)
                {
                    DebugHelper.WriteLine($"No release found for {Owner}/{Repo}");
                    Status = UpdateStatus.UpdateCheckFailed;
                    return;
                }

                DebugHelper.WriteLine($"Found release: {latestRelease.tag_name?.TrimStart('v')} (prerelease: {latestRelease.prerelease})");

                if (UpdateReleaseInfo(latestRelease, IsPortable, IsPortable))
                {
                    RefreshStatus();
                    DebugHelper.WriteLine($"Current: {CurrentVersion?.ToString(3)}, Latest: {LatestVersion?.ToString(3)}, Status: {Status}");
                    return;
                }
                else
                {
                    DebugHelper.WriteLine($"Failed to update release info. Tag: {latestRelease.tag_name}, Assets: {latestRelease.assets?.Length ?? 0}");
                }
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e, $"GitHub update check failed for {Owner}/{Repo}.");
            }

            Status = UpdateStatus.UpdateCheckFailed;
        }

        public virtual async Task<string?> GetLatestDownloadURL(bool isBrowserDownloadURL)
        {
            try
            {
                GitHubRelease? latestRelease = await GetLatestRelease(IncludePreRelease);

                if (UpdateReleaseInfo(latestRelease, IsPortable, isBrowserDownloadURL))
                {
                    return DownloadURL;
                }
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e);
            }

            return null;
        }

        protected async Task<List<GitHubRelease>?> GetReleases()
        {
            List<GitHubRelease>? releases = null;

            string response = await WebHelpers.DownloadStringAsync(ReleasesURL);

            if (!string.IsNullOrEmpty(response))
            {
                releases = JsonConvert.DeserializeObject<List<GitHubRelease>>(response);

                if (releases != null && releases.Count > 0)
                {
                    releases.Sort((x, y) => y.published_at.CompareTo(x.published_at));
                }
            }

            return releases;
        }

        protected async Task<GitHubRelease?> GetLatestRelease()
        {
            GitHubRelease? latestRelease = null;

            try
            {
                string response = await WebHelpers.DownloadStringAsync(LatestReleaseURL);

                if (!string.IsNullOrEmpty(response))
                {
                    latestRelease = JsonConvert.DeserializeObject<GitHubRelease>(response);
                }
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                DebugHelper.WriteLine($"No releases found at {LatestReleaseURL}. The repository may not have any releases yet.");
                throw;
            }

            return latestRelease;
        }

        protected async Task<GitHubRelease?> GetLatestRelease(bool includePreRelease)
        {
            GitHubRelease? latestRelease = null;

            if (includePreRelease)
            {
                List<GitHubRelease>? releases = await GetReleases();

                if (releases != null && releases.Count > 0)
                {
                    latestRelease = releases[0];
                }
            }
            else
            {
                latestRelease = await GetLatestRelease();
            }

            return latestRelease;
        }

        protected virtual bool UpdateReleaseInfo(GitHubRelease? release, bool isPortable, bool isBrowserDownloadURL)
        {
            if (release != null && !string.IsNullOrEmpty(release.tag_name) && release.tag_name.Length > 1 && release.tag_name[0] == 'v')
            {
                LatestVersion = new Version(release.tag_name.Substring(1));

                if (release.assets != null && release.assets.Length > 0)
                {
                    string endsWith = "";
                    string arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "arm64" : "x64";
                    
                    if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                    {
                         endsWith = isPortable ? $"-win-{arch}.zip" : $"-win-{arch}.exe";
                    }
                    else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                    {
                         endsWith = isPortable ? $"-osx-{arch}.zip" : $"-osx-{arch}.dmg";
                    }
                    else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                    {
                         // Linux naming: XerahS-0.6.2-linux-arm64.tar.gz or .deb
                         endsWith = isPortable ? $"-linux-{arch}.tar.gz" : $"-linux-{arch}.deb";
                    }

                    if (!string.IsNullOrEmpty(endsWith))
                    {
                        foreach (GitHubAsset? asset in release.assets)
                        {
                            if (asset != null && !string.IsNullOrEmpty(asset.name) && asset.name.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase))
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

                    // Fallback to legacy naming for backward compatibility if new naming scheme not found
                    if (isPortable)
                    {
                        endsWith = "portable.zip";
                    }
                    else
                    {
                        endsWith = ".exe";
                    }

                    foreach (GitHubAsset? asset in release.assets)
                    {
                        if (asset != null && !string.IsNullOrEmpty(asset.name) && asset.name.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase))
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

            return false;
        }

        protected class GitHubRelease
        {
            public string? url { get; set; }
            public string? assets_url { get; set; }
            public string? upload_url { get; set; }
            public string? html_url { get; set; }
            public long id { get; set; }
            public string? node_id { get; set; }
            public string? tag_name { get; set; }
            public string? target_commitish { get; set; }
            public string? name { get; set; }
            public bool draft { get; set; }
            public bool prerelease { get; set; }
            public DateTime created_at { get; set; }
            public DateTime published_at { get; set; }
            public GitHubAsset[]? assets { get; set; }
            public string? tarball_url { get; set; }
            public string? zipball_url { get; set; }
            public string? body { get; set; }
        }

        protected class GitHubAsset
        {
            public string? url { get; set; }
            public long id { get; set; }
            public string? node_id { get; set; }
            public string? name { get; set; }
            public string? label { get; set; }
            public string? content_type { get; set; }
            public string? state { get; set; }
            public long size { get; set; }
            public long download_count { get; set; }
            public DateTime created_at { get; set; }
            public DateTime updated_at { get; set; }
            public string? browser_download_url { get; set; }
        }
    }
}
