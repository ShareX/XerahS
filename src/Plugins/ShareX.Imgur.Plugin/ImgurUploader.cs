#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2025 ShareX Team

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
using ShareX.Ava.Common;
using ShareX.Ava.Uploaders;
using ShareX.Ava.Uploaders.ImageUploaders;
using System.Collections.Specialized;

namespace ShareX.Imgur.Plugin;

/// <summary>
/// Imgur uploader - supports anonymous and authenticated uploads
/// </summary>
public class ImgurUploader : ImageUploader, IOAuth2
{
    private readonly ImgurConfigModel _config;

    public OAuth2Info AuthInfo => _config.OAuth2Info ??= new OAuth2Info(_config.ClientId, "98871f37e179e496a0149e9c8558487779d424ft"); // Placeholder secret if not provided

    public ImgurUploader(ImgurConfigModel config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public string GetAuthorizationURL()
    {
        var args = new Dictionary<string, string>
        {
            ["client_id"] = AuthInfo.Client_ID,
            ["response_type"] = "pin"
        };

        return URLHelpers.CreateQueryString("https://api.imgur.com/oauth2/authorize", args);
    }

    public bool GetAccessToken(string pin)
    {
        var args = new Dictionary<string, string>
        {
            ["client_id"] = AuthInfo.Client_ID,
            ["client_secret"] = AuthInfo.Client_Secret,
            ["grant_type"] = "pin",
            ["pin"] = pin
        };

        string response = SendRequestMultiPart("https://api.imgur.com/oauth2/token", args);

        if (!string.IsNullOrEmpty(response))
        {
            var token = JsonConvert.DeserializeObject<OAuth2Token>(response);

            if (token != null && !string.IsNullOrEmpty(token.access_token))
            {
                token.UpdateExpireDate();
                AuthInfo.Token = token;
                return true;
            }
        }

        return false;
    }

    public bool RefreshAccessToken()
    {
        if (OAuth2Info.CheckOAuth(AuthInfo) && !string.IsNullOrEmpty(AuthInfo.Token.refresh_token))
        {
            var args = new Dictionary<string, string>
            {
                ["refresh_token"] = AuthInfo.Token.refresh_token,
                ["client_id"] = AuthInfo.Client_ID,
                ["client_secret"] = AuthInfo.Client_Secret,
                ["grant_type"] = "refresh_token"
            };

            string response = SendRequestMultiPart("https://api.imgur.com/oauth2/token", args);

            if (!string.IsNullOrEmpty(response))
            {
                var token = JsonConvert.DeserializeObject<OAuth2Token>(response);

                if (token != null && !string.IsNullOrEmpty(token.access_token))
                {
                    token.UpdateExpireDate();
                    AuthInfo.Token = token;
                    return true;
                }
            }
        }

        return false;
    }

    public bool CheckAuthorization()
    {
        if (OAuth2Info.CheckOAuth(AuthInfo))
        {
            if (AuthInfo.Token.IsExpired && !RefreshAccessToken())
            {
                Errors.Add("Refresh access token failed.");
                return false;
            }
        }
        else
        {
            Errors.Add("Imgur login is required.");
            return false;
        }

        return true;
    }

    private NameValueCollection GetAuthHeaders()
    {
        return new NameValueCollection
        {
            ["Authorization"] = "Bearer " + AuthInfo.Token.access_token
        };
    }

    public List<ImgurAlbumData> GetAlbums(int maxPage = 10, int perPage = 100)
    {
        List<ImgurAlbumData> albums = new List<ImgurAlbumData>();

        if (CheckAuthorization())
        {
            for (int i = 0; i < maxPage; i++)
            {
                var args = new Dictionary<string, string>
                {
                    ["page"] = i.ToString(),
                    ["perPage"] = perPage.ToString()
                };

                string response = SendRequest(ShareX.Ava.Uploaders.HttpMethod.GET, "https://api.imgur.com/3/account/me/albums", args, GetAuthHeaders());
                var imgurResponse = JsonConvert.DeserializeObject<ImgurResponse>(response);

                if (imgurResponse != null && imgurResponse.success && imgurResponse.status == 200)
                {
                    var tempAlbums = JsonConvert.DeserializeObject<List<ImgurAlbumData>>(imgurResponse.data?.ToString() ?? "[]") ?? new List<ImgurAlbumData>();
                    albums.AddRange(tempAlbums);

                    if (tempAlbums.Count < perPage)
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        return albums;
    }

    public override UploadResult Upload(Stream stream, string fileName)
    {
        return InternalUpload(stream, fileName, true);
    }

    private UploadResult InternalUpload(Stream stream, string fileName, bool refreshTokenOnError)
    {
        var args = new Dictionary<string, string>();
        NameValueCollection headers;

        if (_config.AccountType == AccountType.User)
        {
            if (!CheckAuthorization())
            {
                return null;
            }

            if (_config.UploadToSelectedAlbum && _config.SelectedAlbum != null && !string.IsNullOrEmpty(_config.SelectedAlbum.id))
            {
                args.Add("album", _config.SelectedAlbum.id);
            }

            headers = GetAuthHeaders();
        }
        else
        {
            headers = new NameValueCollection
            {
                ["Authorization"] = "Client-ID " + _config.ClientId
            };
        }

        ReturnResponseOnError = true;

        string fileFormName = IsVideoFile(fileName) ? "video" : "image";

        UploadResult result = SendRequestFile("https://api.imgur.com/3/upload", stream, fileName, fileFormName, args, headers);

        if (!string.IsNullOrEmpty(result.Response))
        {
            try
            {
                var response = JsonConvert.DeserializeObject<ImgurResponse>(result.Response);

                if (response?.success == true && response.status == 200 && response.data != null)
                {
                    var imageData = JsonConvert.DeserializeObject<ImgurImageData>(response.data.ToString() ?? "");

                    if (imageData != null && !string.IsNullOrEmpty(imageData.link))
                    {
                        if (_config.DirectLink)
                        {
                            if (_config.UseGIFV && !string.IsNullOrEmpty(imageData.gifv))
                            {
                                result.URL = imageData.gifv;
                            }
                            else
                            {
                                // webm uploads returns link with dot at the end
                                result.URL = imageData.link.TrimEnd('.');
                            }
                        }
                        else
                        {
                            result.URL = $"https://imgur.com/{imageData.id}";
                        }

                        string thumbnail = _config.ThumbnailType switch
                        {
                            ImgurThumbnailType.Small_Square => "s",
                            ImgurThumbnailType.Big_Square => "b",
                            ImgurThumbnailType.Small_Thumbnail => "t",
                            ImgurThumbnailType.Medium_Thumbnail => "m",
                            ImgurThumbnailType.Large_Thumbnail => "l",
                            ImgurThumbnailType.Huge_Thumbnail => "h",
                            _ => "m"
                        };

                        result.ThumbnailURL = $"https://i.imgur.com/{imageData.id}{thumbnail}.jpg";
                        result.DeletionURL = $"https://imgur.com/delete/{imageData.deletehash}";
                        result.IsSuccess = true;
                    }
                }
                else if (response != null)
                {
                    var errorData = ParseError(response);
                    
                    if (_config.AccountType == AccountType.User && refreshTokenOnError && 
                        errorData?.error?.ToString()?.Equals("The access token provided is invalid.", StringComparison.OrdinalIgnoreCase) == true && 
                        RefreshAccessToken())
                    {
                        DebugHelper.WriteLine("Imgur access token refreshed, reuploading image.");
                        return InternalUpload(stream, fileName, false);
                    }

                    Errors.Add($"Imgur upload failed: ({response.status}) {errorData?.error ?? "Unknown error"}");
                }
            }
            catch (Exception ex)
            {
                Errors.Add($"Imgur response parsing failed: {ex.Message}");
            }
        }

        return result;
    }

    private bool IsVideoFile(string fileName)
    {
        string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        return new[] { ".mp4", ".avi", ".mov", ".mkv", ".flv", ".wmv", ".webm" }.Contains(ext);
    }

    private ImgurErrorData? ParseError(ImgurResponse response)
    {
        if (response.data == null) return null;
        
        var errorData = JsonConvert.DeserializeObject<ImgurErrorData>(response.data.ToString() ?? "");
        
        if (errorData != null && errorData.error != null && errorData.error is not string)
        {
            var imgurError = JsonConvert.DeserializeObject<ImgurError>(errorData.error.ToString() ?? "");
            if (imgurError != null)
            {
                errorData.error = imgurError.message;
            }
        }

        return errorData;
    }

    private class ImgurResponse
    {
        public object? data { get; set; }
        public bool success { get; set; }
        public int status { get; set; }
    }

    private class ImgurImageData
    {
        public string? id { get; set; }
        public string? link { get; set; }
        public string? gifv { get; set; }
        public string? deletehash { get; set; }
    }

    private class ImgurErrorData
    {
        public object? error { get; set; }
        public string? request { get; set; }
    }

    private class ImgurError
    {
        public string? message { get; set; }
    }
}
