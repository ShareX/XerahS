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
using System.Collections.Specialized;
using System.Xml.Linq;
using XerahS.Common;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;
using SysHttpMethod = System.Net.Http.HttpMethod;
using SysHttpClient = System.Net.Http.HttpClient;
using SysHttpRequestMessage = System.Net.Http.HttpRequestMessage;

namespace ShareX.AmazonS3.Plugin;

/// <summary>
/// Amazon S3 file uploader provider (supports Image, Text, and File categories).
/// Also implements <see cref="IUploaderExplorer"/> for the Media Explorer.
/// </summary>
public class AmazonS3Provider : UploaderProviderBase, IUploaderExplorer
{
    public override string ProviderId => "amazons3";
    public override string Name => "Amazon S3";
    public override string Description => "Upload files to Amazon Simple Storage Service (S3)";
    public override Version Version => new Version(1, 0, 0);
    public override UploaderCategory[] SupportedCategories => new[] { UploaderCategory.Image, UploaderCategory.Text, UploaderCategory.File };
    public override Type ConfigModelType => typeof(S3ConfigModel);

    public AmazonS3Provider()
    {
        // For plugins, we don't self-register as they are loaded via PluginLoader
        // But for internal ones we might still want it. 
        // In the external plugin assembly, this ctor will still run if activated.
    }

    public override Uploader CreateInstance(string settingsJson)
    {
        var config = JsonConvert.DeserializeObject<S3ConfigModel>(settingsJson);
        if (config == null)
        {
            throw new InvalidOperationException("Failed to deserialize Amazon S3 settings");
        }

        if (Secrets == null)
        {
            throw new InvalidOperationException("Secret store not available for Amazon S3");
        }

        if (string.IsNullOrWhiteSpace(config.SecretKey))
        {
            throw new InvalidOperationException("Amazon S3 secret key is missing");
        }
        if (config.AuthMode == S3AuthMode.AwsSso)
        {
            return CreateSsoInstance(config);
        }

        string accessKeyId = Secrets.GetSecret(ProviderId, config.SecretKey, "accessKeyId") ?? string.Empty;
        string secretAccessKey = Secrets.GetSecret(ProviderId, config.SecretKey, "secretAccessKey") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(accessKeyId) || string.IsNullOrWhiteSpace(secretAccessKey))
        {
            throw new InvalidOperationException("Amazon S3 credentials are missing");
        }

        return new AmazonS3Uploader(config, accessKeyId, secretAccessKey);
    }

    public override Dictionary<UploaderCategory, string[]> GetSupportedFileTypes()
    {
        // S3 supports all file types for all categories
        var allTypes = new[] {
            "png", "jpg", "jpeg", "gif", "bmp", "tiff", "webp", "svg",  // Common images
            "mp4", "avi", "mov", "mkv", "flv", "wmv", "webm",           // Videos  
            "txt", "log", "json", "xml", "md", "html", "css", "js",     // Text
            "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx",         // Documents
            "zip", "rar", "7z", "tar", "gz",                            // Archives
            "exe", "dll", "so", "dmg", "apk", "ipa"                     // Executables
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
        // Return the Axaml view
        return new Views.AmazonS3ConfigView();
    }

    public override IUploaderConfigViewModel? CreateConfigViewModel()
    {
        return new ViewModels.AmazonS3ConfigViewModel();
    }

    // ─── IUploaderExplorer ───────────────────────────────────────────────────

    private static readonly SysHttpClient _explorerHttpClient = new();

    /// <inheritdoc/>
    public bool SupportsFolders => true;

    /// <inheritdoc/>
    public async Task<ExplorerPage> ListAsync(ExplorerQuery query, CancellationToken cancellation = default)
    {
        var config = DeserializeConfig(query.SettingsJson);
        var (ak, sk, st) = ResolveCredentials(config);

        string region = ResolveRegion(config);
        bool pathStyle = config.UsePathStyleUrl || config.BucketName.Contains('.');
        string endpoint = config.Endpoint.TrimEnd('/');
        string host = pathStyle ? endpoint : $"{config.BucketName}.{endpoint}";

        string prefix = (query.FolderPath ?? "").TrimStart('/');

        // Build query parameters sorted alphabetically for canonical form
        var qp = new SortedDictionary<string, string>
        {
            ["delimiter"] = "/",
            ["list-type"] = "2",
            ["max-keys"] = query.PageSize.ToString()
        };
        if (!string.IsNullOrEmpty(prefix))
            qp["prefix"] = prefix;
        if (!string.IsNullOrEmpty(query.ContinuationToken))
            qp["continuation-token"] = query.ContinuationToken;

        string canonicalQs = BuildCanonicalQueryString(qp);
        string canonicalUri = pathStyle ? $"/{config.BucketName}/" : "/";

        string xml = await SendSignedGetAsync(host, canonicalUri, canonicalQs, region, ak, sk, st, cancellation);
        return ParseListObjectsXml(xml, config);
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetThumbnailAsync(MediaItem item, int maxWidthPx = 180, CancellationToken cancellation = default)
    {
        // Only attempt thumbnails for image files
        string ext = System.IO.Path.GetExtension(item.Name).ToLowerInvariant();
        if (!IsImageExtension(ext)) return null;

        // Don't download files larger than 10 MB for thumbnails
        if (item.SizeBytes > 10 * 1024 * 1024) return null;

        return await DownloadItemBytesAsync(item, cancellation);
    }

    /// <inheritdoc/>
    public async Task<Stream?> GetContentAsync(MediaItem item, CancellationToken cancellation = default)
    {
        var bytes = await DownloadItemBytesAsync(item, cancellation);
        return bytes == null ? null : new MemoryStream(bytes);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(MediaItem item, CancellationToken cancellation = default)
    {
        if (!item.Metadata.TryGetValue("settingsKey", out string? secretKey)) return false;
        if (!item.Metadata.TryGetValue("bucket", out string? bucket)) return false;
        if (!item.Metadata.TryGetValue("endpoint", out string? endpoint)) return false;
        if (!item.Metadata.TryGetValue("region", out string? region)) return false;
        if (!item.Metadata.TryGetValue("pathStyle", out string? pathStyleStr)) return false;

        bool pathStyle = pathStyleStr == "1";
        string ak = Secrets?.GetSecret(ProviderId, secretKey, "accessKeyId") ?? string.Empty;
        string sk = Secrets?.GetSecret(ProviderId, secretKey, "secretAccessKey") ?? string.Empty;
        string? st = Secrets?.GetSecret(ProviderId, secretKey, "sessionToken");

        string host = pathStyle ? endpoint : $"{bucket}.{endpoint}";
        string objectKey = item.Path.TrimStart('/');
        string canonicalUri = pathStyle
            ? $"/{Uri.EscapeDataString(bucket)}/{EscapeS3Key(objectKey)}"
            : $"/{EscapeS3Key(objectKey)}";

        try
        {
            using var request = BuildSignedRequest("DELETE", host, canonicalUri, "", region, ak, sk, st);
            using var response = await _explorerHttpClient.SendAsync(request, cancellation);
            return response.IsSuccessStatusCode
                || response.StatusCode == System.Net.HttpStatusCode.NoContent;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CreateFolderAsync(string parentPath, string folderName, CancellationToken cancellation = default)
    {
        // S3 "folders" are zero-byte objects whose key ends with "/"
        // We need settings to determine the bucket. Without a query context here we cannot
        // resolve them, so callers should pass a MediaItem from the current listing.
        // Return false — the VM drives this via a dedicated command that has instance context.
        await Task.CompletedTask;
        return false;
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    private async Task<byte[]?> DownloadItemBytesAsync(MediaItem item, CancellationToken cancellation)
    {
        if (!item.Metadata.TryGetValue("settingsKey", out string? secretKey)) return null;
        if (!item.Metadata.TryGetValue("bucket", out string? bucket)) return null;
        if (!item.Metadata.TryGetValue("endpoint", out string? endpoint)) return null;
        if (!item.Metadata.TryGetValue("region", out string? region)) return null;
        if (!item.Metadata.TryGetValue("pathStyle", out string? pathStyleStr)) return null;

        bool pathStyle = pathStyleStr == "1";
        string ak = Secrets?.GetSecret(ProviderId, secretKey, "accessKeyId") ?? string.Empty;
        string sk = Secrets?.GetSecret(ProviderId, secretKey, "secretAccessKey") ?? string.Empty;
        string? st = Secrets?.GetSecret(ProviderId, secretKey, "sessionToken");

        string host = pathStyle ? endpoint : $"{bucket}.{endpoint}";
        string objectKey = item.Path.TrimStart('/');
        string canonicalUri = pathStyle
            ? $"/{Uri.EscapeDataString(bucket)}/{EscapeS3Key(objectKey)}"
            : $"/{EscapeS3Key(objectKey)}";

        try
        {
            using var request = BuildSignedRequest("GET", host, canonicalUri, "", region, ak, sk, st);
            using var response = await _explorerHttpClient.SendAsync(request, cancellation);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsByteArrayAsync(cancellation);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> SendSignedGetAsync(string host, string canonicalUri, string canonicalQs,
        string region, string ak, string sk, string? st, CancellationToken cancellation)
    {
        using var request = BuildSignedRequest("GET", host, canonicalUri, canonicalQs, region, ak, sk, st);
        try
        {
            using var response = await _explorerHttpClient.SendAsync(request, cancellation);
            return await response.Content.ReadAsStringAsync(cancellation);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static SysHttpRequestMessage BuildSignedRequest(string method, string host,
        string canonicalUri, string canonicalQs, string region, string ak, string sk, string? st)
    {
        string emptyHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        var sigHeaders = new NameValueCollection { ["Host"] = host };
        AwsS3Signer.Sign(sigHeaders, method, canonicalUri, canonicalQs, region, ak, sk, st, emptyHash);

        string sep = string.IsNullOrEmpty(canonicalQs) ? "" : "?";
        string url = $"https://{host}{canonicalUri}{sep}{canonicalQs}";

        var request = new SysHttpRequestMessage(new SysHttpMethod(method), url);
        foreach (string? key in sigHeaders.AllKeys)
        {
            if (key == null || key.Equals("Host", StringComparison.OrdinalIgnoreCase)) continue;
            request.Headers.TryAddWithoutValidation(key, sigHeaders[key]);
        }
        return request;
    }

    private ExplorerPage ParseListObjectsXml(string xml, S3ConfigModel config)
    {
        if (string.IsNullOrWhiteSpace(xml)) return new ExplorerPage();

        try
        {
            XNamespace ns = "http://s3.amazonaws.com/doc/2006-03-01/";
            var doc = XDocument.Parse(xml);
            var root = doc.Root;
            if (root == null) return new ExplorerPage();

            bool pathStyle = config.UsePathStyleUrl || config.BucketName.Contains('.');
            string endpoint = config.Endpoint.TrimEnd('/');

            var items = new List<MediaItem>();

            // Folders: CommonPrefixes
            foreach (var cp in root.Elements(ns + "CommonPrefixes"))
            {
                string pfx = cp.Element(ns + "Prefix")?.Value ?? "";
                string name = pfx.TrimEnd('/').Split('/').LastOrDefault() ?? pfx;
                items.Add(new MediaItem
                {
                    Id = pfx,
                    Name = name,
                    Path = pfx,
                    IsFolder = true,
                    Metadata = BuildMetadata(config)
                });
            }

            // Files: Contents
            foreach (var content in root.Elements(ns + "Contents"))
            {
                string key = content.Element(ns + "Key")?.Value ?? "";
                if (key.EndsWith('/')) continue; // folder marker

                string name = key.Split('/').LastOrDefault() ?? key;
                long size = long.TryParse(content.Element(ns + "Size")?.Value, out long s) ? s : 0;
                DateTime? modified = DateTime.TryParse(
                    content.Element(ns + "LastModified")?.Value,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out DateTime dt) ? dt : null;

                string mime = MimeTypes.GetMimeTypeFromFileName(name);
                string? url = BuildPublicUrl(config, key, pathStyle, endpoint);

                items.Add(new MediaItem
                {
                    Id = key,
                    Name = name,
                    Path = key,
                    SizeBytes = size,
                    ModifiedAt = modified,
                    MimeType = mime,
                    Url = url,
                    Metadata = BuildMetadata(config)
                });
            }

            string? nextToken = root.Element(ns + "NextContinuationToken")?.Value;
            return new ExplorerPage
            {
                Items = items,
                ContinuationToken = string.IsNullOrEmpty(nextToken) ? null : nextToken
            };
        }
        catch
        {
            return new ExplorerPage();
        }
    }

    private Dictionary<string, string> BuildMetadata(S3ConfigModel config)
    {
        bool pathStyle = config.UsePathStyleUrl || config.BucketName.Contains('.');
        return new Dictionary<string, string>
        {
            ["settingsKey"] = config.SecretKey,
            ["bucket"] = config.BucketName,
            ["endpoint"] = config.Endpoint.TrimEnd('/'),
            ["region"] = ResolveRegion(config),
            ["pathStyle"] = pathStyle ? "1" : "0"
        };
    }

    private static string? BuildPublicUrl(S3ConfigModel config, string key, bool pathStyle, string endpoint)
    {
        if (!config.SetPublicACL && !config.UseCustomCNAME) return null;

        string encodedKey = EscapeS3Key(key);
        if (config.UseCustomCNAME && !string.IsNullOrEmpty(config.CustomDomain))
            return $"https://{config.CustomDomain.TrimEnd('/')}/{encodedKey}";

        return pathStyle
            ? $"https://{endpoint}/{config.BucketName}/{encodedKey}"
            : $"https://{config.BucketName}.{endpoint}/{encodedKey}";
    }

    private S3ConfigModel DeserializeConfig(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson)) return new S3ConfigModel();
        return JsonConvert.DeserializeObject<S3ConfigModel>(settingsJson) ?? new S3ConfigModel();
    }

    private (string ak, string sk, string? st) ResolveCredentials(S3ConfigModel config)
    {
        if (Secrets == null) return (string.Empty, string.Empty, null);

        if (config.AuthMode == S3AuthMode.AwsSso)
        {
            var creds = AwsSsoSecretStore.LoadRoleCredentials(Secrets, config.SecretKey);
            if (creds != null && !creds.IsExpired())
                return (creds.AccessKeyId, creds.SecretAccessKey, creds.SessionToken);
            return (string.Empty, string.Empty, null);
        }

        string ak = Secrets.GetSecret(ProviderId, config.SecretKey, "accessKeyId") ?? string.Empty;
        string sk = Secrets.GetSecret(ProviderId, config.SecretKey, "secretAccessKey") ?? string.Empty;
        return (ak, sk, null);
    }

    private static string ResolveRegion(S3ConfigModel config)
    {
        if (!string.IsNullOrEmpty(config.Region)) return config.Region;

        string url = config.Endpoint;
        if (url.Contains("//")) url = url.Split(new[] { "//" }, StringSplitOptions.None)[1];
        url = url.TrimEnd('/');
        if (!url.Contains(".amazonaws.com")) return "us-east-1";

        string serviceAndRegion = url.Split(new[] { ".amazonaws.com" }, StringSplitOptions.None)[0];
        if (serviceAndRegion.StartsWith("s3-"))
            serviceAndRegion = "s3." + serviceAndRegion[3..];

        int sep = serviceAndRegion.LastIndexOf('.');
        return sep == -1 ? "us-east-1" : serviceAndRegion[(sep + 1)..];
    }

    private static string BuildCanonicalQueryString(SortedDictionary<string, string> qp)
    {
        return string.Join("&", qp.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    private static string EscapeS3Key(string key)
    {
        // Encode each segment individually, preserving "/"
        return string.Join("/", key.Split('/').Select(Uri.EscapeDataString));
    }

    private static bool IsImageExtension(string ext)
    {
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".tiff";
    }

    private AmazonS3Uploader CreateSsoInstance(S3ConfigModel config)
    {
        if (Secrets == null)
        {
            throw new InvalidOperationException("Secret store not available for Amazon S3");
        }

        if (string.IsNullOrWhiteSpace(config.SsoRegion))
        {
            throw new InvalidOperationException("SSO region is required for Amazon S3.");
        }

        AwsSsoStoredToken? token = AwsSsoSecretStore.LoadToken(Secrets, config.SecretKey);
        if (token == null)
        {
            throw new InvalidOperationException("SSO login required for Amazon S3.");
        }

        if (token.IsExpired())
        {
            AwsSsoStoredClient? client = AwsSsoSecretStore.LoadClient(Secrets, config.SecretKey);
            if (client == null || client.IsExpired())
            {
                throw new InvalidOperationException("SSO login required for Amazon S3.");
            }

            if (string.IsNullOrWhiteSpace(token.RefreshToken))
            {
                throw new InvalidOperationException("SSO session expired. Please login again.");
            }

            var oidc = new AwsSsoOidcClient(config.SsoRegion);
            token = oidc.RefreshToken(client, token.RefreshToken);
            AwsSsoSecretStore.SaveToken(Secrets, config.SecretKey, token);
        }

        if (string.IsNullOrWhiteSpace(config.SsoAccountId) || string.IsNullOrWhiteSpace(config.SsoRoleName))
        {
            throw new InvalidOperationException("SSO account and role must be selected for Amazon S3.");
        }

        AwsSsoStoredRoleCredentials? creds = AwsSsoSecretStore.LoadRoleCredentials(Secrets, config.SecretKey);
        if (creds == null || creds.IsExpired())
        {
            var ssoClient = new AwsSsoClient(config.SsoRegion);
            creds = ssoClient.GetRoleCredentials(token.AccessToken, config.SsoAccountId, config.SsoRoleName);
            AwsSsoSecretStore.SaveRoleCredentials(Secrets, config.SecretKey, creds);
        }

        if (string.IsNullOrWhiteSpace(creds.AccessKeyId) || string.IsNullOrWhiteSpace(creds.SecretAccessKey))
        {
            throw new InvalidOperationException("SSO role credentials are missing.");
        }

        return new AmazonS3Uploader(config, creds.AccessKeyId, creds.SecretAccessKey, creds.SessionToken);
    }
}
