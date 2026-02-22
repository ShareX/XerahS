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
using XerahS.Uploaders.PluginSystem;

namespace ShareX.AmazonS3.Plugin;

internal sealed class AwsSsoTokenResult
{
    public AwsSsoStoredToken? Token { get; }
    public string? Error { get; }
    public string? ErrorDescription { get; }

    public bool IsSuccess => Token != null;

    public AwsSsoTokenResult(AwsSsoStoredToken token)
    {
        Token = token;
    }

    public AwsSsoTokenResult(string? error, string? errorDescription)
    {
        Error = error;
        ErrorDescription = errorDescription;
    }
}

internal sealed class AwsSsoStoredClient
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public long ClientSecretExpiresAt { get; set; }

    public bool IsExpired(long bufferSeconds = 60)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return now >= ClientSecretExpiresAt - bufferSeconds;
    }
}

internal sealed class AwsSsoStoredToken
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public long ExpiresAt { get; set; }

    public bool IsExpired(long bufferSeconds = 60)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return now >= ExpiresAt - bufferSeconds;
    }
}

internal sealed class AwsSsoStoredRoleCredentials
{
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string SessionToken { get; set; } = string.Empty;
    public long ExpiresAt { get; set; }

    public bool IsExpired(long bufferMilliseconds = 60000)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return now >= ExpiresAt - bufferMilliseconds;
    }
}

internal static class AwsSsoSecretStore
{
    private const string ProviderId = "amazons3";
    private const string ClientSecretName = "ssoClient";
    private const string TokenSecretName = "ssoToken";
    private const string RoleCredentialsSecretName = "ssoRoleCredentials";

    public static AwsSsoStoredClient? LoadClient(ISecretStore secrets, string secretKey)
    {
        string? json = secrets.GetSecret(ProviderId, secretKey, ClientSecretName);
        return string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<AwsSsoStoredClient>(json);
    }

    public static void SaveClient(ISecretStore secrets, string secretKey, AwsSsoStoredClient client)
    {
        string json = JsonConvert.SerializeObject(client, Formatting.None);
        secrets.SetSecret(ProviderId, secretKey, ClientSecretName, json);
    }

    public static AwsSsoStoredToken? LoadToken(ISecretStore secrets, string secretKey)
    {
        string? json = secrets.GetSecret(ProviderId, secretKey, TokenSecretName);
        return string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<AwsSsoStoredToken>(json);
    }

    public static void SaveToken(ISecretStore secrets, string secretKey, AwsSsoStoredToken token)
    {
        string json = JsonConvert.SerializeObject(token, Formatting.None);
        secrets.SetSecret(ProviderId, secretKey, TokenSecretName, json);
    }

    public static AwsSsoStoredRoleCredentials? LoadRoleCredentials(ISecretStore secrets, string secretKey)
    {
        string? json = secrets.GetSecret(ProviderId, secretKey, RoleCredentialsSecretName);
        return string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<AwsSsoStoredRoleCredentials>(json);
    }

    public static void SaveRoleCredentials(ISecretStore secrets, string secretKey, AwsSsoStoredRoleCredentials credentials)
    {
        string json = JsonConvert.SerializeObject(credentials, Formatting.None);
        secrets.SetSecret(ProviderId, secretKey, RoleCredentialsSecretName, json);
    }
}

internal sealed class AwsSsoOidcRegisterResponse
{
    [JsonProperty("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonProperty("clientSecret")]
    public string ClientSecret { get; set; } = string.Empty;

    [JsonProperty("clientSecretExpiresAt")]
    public long ClientSecretExpiresAt { get; set; }
}

internal sealed class AwsSsoOidcDeviceAuthorizationResponse
{
    [JsonProperty("deviceCode")]
    public string DeviceCode { get; set; } = string.Empty;

    [JsonProperty("userCode")]
    public string UserCode { get; set; } = string.Empty;

    [JsonProperty("verificationUri")]
    public string VerificationUri { get; set; } = string.Empty;

    [JsonProperty("verificationUriComplete")]
    public string? VerificationUriComplete { get; set; }

    [JsonProperty("expiresIn")]
    public int ExpiresIn { get; set; }

    [JsonProperty("interval")]
    public int Interval { get; set; }
}

internal sealed class AwsSsoOidcTokenResponse
{
    [JsonProperty("accessToken")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonProperty("refreshToken")]
    public string? RefreshToken { get; set; }

    [JsonProperty("expiresIn")]
    public int ExpiresIn { get; set; }
}

internal sealed class AwsSsoOidcTokenError
{
    [JsonProperty("error")]
    public string? Error { get; set; }

    [JsonProperty("error_description")]
    public string? ErrorDescription { get; set; }
}

public sealed class AwsSsoAccount
{
    [JsonProperty("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonProperty("accountName")]
    public string? AccountName { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(AccountName) ? AccountId : $"{AccountName} ({AccountId})";
}

public sealed class AwsSsoRole
{
    [JsonProperty("roleName")]
    public string RoleName { get; set; } = string.Empty;
}

internal sealed class AwsSsoListAccountsResponse
{
    [JsonProperty("accountList")]
    public List<AwsSsoAccount> AccountList { get; set; } = new();

    [JsonProperty("nextToken")]
    public string? NextToken { get; set; }
}

internal sealed class AwsSsoListAccountRolesResponse
{
    [JsonProperty("roleList")]
    public List<AwsSsoRole> RoleList { get; set; } = new();

    [JsonProperty("nextToken")]
    public string? NextToken { get; set; }
}

internal sealed class AwsSsoRoleCredentialsResponse
{
    [JsonProperty("accessKeyId")]
    public string AccessKeyId { get; set; } = string.Empty;

    [JsonProperty("secretAccessKey")]
    public string SecretAccessKey { get; set; } = string.Empty;

    [JsonProperty("sessionToken")]
    public string SessionToken { get; set; } = string.Empty;

    [JsonProperty("expiration")]
    public long Expiration { get; set; }
}

internal sealed class AwsSsoGetRoleCredentialsResponse
{
    [JsonProperty("roleCredentials")]
    public AwsSsoRoleCredentialsResponse? RoleCredentials { get; set; }
}
