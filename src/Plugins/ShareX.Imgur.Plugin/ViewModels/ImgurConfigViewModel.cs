using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using ShareX.Ava.Uploaders;
using ShareX.Ava.Uploaders.PluginSystem;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ShareX.Imgur.Plugin.ViewModels;

/// <summary>
/// ViewModel for Imgur configuration
/// </summary>
public partial class ImgurConfigViewModel : ObservableObject, IUploaderConfigViewModel
{
    [ObservableProperty]
    private string _clientId = "30d41ft9z9r8jtt"; // Default ShareX client ID

    [ObservableProperty]
    private int _accountTypeIndex = 0;

    [ObservableProperty]
    private string _albumId = string.Empty;

    [ObservableProperty]
    private int _thumbnailTypeIndex = 4; // Large thumbnail default

    [ObservableProperty]
    private bool _useDirectLink = true;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string _pin = string.Empty;

    [ObservableProperty]
    private bool _isLoggedIn;

    private ImgurUploader? _uploader;
    private ImgurConfigModel _config = new();

    public ImgurConfigViewModel()
    {
        _uploader = new ImgurUploader(_config);
    }

    [RelayCommand]
    private void OpenLoginUrl()
    {
        if (_uploader == null) return;
        string url = _uploader.GetAuthorizationURL();
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
        if (_uploader == null || string.IsNullOrWhiteSpace(Pin))
        {
            StatusMessage = "Please enter the PIN from Imgur";
            return;
        }

        if (_uploader.GetAccessToken(Pin))
        {
            IsLoggedIn = true;
            StatusMessage = "Logged in successfully!";
            Pin = string.Empty;
        }
        else
        {
            StatusMessage = "Login failed. Please check the PIN.";
        }
    }

    [RelayCommand]
    private void FetchAlbums()
    {
        if (_uploader == null || !IsLoggedIn)
        {
            StatusMessage = "You must be logged in to fetch albums";
            return;
        }

        var albums = _uploader.GetAlbums();
        if (albums != null && albums.Count > 0)
        {
            StatusMessage = $"Found {albums.Count} albums. Copy an ID to the Album field.";
        }
        else
        {
            StatusMessage = "No albums found or failed to fetch.";
        }
    }

    public void LoadFromJson(string json)
    {
        try
        {
            var config = JsonConvert.DeserializeObject<ImgurConfigModel>(json);
            if (config != null)
            {
                _config = config;
                _uploader = new ImgurUploader(_config);
                
                ClientId = _config.ClientId ?? "30d41ft9z9r8jtt";
                AccountTypeIndex = (int)_config.AccountType;
                AlbumId = _config.SelectedAlbum?.id ?? string.Empty;
                ThumbnailTypeIndex = (int)_config.ThumbnailType;
                UseDirectLink = _config.DirectLink;
                IsLoggedIn = OAuth2Info.CheckOAuth(_config.OAuth2Info);
            }
        }
        catch
        {
            StatusMessage = "Failed to load configuration";
        }
    }

    public string ToJson()
    {
        _config.ClientId = ClientId;
        _config.AccountType = (AccountType)AccountTypeIndex;
        _config.ThumbnailType = (ImgurThumbnailType)ThumbnailTypeIndex;
        _config.DirectLink = UseDirectLink;
        _config.UploadToSelectedAlbum = !string.IsNullOrWhiteSpace(AlbumId);

        if (!string.IsNullOrWhiteSpace(AlbumId))
        {
            _config.SelectedAlbum = new ImgurAlbumData { id = AlbumId };
        }

        return JsonConvert.SerializeObject(_config, Formatting.Indented);
    }

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            StatusMessage = "Client ID is required";
            return false;
        }

        if (AccountTypeIndex == (int)AccountType.User && !IsLoggedIn)
        {
            StatusMessage = "Login is required for User Account type";
            return false;
        }

        StatusMessage = null;
        return true;
    }
}
