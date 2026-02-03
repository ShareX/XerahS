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

namespace ShareX.GitHubGist.Plugin;

/// <summary>
/// GitHub Gist text uploader provider (supports Text category only)
/// </summary>
public class GitHubGistProvider : UploaderProviderBase
{
    public override string ProviderId => "gist";
    public override string Name => "GitHub Gist";
    public override string Description => "Upload text to GitHub Gist";
    public override Version Version => new Version(1, 0, 0);
    public override UploaderCategory[] SupportedCategories => new[] { UploaderCategory.Text };
    public override Type ConfigModelType => typeof(GitHubGistConfigModel);

    public override Uploader CreateInstance(string settingsJson)
    {
        var config = JsonConvert.DeserializeObject<GitHubGistConfigModel>(settingsJson);
        if (config == null)
        {
            throw new InvalidOperationException("Failed to deserialize GitHub Gist settings");
        }

        if (Secrets == null)
        {
            throw new InvalidOperationException("Secret store not available for GitHub Gist");
        }

        if (string.IsNullOrWhiteSpace(config.SecretKey))
        {
            throw new InvalidOperationException("GitHub Gist secret key is missing");
        }

        string clientId = Secrets.GetSecret(ProviderId, config.SecretKey, "clientId") ?? string.Empty;
        string clientSecret = Secrets.GetSecret(ProviderId, config.SecretKey, "clientSecret") ?? string.Empty;
        string? tokenJson = Secrets.GetSecret(ProviderId, config.SecretKey, "oauthToken");

        var authInfo = new OAuth2Info(clientId, clientSecret);
        if (!string.IsNullOrWhiteSpace(tokenJson))
        {
            var token = JsonConvert.DeserializeObject<OAuth2Token>(tokenJson);
            if (token != null)
            {
                authInfo.Token = token;
            }
        }

        return new GitHubGistUploader(config, authInfo);
    }

    public override Dictionary<UploaderCategory, string[]> GetSupportedFileTypes()
    {
        return new Dictionary<UploaderCategory, string[]>
        {
            { UploaderCategory.Text, new[] { "txt", "log", "json", "xml", "md", "html", "css", "js" } }
        };
    }

    public override object? CreateConfigView()
    {
        return new Views.GitHubGistConfigView();
    }

    public override IUploaderConfigViewModel? CreateConfigViewModel()
    {
        return new ViewModels.GitHubGistConfigViewModel();
    }
}
