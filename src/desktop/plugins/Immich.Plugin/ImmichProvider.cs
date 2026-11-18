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
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace ShareX.Immich.Plugin;

public sealed class ImmichProvider : UploaderProviderBase, IUploaderExplorer, IInstanceSecretMigrator, IInstanceSecretBackupProvider
{
    private readonly Lock _latestSettingsLock = new();
    private string? _latestSettingsJson;

    public override string ProviderId => "immich";
    public override string Name => "Immich";
    public override string Description => "Upload media to Immich with native API-key bootstrap, duplicate checks, albums, and shared links";
    public override Version Version => new(1, 0, 0);
    public override UploaderCategory[] SupportedCategories => new[] { UploaderCategory.Image, UploaderCategory.File };
    public override Type ConfigModelType => typeof(ImmichConfigModel);

    public IReadOnlyList<InstanceSecretReference> GetSecretReferences(string settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return Array.Empty<InstanceSecretReference>();
        }

        string? secretKey;
        try
        {
            secretKey = JObject.Parse(settingsJson).Value<string>(nameof(ImmichConfigModel.SecretKey));
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
            new(ProviderId, secretKey, "apiKey"),
            new(ProviderId, secretKey, "apiToken"),
            new(ProviderId, secretKey, "sharePassword")
        ];
    }
    public override UploaderCapabilities Capabilities =>
        UploaderCapabilities.Cancellation | UploaderCapabilities.Progress | UploaderCapabilities.Explorer;

    public bool SupportsFolders => true;

    public override Uploader CreateInstance(string settingsJson)
    {
        ImmichConfigModel config = DeserializeConfig(settingsJson);
        return new ImmichUploader(
            config,
            ResolveApiKey(config.SecretKey),
            ResolveSecret(config.SecretKey, "sharePassword"));
    }

    public override bool ValidateSettings(string settingsJson)
    {
        ImmichConfigModel config = DeserializeConfig(settingsJson);
        if (string.IsNullOrWhiteSpace(config.ServerUrl) || string.IsNullOrWhiteSpace(ResolveApiKey(config.SecretKey)))
        {
            return false;
        }

        if (config.ShareMode == ImmichShareMode.Album && !config.AddToAlbum)
        {
            return false;
        }

        if (config.AddToAlbum && string.IsNullOrWhiteSpace(config.AlbumId) && string.IsNullOrWhiteSpace(config.AlbumName))
        {
            return false;
        }

        return true;
    }

    public override Dictionary<UploaderCategory, string[]> GetSupportedFileTypes()
    {
        string[] mediaTypes =
        {
            "png", "jpg", "jpeg", "gif", "bmp", "tiff", "webp", "avif", "heic", "heif", "jxl",
            "dng", "cr2", "cr3", "nef", "arw", "raf", "rw2", "orf",
            "mp4", "mov", "avi", "mkv", "webm", "wmv", "m4v", "mts", "m2ts",
            "mp3", "wav", "m4a", "aac", "flac", "ogg"
        };

        return new Dictionary<UploaderCategory, string[]>
        {
            { UploaderCategory.Image, mediaTypes },
            { UploaderCategory.File, mediaTypes }
        };
    }

    public override object? CreateConfigView()
    {
        return new Views.ImmichConfigView();
    }

    public override IUploaderConfigViewModel? CreateConfigViewModel()
    {
        return new ViewModels.ImmichConfigViewModel();
    }

    public async Task<ExplorerPage> ListAsync(ExplorerQuery query, CancellationToken cancellation = default)
    {
        ImmichConfigModel config = DeserializeConfig(query.SettingsJson);
        CacheLatestSettings(query.SettingsJson);

        ImmichClient client = CreateClient(config);

        if (TryGetAlbumId(query.FolderPath, out string? albumId))
        {
            ImmichAlbum album = await client.GetAlbumAsync(albumId!, true, cancellation);
            IEnumerable<MediaItem> items = album.Assets.Select(asset => MapAssetItem(asset, query.SettingsJson ?? string.Empty, album.Id));
            items = ApplyFilters(items, query);

            return new ExplorerPage
            {
                Items = ApplySorting(items, query).ToList(),
                TotalCount = album.AssetCount
            };
        }

        IEnumerable<MediaItem> albumItems = await GetAlbumItemsAsync(client, query.SettingsJson ?? string.Empty, cancellation);
        albumItems = ApplyFilters(albumItems, query);

        IReadOnlyList<MediaItem> orderedItems = ApplySorting(albumItems, query).ToList();
        return new ExplorerPage
        {
            Items = orderedItems,
            TotalCount = orderedItems.Count
        };
    }

    public async Task<byte[]?> GetThumbnailAsync(MediaItem item, int maxWidthPx = 180, CancellationToken cancellation = default)
    {
        ImmichConfigModel? config = ResolveItemConfig(item);
        if (config == null)
        {
            return null;
        }

        string? assetId = ResolveThumbnailAssetId(item);
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return null;
        }

        return await CreateClient(config).DownloadThumbnailAsync(assetId, maxWidthPx, cancellation);
    }

    public async Task<Stream?> GetContentAsync(MediaItem item, CancellationToken cancellation = default)
    {
        if (item.IsFolder)
        {
            return null;
        }

        ImmichConfigModel? config = ResolveItemConfig(item);
        if (config == null || string.IsNullOrWhiteSpace(item.Id))
        {
            return null;
        }

        byte[]? content = await CreateClient(config).DownloadAssetAsync(item.Id, cancellation);
        return content == null ? null : new MemoryStream(content);
    }

    public async Task<bool> DeleteAsync(MediaItem item, CancellationToken cancellation = default)
    {
        ImmichConfigModel? config = ResolveItemConfig(item);
        if (config == null || string.IsNullOrWhiteSpace(item.Id))
        {
            return false;
        }

        ImmichClient client = CreateClient(config);
        if (item.IsFolder)
        {
            return await client.DeleteAlbumAsync(item.Id, cancellation);
        }

        return await client.DeleteAssetAsync(item.Id, cancellation);
    }

    public async Task<bool> CreateFolderAsync(string parentPath, string folderName, CancellationToken cancellation = default)
    {
        if (!string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(folderName))
        {
            return false;
        }

        string? settingsJson = GetLatestSettingsJson();
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return false;
        }

        ImmichConfigModel config = DeserializeConfig(settingsJson);
        await CreateClient(config).CreateAlbumAsync(folderName.Trim(), cancellation);
        return true;
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

        migratedSecretCount += TryMoveSecret(json, secrets, secretKey, "ApiKey", "apiKey", ref changed);
        migratedSecretCount += TryMoveSecret(json, secrets, secretKey, "ApiToken", "apiKey", ref changed);
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

        secrets.SetSecret("immich", secretKey, secretName, secretValue);
        json.Remove(propertyName);
        changed = true;
        return 1;
    }

    private async Task<IReadOnlyList<MediaItem>> GetAlbumItemsAsync(ImmichClient client, string settingsJson, CancellationToken cancellation)
    {
        IReadOnlyList<ImmichAlbum> albums = await client.GetAlbumsAsync(cancellation);
        return albums.Select(album => new MediaItem
        {
            Id = album.Id,
            Name = album.AlbumName,
            Path = BuildAlbumPath(album.Id),
            IsFolder = true,
            SizeBytes = 0,
            CreatedAt = album.CreatedAt.UtcDateTime,
            ModifiedAt = album.UpdatedAt.UtcDateTime,
            Url = album.SharedLinkUrl,
            Metadata = new Dictionary<string, string>
            {
                ["kind"] = "album",
                ["settingsJson"] = settingsJson,
                ["albumThumbnailAssetId"] = album.ThumbnailAssetId ?? string.Empty
            }
        }).ToList();
    }

    private static MediaItem MapAssetItem(ImmichAsset asset, string settingsJson, string albumId)
    {
        return new MediaItem
        {
            Id = asset.Id,
            Name = asset.FileName,
            Path = asset.Id,
            IsFolder = false,
            SizeBytes = asset.SizeBytes,
            MimeType = asset.MimeType,
            CreatedAt = asset.CreatedAt.UtcDateTime,
            ModifiedAt = asset.ModifiedAt.UtcDateTime,
            Metadata = new Dictionary<string, string>
            {
                ["kind"] = "asset",
                ["settingsJson"] = settingsJson,
                ["albumId"] = albumId
            }
        };
    }

    private static IEnumerable<MediaItem> ApplyFilters(IEnumerable<MediaItem> items, ExplorerQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            items = items.Where(item => item.Name.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.FileTypeFilter))
        {
            items = items.Where(item => item.IsFolder || MatchesMimeFilter(item.MimeType, query.FileTypeFilter));
        }

        return items;
    }

    private static IOrderedEnumerable<MediaItem> ApplySorting(IEnumerable<MediaItem> items, ExplorerQuery query)
    {
        IOrderedEnumerable<MediaItem> ordered = items.OrderByDescending(item => item.IsFolder);
        return query.SortBy switch
        {
            ExplorerSortField.Name when query.SortDescending => ordered.ThenByDescending(item => item.Name, StringComparer.OrdinalIgnoreCase),
            ExplorerSortField.Name => ordered.ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase),
            ExplorerSortField.Size when query.SortDescending => ordered.ThenByDescending(item => item.SizeBytes),
            ExplorerSortField.Size => ordered.ThenBy(item => item.SizeBytes),
            ExplorerSortField.Date when query.SortDescending => ordered.ThenByDescending(item => item.ModifiedAt ?? DateTime.MinValue),
            _ => ordered.ThenBy(item => item.ModifiedAt ?? DateTime.MinValue)
        };
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

    private static bool TryGetAlbumId(string? folderPath, out string? albumId)
    {
        albumId = null;
        string path = folderPath?.Trim() ?? string.Empty;
        if (!path.StartsWith("album/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        albumId = path["album/".Length..].Trim();
        return !string.IsNullOrWhiteSpace(albumId);
    }

    private static string BuildAlbumPath(string albumId)
    {
        return "album/" + albumId;
    }

    private string ResolveApiKey(string secretKey)
    {
        string apiKey = ResolveSecret(secretKey, "apiKey");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return apiKey;
        }

        return ResolveSecret(secretKey, "apiToken");
    }

    private string ResolveSecret(string secretKey, string secretName)
    {
        if (Secrets == null || string.IsNullOrWhiteSpace(secretKey))
        {
            return string.Empty;
        }

        return Secrets.GetSecret(ProviderId, secretKey, secretName) ?? string.Empty;
    }

    private ImmichClient CreateClient(ImmichConfigModel config)
    {
        return new ImmichClient(config.ServerUrl, ResolveApiKey(config.SecretKey));
    }

    private static ImmichConfigModel DeserializeConfig(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return new ImmichConfigModel();
        }

        return JsonConvert.DeserializeObject<ImmichConfigModel>(settingsJson) ?? new ImmichConfigModel();
    }

    private ImmichConfigModel? ResolveItemConfig(MediaItem item)
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

        return string.IsNullOrWhiteSpace(settingsJson) ? null : DeserializeConfig(settingsJson);
    }

    private static string? ResolveThumbnailAssetId(MediaItem item)
    {
        if (!item.IsFolder)
        {
            return item.Id;
        }

        return item.Metadata.TryGetValue("albumThumbnailAssetId", out string? assetId) && !string.IsNullOrWhiteSpace(assetId)
            ? assetId
            : null;
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
