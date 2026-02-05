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
using XerahS.Uploaders;
using UploadersHttpMethod = XerahS.Uploaders.HttpMethod;

namespace ShareX.AmazonS3.Plugin;

internal sealed class AwsSsoClient : AwsApiClientBase
{
    private readonly string _baseUrl;

    public AwsSsoClient(string region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            throw new ArgumentException("Region is required.", nameof(region));
        }

        _baseUrl = $"https://portal.sso.{region}.amazonaws.com";
    }

    public List<AwsSsoAccount> ListAccounts(string accessToken)
    {
        List<AwsSsoAccount> accounts = new();
        string? nextToken = null;

        do
        {
            var payload = new Dictionary<string, object>
            {
                ["accessToken"] = accessToken,
                ["maxResults"] = 100
            };

            if (!string.IsNullOrWhiteSpace(nextToken))
            {
                payload["nextToken"] = nextToken!;
            }

            AwsSsoListAccountsResponse? response = SendSsoRequest<AwsSsoListAccountsResponse>("awsssoportalservice.ListAccounts", payload);
            if (response != null && response.AccountList.Count > 0)
            {
                accounts.AddRange(response.AccountList);
            }

            nextToken = response?.NextToken;
        }
        while (!string.IsNullOrWhiteSpace(nextToken));

        return accounts;
    }

    public List<AwsSsoRole> ListAccountRoles(string accessToken, string accountId)
    {
        List<AwsSsoRole> roles = new();
        string? nextToken = null;

        do
        {
            var payload = new Dictionary<string, object>
            {
                ["accessToken"] = accessToken,
                ["accountId"] = accountId,
                ["maxResults"] = 100
            };

            if (!string.IsNullOrWhiteSpace(nextToken))
            {
                payload["nextToken"] = nextToken!;
            }

            AwsSsoListAccountRolesResponse? response = SendSsoRequest<AwsSsoListAccountRolesResponse>("awsssoportalservice.ListAccountRoles", payload);
            if (response != null && response.RoleList.Count > 0)
            {
                roles.AddRange(response.RoleList);
            }

            nextToken = response?.NextToken;
        }
        while (!string.IsNullOrWhiteSpace(nextToken));

        return roles;
    }

    public AwsSsoStoredRoleCredentials GetRoleCredentials(string accessToken, string accountId, string roleName)
    {
        var payload = new Dictionary<string, object>
        {
            ["accessToken"] = accessToken,
            ["accountId"] = accountId,
            ["roleName"] = roleName
        };

        AwsSsoGetRoleCredentialsResponse? response = SendSsoRequest<AwsSsoGetRoleCredentialsResponse>("awsssoportalservice.GetRoleCredentials", payload);
        AwsSsoRoleCredentialsResponse? creds = response?.RoleCredentials;

        if (creds == null || string.IsNullOrWhiteSpace(creds.AccessKeyId) || string.IsNullOrWhiteSpace(creds.SecretAccessKey))
        {
            throw new InvalidOperationException("Role credentials response was invalid.");
        }

        return new AwsSsoStoredRoleCredentials
        {
            AccessKeyId = creds.AccessKeyId,
            SecretAccessKey = creds.SecretAccessKey,
            SessionToken = creds.SessionToken,
            ExpiresAt = creds.Expiration
        };
    }

    private T? SendSsoRequest<T>(string target, Dictionary<string, object> payload) where T : class
    {
        NameValueCollection headers = new NameValueCollection
        {
            ["X-Amz-Target"] = target
        };

        string json = JsonConvert.SerializeObject(payload);
        AwsApiResponse response = SendJsonRequest(UploadersHttpMethod.POST, _baseUrl, json, headers, "application/x-amz-json-1.1");

        if (!response.IsSuccess || string.IsNullOrWhiteSpace(response.Body))
        {
            throw new InvalidOperationException($"SSO request failed ({response.StatusCode}). {response.Body}");
        }

        return JsonConvert.DeserializeObject<T>(response.Body);
    }
}
