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
using ShareX.AmazonS3.Plugin;
using XerahS.Common;
using XerahS.Uploaders.FileUploaders;
using XerahS.Uploaders.PluginSystem;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ShareX.AmazonS3.Plugin.ViewModels;

/// <summary>
/// ViewModel for Amazon S3 configuration
/// </summary>
public partial class AmazonS3ConfigViewModel : ObservableObject, IUploaderConfigViewModel, IProviderContextAware
{
    [ObservableProperty]
    private string _accessKeyId = string.Empty;

    [ObservableProperty]
    private string _secretAccessKey = string.Empty;

    [ObservableProperty]
    private int _authModeIndex = 0;

    [ObservableProperty]
    private string _bucketName = string.Empty;

    [ObservableProperty]
    private int _regionIndex = 16; // Default to US East (N. Virginia)

    [ObservableProperty]
    private string _objectPrefix = string.Empty;

    [ObservableProperty]
    private string _customDomain = string.Empty;

    [ObservableProperty]
    private string _cnameTarget = string.Empty;

    [ObservableProperty]
    private string _ssoStartUrl = string.Empty;

    [ObservableProperty]
    private string _ssoRegion = "us-east-1";

    [ObservableProperty]
    private string _ssoAccountId = string.Empty;

    [ObservableProperty]
    private string _ssoRoleName = string.Empty;

    [ObservableProperty]
    private string _ssoUserCode = string.Empty;

    [ObservableProperty]
    private string _ssoVerificationUrl = string.Empty;

    [ObservableProperty]
    private bool _isSsoLoggedIn = false;

    [ObservableProperty]
    private string? _ssoStatusMessage;

    [ObservableProperty]
    private AwsSsoAccount? _selectedSsoAccount;

    [ObservableProperty]
    private AwsSsoRole? _selectedSsoRole;

    [ObservableProperty]
    private bool _useCustomCNAME = false;

    [ObservableProperty]
    private int _storageClassIndex = 0; // STANDARD

    [ObservableProperty]
    private bool _setPublicACL = false;

    [ObservableProperty]
    private bool _setPublicPolicy = true;

    [ObservableProperty]
    private bool _signedPayload = true;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _removeExtensionImage = false;

    [ObservableProperty]
    private bool _removeExtensionVideo = false;

    [ObservableProperty]
    private bool _removeExtensionText = false;

    private string _secretKey = Guid.NewGuid().ToString("N");
    private ISecretStore? _secrets;
    private string? _deviceCode;
    private DateTimeOffset _deviceCodeExpiresAt;
    private int _deviceCodeInterval = 5;
    private const string SsoStartUrlHelpLink = "https://console.aws.amazon.com/singlesignon/";

    public ObservableCollection<AmazonS3Endpoint> Endpoints { get; } = new(AmazonS3Uploader.Endpoints);
    public ObservableCollection<AwsSsoAccount> SsoAccounts { get; } = new();
    public ObservableCollection<AwsSsoRole> SsoRoles { get; } = new();

    public string[] AuthModes { get; } = new[]
    {
        "Access keys",
        "AWS SSO (IAM Identity Center)"
    };

    public bool IsAccessKeysMode => AuthModeIndex == 0;
    public bool IsSsoMode => AuthModeIndex == 1;

    public string[] StorageClasses { get; } = new[]
    {
        "STANDARD",
        "REDUCED_REDUNDANCY",
        "STANDARD_IA",
        "ONEZONE_IA",
        "INTELLIGENT_TIERING"
    };

    partial void OnAuthModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsAccessKeysMode));
        OnPropertyChanged(nameof(IsSsoMode));

        if (IsSsoMode)
        {
            UseCustomCNAME = true;
            SetPublicACL = false;
            SetPublicPolicy = true;
            EnsureDefaultAwsRegion();
            UpdateBucketFromCustomDomain();
            UpdateSsoRegionFromSelection();
        }

        UpdateCnameTarget();
        StatusMessage = null;
        SsoStatusMessage = null;
    }

    partial void OnRegionIndexChanged(int value)
    {
        if (IsSsoMode)
        {
            UpdateSsoRegionFromSelection();
        }

        UpdateCnameTarget();
    }

    partial void OnCustomDomainChanged(string value)
    {
        if (IsSsoMode)
        {
            UpdateBucketFromCustomDomain();
        }

        UpdateCnameTarget();
    }

    partial void OnUseCustomCNAMEChanged(bool value)
    {
        UpdateCnameTarget();
    }

    partial void OnBucketNameChanged(string value)
    {
        UpdateCnameTarget();
    }

    partial void OnSelectedSsoAccountChanged(AwsSsoAccount? value)
    {
        SsoAccountId = value?.AccountId ?? string.Empty;
        SsoRoles.Clear();
        SelectedSsoRole = null;
        SsoRoleName = string.Empty;
    }

    partial void OnSelectedSsoRoleChanged(AwsSsoRole? value)
    {
        SsoRoleName = value?.RoleName ?? string.Empty;
    }

    [RelayCommand]
    private void StartSsoLogin()
    {
        if (!IsSsoMode)
        {
            return;
        }

        if (_secrets == null)
        {
            SsoStatusMessage = "Secret store not available.";
            return;
        }

        AmazonS3Endpoint endpoint = GetSelectedEndpoint();
        if (!IsAwsEndpoint(endpoint))
        {
            SsoStatusMessage = "SSO mode requires an AWS S3 endpoint.";
            return;
        }

        UpdateSsoRegionFromSelection();

        if (string.IsNullOrWhiteSpace(SsoStartUrl) || string.IsNullOrWhiteSpace(SsoRegion))
        {
            SsoStatusMessage = "SSO start URL and region are required.";
            return;
        }

        try
        {
            var oidc = new AwsSsoOidcClient(SsoRegion);
            AwsSsoStoredClient? client = AwsSsoSecretStore.LoadClient(_secrets, _secretKey);
            if (client == null || client.IsExpired())
            {
                client = oidc.RegisterClient("XerahS");
                AwsSsoSecretStore.SaveClient(_secrets, _secretKey, client);
            }

            AwsSsoOidcDeviceAuthorizationResponse device = oidc.StartDeviceAuthorization(client, SsoStartUrl);
            _deviceCode = device.DeviceCode;
            _deviceCodeExpiresAt = DateTimeOffset.UtcNow.AddSeconds(device.ExpiresIn);
            _deviceCodeInterval = Math.Max(1, device.Interval);

            SsoUserCode = device.UserCode;
            SsoVerificationUrl = string.IsNullOrWhiteSpace(device.VerificationUriComplete)
                ? device.VerificationUri
                : device.VerificationUriComplete!;

            SsoStatusMessage = "Open the verification URL, then click Complete Login.";
            OpenUrl(SsoVerificationUrl);
        }
        catch (Exception ex)
        {
            SsoStatusMessage = "SSO login start failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void OpenSsoStartUrlHelp()
    {
        OpenUrl(SsoStartUrlHelpLink);
    }

    [RelayCommand]
    private async Task CompleteSsoLoginAsync()
    {
        if (!IsSsoMode)
        {
            return;
        }

        if (_secrets == null)
        {
            SsoStatusMessage = "Secret store not available.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_deviceCode))
        {
            SsoStatusMessage = "Start SSO login first.";
            return;
        }

        AwsSsoStoredClient? client = AwsSsoSecretStore.LoadClient(_secrets, _secretKey);
        if (client == null || client.IsExpired())
        {
            SsoStatusMessage = "SSO client registration missing or expired.";
            return;
        }

        var oidc = new AwsSsoOidcClient(SsoRegion);
        DateTimeOffset deadline = _deviceCodeExpiresAt;
        int interval = Math.Max(1, _deviceCodeInterval);

        SsoStatusMessage = "Waiting for authorization...";

        while (DateTimeOffset.UtcNow < deadline)
        {
            AwsSsoTokenResult result = await Task.Run(() => oidc.CreateTokenByDeviceCode(client, _deviceCode));
            if (result.IsSuccess && result.Token != null)
            {
                AwsSsoSecretStore.SaveToken(_secrets, _secretKey, result.Token);
                IsSsoLoggedIn = true;
                SsoStatusMessage = "SSO login completed.";
                await LoadAccountsAndRolesAsync(result.Token.AccessToken);
                return;
            }

            if (string.Equals(result.Error, "authorization_pending", StringComparison.OrdinalIgnoreCase))
            {
                await Task.Delay(TimeSpan.FromSeconds(interval));
                continue;
            }

            if (string.Equals(result.Error, "slow_down", StringComparison.OrdinalIgnoreCase))
            {
                interval += 5;
                await Task.Delay(TimeSpan.FromSeconds(interval));
                continue;
            }

            SsoStatusMessage = $"SSO login failed: {result.ErrorDescription ?? result.Error}";
            return;
        }

        SsoStatusMessage = "Device code expired. Start SSO login again.";
    }

    [RelayCommand]
    private async Task RefreshSsoCredentialsAsync()
    {
        if (!IsSsoMode)
        {
            return;
        }

        AwsSsoStoredToken? token = EnsureSsoToken(out string? error);
        if (token == null)
        {
            SsoStatusMessage = error;
            return;
        }

        await LoadAccountsAndRolesAsync(token.AccessToken);

        if (string.IsNullOrWhiteSpace(SsoAccountId) || string.IsNullOrWhiteSpace(SsoRoleName))
        {
            SsoStatusMessage = "Select an account and role.";
            return;
        }

        try
        {
            var ssoClient = new AwsSsoClient(SsoRegion);
            AwsSsoStoredRoleCredentials creds = await Task.Run(() => ssoClient.GetRoleCredentials(token.AccessToken, SsoAccountId, SsoRoleName));
            if (_secrets != null)
            {
                AwsSsoSecretStore.SaveRoleCredentials(_secrets, _secretKey, creds);
            }

            SsoStatusMessage = "SSO role credentials refreshed.";
        }
        catch (Exception ex)
        {
            SsoStatusMessage = "Failed to refresh role credentials: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task ProvisionBucketAsync()
    {
        if (!IsSsoMode)
        {
            return;
        }

        if (!TryGetBucketNameFromCustomDomain(out string bucketName, out string? error))
        {
            SsoStatusMessage = error;
            return;
        }

        AwsSsoStoredToken? token = EnsureSsoToken(out string? tokenError);
        if (token == null)
        {
            SsoStatusMessage = tokenError;
            return;
        }

        if (string.IsNullOrWhiteSpace(SsoAccountId) || string.IsNullOrWhiteSpace(SsoRoleName))
        {
            SsoStatusMessage = "Select an account and role.";
            return;
        }

        AwsSsoStoredRoleCredentials? creds = _secrets != null
            ? AwsSsoSecretStore.LoadRoleCredentials(_secrets, _secretKey)
            : null;

        if (creds == null || creds.IsExpired())
        {
            try
            {
                var ssoClient = new AwsSsoClient(SsoRegion);
                creds = await Task.Run(() => ssoClient.GetRoleCredentials(token.AccessToken, SsoAccountId, SsoRoleName));
                if (_secrets != null)
                {
                    AwsSsoSecretStore.SaveRoleCredentials(_secrets, _secretKey, creds);
                }
            }
            catch (Exception ex)
            {
                SsoStatusMessage = "Failed to get role credentials: " + ex.Message;
                return;
            }
        }

        AmazonS3Endpoint endpoint = GetSelectedEndpoint();
        if (!IsAwsEndpoint(endpoint))
        {
            SsoStatusMessage = "SSO mode requires an AWS S3 endpoint.";
            return;
        }

        string region = GetSelectedRegion(endpoint);
        if (string.IsNullOrWhiteSpace(region))
        {
            SsoStatusMessage = "Select a valid AWS region.";
            return;
        }

        var provisioner = new S3Provisioner(creds.AccessKeyId, creds.SecretAccessKey, creds.SessionToken, region, endpoint.Endpoint);
        S3ProvisionResult result = await Task.Run(() => provisioner.EnsureBucket(bucketName, SetPublicPolicy));
        SsoStatusMessage = result.Message;

        if (result.IsSuccess)
        {
            BucketName = bucketName;
            UseCustomCNAME = true;
            SetPublicACL = false;
            SetPublicPolicy = true;
        }
    }

    private AwsSsoStoredToken? EnsureSsoToken(out string? error)
    {
        error = null;

        if (_secrets == null)
        {
            error = "Secret store not available.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(SsoRegion))
        {
            error = "SSO region is required.";
            return null;
        }

        AwsSsoStoredToken? token = AwsSsoSecretStore.LoadToken(_secrets, _secretKey);
        if (token == null)
        {
            error = "SSO login required.";
            return null;
        }

        if (!token.IsExpired())
        {
            IsSsoLoggedIn = true;
            return token;
        }

        AwsSsoStoredClient? client = AwsSsoSecretStore.LoadClient(_secrets, _secretKey);
        if (client == null || client.IsExpired())
        {
            error = "SSO login required.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            error = "SSO session expired. Please login again.";
            return null;
        }

        try
        {
            var oidc = new AwsSsoOidcClient(SsoRegion);
            token = oidc.RefreshToken(client, token.RefreshToken);
            AwsSsoSecretStore.SaveToken(_secrets, _secretKey, token);
            IsSsoLoggedIn = true;
            return token;
        }
        catch (Exception ex)
        {
            error = "SSO token refresh failed: " + ex.Message;
            return null;
        }
    }

    private async Task LoadAccountsAndRolesAsync(string accessToken)
    {
        try
        {
            var ssoClient = new AwsSsoClient(SsoRegion);
            List<AwsSsoAccount> accounts = await Task.Run(() => ssoClient.ListAccounts(accessToken));
            SsoAccounts.Clear();
            foreach (AwsSsoAccount account in accounts)
            {
                SsoAccounts.Add(account);
            }

            if (!string.IsNullOrWhiteSpace(SsoAccountId))
            {
                SelectedSsoAccount = SsoAccounts.FirstOrDefault(a => a.AccountId == SsoAccountId);
            }

            if (SelectedSsoAccount == null && SsoAccounts.Count == 1)
            {
                SelectedSsoAccount = SsoAccounts[0];
            }

            if (SelectedSsoAccount != null)
            {
                List<AwsSsoRole> roles = await Task.Run(() => ssoClient.ListAccountRoles(accessToken, SelectedSsoAccount.AccountId));
                SsoRoles.Clear();
                foreach (AwsSsoRole role in roles)
                {
                    SsoRoles.Add(role);
                }

                if (!string.IsNullOrWhiteSpace(SsoRoleName))
                {
                    SelectedSsoRole = SsoRoles.FirstOrDefault(r => r.RoleName == SsoRoleName);
                }

                if (SelectedSsoRole == null && SsoRoles.Count == 1)
                {
                    SelectedSsoRole = SsoRoles[0];
                }
            }
        }
        catch (Exception ex)
        {
            SsoStatusMessage = "Failed to load accounts or roles: " + ex.Message;
        }
    }

    private bool TryGetBucketNameFromCustomDomain(out string bucketName, out string? error)
    {
        bucketName = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(CustomDomain))
        {
            error = "Custom Domain is required for SSO.";
            return false;
        }

        string input = CustomDomain.Trim();
        string url = URLHelpers.HasPrefix(input) ? input : "https://" + input;

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            error = "Custom Domain is invalid.";
            return false;
        }

        bucketName = uri.Host.ToLowerInvariant();
        if (!S3BucketNameValidator.IsValid(bucketName))
        {
            error = "Custom Domain must map to a valid S3 bucket name.";
            return false;
        }

        return true;
    }

    private void UpdateBucketFromCustomDomain()
    {
        if (TryGetBucketNameFromCustomDomain(out string bucketName, out _))
        {
            BucketName = bucketName;
        }
        else
        {
            BucketName = string.Empty;
        }
    }

    private void UpdateCnameTarget()
    {
        if (!UseCustomCNAME || string.IsNullOrWhiteSpace(BucketName))
        {
            CnameTarget = string.Empty;
            return;
        }

        AmazonS3Endpoint selectedEndpoint = GetSelectedEndpoint();
        string endpointHost = NormalizeEndpointHost(selectedEndpoint.Endpoint);
        if (string.IsNullOrWhiteSpace(endpointHost))
        {
            CnameTarget = string.Empty;
            return;
        }

        CnameTarget = $"{BucketName}.{endpointHost}";
    }

    private static string NormalizeEndpointHost(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return string.Empty;
        }

        string value = endpoint.Trim();
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && !string.IsNullOrWhiteSpace(uri.Host))
            {
                return uri.Host;
            }
        }

        return value.TrimEnd('/');
    }

    private void UpdateSsoRegionFromSelection()
    {
        SsoRegion = GetSelectedRegion(GetSelectedEndpoint());
    }

    private void EnsureDefaultAwsRegion()
    {
        if (IsAwsEndpoint(GetSelectedEndpoint()))
        {
            return;
        }

        int index = Endpoints.ToList().FindIndex(endpoint => IsAwsEndpoint(endpoint) &&
                                                             (endpoint.Endpoint.Equals("s3.amazonaws.com", StringComparison.OrdinalIgnoreCase) ||
                                                              endpoint.Region == "us-east-1"));
        if (index >= 0)
        {
            RegionIndex = index;
        }
    }

    private AmazonS3Endpoint GetSelectedEndpoint()
    {
        if (RegionIndex < 0 || RegionIndex >= Endpoints.Count)
        {
            return Endpoints.Count > 0 ? Endpoints[0] : new AmazonS3Endpoint("US East (N. Virginia)", "s3.amazonaws.com", "us-east-1");
        }

        return Endpoints[RegionIndex];
    }

    private static bool IsAwsEndpoint(AmazonS3Endpoint endpoint)
    {
        return endpoint.Endpoint.Contains("amazonaws.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSelectedRegion(AmazonS3Endpoint endpoint)
    {
        if (!string.IsNullOrWhiteSpace(endpoint.Region))
        {
            return endpoint.Region;
        }

        return ResolveRegionFromEndpoint(endpoint.Endpoint, "us-east-1");
    }

    private static string ResolveRegionFromEndpoint(string endpoint, string fallbackRegion)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return fallbackRegion;
        }

        string host = endpoint.Trim();
        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(host, UriKind.Absolute, out Uri? uri) && !string.IsNullOrWhiteSpace(uri.Host))
            {
                host = uri.Host;
            }
        }

        host = host.TrimEnd('/');
        if (!host.Contains(".amazonaws.com", StringComparison.OrdinalIgnoreCase))
        {
            return fallbackRegion;
        }

        string serviceAndRegion = host.Split(new[] { ".amazonaws.com" }, StringSplitOptions.None)[0];
        if (serviceAndRegion.StartsWith("s3-"))
        {
            serviceAndRegion = "s3." + serviceAndRegion.Substring(3);
        }

        int separatorIndex = serviceAndRegion.LastIndexOf('.');
        if (separatorIndex == -1)
        {
            return fallbackRegion;
        }

        return serviceAndRegion.Substring(separatorIndex + 1);
    }

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else
            {
                URLHelpers.OpenURL(url);
            }
        }
        catch
        {
            // ignore browser failures
        }
    }

    public void LoadFromJson(string json)
    {
        try
        {
            var config = JsonConvert.DeserializeObject<S3ConfigModel>(json);
            if (config != null)
            {
                _secretKey = string.IsNullOrWhiteSpace(config.SecretKey) ? Guid.NewGuid().ToString("N") : config.SecretKey;
                AccessKeyId = _secrets?.GetSecret("amazons3", _secretKey, "accessKeyId") ?? string.Empty;
                SecretAccessKey = _secrets?.GetSecret("amazons3", _secretKey, "secretAccessKey") ?? string.Empty;
                AuthModeIndex = config.AuthMode == S3AuthMode.AwsSso ? 1 : 0;
                BucketName = config.BucketName ?? string.Empty;

                int index = Endpoints.ToList().FindIndex(e => e.Endpoint == config.Endpoint);
                if (index >= 0) RegionIndex = index;

                ObjectPrefix = config.ObjectPrefix ?? string.Empty;
                CustomDomain = config.CustomDomain ?? string.Empty;
                UseCustomCNAME = config.UseCustomCNAME;
                SsoStartUrl = config.SsoStartUrl ?? string.Empty;
                SsoRegion = string.IsNullOrWhiteSpace(config.SsoRegion) ? "us-east-1" : config.SsoRegion;
                SsoAccountId = config.SsoAccountId ?? string.Empty;
                SsoRoleName = config.SsoRoleName ?? string.Empty;
                StorageClassIndex = (int)config.StorageClass;
                SetPublicACL = config.SetPublicACL;
                SetPublicPolicy = config.SetPublicPolicy;
                SignedPayload = config.SignedPayload;
                RemoveExtensionImage = config.RemoveExtensionImage;
                RemoveExtensionVideo = config.RemoveExtensionVideo;
                RemoveExtensionText = config.RemoveExtensionText;

                if (_secrets != null)
                {
                    AwsSsoStoredToken? token = AwsSsoSecretStore.LoadToken(_secrets, _secretKey);
                    IsSsoLoggedIn = token != null && !token.IsExpired();
                }

                if (IsSsoMode)
                {
                    UseCustomCNAME = true;
                    SetPublicACL = config.SetPublicACL;
                    SetPublicPolicy = config.SetPublicPolicy;
                    EnsureDefaultAwsRegion();
                    UpdateBucketFromCustomDomain();
                    UpdateSsoRegionFromSelection();
                }

                UpdateCnameTarget();
            }
        }
        catch
        {
            StatusMessage = "Failed to load configuration";
        }
    }

    public string ToJson()
    {
        string bucketName = BucketName;
        string endpoint = Endpoints[RegionIndex].Endpoint;
        string region = Endpoints[RegionIndex].Region;

        if (IsSsoMode)
        {
            if (TryGetBucketNameFromCustomDomain(out string derivedBucket, out _))
            {
                bucketName = derivedBucket;
                BucketName = derivedBucket;
            }

            AmazonS3Endpoint selectedEndpoint = GetSelectedEndpoint();
            endpoint = selectedEndpoint.Endpoint;
            region = GetSelectedRegion(selectedEndpoint);
        }

        var config = new S3ConfigModel
        {
            AuthMode = IsSsoMode ? S3AuthMode.AwsSso : S3AuthMode.AccessKeys,
            SecretKey = _secretKey,
            BucketName = bucketName,
            Endpoint = endpoint,
            Region = region,
            ObjectPrefix = string.IsNullOrWhiteSpace(ObjectPrefix) ? null! : ObjectPrefix,
            CustomDomain = string.IsNullOrWhiteSpace(CustomDomain) ? null! : CustomDomain,
            UseCustomCNAME = UseCustomCNAME,
            StorageClass = (AmazonS3StorageClass)StorageClassIndex,
            SetPublicACL = SetPublicACL,
            SetPublicPolicy = SetPublicPolicy,
            SignedPayload = SignedPayload,
            RemoveExtensionImage = RemoveExtensionImage,
            RemoveExtensionVideo = RemoveExtensionVideo,
            RemoveExtensionText = RemoveExtensionText,
            SsoStartUrl = SsoStartUrl,
            SsoRegion = IsSsoMode ? GetSelectedRegion(GetSelectedEndpoint()) : SsoRegion,
            SsoAccountId = SsoAccountId,
            SsoRoleName = SsoRoleName
        };

        if (_secrets != null)
        {
            if (string.IsNullOrWhiteSpace(AccessKeyId))
            {
                _secrets.DeleteSecret("amazons3", _secretKey, "accessKeyId");
            }
            else
            {
                _secrets.SetSecret("amazons3", _secretKey, "accessKeyId", AccessKeyId);
            }

            if (string.IsNullOrWhiteSpace(SecretAccessKey))
            {
                _secrets.DeleteSecret("amazons3", _secretKey, "secretAccessKey");
            }
            else
            {
                _secrets.SetSecret("amazons3", _secretKey, "secretAccessKey", SecretAccessKey);
            }
        }

        return JsonConvert.SerializeObject(config, Formatting.Indented);
    }

    public bool Validate()
    {
        if (IsAccessKeysMode)
        {
            if (string.IsNullOrWhiteSpace(AccessKeyId))
            {
                StatusMessage = "Access Key ID is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(SecretAccessKey))
            {
                StatusMessage = "Secret Access Key is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(BucketName))
            {
                StatusMessage = "Bucket Name is required";
                return false;
            }

            if (_secrets != null)
            {
                _secrets.SetSecret("amazons3", _secretKey, "accessKeyId", AccessKeyId);
                _secrets.SetSecret("amazons3", _secretKey, "secretAccessKey", SecretAccessKey);
            }

            StatusMessage = null;
            return true;
        }

        if (!IsSsoLoggedIn)
        {
            StatusMessage = "SSO login is required";
            return false;
        }

        if (!TryGetBucketNameFromCustomDomain(out string bucketName, out string? error))
        {
            StatusMessage = error;
            return false;
        }

        if (string.IsNullOrWhiteSpace(SsoAccountId) || string.IsNullOrWhiteSpace(SsoRoleName))
        {
            StatusMessage = "SSO account and role are required";
            return false;
        }

        AmazonS3Endpoint selectedEndpoint = GetSelectedEndpoint();
        if (!IsAwsEndpoint(selectedEndpoint))
        {
            StatusMessage = "SSO mode requires an AWS S3 endpoint.";
            return false;
        }

        string region = GetSelectedRegion(selectedEndpoint);
        if (string.IsNullOrWhiteSpace(region))
        {
            StatusMessage = "SSO mode requires a valid AWS region.";
            return false;
        }

        BucketName = bucketName;
        UseCustomCNAME = true;
        SetPublicACL = true;

        StatusMessage = null;
        return true;
    }

    public void SetContext(IProviderContext context)
    {
        _secrets = context.Secrets;

        if (_secrets != null)
        {
            AwsSsoStoredToken? token = AwsSsoSecretStore.LoadToken(_secrets, _secretKey);
            IsSsoLoggedIn = token != null && !token.IsExpired();
        }
    }
}
