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
using XerahS.Common;
using XerahS.Uploaders;
using System.Collections.Specialized;
using System.Linq;

namespace ShareX.GitHubGist.Plugin;

/// <summary>
/// GitHub Gist uploader - supports public or secret gists
/// </summary>
public sealed class GitHubGistUploader : TextUploader, IOAuth2Basic
{
    private const string DefaultApiUrl = "https://api.github.com";
    private readonly GitHubGistConfigModel _config;

    public OAuth2Info AuthInfo => _config.OAuth2Info ??= new OAuth2Info(string.Empty, string.Empty);

    public GitHubGistUploader(GitHubGistConfigModel config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public string GetAuthorizationURL()
    {
        if (string.IsNullOrWhiteSpace(AuthInfo.Client_ID))
        {
            Errors.Add("Client ID is required for GitHub Gist authentication.");
            return string.Empty;
        }

        Dictionary<string, string> args = new Dictionary<string, string>
        {
            ["client_id"] = AuthInfo.Client_ID,
            ["redirect_uri"] = Links.Callback,
            ["scope"] = "gist"
        };

        return URLHelpers.CreateQueryString("https://github.com/login/oauth/authorize", args);
    }

    public bool GetAccessToken(string code)
    {
        if (string.IsNullOrWhiteSpace(AuthInfo.Client_ID) || string.IsNullOrWhiteSpace(AuthInfo.Client_Secret))
        {
            Errors.Add("Client ID and Client Secret are required.");
            return false;
        }

        Dictionary<string, string> args = new Dictionary<string, string>
        {
            ["client_id"] = AuthInfo.Client_ID,
            ["client_secret"] = AuthInfo.Client_Secret,
            ["code"] = code
        };

        NameValueCollection headers = new NameValueCollection
        {
            ["Accept"] = "application/json"
        };

        string? response = SendRequestMultiPart("https://github.com/login/oauth/access_token", args, headers);

        if (!string.IsNullOrEmpty(response))
        {
            OAuth2Token? token = JsonConvert.DeserializeObject<OAuth2Token>(response);

            if (token != null && !string.IsNullOrEmpty(token.access_token))
            {
                AuthInfo.Token = token;
                return true;
            }
        }

        return false;
    }

    public override UploadResult UploadText(string text, string fileName)
    {
        UploadResult result = new UploadResult();

        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(fileName))
        {
            return result;
        }

        if (!OAuth2Info.CheckOAuth(AuthInfo))
        {
            Errors.Add("GitHub Gist login is required.");
            return result;
        }

        string apiBase = string.IsNullOrWhiteSpace(_config.CustomURLAPI) ? DefaultApiUrl : _config.CustomURLAPI.Trim();
        string url = URLHelpers.CombineURL(apiBase, "gists");

        GistUpload gistUpload = new GistUpload
        {
            description = string.Empty,
            @public = _config.PublicUpload,
            files = new Dictionary<string, GistUploadFileInfo>
            {
                [fileName] = new GistUploadFileInfo { content = text }
            }
        };

        string json = JsonConvert.SerializeObject(gistUpload);

        NameValueCollection headers = new NameValueCollection
        {
            ["Authorization"] = "token " + AuthInfo.Token.access_token
        };

        string? response = SendRequest(XerahS.Uploaders.HttpMethod.POST, url, json, "application/json", null, headers);

        if (string.IsNullOrEmpty(response))
        {
            return result;
        }

        GistResponse? gistResponse = JsonConvert.DeserializeObject<GistResponse>(response);
        if (gistResponse == null)
        {
            return result;
        }

        if (_config.RawURL)
        {
            var firstFile = gistResponse.files?.Values.FirstOrDefault();
            result.URL = firstFile?.raw_url ?? string.Empty;
        }
        else
        {
            result.URL = gistResponse.html_url ?? string.Empty;
        }

        return result;
    }

    private class GistUpload
    {
        public string description { get; set; } = string.Empty;
        public bool @public { get; set; }
        public Dictionary<string, GistUploadFileInfo> files { get; set; } = new();
    }

    private class GistUploadFileInfo
    {
        public string content { get; set; } = string.Empty;
    }

    private class GistResponse
    {
        public string? html_url { get; set; }
        public Dictionary<string, GistResponseFileInfo>? files { get; set; }
    }

    private class GistResponseFileInfo
    {
        public string? filename { get; set; }
        public string? raw_url { get; set; }
    }
}
