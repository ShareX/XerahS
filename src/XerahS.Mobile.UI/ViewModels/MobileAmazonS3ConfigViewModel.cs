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
using XerahS.Common;
using XerahS.Core;
using XerahS.Uploaders.FileUploaders;

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
        set { _accessKeyId = value; OnPropertyChanged(); UpdateIsConfigured(); }
    }

    private string _secretAccessKey = string.Empty;
    public string SecretAccessKey
    {
        get => _secretAccessKey;
        set { _secretAccessKey = value; OnPropertyChanged(); UpdateIsConfigured(); }
    }

    private string _bucketName = string.Empty;
    public string BucketName
    {
        get => _bucketName;
        set { _bucketName = value; OnPropertyChanged(); UpdateIsConfigured(); }
    }

    private int _regionIndex;
    public int RegionIndex
    {
        get => _regionIndex;
        set { _regionIndex = value; OnPropertyChanged(); }
    }

    private string _objectPrefix = "ShareX/%y/%mo";
    public string ObjectPrefix
    {
        get => _objectPrefix;
        set => _objectPrefix = value ?? "ShareX/%y/%mo";
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

    private string? _statusMessage;
    public string? StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    private bool _isTesting;
    public bool IsTesting
    {
        get => _isTesting;
        set { _isTesting = value; OnPropertyChanged(); }
    }

    #endregion

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
        LoadConfig();
    }

    #region IMobileUploaderConfig Implementation

    public void LoadConfig()
    {
        try
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

            // Find matching region
            var endpoint = settings.Endpoint ?? "s3.amazonaws.com";
            var region = Regions.FindIndex(r => 
                r.Endpoint.Equals(endpoint, StringComparison.OrdinalIgnoreCase) ||
                r.RegionCode.Equals(settings.Region, StringComparison.OrdinalIgnoreCase));
            RegionIndex = region >= 0 ? region : 0;

            UpdateIsConfigured();
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[MobileAmazonS3Config] LoadConfig failed");
            StatusMessage = "Failed to load settings";
        }
    }

    public bool SaveConfig()
    {
        try
        {
            if (!Validate()) return false;

            var settings = SettingsManager.UploadersConfig?.AmazonS3Settings;
            if (settings == null)
            {
                StatusMessage = "UploadersConfig not available";
                return false;
            }

            var region = Regions[RegionIndex];

            settings.AccessKeyID = AccessKeyId.Trim();
            settings.SecretAccessKey = SecretAccessKey.Trim();
            settings.Bucket = BucketName.Trim().ToLowerInvariant();
            settings.Endpoint = region.Endpoint;
            settings.Region = region.RegionCode;
            settings.ObjectPrefix = ObjectPrefix.Trim();
            settings.CustomDomain = CustomDomain.Trim();
            settings.UseCustomCNAME = UseCustomDomain;
            settings.SetPublicACL = SetPublicAcl;
            settings.UsePathStyle = false;

            SettingsManager.SaveUploadersConfig();
            UpdateIsConfigured();
            StatusMessage = "Settings saved successfully!";
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[MobileAmazonS3Config] SaveConfig failed");
            StatusMessage = "Failed to save settings";
            return false;
        }
    }

    public async Task<bool> TestConfigAsync()
    {
        IsTesting = true;
        StatusMessage = "Testing connection...";

        try
        {
            // Basic validation first
            if (!Validate())
            {
                IsTesting = false;
                return false;
            }

            // TODO: Implement actual S3 connection test
            // For MVP, we'll do a basic validation
            await Task.Delay(500); // Simulate network delay

            StatusMessage = "Configuration looks valid. Full test coming soon.";
            IsTesting = false;
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[MobileAmazonS3Config] TestConfig failed");
            StatusMessage = $"Test failed: {ex.Message}";
            IsTesting = false;
            return false;
        }
    }

    #endregion

    #region Private Methods

    private bool Validate()
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

        if (RegionIndex < 0 || RegionIndex >= Regions.Count)
        {
            StatusMessage = "Please select a region";
            return false;
        }

        return true;
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
