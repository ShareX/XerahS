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

namespace ShareX.Nextcloud.Plugin;

public sealed class NextcloudProvider : UploaderProviderBase, IUploaderExplorer, IInstanceSecretMigrator, IInstanceSecretBackupProvider
{
    private readonly Lock _latestSettingsLock = new();
    private string? _latestSettingsJson;

    public override string ProviderId => "nextcloud";
    public override string Name => "Nextcloud";
    public override string Description => "Upload files to Nextcloud with Login Flow v2, WebDAV, and the OCS Share API";
    public override Version Version => new(1, 0, 0);
    public override UploaderCategory[] SupportedCategories => new[] { UploaderCategory.Image, UploaderCategory.Text, UploaderCategory.File };
    public override Type ConfigModelType => typeof(NextcloudConfigModel);

    public IReadOnlyList<InstanceSecretReference> GetSecretReferences(string settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return Array.Empty<InstanceSecretReference>();
        }

        string? secretKey;
        try
        {
            secretKey = JObject.Parse(settingsJson).Value<string>(nameof(NextcloudConfigModel.SecretKey));
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
            new(ProviderId, secretKey, "appPassword"),
            new(ProviderId, secretKey, "sharePassword")
        ];
    }
    public override UploaderCapabilities Capabilities =>
        UploaderCapabilities.Cancellation | UploaderCapabilities.Progress | UploaderCapabilities.Explorer;

    public bool SupportsFolders => true;

    public override Uploader CreateInstance(string settingsJson)
    {
        NextcloudConfigModel config = DeserializeConfig(settingsJson);
        return new NextcloudUploader(
            config,
            ResolveSecret(config.SecretKey, "appPassword"),
            ResolveSecret(config.SecretKey, "sharePassword"));
    }

    public override bool ValidateSettings(string settingsJson)
    {
        NextcloudConfigModel config = DeserializeConfig(settingsJson);
        return !string.IsNullOrWhiteSpace(config.ServerUrl) &&
               !string.IsNullOrWhiteSpace(ResolveLoginName(config)) &&
               !string.IsNullOrWhiteSpace(ResolveSecret(config.SecretKey, "appPassword"));
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
        return new Views.NextcloudConfigView();
    }

    public override IUploaderConfigViewModel? CreateConfigViewModel()
    {
        return new ViewModels.NextcloudConfigViewModel();
    }

    public async Task<ExplorerPage> ListAsync(ExplorerQuery query, CancellationToken cancellation = default)
    {
        NextcloudConfigModel config = DeserializeConfig(query.SettingsJson);
        CacheLatestSettings(query.SettingsJson);

        string userId = ResolveUserId(config);
        string folderPath = ResolveExplorerFolderPath(query.FolderPath, config);

        IReadOnlyList<NextcloudFileEntry> entries = await CreateClient(config).ListFolderAsync(userId, folderPath, cancellation);
        IEnumerable<MediaItem> items = entries.Select(entry => new MediaItem
        {
            Id = entry.Id,
            Name = entry.Name,
            Path = entry.RelativePath,
            IsFolder = entry.IsFolder,
            SizeBytes = entry.SizeBytes,
            MimeType = entry.MimeType,
            ModifiedAt = entry.ModifiedAt,
            Metadata = new Dictionary<string, string>
            {
                ["settingsJson"] = query.SettingsJson ?? string.Empty,
                ["userId"] = userId
            }
        });

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            items = items.Where(item => item.Name.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.FileTypeFilter))
        {
            items = items.Where(item => item.IsFolder || MatchesMimeFilter(item.MimeType, query.FileTypeFilter));
        }

        IOrderedEnumerable<MediaItem> orderedItems = items.OrderByDescending(item => item.IsFolder);
        orderedItems = query.SortBy switch
        {
            ExplorerSortField.Name when query.SortDescending => orderedItems.ThenByDescending(item => item.Name, StringComparer.OrdinalIgnoreCase),
            ExplorerSortField.Name => orderedItems.ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase),
            ExplorerSortField.Size when query.SortDescending => orderedItems.ThenByDescending(item => item.SizeBytes),
            ExplorerSortField.Size => orderedItems.ThenBy(item => item.SizeBytes),
            ExplorerSortField.Date when query.SortDescending => orderedItems.ThenByDescending(item => item.ModifiedAt ?? DateTime.MinValue),
            _ => orderedItems.ThenBy(item => item.ModifiedAt ?? DateTime.MinValue)
        };

        return new ExplorerPage
        {
            Items = orderedItems.ToList()
        };
    }

    public async Task<byte[]?> GetThumbnailAsync(MediaItem item, int maxWidthPx = 180, CancellationToken cancellation = default)
    {
        if (item.IsFolder || !IsImageExtension(Path.GetExtension(item.Name)) || item.SizeBytes > 10 * 1024 * 1024)
        {
            return null;
        }

        (NextcloudConfigModel config, string userId)? context = ResolveItemContext(item);
        if (context == null)
        {
            return null;
        }

        return await CreateClient(context.Value.config).DownloadFileAsync(context.Value.userId, item.Path, cancellation);
    }

    public async Task<Stream?> GetContentAsync(MediaItem item, CancellationToken cancellation = default)
    {
        (NextcloudConfigModel config, string userId)? context = ResolveItemContext(item);
        if (context == null)
        {
            return null;
        }

        byte[]? bytes = await CreateClient(context.Value.config).DownloadFileAsync(context.Value.userId, item.Path, cancellation);
        return bytes == null ? null : new MemoryStream(bytes);
    }

    public async Task<bool> DeleteAsync(MediaItem item, CancellationToken cancellation = default)
    {
        (NextcloudConfigModel config, string userId)? context = ResolveItemContext(item);
        if (context == null)
        {
            return false;
        }

        return await CreateClient(context.Value.config).DeleteFileAsync(context.Value.userId, item.Path, cancellation);
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

        NextcloudConfigModel config = DeserializeConfig(settingsJson);
        string userId = ResolveUserId(config);
        string combinedPath = NextcloudClient.CombineRelativePath(parentPath, folderName);
        return await CreateClient(config).CreateFolderAsync(userId, combinedPath, cancellation);
    }

    public ExplorerCapabilities BrowserCapabilities =>
        ExplorerCapabilities.Download | ExplorerCapabilities.Upload | ExplorerCapabilities.Rename |
        ExplorerCapabilities.Delete | ExplorerCapabilities.Url | ExplorerCapabilities.CreateFolder |
        ExplorerCapabilities.Thumbnails;

    public async Task<bool> CreateFolderAsync(ExplorerContext context, string parentPath, string folderName, CancellationToken cancellation = default)
    {
        (NextcloudConfigModel config, string userId) = ResolveContext(context);
        return await CreateClient(config).CreateFolderAsync(userId, NextcloudClient.CombineRelativePath(parentPath, folderName), cancellation);
    }

    public async Task<bool> UploadAsync(ExplorerContext context, string folderPath, string fileName, Stream content, CancellationToken cancellation = default)
    {
        (NextcloudConfigModel config, string userId) = ResolveContext(context);
        await CreateClient(config).UploadFileAsync(content, userId, folderPath, fileName, config.UseChunkedUpload, config.ChunkSizeMiB, null, cancellation);
        return true;
    }

    public async Task<bool> RenameAsync(ExplorerContext context, MediaItem item, string newName, CancellationToken cancellation = default)
    {
        (NextcloudConfigModel config, string userId) = ResolveContext(context);
        string path = NextcloudClient.NormalizeRelativePath(item.Path);
        int separator = path.LastIndexOf('/');
        string parent = separator >= 0 ? path[..separator] : string.Empty;
        await CreateClient(config).MoveAsync(userId, path, NextcloudClient.CombineRelativePath(parent, newName), cancellation);
        return true;
    }

    public async Task<bool> DeleteAsync(ExplorerContext context, MediaItem item, CancellationToken cancellation = default)
    {
        // WebDAV DELETE on a collection removes it with everything inside.
        (NextcloudConfigModel config, string userId) = ResolveContext(context);
        return await CreateClient(config).DeleteFileAsync(userId, item.Path, cancellation);
    }

    public async Task<bool?> HasChildrenAsync(ExplorerContext context, MediaItem folder, CancellationToken cancellation = default)
    {
        (NextcloudConfigModel config, string userId) = ResolveContext(context);
        IReadOnlyList<NextcloudFileEntry> entries = await CreateClient(config).ListFolderAsync(userId, folder.Path, cancellation);
        return entries.Count > 0;
    }

    private (NextcloudConfigModel Config, string UserId) ResolveContext(ExplorerContext context)
    {
        if (string.IsNullOrWhiteSpace(context.SettingsJson))
        {
            throw new InvalidOperationException("Nextcloud account settings are missing.");
        }

        NextcloudConfigModel config = DeserializeConfig(context.SettingsJson);
        return (config, ResolveUserId(config));
    }

    public bool TryMigrateSecrets(string settingsJson, ISecretStore secrets, out string updatedSettingsJson, out int migratedSecretCount)
    {
        updatedSettingsJson = settingsJson;
        migratedSecretCount = 0;

        JObject? json;
        try
        {
            json = JObject.Parse(settingsJson);
        }
        catch
        {
            return false;
        }

        string secretKey = json.Value<string>("SecretKey") ?? Guid.NewGuid().ToString("N");
        bool changed = false;

        if (!string.Equals(json.Value<string>("SecretKey"), secretKey, StringComparison.Ordinal))
        {
            json["SecretKey"] = secretKey;
            changed = true;
        }

        migratedSecretCount += TryMoveSecret(json, secrets, secretKey, "AppPassword", "appPassword", ref changed);
        migratedSecretCount += TryMoveSecret(json, secrets, secretKey, "SharePassword", "sharePassword", ref changed);

        if (changed)
        {
            updatedSettingsJson = json.ToString(Formatting.Indented);
        }

        return changed;
    }

    private static int TryMoveSecret(JObject json, ISecretStore secrets, string secretKey, string propertyName, string secretName, ref bool changed)
    {
        string? secretValue = json.Value<string>(propertyName);
        if (string.IsNullOrWhiteSpace(secretValue))
        {
            return 0;
        }

        secrets.SetSecret("nextcloud", secretKey, secretName, secretValue);
        json.Remove(propertyName);
        changed = true;
        return 1;
    }

    private NextcloudClient CreateClient(NextcloudConfigModel config)
    {
        return new NextcloudClient(config.ServerUrl, ResolveLoginName(config), ResolveSecret(config.SecretKey, "appPassword"));
    }

    private string ResolveSecret(string secretKey, string secretName)
    {
        if (Secrets == null || string.IsNullOrWhiteSpace(secretKey))
        {
            return string.Empty;
        }

        return Secrets.GetSecret(ProviderId, secretKey, secretName) ?? string.Empty;
    }

    private static NextcloudConfigModel DeserializeConfig(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return new NextcloudConfigModel();
        }

        return JsonConvert.DeserializeObject<NextcloudConfigModel>(settingsJson) ?? new NextcloudConfigModel();
    }

    private static string ResolveExplorerFolderPath(string? folderPathFromQuery, NextcloudConfigModel config)
    {
        if (!string.IsNullOrWhiteSpace(folderPathFromQuery))
        {
            return NextcloudClient.NormalizeRelativePath(folderPathFromQuery);
        }

        string parsedPath = NameParser.Parse(NameParserType.Default, NextcloudClient.NormalizeRelativePath(config.RemotePath));
        return NextcloudClient.NormalizeRelativePath(parsedPath);
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

    private static string ResolveLoginName(NextcloudConfigModel config)
    {
        if (!string.IsNullOrWhiteSpace(config.LoginName))
        {
            return config.LoginName;
        }

        return config.UserId;
    }

    private static string ResolveUserId(NextcloudConfigModel config)
    {
        if (!string.IsNullOrWhiteSpace(config.UserId))
        {
            return config.UserId;
        }

        return ResolveLoginName(config);
    }

    private (NextcloudConfigModel config, string userId)? ResolveItemContext(MediaItem item)
    {
        string? settingsJson = null;
        if (item.Metadata.TryGetValue("settingsJson", out string? itemSettingsJson) && !string.IsNullOrWhiteSpace(itemSettingsJson))
        {
            settingsJson = itemSettingsJson;
        }
        else
        {
            settingsJson = GetLatestSettingsJson();
        }

        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return null;
        }

        NextcloudConfigModel config = DeserializeConfig(settingsJson);
        string userId = item.Metadata.TryGetValue("userId", out string? itemUserId) && !string.IsNullOrWhiteSpace(itemUserId)
            ? itemUserId
            : ResolveUserId(config);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return (config, userId);
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
