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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using XerahS.Uploaders.PluginSystem;

namespace ShareX.KovaForge.Plugin.ViewModels;

public partial class KovaForgeConfigViewModel : ObservableObject, IUploaderConfigViewModel, IProviderContextAware
{
    [ObservableProperty]
    private string _serverUrl = KovaForgeConfigModel.DefaultServerUrl;

    [ObservableProperty]
    private bool _textAsMarkdown = true;

    [ObservableProperty]
    private string _apiToken = string.Empty;

    [ObservableProperty]
    private string _tokenSummary = "No upload token is stored.";

    [ObservableProperty]
    private string? _statusMessage;

    private string _secretKey = Guid.NewGuid().ToString("N");
    private ISecretStore? _secrets;

    partial void OnApiTokenChanged(string value)
    {
        TokenSummary = string.IsNullOrWhiteSpace(value)
            ? "No upload token is stored."
            : "An upload token is configured and will be stored securely.";
    }

    [RelayCommand]
    private void ClearStoredToken()
    {
        _secrets?.DeleteSecret("kovaforge", _secretKey, "apiToken");
        ApiToken = string.Empty;
        StatusMessage = "Stored KovaForge upload token was cleared.";
    }

    public void LoadFromJson(string json)
    {
        try
        {
            KovaForgeConfigModel? config = JsonConvert.DeserializeObject<KovaForgeConfigModel>(json);
            if (config == null)
            {
                return;
            }

            _secretKey = string.IsNullOrWhiteSpace(config.SecretKey) ? Guid.NewGuid().ToString("N") : config.SecretKey;
            ServerUrl = string.IsNullOrWhiteSpace(config.ServerUrl) ? KovaForgeConfigModel.DefaultServerUrl : config.ServerUrl;
            TextAsMarkdown = config.TextAsMarkdown;
            LoadTokenFromStore();
            StatusMessage = null;
        }
        catch (JsonException)
        {
            StatusMessage = "Failed to load KovaForge configuration.";
        }
    }

    public string ToJson()
    {
        string normalizedServerUrl = KovaForgeClient.NormalizeServerUrl(ServerUrl);
        if (KovaForgeProvider.IsValidServerUrl(normalizedServerUrl))
        {
            ServerUrl = normalizedServerUrl;
        }

        PersistToken();

        KovaForgeConfigModel config = new()
        {
            SecretKey = _secretKey,
            ServerUrl = ServerUrl,
            TextAsMarkdown = TextAsMarkdown
        };

        return JsonConvert.SerializeObject(config, Formatting.Indented);
    }

    public bool Validate()
    {
        string normalizedServerUrl = KovaForgeClient.NormalizeServerUrl(ServerUrl);
        if (!KovaForgeProvider.IsValidServerUrl(normalizedServerUrl))
        {
            StatusMessage = "KovaForge server URL must be a valid http:// or https:// URL.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ApiToken))
        {
            StatusMessage = "KovaForge upload token is required.";
            return false;
        }

        ServerUrl = normalizedServerUrl;
        PersistToken();
        StatusMessage = null;
        return true;
    }

    public void SetContext(IProviderContext context)
    {
        _secrets = context.Secrets;
        LoadTokenFromStore();
    }

    private void LoadTokenFromStore()
    {
        if (_secrets == null || string.IsNullOrWhiteSpace(_secretKey))
        {
            return;
        }

        ApiToken = _secrets.GetSecret("kovaforge", _secretKey, "apiToken") ?? string.Empty;
    }

    private void PersistToken()
    {
        if (_secrets == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ApiToken))
        {
            _secrets.DeleteSecret("kovaforge", _secretKey, "apiToken");
        }
        else
        {
            _secrets.SetSecret("kovaforge", _secretKey, "apiToken", ApiToken);
        }
    }
}
