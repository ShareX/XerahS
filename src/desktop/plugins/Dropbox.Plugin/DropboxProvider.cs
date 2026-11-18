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
using Newtonsoft.Json.Linq;
using XerahS.Common;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using SysHttpClient = System.Net.Http.HttpClient;
using SysHttpMethod = System.Net.Http.HttpMethod;
using SysHttpRequestMessage = System.Net.Http.HttpRequestMessage;

namespace ShareX.Dropbox.Plugin;

/// <summary>
/// Dropbox file uploader provider with media explorer support.
/// </summary>
public class DropboxProvider : UploaderProviderBase, IUploaderExplorer, IInstanceSecretBackupProvider
{
    private const string ApiVersion = "2";
    private const string UrlApiBase = "https://api.dropboxapi.com";
    private const string UrlApi = UrlApiBase + "/" + ApiVersion;
    private const string UrlContent = "https://content.dropboxapi.com/" + ApiVersion;

    private const string UrlListFolder = UrlApi + "/files/list_folder";
    private const string UrlListFolderContinue = UrlApi + "/files/list_folder/continue";
    private const string UrlDelete = UrlApi + "/files/delete_v2";
    private const string UrlCreateFolder = UrlApi + "/files/create_folder_v2";
    private const string UrlMove = UrlApi + "/files/move_v2";
    private const string UrlGetTemporaryLink = UrlApi + "/files/get_temporary_link";
    private const string UrlDownload = UrlContent + "/files/download";
    private const string UrlGetThumbnail = UrlContent + "/files/get_thumbnail";

    private static SysHttpClient ExplorerHttpClient => HttpClientFactory.Create();
    private readonly Lock _latestSettingsLock = new();
    private string? _latestSettingsJson;

    public override string ProviderId => "dropbox";
    public override string Name => "Dropbox";
    public override string Description => "Upload files to Dropbox cloud storage";
    public override Version Version => new(1, 0, 0);
    public override UploaderCategory[] SupportedCategories => new[] { UploaderCategory.Image, UploaderCategory.Text, UploaderCategory.File };
    public override Type ConfigModelType => typeof(DropboxConfigModel);

    public IReadOnlyList<InstanceSecretReference> GetSecretReferences(string settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return Array.Empty<InstanceSecretReference>();
        }

        string? secretKey;
        try
        {
            secretKey = JObject.Parse(settingsJson).Value<string>(nameof(DropboxConfigModel.SecretKey));
        }
        catch (JsonException)
        {
            return Array.Empty<InstanceSecretReference>();
        }

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return Array.Empty<InstanceSecretReference>();
        }

        return
        [
            new(ProviderId, secretKey, "clientId"),
            new(ProviderId, secretKey, "clientSecret"),
            new(ProviderId, secretKey, "oauthToken")
        ];
    }

    public override Uploader CreateInstance(string settingsJson)
    {
        DropboxConfigModel config = DeserializeConfig(settingsJson);
        return BuildUploader(config, requireToken: true);
    }

    public override Dictionary<UploaderCategory, string[]> GetSupportedFileTypes()
    {
        string[] allTypes =
        {
            "png", "jpg", "jpeg", "gif", "bmp", "tiff", "webp", "svg",
            "mp4", "avi", "mov", "mkv", "flv", "wmv", "webm",
            "txt", "log", "json", "xml", "md", "html", "css", "js",
            "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx",
            "zip", "rar", "7z", "tar", "gz",
            "exe", "dll", "so", "dmg", "apk", "ipa"
        };

        return new Dictionary<UploaderCategory, string[]>
        {
            { UploaderCategory.Image, allTypes },
            { UploaderCategory.Text, allTypes },
            { UploaderCategory.File, allTypes }
        };
    }

    public override object? CreateConfigView()
    {
        return new Views.DropboxConfigView();
    }

    public override IUploaderConfigViewModel? CreateConfigViewModel()
    {
        return new ViewModels.DropboxConfigViewModel();
    }

    public bool SupportsFolders => true;

    public override bool ValidateSettings(string settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson) || Secrets == null)
        {
            return false;
        }

        DropboxConfigModel config = DeserializeConfig(settingsJson);
        if (string.IsNullOrWhiteSpace(config.SecretKey))
        {
            return false;
        }

        string clientId = Secrets.GetSecret(ProviderId, config.SecretKey, "clientId") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return false;
        }

        string? tokenJson = Secrets.GetSecret(ProviderId, config.SecretKey, "oauthToken");
        if (string.IsNullOrWhiteSpace(tokenJson))
        {
            return false;
        }

        try
        {
            OAuth2Token? token = JsonConvert.DeserializeObject<OAuth2Token>(tokenJson);
            return token != null && !string.IsNullOrWhiteSpace(token.access_token);
        }
        catch
        {
            return false;
        }
    }

    public async Task<ExplorerPage> ListAsync(ExplorerQuery query, CancellationToken cancellation = default)
    {
        DropboxConfigModel config = DeserializeConfig(query.SettingsJson);
        CacheLatestSettings(query.SettingsJson);

        DropboxUploader uploader = BuildUploader(config, requireToken: true);
        if (!uploader.CheckAuthorization())
        {
            throw new InvalidOperationException("Dropbox authorization is required.");
        }

        string accessToken = uploader.AuthInfo.Token.access_token;
        DropboxListFolderResult? rawPage;
        if (string.IsNullOrWhiteSpace(query.ContinuationToken))
        {
            string initialFolderPath = ResolveInitialFolderPath(query.FolderPath, config);

            try
            {
                rawPage = await ListFolderAsync(accessToken, initialFolderPath, query.PageSize, cancellation);
            }
            catch (InvalidOperationException ex) when (ShouldFallbackToRoot(initialFolderPath, ex))
            {
                rawPage = await ListFolderAsync(accessToken, string.Empty, query.PageSize, cancellation);
            }
        }
        else
        {
            rawPage = await ListFolderContinueAsync(accessToken, query.ContinuationToken, cancellation);
        }

        if (rawPage == null)
        {
            return new ExplorerPage();
        }

        List<MediaItem> items = rawPage.Entries
            .Select(entry => MapMediaItem(entry, query.SettingsJson ?? string.Empty))
            .ToList();

        items = ApplyFiltersAndSort(items, query);

        return new ExplorerPage
        {
            Items = items,
            ContinuationToken = rawPage.HasMore ? rawPage.Cursor : null,
            TotalCount = rawPage.HasMore ? null : items.Count
        };
    }

    public async Task<byte[]?> GetThumbnailAsync(MediaItem item, int maxWidthPx = 180, CancellationToken cancellation = default)
    {
        if (item.IsFolder)
        {
            return null;
        }

        if (!IsImageExtension(Path.GetExtension(item.Name)))
        {
            return null;
        }

        DropboxUploader? uploader = BuildUploaderFromMediaItem(item);
        if (uploader == null || !uploader.CheckAuthorization())
        {
            return null;
        }

        string path = NormalizeItemPath(item.Path);
        byte[]? thumbnail = await GetThumbnailAsync(uploader.AuthInfo.Token.access_token, path, cancellation);
        if (thumbnail is { Length: > 0 })
        {
            return thumbnail;
        }

        string? tempLink = await GetTemporaryLinkAsync(uploader.AuthInfo.Token.access_token, path, cancellation);
        if (string.IsNullOrWhiteSpace(tempLink))
        {
            return null;
        }

        return await DownloadBytesFromUrlAsync(tempLink, cancellation);
    }

    public async Task<Stream?> GetContentAsync(MediaItem item, CancellationToken cancellation = default)
    {
        if (item.IsFolder)
        {
            return null;
        }

        DropboxUploader? uploader = BuildUploaderFromMediaItem(item);
        if (uploader == null || !uploader.CheckAuthorization())
        {
            return null;
        }

        byte[]? bytes = await DownloadFileBytesAsync(uploader.AuthInfo.Token.access_token, NormalizeItemPath(item.Path), cancellation);
        return bytes == null ? null : new MemoryStream(bytes);
    }

    public async Task<bool> DeleteAsync(MediaItem item, CancellationToken cancellation = default)
    {
        DropboxUploader? uploader = BuildUploaderFromMediaItem(item);
        if (uploader == null || !uploader.CheckAuthorization())
        {
            return false;
        }

        return await DeletePathAsync(uploader.AuthInfo.Token.access_token, NormalizeItemPath(item.Path), cancellation);
    }

    public async Task<bool> CreateFolderAsync(string parentPath, string folderName, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return false;
        }

        string? settingsJson = GetLatestSettingsJson();
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return false;
        }

        DropboxConfigModel config = DeserializeConfig(settingsJson);
        DropboxUploader uploader;
        try
        {
            uploader = BuildUploader(config, requireToken: true);
        }
        catch
        {
            return false;
        }

        if (!uploader.CheckAuthorization())
        {
            return false;
        }

        string path = CombineFolderPath(parentPath, folderName);
        return await CreateFolderPathAsync(uploader.AuthInfo.Token.access_token, path, cancellation);
    }

    public ExplorerCapabilities BrowserCapabilities =>
        ExplorerCapabilities.Download | ExplorerCapabilities.Upload | ExplorerCapabilities.Rename |
        ExplorerCapabilities.Delete | ExplorerCapabilities.Url | ExplorerCapabilities.CreateFolder |
        ExplorerCapabilities.Thumbnails;

    public async Task<bool> CreateFolderAsync(ExplorerContext context, string parentPath, string folderName, CancellationToken cancellation = default)
    {
        DropboxUploader uploader = ResolveAuthorizedUploader(context);
        return await CreateFolderPathAsync(uploader.AuthInfo.Token.access_token, CombineFolderPath(parentPath, folderName), cancellation);
    }

    public async Task<bool> UploadAsync(ExplorerContext context, string folderPath, string fileName, Stream content, CancellationToken cancellation = default)
    {
        DropboxUploader uploader = ResolveAuthorizedUploader(context);
        UploadResult result = await Task.Run(() => uploader.UploadFile(content, NormalizeItemPath(folderPath), fileName), cancellation);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(uploader.Errors.Count > 0 ? string.Join(Environment.NewLine, uploader.Errors) : $"Dropbox did not accept {fileName}.");
        }

        return true;
    }

    public async Task<bool> RenameAsync(ExplorerContext context, MediaItem item, string newName, CancellationToken cancellation = default)
    {
        DropboxUploader uploader = ResolveAuthorizedUploader(context);
        string from = NormalizeItemPath(item.Path);
        string parent = from.TrimEnd('/');
        parent = parent.Contains('/') ? parent[..parent.LastIndexOf('/')] : string.Empty;
        object payload = new { from_path = from, to_path = CombineFolderPath(parent, newName), autorename = false };
        DropboxCreateFolderResponse? response = await PostJsonAsync<DropboxCreateFolderResponse>(UrlMove, uploader.AuthInfo.Token.access_token, payload, cancellation);
        return response?.Metadata != null;
    }

    public async Task<bool> DeleteAsync(ExplorerContext context, MediaItem item, CancellationToken cancellation = default)
    {
        // delete_v2 removes folders together with their contents.
        DropboxUploader uploader = ResolveAuthorizedUploader(context);
        return await DeletePathAsync(uploader.AuthInfo.Token.access_token, NormalizeItemPath(item.Path), cancellation);
    }

    public async Task<bool?> HasChildrenAsync(ExplorerContext context, MediaItem folder, CancellationToken cancellation = default)
    {
        DropboxUploader uploader = ResolveAuthorizedUploader(context);
        DropboxListFolderResult? result = await ListFolderAsync(uploader.AuthInfo.Token.access_token, NormalizeItemPath(folder.Path), 1, cancellation);
        return result == null ? null : result.Entries.Count > 0;
    }

    private DropboxUploader ResolveAuthorizedUploader(ExplorerContext context)
    {
        if (string.IsNullOrWhiteSpace(context.SettingsJson))
        {
            throw new InvalidOperationException("Dropbox account settings are missing.");
        }

        DropboxUploader uploader = BuildUploader(DeserializeConfig(context.SettingsJson), requireToken: true);
        if (!uploader.CheckAuthorization())
        {
            throw new InvalidOperationException("Dropbox authorization has expired. Sign in again from Destinations.");
        }

        return uploader;
    }

    private DropboxUploader BuildUploader(DropboxConfigModel config, bool requireToken)
    {
        if (Secrets == null)
        {
            throw new InvalidOperationException("Secret store is not available for Dropbox.");
        }

        if (string.IsNullOrWhiteSpace(config.SecretKey))
        {
            throw new InvalidOperationException("Dropbox secret key is missing.");
        }

        string clientId = Secrets.GetSecret(ProviderId, config.SecretKey, "clientId") ?? string.Empty;
        string clientSecret = Secrets.GetSecret(ProviderId, config.SecretKey, "clientSecret") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("Dropbox client ID is missing.");
        }

        OAuth2Info authInfo = new(clientId, clientSecret);
        string? tokenJson = Secrets.GetSecret(ProviderId, config.SecretKey, "oauthToken");
        if (!string.IsNullOrWhiteSpace(tokenJson))
        {
            OAuth2Token? token = JsonConvert.DeserializeObject<OAuth2Token>(tokenJson);
            if (token != null)
            {
                authInfo.Token = token;
            }
        }

        if (requireToken && (authInfo.Token == null || string.IsNullOrWhiteSpace(authInfo.Token.access_token)))
        {
            throw new InvalidOperationException("Dropbox authorization token is missing.");
        }

        return new DropboxUploader(config, authInfo, token => PersistToken(config.SecretKey, token));
    }

    private DropboxUploader? BuildUploaderFromMediaItem(MediaItem item)
    {
        string? settingsJson = null;
        if (item.Metadata.TryGetValue("settingsJson", out string? itemSettingsJson))
        {
            settingsJson = itemSettingsJson;
        }

        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            settingsJson = GetLatestSettingsJson();
        }

        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return null;
        }

        try
        {
            return BuildUploader(DeserializeConfig(settingsJson), requireToken: true);
        }
        catch
        {
            return null;
        }
    }

    private static MediaItem MapMediaItem(DropboxMetadata entry, string settingsJson)
    {
        bool isFolder = string.Equals(entry.Tag, "folder", StringComparison.OrdinalIgnoreCase);
        string path = entry.PathDisplay ?? entry.PathLower ?? DropboxUploader.VerifyPath(entry.Name);
        DateTime? modifiedAt = TryParseDateTime(entry.ServerModified);
        DateTime? createdAt = TryParseDateTime(entry.ClientModified);

        return new MediaItem
        {
            Id = string.IsNullOrWhiteSpace(entry.Id) ? path : entry.Id,
            Name = string.IsNullOrWhiteSpace(entry.Name) ? Path.GetFileName(path.TrimEnd('/')) : entry.Name,
            Path = path,
            IsFolder = isFolder,
            SizeBytes = isFolder ? 0 : entry.Size,
            MimeType = isFolder ? null : MimeTypes.GetMimeTypeFromFileName(entry.Name),
            CreatedAt = createdAt,
            ModifiedAt = modifiedAt,
            Url = BuildDropboxHomeUrl(path),
            Metadata = new Dictionary<string, string>
            {
                ["settingsJson"] = settingsJson
            }
        };
    }

    private static List<MediaItem> ApplyFiltersAndSort(List<MediaItem> items, ExplorerQuery query)
    {
        IEnumerable<MediaItem> filtered = items;

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            filtered = filtered.Where(item => item.Name.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.FileTypeFilter))
        {
            filtered = filtered.Where(item => item.IsFolder || MatchesMimeFilter(item.MimeType, query.FileTypeFilter));
        }

        IOrderedEnumerable<MediaItem> ordered = filtered.OrderByDescending(item => item.IsFolder);
        ordered = query.SortBy switch
        {
            ExplorerSortField.Name when query.SortDescending => ordered.ThenByDescending(item => item.Name, StringComparer.OrdinalIgnoreCase),
            ExplorerSortField.Name => ordered.ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase),
            ExplorerSortField.Size when query.SortDescending => ordered.ThenByDescending(item => item.SizeBytes),
            ExplorerSortField.Size => ordered.ThenBy(item => item.SizeBytes),
            ExplorerSortField.Date when query.SortDescending => ordered.ThenByDescending(item => item.ModifiedAt ?? DateTime.MinValue),
            _ => ordered.ThenBy(item => item.ModifiedAt ?? DateTime.MinValue)
        };

        return ordered.ToList();
    }

    private static bool MatchesMimeFilter(string? mimeType, string fileTypeFilter)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return false;
        }

        if (fileTypeFilter.EndsWith("/*", StringComparison.Ordinal))
        {
            string prefix = fileTypeFilter[..^1];
            return mimeType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(mimeType, fileTypeFilter, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime? TryParseDateTime(string? value)
    {
        return DateTime.TryParse(value, out DateTime parsed) ? parsed : null;
    }

    private static bool IsImageExtension(string extension)
    {
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeItemPath(string path)
    {
        return DropboxUploader.VerifyPath(path);
    }

    private static string BuildDropboxHomeUrl(string path)
    {
        string normalizedPath = path.Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return "https://www.dropbox.com/home";
        }

        string encodedPath = string.Join("/", normalizedPath.Split('/').Select(Uri.EscapeDataString));
        return $"https://www.dropbox.com/home/{encodedPath}";
    }

    private static string CombineFolderPath(string parentPath, string folderName)
    {
        string safeParent = DropboxUploader.VerifyPath(parentPath).TrimEnd('/');
        string safeFolderName = folderName.Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(safeParent))
        {
            return DropboxUploader.VerifyPath(safeFolderName);
        }

        return DropboxUploader.VerifyPath($"{safeParent}/{safeFolderName}");
    }

    private static string ResolveInitialFolderPath(string? folderPathFromQuery, DropboxConfigModel config)
    {
        if (!string.IsNullOrWhiteSpace(folderPathFromQuery))
        {
            return folderPathFromQuery;
        }

        if (string.IsNullOrWhiteSpace(config.UploadPath))
        {
            return string.Empty;
        }

        string normalizedUploadPath = DropboxUploader.VerifyPath(config.UploadPath);
        string parsedUploadPath = NameParser.Parse(NameParserType.Default, normalizedUploadPath);
        return DropboxUploader.VerifyPath(parsedUploadPath);
    }

    private static bool ShouldFallbackToRoot(string attemptedPath, InvalidOperationException ex)
    {
        if (string.IsNullOrWhiteSpace(attemptedPath))
        {
            return false;
        }

        return ex.Message.Contains("path/not_found", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<DropboxListFolderResult?> ListFolderAsync(string accessToken, string? folderPath, int pageSize, CancellationToken cancellation)
    {
        int safePageSize = Math.Clamp(pageSize, 1, 2000);
        object payload = new
        {
            path = DropboxUploader.VerifyPath(folderPath),
            recursive = false,
            include_media_info = false,
            include_deleted = false,
            include_non_downloadable_files = false,
            limit = safePageSize
        };

        return await PostJsonAsync<DropboxListFolderResult>(UrlListFolder, accessToken, payload, cancellation);
    }

    private static async Task<DropboxListFolderResult?> ListFolderContinueAsync(string accessToken, string continuationToken, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(continuationToken))
        {
            return new DropboxListFolderResult();
        }

        object payload = new
        {
            cursor = continuationToken
        };

        return await PostJsonAsync<DropboxListFolderResult>(UrlListFolderContinue, accessToken, payload, cancellation);
    }

    private static async Task<bool> DeletePathAsync(string accessToken, string path, CancellationToken cancellation)
    {
        object payload = new
        {
            path
        };

        DropboxDeleteResponse? response = await PostJsonAsync<DropboxDeleteResponse>(UrlDelete, accessToken, payload, cancellation);
        return response?.Metadata != null;
    }

    private static async Task<bool> CreateFolderPathAsync(string accessToken, string path, CancellationToken cancellation)
    {
        object payload = new
        {
            path,
            autorename = false
        };

        DropboxCreateFolderResponse? response = await PostJsonAsync<DropboxCreateFolderResponse>(UrlCreateFolder, accessToken, payload, cancellation);
        return response?.Metadata != null;
    }

    private static async Task<string?> GetTemporaryLinkAsync(string accessToken, string path, CancellationToken cancellation)
    {
        object payload = new
        {
            path
        };

        DropboxTemporaryLinkResult? response = await PostJsonAsync<DropboxTemporaryLinkResult>(UrlGetTemporaryLink, accessToken, payload, cancellation);
        return response?.Link;
    }

    private static async Task<byte[]?> DownloadFileBytesAsync(string accessToken, string path, CancellationToken cancellation)
    {
        using SysHttpRequestMessage request = new(SysHttpMethod.Post, UrlDownload);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("Dropbox-API-Arg", JsonConvert.SerializeObject(new
        {
            path
        }));

        try
        {
            using var response = await ExplorerHttpClient.SendAsync(request, cancellation);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellation);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<byte[]?> GetThumbnailAsync(string accessToken, string path, CancellationToken cancellation)
    {
        object payload = new
        {
            path,
            format = "jpeg",
            size = "w256h256",
            mode = "strict"
        };

        using SysHttpRequestMessage request = new(SysHttpMethod.Post, UrlGetThumbnail);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("Dropbox-API-Arg", JsonConvert.SerializeObject(payload));

        try
        {
            using var response = await ExplorerHttpClient.SendAsync(request, cancellation);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellation);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<byte[]?> DownloadBytesFromUrlAsync(string url, CancellationToken cancellation)
    {
        try
        {
            using var response = await ExplorerHttpClient.GetAsync(url, cancellation);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellation);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<T?> PostJsonAsync<T>(string url, string accessToken, object payload, CancellationToken cancellation)
    {
        string jsonPayload = JsonConvert.SerializeObject(payload);
        using SysHttpRequestMessage request = new(SysHttpMethod.Post, url)
        {
            Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await ExplorerHttpClient.SendAsync(request, cancellation);
        string responseBody = await response.Content.ReadAsStringAsync(cancellation);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(BuildDropboxErrorMessage(responseBody, response.StatusCode));
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return default;
        }

        return JsonConvert.DeserializeObject<T>(responseBody);
    }

    private static string BuildDropboxErrorMessage(string responseBody, System.Net.HttpStatusCode statusCode)
    {
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                DropboxApiError? error = JsonConvert.DeserializeObject<DropboxApiError>(responseBody);
                if (!string.IsNullOrWhiteSpace(error?.ErrorSummary))
                {
                    return $"Dropbox API error ({(int)statusCode}): {error.ErrorSummary}";
                }
            }
            catch
            {
            }
        }

        return $"Dropbox API request failed with status code {(int)statusCode}.";
    }

    private DropboxConfigModel DeserializeConfig(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return new DropboxConfigModel();
        }

        return JsonConvert.DeserializeObject<DropboxConfigModel>(settingsJson) ?? new DropboxConfigModel();
    }

    private void PersistToken(string secretKey, OAuth2Token token)
    {
        if (Secrets == null || string.IsNullOrWhiteSpace(secretKey))
        {
            return;
        }

        string tokenJson = JsonConvert.SerializeObject(token, Formatting.None);
        Secrets.SetSecret(ProviderId, secretKey, "oauthToken", tokenJson);
    }

    private void CacheLatestSettings(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return;
        }

        lock (_latestSettingsLock)
        {
            _latestSettingsJson = settingsJson;
        }
    }

    private string? GetLatestSettingsJson()
    {
        lock (_latestSettingsLock)
        {
            return _latestSettingsJson;
        }
    }
}
