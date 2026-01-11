#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
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

using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using XerahS.Uploaders.FileUploaders;
using XerahS.Uploaders.PluginSystem;
using System.Collections.ObjectModel;

namespace ShareX.AmazonS3.Plugin.ViewModels;

/// <summary>
/// ViewModel for Amazon S3 configuration
/// </summary>
public partial class AmazonS3ConfigViewModel : ObservableObject, IUploaderConfigViewModel
{
    [ObservableProperty]
    private string _accessKeyId = string.Empty;

    [ObservableProperty]
    private string _secretAccessKey = string.Empty;

    [ObservableProperty]
    private string _bucketName = string.Empty;

    [ObservableProperty]
    private int _regionIndex = 16; // Default to US East (N. Virginia)

    [ObservableProperty]
    private string _objectPrefix = string.Empty;

    [ObservableProperty]
    private string _customDomain = string.Empty;

    [ObservableProperty]
    private bool _useCustomCNAME = false;

    [ObservableProperty]
    private int _storageClassIndex = 0; // STANDARD

    [ObservableProperty]
    private bool _setPublicACL = true;

    [ObservableProperty]
    private bool _signedPayload = false;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _removeExtensionImage = false;

    [ObservableProperty]
    private bool _removeExtensionVideo = false;

    [ObservableProperty]
    private bool _removeExtensionText = false;

    public ObservableCollection<AmazonS3Endpoint> Endpoints { get; } = new(AmazonS3Uploader.Endpoints);

    public string[] StorageClasses { get; } = new[]
    {
        "STANDARD",
        "REDUCED_REDUNDANCY",
        "STANDARD_IA",
        "ONEZONE_IA",
        "INTELLIGENT_TIERING"
    };

    public void LoadFromJson(string json)
    {
        try
        {
            var config = JsonConvert.DeserializeObject<S3ConfigModel>(json);
            if (config != null)
            {
                AccessKeyId = config.AccessKeyId ?? string.Empty;
                SecretAccessKey = config.SecretAccessKey ?? string.Empty;
                BucketName = config.BucketName ?? string.Empty;

                int index = Endpoints.ToList().FindIndex(e => e.Endpoint == config.Endpoint);
                if (index >= 0) RegionIndex = index;

                ObjectPrefix = config.ObjectPrefix ?? string.Empty;
                CustomDomain = config.CustomDomain ?? string.Empty;
                UseCustomCNAME = config.UseCustomCNAME;
                StorageClassIndex = (int)config.StorageClass;
                SetPublicACL = config.SetPublicACL;
                SignedPayload = config.SignedPayload;
                RemoveExtensionImage = config.RemoveExtensionImage;
                RemoveExtensionVideo = config.RemoveExtensionVideo;
                RemoveExtensionText = config.RemoveExtensionText;
            }
        }
        catch
        {
            StatusMessage = "Failed to load configuration";
        }
    }

    public string ToJson()
    {
        var config = new S3ConfigModel
        {
            AccessKeyId = AccessKeyId,
            SecretAccessKey = SecretAccessKey,
            BucketName = BucketName,
            Endpoint = Endpoints[RegionIndex].Endpoint,
            Region = Endpoints[RegionIndex].Region,
            ObjectPrefix = string.IsNullOrWhiteSpace(ObjectPrefix) ? null : ObjectPrefix,
            CustomDomain = string.IsNullOrWhiteSpace(CustomDomain) ? null : CustomDomain,
            UseCustomCNAME = UseCustomCNAME,
            StorageClass = (AmazonS3StorageClass)StorageClassIndex,
            SetPublicACL = SetPublicACL,
            SignedPayload = SignedPayload,
            RemoveExtensionImage = RemoveExtensionImage,
            RemoveExtensionVideo = RemoveExtensionVideo,
            RemoveExtensionText = RemoveExtensionText
        };

        return JsonConvert.SerializeObject(config, Formatting.Indented);
    }

    public bool Validate()
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

        StatusMessage = null;
        return true;
    }
}
