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

namespace ShareX.AmazonS3.Plugin;

/// <summary>
/// Amazon S3 file uploader provider (supports Image, Text, and File categories)
/// </summary>
public class AmazonS3Provider : UploaderProviderBase
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
