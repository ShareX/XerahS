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
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ShareX.GitHubGist.Plugin.ViewModels;

/// <summary>
/// ViewModel for GitHub Gist configuration
/// </summary>
public partial class GitHubGistConfigViewModel : ObservableObject, IUploaderConfigViewModel, IProviderContextAware
{
    [ObservableProperty]
    private string _clientId = string.Empty;

    [ObservableProperty]
    private string _clientSecret = string.Empty;

    [ObservableProperty]
    private bool _publicUpload = false;

    [ObservableProperty]
    private bool _rawUrl = false;

    [ObservableProperty]
    private string _customApiUrl = string.Empty;

    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string? _statusMessage;

    private string _secretKey = Guid.NewGuid().ToString("N");
    private ISecretStore? _secrets;

    private GitHubGistUploader? _uploader;
    private GitHubGistConfigModel _config = new();

    public GitHubGistConfigViewModel()
    {
        _uploader = null;
    }

    [RelayCommand]
    private void OpenLoginUrl()
    {
        EnsureConfigFromFields(resetToken: true);

        if (_uploader == null)
        {
            _uploader = BuildUploader();
        }

        string url = _uploader.GetAuthorizationURL();
        if (string.IsNullOrWhiteSpace(url))
        {
            StatusMessage = "Client ID is required to open the login URL.";
            return;
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to open browser: " + ex.Message;
        }
    }

    [RelayCommand]
    private void CompleteLogin()
    {
        EnsureConfigFromFields(resetToken: false);

        if (_uploader == null)
        {
            _uploader = BuildUploader();
        }

        if (string.IsNullOrWhiteSpace(Code))
        {
            StatusMessage = "Please enter the code from the GitHub callback URL.";
            return;
        }

        if (_uploader.GetAccessToken(Code))
        {
            IsLoggedIn = true;
            StatusMessage = "Logged in successfully.";
            Code = string.Empty;
            PersistToken();
        }
        else
        {
            StatusMessage = "Login failed. Please verify the code and credentials.";
        }
    }

    [RelayCommand]
    private void ClearLogin()
    {
        EnsureConfigFromFields(resetToken: true);
        IsLoggedIn = false;
        StatusMessage = "Login cleared.";
    }

    public void LoadFromJson(string json)
    {
        try
        {
            var config = JsonConvert.DeserializeObject<GitHubGistConfigModel>(json);
            if (config != null)
            {
                _config = config;
                _secretKey = string.IsNullOrWhiteSpace(_config.SecretKey) ? Guid.NewGuid().ToString("N") : _config.SecretKey;
                _uploader = BuildUploader();

                ClientId = _secrets?.GetSecret("gist", _secretKey, "clientId") ?? string.Empty;
                ClientSecret = _secrets?.GetSecret("gist", _secretKey, "clientSecret") ?? string.Empty;
                PublicUpload = _config.PublicUpload;
                RawUrl = _config.RawURL;
                CustomApiUrl = _config.CustomURLAPI ?? string.Empty;

                IsLoggedIn = HasToken();
            }
        }
        catch
        {
            StatusMessage = "Failed to load configuration";
        }
    }

    public string ToJson()
    {
        EnsureConfigFromFields(resetToken: false);

        PersistCredentials();

        return JsonConvert.SerializeObject(_config, Formatting.Indented);
    }

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            StatusMessage = "Client ID is required";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ClientSecret))
        {
            StatusMessage = "Client Secret is required";
            return false;
        }

        if (!IsLoggedIn)
        {
            StatusMessage = "Login is required";
            return false;
        }

        PersistCredentials();
        StatusMessage = null;
        return true;
    }

    private void EnsureConfigFromFields(bool resetToken)
    {
        _config.SecretKey = _secretKey;
        _config.PublicUpload = PublicUpload;
        _config.RawURL = RawUrl;
        _config.CustomURLAPI = CustomApiUrl ?? string.Empty;
        if (resetToken)
        {
            _secrets?.DeleteSecret("gist", _secretKey, "oauthToken");
            IsLoggedIn = false;
        }
    }

    public void SetContext(IProviderContext context)
    {
        _secrets = context.Secrets;
    }

    private GitHubGistUploader BuildUploader()
    {
        var authInfo = new OAuth2Info(ClientId ?? string.Empty, ClientSecret ?? string.Empty);
        var tokenJson = _secrets?.GetSecret("gist", _secretKey, "oauthToken");
        if (!string.IsNullOrWhiteSpace(tokenJson))
        {
            var token = JsonConvert.DeserializeObject<OAuth2Token>(tokenJson);
            if (token != null)
            {
                authInfo.Token = token;
            }
        }

        return new GitHubGistUploader(_config, authInfo);
    }

    private void PersistCredentials()
    {
        if (_secrets == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ClientId))
        {
            _secrets.DeleteSecret("gist", _secretKey, "clientId");
        }
        else
        {
            _secrets.SetSecret("gist", _secretKey, "clientId", ClientId);
        }

        if (string.IsNullOrWhiteSpace(ClientSecret))
        {
            _secrets.DeleteSecret("gist", _secretKey, "clientSecret");
        }
        else
        {
            _secrets.SetSecret("gist", _secretKey, "clientSecret", ClientSecret);
        }
    }

    private void PersistToken()
    {
        if (_secrets == null || _uploader == null)
        {
            return;
        }

        if (_uploader.AuthInfo.Token != null && !string.IsNullOrEmpty(_uploader.AuthInfo.Token.access_token))
        {
            var json = JsonConvert.SerializeObject(_uploader.AuthInfo.Token, Formatting.None);
            _secrets.SetSecret("gist", _secretKey, "oauthToken", json);
        }
    }

    private bool HasToken()
    {
        if (_secrets == null)
        {
            return false;
        }

        var tokenJson = _secrets.GetSecret("gist", _secretKey, "oauthToken");
        if (string.IsNullOrWhiteSpace(tokenJson))
        {
            return false;
        }

        var token = JsonConvert.DeserializeObject<OAuth2Token>(tokenJson);
        return token != null && !string.IsNullOrEmpty(token.access_token);
    }
}
