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

namespace ShareX.XBackBone.Plugin;

public sealed class XBackBoneProvider : UploaderProviderBase, IInstanceSecretMigrator, IInstanceSecretBackupProvider
{
    public override string ProviderId => "xbackbone";
    public override string Name => "XBackBone";
    public override string Description => "Upload images, text, and files using the native XBackBone API";
    public override Version Version => new(1, 0, 0);
    public override UploaderCategory[] SupportedCategories =>
        new[] { UploaderCategory.Image, UploaderCategory.Text, UploaderCategory.File };
    public override Type ConfigModelType => typeof(XBackBoneConfigModel);
    public override UploaderCapabilities Capabilities =>
        UploaderCapabilities.Cancellation | UploaderCapabilities.Progress;

    public IReadOnlyList<InstanceSecretReference> GetSecretReferences(string settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return Array.Empty<InstanceSecretReference>();
        }

        string? secretKey;
        try
        {
            secretKey = JObject.Parse(settingsJson).Value<string>(nameof(XBackBoneConfigModel.SecretKey));
        }
        catch (JsonException)
        {
            return Array.Empty<InstanceSecretReference>();
        }

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return Array.Empty<InstanceSecretReference>();
        }

        return [new(ProviderId, secretKey, "apiToken")];
    }

    public override Uploader CreateInstance(string settingsJson)
    {
        XBackBoneConfigModel config = DeserializeConfig(settingsJson);
        return new XBackBoneUploader(config, ResolveApiToken(config.SecretKey));
    }

    public override bool ValidateSettings(string settingsJson)
    {
        XBackBoneConfigModel config;
        try
        {
            config = DeserializeConfig(settingsJson);
        }
        catch (JsonException)
        {
            return false;
        }

        string normalizedServerUrl = XBackBoneClient.NormalizeServerUrl(config.ServerUrl);
        return IsValidServerUrl(normalizedServerUrl) &&
            Enum.IsDefined(config.ApiGeneration) &&
            !string.IsNullOrWhiteSpace(ResolveApiToken(config.SecretKey));
    }

    public override Dictionary<UploaderCategory, string[]> GetSupportedFileTypes()
    {
        string[] allTypes =
        {
            "png", "jpg", "jpeg", "gif", "bmp", "tiff", "webp", "svg", "avif", "jxl", "heic",
            "txt", "log", "json", "xml", "md", "html", "css", "js", "csv",
            "mp4", "avi", "mov", "mkv", "webm", "mp3", "wav", "flac", "ogg",
            "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx",
            "zip", "rar", "7z", "tar", "gz"
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
        return new Views.XBackBoneConfigView();
    }

    public override IUploaderConfigViewModel? CreateConfigViewModel()
    {
        return new ViewModels.XBackBoneConfigViewModel();
    }

    public bool TryMigrateSecrets(
        string settingsJson,
        ISecretStore secrets,
        out string updatedSettingsJson,
        out int migratedSecretCount)
    {
        updatedSettingsJson = settingsJson;
        migratedSecretCount = 0;

        JObject json;
        try
        {
            json = JObject.Parse(settingsJson);
        }
        catch (JsonException)
        {
            return false;
        }

        string secretKey = json.Value<string>(nameof(XBackBoneConfigModel.SecretKey)) ?? Guid.NewGuid().ToString("N");
        bool changed = false;

        if (!string.Equals(json.Value<string>(nameof(XBackBoneConfigModel.SecretKey)), secretKey, StringComparison.Ordinal))
        {
            json[nameof(XBackBoneConfigModel.SecretKey)] = secretKey;
            changed = true;
        }

        string? apiToken = json.Value<string>("ApiToken");
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            apiToken = json.Value<string>("Token");
        }

        if (!string.IsNullOrWhiteSpace(apiToken))
        {
            secrets.SetSecret(ProviderId, secretKey, "apiToken", apiToken);
            migratedSecretCount = 1;
        }

        changed |= json.Remove("ApiToken");
        changed |= json.Remove("Token");

        if (changed)
        {
            updatedSettingsJson = json.ToString(Formatting.Indented);
        }

        return changed;
    }

    internal static bool IsValidServerUrl(string serverUrl)
    {
        return Uri.TryCreate(serverUrl, UriKind.Absolute, out Uri? uri) &&
            (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveApiToken(string secretKey)
    {
        if (Secrets == null || string.IsNullOrWhiteSpace(secretKey))
        {
            return string.Empty;
        }

        return Secrets.GetSecret(ProviderId, secretKey, "apiToken") ?? string.Empty;
    }

    private static XBackBoneConfigModel DeserializeConfig(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return new XBackBoneConfigModel();
        }

        return JsonConvert.DeserializeObject<XBackBoneConfigModel>(settingsJson) ?? new XBackBoneConfigModel();
    }
}
