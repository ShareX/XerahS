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
using UploadersHttpMethod = XerahS.Uploaders.HttpMethod;

namespace ShareX.AmazonS3.Plugin;

internal sealed class AwsSsoOidcClient : AwsApiClientBase
{
    private readonly string _baseUrl;

    public AwsSsoOidcClient(string region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            throw new ArgumentException("Region is required.", nameof(region));
        }

        _baseUrl = $"https://oidc.{region}.amazonaws.com";
    }

    public AwsSsoStoredClient RegisterClient(string clientName)
    {
        var payload = new Dictionary<string, object>
        {
            ["clientName"] = string.IsNullOrWhiteSpace(clientName) ? "XerahS" : clientName,
            ["clientType"] = "public"
        };

        string json = JsonConvert.SerializeObject(payload);
        AwsApiResponse response = SendJsonRequest(UploadersHttpMethod.POST, $"{_baseUrl}/client/register", json, null, "application/json");

        if (!response.IsSuccess || string.IsNullOrWhiteSpace(response.Body))
        {
            throw new InvalidOperationException($"SSO client registration failed ({response.StatusCode}). {response.Body}");
        }

        AwsSsoOidcRegisterResponse? register = JsonConvert.DeserializeObject<AwsSsoOidcRegisterResponse>(response.Body);
        if (register == null || string.IsNullOrWhiteSpace(register.ClientId) || string.IsNullOrWhiteSpace(register.ClientSecret))
        {
            throw new InvalidOperationException("SSO client registration response was invalid.");
        }

        return new AwsSsoStoredClient
        {
            ClientId = register.ClientId,
            ClientSecret = register.ClientSecret,
            ClientSecretExpiresAt = register.ClientSecretExpiresAt
        };
    }

    public AwsSsoOidcDeviceAuthorizationResponse StartDeviceAuthorization(AwsSsoStoredClient client, string startUrl)
    {
        var payload = new Dictionary<string, object>
        {
            ["clientId"] = client.ClientId,
            ["clientSecret"] = client.ClientSecret,
            ["startUrl"] = startUrl
        };

        string json = JsonConvert.SerializeObject(payload);
        AwsApiResponse response = SendJsonRequest(UploadersHttpMethod.POST, $"{_baseUrl}/device_authorization", json, null, "application/json");

        if (!response.IsSuccess || string.IsNullOrWhiteSpace(response.Body))
        {
            throw new InvalidOperationException($"Device authorization failed ({response.StatusCode}). {response.Body}");
        }

        AwsSsoOidcDeviceAuthorizationResponse? device = JsonConvert.DeserializeObject<AwsSsoOidcDeviceAuthorizationResponse>(response.Body);
        if (device == null || string.IsNullOrWhiteSpace(device.DeviceCode))
        {
            throw new InvalidOperationException("Device authorization response was invalid.");
        }

        return device;
    }

    public AwsSsoTokenResult CreateTokenByDeviceCode(AwsSsoStoredClient client, string deviceCode)
    {
        var payload = new Dictionary<string, object>
        {
            ["clientId"] = client.ClientId,
            ["clientSecret"] = client.ClientSecret,
            ["deviceCode"] = deviceCode,
            ["grantType"] = "urn:ietf:params:oauth:grant-type:device_code"
        };

        string json = JsonConvert.SerializeObject(payload);
        AwsApiResponse response = SendJsonRequest(UploadersHttpMethod.POST, $"{_baseUrl}/token", json, null, "application/json", allowNon2xx: true);

        if (response.IsSuccess && !string.IsNullOrWhiteSpace(response.Body))
        {
            AwsSsoOidcTokenResponse? token = JsonConvert.DeserializeObject<AwsSsoOidcTokenResponse>(response.Body);
            if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return new AwsSsoTokenResult("invalid_response", "Token response was invalid.");
            }

            long expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(0, token.ExpiresIn);
            return new AwsSsoTokenResult(new AwsSsoStoredToken
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken ?? string.Empty,
                ExpiresAt = expiresAt
            });
        }

        AwsSsoOidcTokenError? error = string.IsNullOrWhiteSpace(response.Body)
            ? null
            : JsonConvert.DeserializeObject<AwsSsoOidcTokenError>(response.Body);

        return new AwsSsoTokenResult(error?.Error, error?.ErrorDescription ?? response.Body);
    }

    public AwsSsoStoredToken RefreshToken(AwsSsoStoredClient client, string refreshToken)
    {
        var payload = new Dictionary<string, object>
        {
            ["clientId"] = client.ClientId,
            ["clientSecret"] = client.ClientSecret,
            ["refreshToken"] = refreshToken,
            ["grantType"] = "refresh_token"
        };

        string json = JsonConvert.SerializeObject(payload);
        AwsApiResponse response = SendJsonRequest(UploadersHttpMethod.POST, $"{_baseUrl}/token", json, null, "application/json");

        if (!response.IsSuccess || string.IsNullOrWhiteSpace(response.Body))
        {
            throw new InvalidOperationException($"Token refresh failed ({response.StatusCode}). {response.Body}");
        }

        AwsSsoOidcTokenResponse? token = JsonConvert.DeserializeObject<AwsSsoOidcTokenResponse>(response.Body);
        if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException("Token refresh response was invalid.");
        }

        long expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(0, token.ExpiresIn);
        return new AwsSsoStoredToken
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken ?? refreshToken,
            ExpiresAt = expiresAt
        };
    }
}
