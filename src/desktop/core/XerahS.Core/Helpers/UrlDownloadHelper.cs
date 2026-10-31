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

using System.Net;
using System.Net.Http.Headers;
using XerahS.Common;

namespace XerahS.Core;

/// <summary>A file on GitHub, named by a github.com blob/raw link or a raw.githubusercontent.com link.</summary>
public sealed record GitHubFileReference(string Owner, string Repository, string Ref, string Path);

public sealed record UrlDownloadResult(bool Success, string? FilePath, string? ErrorMessage, bool GitHubTokenRequired = false)
{
    public static UrlDownloadResult Downloaded(string filePath) => new(true, filePath, null);
    public static UrlDownloadResult Failed(string message, bool gitHubTokenRequired = false) => new(false, null, message, gitHubTokenRequired);
}

/// <summary>
/// Downloads a URL to a local file so Upload Content can upload it like any other file.
/// GitHub file pages are fetched as the raw file, through the GitHub API when a token is given
/// so files in private repositories work.
/// </summary>
public static class UrlDownloadHelper
{
    public const long MaxDownloadBytes = 512L * 1024 * 1024;

    private static readonly Dictionary<string, string> ContentTypeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text/html"] = ".html",
        ["text/plain"] = ".txt",
        ["text/markdown"] = ".md",
        ["text/css"] = ".css",
        ["text/csv"] = ".csv",
        ["application/json"] = ".json",
        ["application/pdf"] = ".pdf",
        ["application/zip"] = ".zip",
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp",
        ["image/svg+xml"] = ".svg",
        ["video/mp4"] = ".mp4",
        ["video/webm"] = ".webm"
    };

    public static bool TryParseDownloadUrl(string? input, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(input) ||
            !Uri.TryCreate(input.Trim(), UriKind.Absolute, out Uri? parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    public static GitHubFileReference? ParseGitHubFile(Uri uri)
    {
        string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString).ToArray();

        if (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase))
        {
            // /{owner}/{repo}/blob|raw/{ref}/{path...}
            if (segments.Length >= 5 && (segments[2] == "blob" || segments[2] == "raw"))
            {
                return new GitHubFileReference(segments[0], segments[1], segments[3], string.Join('/', segments.Skip(4)));
            }

            return null;
        }

        if (uri.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase) && segments.Length >= 4)
        {
            // /{owner}/{repo}/{ref}/{path...}
            return new GitHubFileReference(segments[0], segments[1], segments[2], string.Join('/', segments.Skip(3)));
        }

        return null;
    }

    public static Uri GetGitHubRawUri(GitHubFileReference file) =>
        new($"https://raw.githubusercontent.com/{Escape(file.Owner)}/{Escape(file.Repository)}/{Escape(file.Ref)}/{EscapePath(file.Path)}");

    public static Uri GetGitHubContentsApiUri(GitHubFileReference file) =>
        new($"https://api.github.com/repos/{Escape(file.Owner)}/{Escape(file.Repository)}/contents/{EscapePath(file.Path)}?ref={Escape(file.Ref)}");

    /// <summary>
    /// Picks a safe local file name: Content-Disposition first, then the last URL segment,
    /// adding an extension from the content type when the name has none.
    /// </summary>
    public static string GetFileName(Uri uri, string? contentDispositionFileName, string? mediaType)
    {
        string? name = contentDispositionFileName?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(name))
        {
            string? last = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            name = last == null ? null : Uri.UnescapeDataString(last);
        }

        name = Path.GetFileName(name ?? string.Empty);
        char[] invalid = Path.GetInvalidFileNameChars();
        name = new string(name.Where(c => !invalid.Contains(c) && !char.IsControl(c)).ToArray()).Trim().TrimStart('.');
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "download";
        }

        if (string.IsNullOrEmpty(Path.GetExtension(name)) && mediaType != null &&
            ContentTypeExtensions.TryGetValue(mediaType, out string? extension))
        {
            name += extension;
        }

        return name.Length > 200 ? name[..200] : name;
    }

    public static async Task<UrlDownloadResult> DownloadAsync(string url, string targetFolder, string? gitHubToken, CancellationToken cancellationToken = default)
    {
        if (!TryParseDownloadUrl(url, out Uri uri))
        {
            return UrlDownloadResult.Failed("Enter an http or https URL.");
        }

        GitHubFileReference? gitHubFile = ParseGitHubFile(uri);
        bool useGitHubApi = gitHubFile != null && !string.IsNullOrWhiteSpace(gitHubToken);
        Uri requestUri = gitHubFile == null ? uri : useGitHubApi ? GetGitHubContentsApiUri(gitHubFile) : GetGitHubRawUri(gitHubFile);
        Uri nameUri = gitHubFile == null ? uri : GetGitHubRawUri(gitHubFile);

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        if (useGitHubApi)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gitHubToken!.Trim());
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.raw+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        }

        try
        {
            HttpClient client = HttpClientFactory.Create(allowAutoRedirect: true, infiniteTimeout: true);
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                if (gitHubFile != null && response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    // GitHub answers 404 for private files it will not show this caller.
                    return useGitHubApi
                        ? UrlDownloadResult.Failed($"GitHub refused the token for {gitHubFile.Owner}/{gitHubFile.Repository} ({(int)response.StatusCode}).", gitHubTokenRequired: true)
                        : UrlDownloadResult.Failed("This GitHub file is private or does not exist.", gitHubTokenRequired: true);
                }

                return UrlDownloadResult.Failed($"Download failed: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            long? length = response.Content.Headers.ContentLength;
            if (length > MaxDownloadBytes)
            {
                return UrlDownloadResult.Failed($"The file is larger than {MaxDownloadBytes / (1024 * 1024)} MB.");
            }

            ContentDispositionHeaderValue? disposition = response.Content.Headers.ContentDisposition;
            string fileName = GetFileName(nameUri, disposition?.FileNameStar ?? disposition?.FileName,
                gitHubFile != null ? null : response.Content.Headers.ContentType?.MediaType);

            string folder = Path.Combine(targetFolder, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string filePath = Path.Combine(folder, fileName);

            try
            {
                await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (var target = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    byte[] buffer = new byte[81920];
                    long total = 0;
                    int read;
                    while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        total += read;
                        if (total > MaxDownloadBytes)
                        {
                            throw new InvalidDataException($"The file is larger than {MaxDownloadBytes / (1024 * 1024)} MB.");
                        }

                        await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    }
                }

                return UrlDownloadResult.Downloaded(filePath);
            }
            catch
            {
                TryDeleteFolder(folder);
                throw;
            }
        }
        catch (InvalidDataException ex)
        {
            return UrlDownloadResult.Failed(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return UrlDownloadResult.Failed($"Download failed: {ex.Message}");
        }
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string EscapePath(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private static void TryDeleteFolder(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "UrlDownloadHelper: could not remove partial download.");
        }
    }
}
