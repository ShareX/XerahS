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
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;
using SysHttpClient = System.Net.Http.HttpClient;
using SysHttpMethod = System.Net.Http.HttpMethod;
using SysHttpRequestMessage = System.Net.Http.HttpRequestMessage;

namespace ShareX.Imgur.Plugin;

/// <summary>
/// Imgur image uploader provider (supports Image category only).
/// Also implements <see cref="IUploaderExplorer"/> — albums are treated as folders,
/// images within albums as files.
/// </summary>
public class ImgurProvider : UploaderProviderBase, IUploaderExplorer
{
    public override string ProviderId => "imgur";
    public override string Name => "Imgur";
    public override string Description => "Upload images to Imgur - free image hosting service";
    public override Version Version => new Version(1, 0, 0);
    public override UploaderCategory[] SupportedCategories => new[] { UploaderCategory.Image };
    public override Type ConfigModelType => typeof(ImgurConfigModel);

    public ImgurProvider()
    {
    }

    public override Uploader CreateInstance(string settingsJson)
    {
        var config = JsonConvert.DeserializeObject<ImgurConfigModel>(settingsJson);
        if (config == null)
        {
            throw new InvalidOperationException("Failed to deserialize Imgur settings");
        }

        if (Secrets == null)
        {
            throw new InvalidOperationException("Secret store not available for Imgur");
        }

        if (string.IsNullOrWhiteSpace(config.SecretKey))
        {
            throw new InvalidOperationException("Imgur secret key is missing");
        }

        string clientSecret = Secrets.GetSecret(ProviderId, config.SecretKey, "clientSecret")
            ?? "98871f37e179e496a0149e9c8558487779d424ft";

        var authInfo = new OAuth2Info(config.ClientId, clientSecret);
        string? tokenJson = Secrets.GetSecret(ProviderId, config.SecretKey, "oauthToken");
        if (!string.IsNullOrWhiteSpace(tokenJson))
        {
            var token = JsonConvert.DeserializeObject<OAuth2Token>(tokenJson);
            if (token != null)
            {
                authInfo.Token = token;
            }
        }

        return new ImgurUploader(config, authInfo);
    }

    public override Dictionary<UploaderCategory, string[]> GetSupportedFileTypes()
    {
        return new Dictionary<UploaderCategory, string[]>
        {
            {
                UploaderCategory.Image,
                new[] { "png", "jpg", "jpeg", "gif", "apng", "bmp", "tiff", "webp", "mp4", "avi", "mov" }
            }
        };
    }

    public override object? CreateConfigView()
    {
        return new Views.ImgurConfigView();
    }

    public override IUploaderConfigViewModel? CreateConfigViewModel()
    {
        return new ViewModels.ImgurConfigViewModel();
    }

    // ─── IUploaderExplorer ───────────────────────────────────────────────────

    private static readonly SysHttpClient _explorerHttpClient = new();

    /// <inheritdoc/>
    public bool SupportsFolders => true; // Albums are folders

    /// <inheritdoc/>
    public async Task<ExplorerPage> ListAsync(ExplorerQuery query, CancellationToken cancellation = default)
    {
        var config = DeserializeImgurConfig(query.SettingsJson);
        var authInfo = BuildAuthInfo(config);
        if (authInfo?.Token == null)
            return new ExplorerPage();

        // Imgur uses a two-level hierarchy: albums (folders) at root, images inside albums.
        string folderPath = (query.FolderPath ?? "").Trim('/');

        if (string.IsNullOrEmpty(folderPath))
        {
            // Root: list albums as folders
            return await ListAlbumsAsync(authInfo, query, cancellation);
        }
        else
        {
            // Inside an album: list images
            return await ListAlbumImagesAsync(folderPath, authInfo, query, cancellation);
        }
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetThumbnailAsync(MediaItem item, int maxWidthPx = 180, CancellationToken cancellation = default)
    {
        string? thumbUrl = item.ThumbnailUrl ?? item.Url;
        if (string.IsNullOrEmpty(thumbUrl)) return null;

        try
        {
            return await _explorerHttpClient.GetByteArrayAsync(thumbUrl, cancellation);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<Stream?> GetContentAsync(MediaItem item, CancellationToken cancellation = default)
    {
        string? url = item.Url;
        if (string.IsNullOrEmpty(url)) return null;

        try
        {
            var response = await _explorerHttpClient.GetAsync(url, cancellation);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStreamAsync(cancellation) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(MediaItem item, CancellationToken cancellation = default)
    {
        if (!item.Metadata.TryGetValue("deleteHash", out string? deleteHash)) return false;
        if (!item.Metadata.TryGetValue("settingsJson", out string? settingsJson)) return false;

        var config = DeserializeImgurConfig(settingsJson);
        var authInfo = BuildAuthInfo(config);
        if (authInfo?.Token == null) return false;

        string deleteUrl = $"https://api.imgur.com/3/image/{deleteHash}";
        using var request = new SysHttpRequestMessage(SysHttpMethod.Delete, deleteUrl);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + authInfo.Token.access_token);
        try
        {
            using var response = await _explorerHttpClient.SendAsync(request, cancellation);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CreateFolderAsync(string parentPath, string folderName, CancellationToken cancellation = default)
    {
        // Creating Imgur albums requires separate OAuth flow — not supported in initial implementation
        await Task.CompletedTask;
        return false;
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    private async Task<ExplorerPage> ListAlbumsAsync(OAuth2Info authInfo, ExplorerQuery query, CancellationToken cancellation)
    {
        // Imgur uses integer page numbers; store page number as continuation token
        int page = int.TryParse(query.ContinuationToken, out int p) ? p : 0;
        int perPage = query.PageSize;

        string url = $"https://api.imgur.com/3/account/me/albums?page={page}&perPage={perPage}";
        using var request = new SysHttpRequestMessage(SysHttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + authInfo.Token!.access_token);

        try
        {
            using var response = await _explorerHttpClient.SendAsync(request, cancellation);
            if (!response.IsSuccessStatusCode) return new ExplorerPage();

            string json = await response.Content.ReadAsStringAsync(cancellation);
            var wrapper = JsonConvert.DeserializeObject<ImgurApiResponse>(json);
            if (wrapper?.data == null || !wrapper.success) return new ExplorerPage();

            var albums = JsonConvert.DeserializeObject<List<ImgurAlbumResponse>>(wrapper.data.ToString() ?? "[]")
                         ?? new List<ImgurAlbumResponse>();

            var items = albums.Select(a => new MediaItem
            {
                Id = a.id ?? "",
                Name = string.IsNullOrEmpty(a.title) ? (a.id ?? "Album") : a.title!,
                Path = a.id ?? "",
                IsFolder = true,
                Metadata = new Dictionary<string, string>
                {
                    ["albumId"] = a.id ?? "",
                    ["settingsJson"] = query.SettingsJson ?? ""
                }
            }).ToList();

            // null continuation = last page (when fewer items than requested)
            string? nextToken = albums.Count < perPage ? null : (page + 1).ToString();
            return new ExplorerPage { Items = items, ContinuationToken = nextToken };
        }
        catch
        {
            return new ExplorerPage();
        }
    }

    private async Task<ExplorerPage> ListAlbumImagesAsync(string albumId, OAuth2Info authInfo, ExplorerQuery query, CancellationToken cancellation)
    {
        string url = $"https://api.imgur.com/3/album/{albumId}/images";
        using var request = new SysHttpRequestMessage(SysHttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + authInfo.Token!.access_token);

        try
        {
            using var response = await _explorerHttpClient.SendAsync(request, cancellation);
            if (!response.IsSuccessStatusCode) return new ExplorerPage();

            string json = await response.Content.ReadAsStringAsync(cancellation);
            var wrapper = JsonConvert.DeserializeObject<ImgurApiResponse>(json);
            if (wrapper?.data == null || !wrapper.success) return new ExplorerPage();

            var images = JsonConvert.DeserializeObject<List<ImgurImageResponse>>(wrapper.data.ToString() ?? "[]")
                         ?? new List<ImgurImageResponse>();

            var items = images.Select(img => new MediaItem
            {
                Id = img.id ?? "",
                Name = string.IsNullOrEmpty(img.name) ? (img.id + ".jpg") : img.name!,
                Path = albumId + "/" + img.id,
                SizeBytes = img.size,
                MimeType = img.type,
                ModifiedAt = img.datetime > 0 ? DateTimeOffset.FromUnixTimeSeconds(img.datetime).DateTime : null,
                Url = img.link,
                ThumbnailUrl = string.IsNullOrEmpty(img.id) ? null : $"https://i.imgur.com/{img.id}m.jpg",
                Metadata = new Dictionary<string, string>
                {
                    ["deleteHash"] = img.deletehash ?? "",
                    ["settingsJson"] = query.SettingsJson ?? ""
                }
            }).ToList();

            // Album images API returns all images in one call — no pagination needed
            return new ExplorerPage { Items = items, TotalCount = items.Count };
        }
        catch
        {
            return new ExplorerPage();
        }
    }

    private ImgurConfigModel DeserializeImgurConfig(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson)) return new ImgurConfigModel();
        return JsonConvert.DeserializeObject<ImgurConfigModel>(settingsJson) ?? new ImgurConfigModel();
    }

    private OAuth2Info? BuildAuthInfo(ImgurConfigModel config)
    {
        if (Secrets == null || string.IsNullOrWhiteSpace(config.SecretKey)) return null;

        string clientSecret = Secrets.GetSecret(ProviderId, config.SecretKey, "clientSecret")
            ?? "98871f37e179e496a0149e9c8558487779d424ft";
        var authInfo = new OAuth2Info(config.ClientId, clientSecret);

        string? tokenJson = Secrets.GetSecret(ProviderId, config.SecretKey, "oauthToken");
        if (!string.IsNullOrWhiteSpace(tokenJson))
        {
            var token = JsonConvert.DeserializeObject<OAuth2Token>(tokenJson);
            if (token != null) authInfo.Token = token;
        }

        return authInfo;
    }

    // Minimal response models for the explorer (used only within this file)
    private class ImgurApiResponse
    {
        public object? data { get; set; }
        public bool success { get; set; }
        public int status { get; set; }
    }

    private class ImgurAlbumResponse
    {
        public string? id { get; set; }
        public string? title { get; set; }
        public int images_count { get; set; }
    }

    private class ImgurImageResponse
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? link { get; set; }
        public string? type { get; set; }
        public long size { get; set; }
        public long datetime { get; set; }
        public string? deletehash { get; set; }
    }
}
