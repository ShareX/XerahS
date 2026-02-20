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

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XerahS.Common;
using XerahS.Core;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Mobile.UI.ViewModels;

/// <summary>
/// Mobile-friendly ViewModel for Amazon S3 configuration.
/// Simplified for touch interfaces - uses Access Key auth only (no SSO for mobile MVP).
/// </summary>
[MobileUploaderConfig("amazons3", "Amazon S3", 1)]
public class MobileAmazonS3ConfigViewModel : IMobileUploaderConfig, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    #region Properties

    public string UploaderName => "Amazon S3";
    public string IconPath => "CloudStorage"; // Symbol name
    public string Description => IsConfigured 
        ? $"Bucket: {BucketName}" 
        : "Not configured - tap to set up";

    private string _accessKeyId = string.Empty;
    public string AccessKeyId
    {
        get => _accessKeyId;
        set { _accessKeyId = value; HasAccessKeyError = false; OnPropertyChanged(); UpdateIsConfigured(); }
    }

    private string _secretAccessKey = string.Empty;
    public string SecretAccessKey
    {
        get => _secretAccessKey;
        set { _secretAccessKey = value; HasSecretKeyError = false; OnPropertyChanged(); UpdateIsConfigured(); }
    }

    private string _bucketName = string.Empty;
    public string BucketName
    {
        get => _bucketName;
        set { _bucketName = value; HasBucketError = false; OnPropertyChanged(); UpdateIsConfigured(); }
    }

    private int _regionIndex;
    public int RegionIndex
    {
        get => _regionIndex;
        set { _regionIndex = value; HasRegionError = false; OnPropertyChanged(); }
    }

    private string _objectPrefix = "ShareX/%y/%mo";
    public string ObjectPrefix
    {
        get => _objectPrefix;
        set { _objectPrefix = value ?? "ShareX/%y/%mo"; OnPropertyChanged(); }
    }

    private string _customDomain = string.Empty;
    public string CustomDomain
    {
        get => _customDomain;
        set { _customDomain = value; OnPropertyChanged(); }
    }

    private bool _useCustomDomain;
    public bool UseCustomDomain
    {
        get => _useCustomDomain;
        set { _useCustomDomain = value; OnPropertyChanged(); }
    }

    private bool _setPublicAcl = true;
    public bool SetPublicAcl
    {
        get => _setPublicAcl;
        set { _setPublicAcl = value; OnPropertyChanged(); }
    }

    private bool _isConfigured;
    public bool IsConfigured
    {
        get => _isConfigured;
        private set { _isConfigured = value; OnPropertyChanged(); OnPropertyChanged(nameof(Description)); }
    }

    // Validation Errors
    private bool _hasAccessKeyError;
    public bool HasAccessKeyError
    {
        get => _hasAccessKeyError;
        set { _hasAccessKeyError = value; OnPropertyChanged(); }
    }

    private bool _hasSecretKeyError;
    public bool HasSecretKeyError
    {
        get => _hasSecretKeyError;
        set { _hasSecretKeyError = value; OnPropertyChanged(); }
    }

    private bool _hasBucketError;
    public bool HasBucketError
    {
        get => _hasBucketError;
        set { _hasBucketError = value; OnPropertyChanged(); }
    }

    private bool _hasRegionError;
    public bool HasRegionError
    {
        get => _hasRegionError;
        set { _hasRegionError = value; OnPropertyChanged(); }
    }

    public Action? ScrollToFirstError { get; set; }

    private bool _isTesting;
    public bool IsTesting
    {
        get => _isTesting;
        set { _isTesting = value; OnPropertyChanged(); }
    }

    #endregion

    private const string ProviderId = "amazons3";

    /// <summary>
    /// GUID reference for ISecretStore credential lookups. Loaded from existing instance or generated on first save.
    /// </summary>
    private string? _secretKey;

    /// <summary>
    /// InstanceId of the existing UploaderInstance in InstanceManager (null if not yet created).
    /// </summary>
    private string? _instanceId;

    #region Commands

    public ICommand SaveCommand { get; }
    public ICommand TestCommand { get; }

    #endregion

    #region Data

    /// <summary>
    /// Available S3 regions with display names
    /// </summary>
    public static readonly List<S3RegionOption> Regions = new()
    {
        new("US East (N. Virginia)", "s3.amazonaws.com", "us-east-1"),
        new("US East (Ohio)", "s3.us-east-2.amazonaws.com", "us-east-2"),
        new("US West (N. California)", "s3.us-west-1.amazonaws.com", "us-west-1"),
        new("US West (Oregon)", "s3.us-west-2.amazonaws.com", "us-west-2"),
        new("Africa (Cape Town)", "s3.af-south-1.amazonaws.com", "af-south-1"),
        new("Asia Pacific (Hong Kong)", "s3.ap-east-1.amazonaws.com", "ap-east-1"),
        new("Asia Pacific (Hyderabad)", "s3.ap-south-2.amazonaws.com", "ap-south-2"),
        new("Asia Pacific (Jakarta)", "s3.ap-southeast-3.amazonaws.com", "ap-southeast-3"),
        new("Asia Pacific (Melbourne)", "s3.ap-southeast-4.amazonaws.com", "ap-southeast-4"),
        new("Asia Pacific (Mumbai)", "s3.ap-south-1.amazonaws.com", "ap-south-1"),
        new("Asia Pacific (Osaka)", "s3.ap-northeast-3.amazonaws.com", "ap-northeast-3"),
        new("Asia Pacific (Seoul)", "s3.ap-northeast-2.amazonaws.com", "ap-northeast-2"),
        new("Asia Pacific (Singapore)", "s3.ap-southeast-1.amazonaws.com", "ap-southeast-1"),
        new("Asia Pacific (Sydney)", "s3.ap-southeast-2.amazonaws.com", "ap-southeast-2"),
        new("Asia Pacific (Tokyo)", "s3.ap-northeast-1.amazonaws.com", "ap-northeast-1"),
        new("Canada (Central)", "s3.ca-central-1.amazonaws.com", "ca-central-1"),
        new("Canada West (Calgary)", "s3.ca-west-1.amazonaws.com", "ca-west-1"),
        new("Europe (Frankfurt)", "s3.eu-central-1.amazonaws.com", "eu-central-1"),
        new("Europe (Ireland)", "s3.eu-west-1.amazonaws.com", "eu-west-1"),
        new("Europe (London)", "s3.eu-west-2.amazonaws.com", "eu-west-2"),
        new("Europe (Milan)", "s3.eu-south-1.amazonaws.com", "eu-south-1"),
        new("Europe (Paris)", "s3.eu-west-3.amazonaws.com", "eu-west-3"),
        new("Europe (Spain)", "s3.eu-south-2.amazonaws.com", "eu-south-2"),
        new("Europe (Stockholm)", "s3.eu-north-1.amazonaws.com", "eu-north-1"),
        new("Europe (Zurich)", "s3.eu-central-2.amazonaws.com", "eu-central-2"),
        new("Israel (Tel Aviv)", "s3.il-central-1.amazonaws.com", "il-central-1"),
        new("Mexico (Central)", "s3.mx-central-1.amazonaws.com", "mx-central-1"),
        new("Middle East (Bahrain)", "s3.me-south-1.amazonaws.com", "me-south-1"),
        new("Middle East (UAE)", "s3.me-central-1.amazonaws.com", "me-central-1"),
        new("South America (São Paulo)", "s3.sa-east-1.amazonaws.com", "sa-east-1"),
    };

    #endregion

    public MobileAmazonS3ConfigViewModel()
    {
        SaveCommand = new RelayCommand(_ => SaveConfig());
        TestCommand = new RelayCommand(async _ => await TestConfigAsync());
        // Note: LoadConfig() is NOT called here. MobileSettingsViewModel calls it
        // on a background thread to avoid blocking the UI.
    }

    #region IMobileUploaderConfig Implementation

    public void LoadConfig()
    {
        try
        {
            // Try loading from InstanceManager first (this is what the upload pipeline uses)
            if (LoadFromInstanceManager())
            {
                return;
            }

            // Fall back to legacy UploadersConfig for migration
            LoadFromLegacySettings();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[MobileAmazonS3Config] LoadConfig failed");
        }
    }

    private bool LoadFromInstanceManager()
    {
        var instance = InstanceManager.Instance.GetInstances()
            .FirstOrDefault(i => i.ProviderId == ProviderId);

        if (instance == null || string.IsNullOrWhiteSpace(instance.SettingsJson))
            return false;

        _instanceId = instance.InstanceId;

        var json = JObject.Parse(instance.SettingsJson);

        _secretKey = json.Value<string>("SecretKey");
        BucketName = json.Value<string>("BucketName") ?? string.Empty;
        ObjectPrefix = json.Value<string>("ObjectPrefix") ?? "ShareX/%y/%mo";
        CustomDomain = json.Value<string>("CustomDomain") ?? string.Empty;
        UseCustomDomain = json.Value<bool?>("UseCustomCNAME") ?? false;
        SetPublicAcl = json.Value<bool?>("SetPublicACL") ?? false;

        // Find matching region
        var endpoint = json.Value<string>("Endpoint") ?? "s3.amazonaws.com";
        var regionCode = json.Value<string>("Region") ?? "us-east-1";
        var regionIdx = Regions.FindIndex(r =>
            r.Endpoint.Equals(endpoint, StringComparison.OrdinalIgnoreCase) ||
            r.RegionCode.Equals(regionCode, StringComparison.OrdinalIgnoreCase));
        RegionIndex = regionIdx >= 0 ? regionIdx : 0;

        // Retrieve credentials from ISecretStore
        if (!string.IsNullOrWhiteSpace(_secretKey))
        {
            var secrets = ProviderCatalog.GetProviderContext()?.Secrets;
            if (secrets != null)
            {
                AccessKeyId = secrets.GetSecret(ProviderId, _secretKey, "accessKeyId") ?? string.Empty;
                SecretAccessKey = secrets.GetSecret(ProviderId, _secretKey, "secretAccessKey") ?? string.Empty;
            }
        }

        UpdateIsConfigured();
        return true;
    }

    private void LoadFromLegacySettings()
    {
        var settings = SettingsManager.UploadersConfig?.AmazonS3Settings;
        if (settings == null) return;

        AccessKeyId = settings.AccessKeyID ?? string.Empty;
        SecretAccessKey = settings.SecretAccessKey ?? string.Empty;
        BucketName = settings.Bucket ?? string.Empty;
        ObjectPrefix = settings.ObjectPrefix ?? "ShareX/%y/%mo";
        CustomDomain = settings.CustomDomain ?? string.Empty;
        UseCustomDomain = settings.UseCustomCNAME;
        SetPublicAcl = settings.SetPublicACL;

        var endpoint = settings.Endpoint ?? "s3.amazonaws.com";
        var regionIdx = Regions.FindIndex(r =>
            r.Endpoint.Equals(endpoint, StringComparison.OrdinalIgnoreCase) ||
            r.RegionCode.Equals(settings.Region, StringComparison.OrdinalIgnoreCase));
        RegionIndex = regionIdx >= 0 ? regionIdx : 0;

        UpdateIsConfigured();
    }

    public bool SaveConfig()
    {
        try
        {
            if (!Validate())
            {
                ScrollToFirstError?.Invoke();
                return false;
            }

            var secrets = ProviderCatalog.GetProviderContext()?.Secrets;
            if (secrets == null)
            {
                return false;
            }

            // Reuse existing SecretKey GUID or generate a new one
            if (string.IsNullOrWhiteSpace(_secretKey))
            {
                _secretKey = Guid.NewGuid().ToString("N");
            }

            // Store actual credentials in ISecretStore (NOT in JSON)
            secrets.SetSecret(ProviderId, _secretKey, "accessKeyId", AccessKeyId.Trim());
            secrets.SetSecret(ProviderId, _secretKey, "secretAccessKey", SecretAccessKey.Trim());

            var region = Regions[RegionIndex];

            // Build S3ConfigModel-compatible JSON (credentials are NOT stored here)
            var settingsJson = new JObject
            {
                ["AuthMode"] = 0, // AccessKeys
                ["SecretKey"] = _secretKey,
                ["BucketName"] = BucketName.Trim().ToLowerInvariant(),
                ["Region"] = region.RegionCode,
                ["Endpoint"] = region.Endpoint,
                ["ObjectPrefix"] = ObjectPrefix.Trim(),
                ["UseCustomCNAME"] = UseCustomDomain,
                ["CustomDomain"] = CustomDomain.Trim(),
                ["StorageClass"] = 0, // Standard
                ["SetPublicACL"] = SetPublicAcl,
                ["SetPublicPolicy"] = false,
                ["UsePathStyleUrl"] = false,
                ["SignedPayload"] = true,
                ["RemoveExtensionImage"] = false,
                ["RemoveExtensionVideo"] = false,
                ["RemoveExtensionText"] = false
            };

            var instanceManager = InstanceManager.Instance;
            var existingInstance = _instanceId != null
                ? instanceManager.GetInstance(_instanceId)
                : instanceManager.GetInstances().FirstOrDefault(i => i.ProviderId == ProviderId);

            if (existingInstance != null)
            {
                // Update existing instance
                existingInstance.SettingsJson = settingsJson.ToString(Formatting.Indented);
                instanceManager.UpdateInstance(existingInstance);
                _instanceId = existingInstance.InstanceId;
            }
            else
            {
                // Create new instance
                var newInstance = new UploaderInstance
                {
                    ProviderId = ProviderId,
                    Category = UploaderCategory.File,
                    DisplayName = "Amazon S3",
                    SettingsJson = settingsJson.ToString(Formatting.Indented),
                    FileTypeRouting = new FileTypeScope { AllFileTypes = true }
                };

                instanceManager.AddInstance(newInstance);
                _instanceId = newInstance.InstanceId;

                // Set as default for File uploads (mobile uses WorkflowType.FileUpload)
                instanceManager.SetDefaultInstance(UploaderCategory.File, _instanceId);
            }

            UpdateIsConfigured();
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[MobileAmazonS3Config] SaveConfig failed");
            return false;
        }
    }

    public async Task<bool> TestConfigAsync()
    {
        IsTesting = true;

        try
        {
            // Basic validation first
            if (!Validate())
            {
                ScrollToFirstError?.Invoke();
                IsTesting = false;
                return false;
            }

            // TODO: Implement actual S3 connection test
            // For MVP, we'll do a basic validation
            await Task.Delay(500); // Simulate network delay

            IsTesting = false;
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[MobileAmazonS3Config] TestConfig failed");
            IsTesting = false;
            return false;
        }
    }

    #endregion

    #region Private Methods

    private bool Validate()
    {
        bool valid = true;

        if (string.IsNullOrWhiteSpace(AccessKeyId))
        {
            HasAccessKeyError = true;
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(SecretAccessKey))
        {
            HasSecretKeyError = true;
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(BucketName))
        {
            HasBucketError = true;
            valid = false;
        }

        if (RegionIndex < 0 || RegionIndex >= Regions.Count)
        {
            HasRegionError = true;
            valid = false;
        }

        return valid;
    }

    private void UpdateIsConfigured()
    {
        IsConfigured = !string.IsNullOrWhiteSpace(AccessKeyId) &&
                       !string.IsNullOrWhiteSpace(SecretAccessKey) &&
                       !string.IsNullOrWhiteSpace(BucketName);
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    #endregion
}

/// <summary>
/// S3 region option for dropdown
/// </summary>
public class S3RegionOption
{
    public string DisplayName { get; }
    public string Endpoint { get; }
    public string RegionCode { get; }

    public S3RegionOption(string displayName, string endpoint, string regionCode)
    {
        DisplayName = displayName;
        Endpoint = endpoint;
        RegionCode = regionCode;
    }

    public override string ToString() => DisplayName;
}
