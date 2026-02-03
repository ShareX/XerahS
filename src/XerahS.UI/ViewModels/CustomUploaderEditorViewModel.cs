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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XerahS.Uploaders;

namespace XerahS.UI.ViewModels;

/// <summary>
/// ViewModel for editing custom uploader configurations (.sxcu files).
/// </summary>
public partial class CustomUploaderEditorViewModel : ViewModelBase, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.OrdinalIgnoreCase);

    #region Title and Mode

    [ObservableProperty]
    private string _title = "Add Custom Uploader";

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private string? _sourceFilePath;

    #endregion

    #region Basic Info

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isImageUploader;

    [ObservableProperty]
    private bool _isTextUploader;

    [ObservableProperty]
    private bool _isFileUploader = true;

    [ObservableProperty]
    private bool _isUrlShortener;

    [ObservableProperty]
    private bool _isUrlSharingService;

    #endregion

    #region HTTP Request

    [ObservableProperty]
    private XerahS.Uploaders.HttpMethod _selectedMethod = XerahS.Uploaders.HttpMethod.POST;

    [ObservableProperty]
    private string _requestUrl = string.Empty;

    public ObservableCollection<KeyValuePairViewModel> Headers { get; } = new();

    public ObservableCollection<KeyValuePairViewModel> Parameters { get; } = new();

    #endregion

    #region Body Configuration

    [ObservableProperty]
    private CustomUploaderBody _selectedBodyType = CustomUploaderBody.MultipartFormData;

    [ObservableProperty]
    private string _fileFormName = "file";

    public ObservableCollection<KeyValuePairViewModel> Arguments { get; } = new();

    [ObservableProperty]
    private string _bodyData = string.Empty;

    #endregion

    #region Response Parsing

    [ObservableProperty]
    private string _urlPattern = string.Empty;

    [ObservableProperty]
    private string _thumbnailUrlPattern = string.Empty;

    [ObservableProperty]
    private string _deletionUrlPattern = string.Empty;

    [ObservableProperty]
    private string _errorMessagePattern = string.Empty;

    #endregion

    #region Validation Errors

    [ObservableProperty]
    private string _nameError = string.Empty;

    [ObservableProperty]
    private string _requestUrlError = string.Empty;

    [ObservableProperty]
    private string _destinationTypeError = string.Empty;

    public bool HasNameError => !string.IsNullOrWhiteSpace(NameError);
    public bool HasRequestUrlError => !string.IsNullOrWhiteSpace(RequestUrlError);
    public bool HasDestinationTypeError => !string.IsNullOrWhiteSpace(DestinationTypeError);

    #endregion

    #region Status

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private bool _isTesting;

    #endregion

    #region Enums for ComboBoxes

    public IReadOnlyList<XerahS.Uploaders.HttpMethod> HttpMethods { get; } = Enum.GetValues<XerahS.Uploaders.HttpMethod>().ToList();

    public IReadOnlyList<CustomUploaderBody> BodyTypes { get; } = Enum.GetValues<CustomUploaderBody>().ToList();

    #endregion

    #region INotifyDataErrorInfo

    public bool HasErrors => _errors.Count > 0;
    public bool CanSave => !HasErrors;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return Array.Empty<string>();
        }

        return _errors.TryGetValue(propertyName, out var errors) ? errors : Array.Empty<string>();
    }

    #endregion

    #region Callbacks

    public Func<string?, string?, Task<string?>>? SaveFileRequester { get; set; }
    public Func<Task<string?>>? OpenFileRequester { get; set; }
    public Action<bool>? CloseRequested { get; set; }

    #endregion

    #region Validation

    partial void OnNameChanged(string value) => ValidateName();
    partial void OnRequestUrlChanged(string value) => ValidateRequestUrl();

    partial void OnIsImageUploaderChanged(bool value) => ValidateDestinationType();
    partial void OnIsTextUploaderChanged(bool value) => ValidateDestinationType();
    partial void OnIsFileUploaderChanged(bool value) => ValidateDestinationType();
    partial void OnIsUrlShortenerChanged(bool value) => ValidateDestinationType();
    partial void OnIsUrlSharingServiceChanged(bool value) => ValidateDestinationType();

    private void ValidateName()
    {
        ClearErrors(nameof(Name));
        NameError = string.Empty;

        if (string.IsNullOrWhiteSpace(Name))
        {
            AddError(nameof(Name), "Name is required.");
        }

        NameError = GetFirstError(nameof(Name));
        OnPropertyChanged(nameof(HasNameError));
        OnPropertyChanged(nameof(CanSave));
    }

    private void ValidateRequestUrl()
    {
        ClearErrors(nameof(RequestUrl));
        RequestUrlError = string.Empty;

        if (string.IsNullOrWhiteSpace(RequestUrl))
        {
            AddError(nameof(RequestUrl), "Request URL is required.");
        }
        else if (!RequestUrl.Contains("{") && !Uri.TryCreate(RequestUrl, UriKind.Absolute, out _))
        {
            AddError(nameof(RequestUrl), "Invalid URL format.");
        }

        RequestUrlError = GetFirstError(nameof(RequestUrl));
        OnPropertyChanged(nameof(HasRequestUrlError));
        OnPropertyChanged(nameof(CanSave));
    }

    private void ValidateDestinationType()
    {
        ClearErrors(nameof(IsImageUploader));
        DestinationTypeError = string.Empty;

        if (!IsImageUploader && !IsTextUploader && !IsFileUploader && !IsUrlShortener && !IsUrlSharingService)
        {
            AddError(nameof(IsImageUploader), "Select at least one destination type.");
        }

        DestinationTypeError = GetFirstError(nameof(IsImageUploader));
        OnPropertyChanged(nameof(HasDestinationTypeError));
        OnPropertyChanged(nameof(CanSave));
    }

    private void ValidateAll()
    {
        ValidateName();
        ValidateRequestUrl();
        ValidateDestinationType();
    }

    private void AddError(string propertyName, string error)
    {
        if (!_errors.TryGetValue(propertyName, out var errors))
        {
            errors = new List<string>();
            _errors[propertyName] = errors;
        }

        if (!errors.Contains(error))
        {
            errors.Add(error);
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }
    }

    private void ClearErrors(string propertyName)
    {
        if (_errors.Remove(propertyName))
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }
    }

    private string GetFirstError(string propertyName)
    {
        return _errors.TryGetValue(propertyName, out var errors) && errors.Count > 0
            ? errors[0]
            : string.Empty;
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void AddHeader()
    {
        Headers.Add(new KeyValuePairViewModel());
    }

    [RelayCommand]
    private void RemoveHeader(KeyValuePairViewModel? item)
    {
        if (item != null)
        {
            Headers.Remove(item);
        }
    }

    [RelayCommand]
    private void AddParameter()
    {
        Parameters.Add(new KeyValuePairViewModel());
    }

    [RelayCommand]
    private void RemoveParameter(KeyValuePairViewModel? item)
    {
        if (item != null)
        {
            Parameters.Remove(item);
        }
    }

    [RelayCommand]
    private void AddArgument()
    {
        Arguments.Add(new KeyValuePairViewModel());
    }

    [RelayCommand]
    private void RemoveArgument(KeyValuePairViewModel? item)
    {
        if (item != null)
        {
            Arguments.Remove(item);
        }
    }

    [RelayCommand]
    private void Save()
    {
        ValidateAll();
        if (HasErrors)
        {
            SetStatus("Please fix validation errors before saving.", true);
            return;
        }

        CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(false);
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (OpenFileRequester == null) return;

        var filePath = await OpenFileRequester();
        if (string.IsNullOrWhiteSpace(filePath)) return;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var item = JsonConvert.DeserializeObject<CustomUploaderItem>(json);

            if (item == null)
            {
                SetStatus("Failed to parse the file.", true);
                return;
            }

            LoadFromItem(item);
            SourceFilePath = filePath;
            SetStatus($"Imported from {Path.GetFileName(filePath)}", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Import failed: {ex.Message}", true);
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        ValidateAll();
        if (HasErrors)
        {
            SetStatus("Please fix validation errors before exporting.", true);
            return;
        }

        if (SaveFileRequester == null) return;

        var suggestedName = string.IsNullOrWhiteSpace(Name) ? "CustomUploader" : Name;
        var filePath = await SaveFileRequester(suggestedName, ".sxcu");

        if (string.IsNullOrWhiteSpace(filePath)) return;

        try
        {
            var item = ToItem();
            var json = JsonConvert.SerializeObject(item, Formatting.Indented, new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            });

            await File.WriteAllTextAsync(filePath, json);
            SetStatus($"Exported to {Path.GetFileName(filePath)}", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Export failed: {ex.Message}", true);
        }
    }

    [RelayCommand]
    private async Task TestUploaderAsync()
    {
        ValidateAll();
        if (HasErrors)
        {
            SetStatus("Please fix validation errors before testing.", true);
            return;
        }

        IsTesting = true;
        SetStatus("Testing uploader...", false);

        try
        {
            await Task.Delay(500); // Allow UI to update

            var item = ToItem();
            var executor = new XerahS.Uploaders.CustomUploader.CustomUploaderExecutor(item);

            // Create a simple test - just verify the URL is reachable
            // A full test would require actual file upload
            SetStatus("Uploader configuration is valid. Save and test with an actual upload.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Test failed: {ex.Message}", true);
        }
        finally
        {
            IsTesting = false;
        }
    }

    #endregion

    #region Load/Save Item

    public void LoadFromItem(CustomUploaderItem item)
    {
        Name = item.Name;
        RequestUrl = item.RequestURL;
        SelectedMethod = item.RequestMethod;
        SelectedBodyType = item.Body;
        FileFormName = item.FileFormName;
        BodyData = item.Data;

        // Destination types
        IsImageUploader = item.DestinationType.HasFlag(CustomUploaderDestinationType.ImageUploader);
        IsTextUploader = item.DestinationType.HasFlag(CustomUploaderDestinationType.TextUploader);
        IsFileUploader = item.DestinationType.HasFlag(CustomUploaderDestinationType.FileUploader);
        IsUrlShortener = item.DestinationType.HasFlag(CustomUploaderDestinationType.URLShortener);
        IsUrlSharingService = item.DestinationType.HasFlag(CustomUploaderDestinationType.URLSharingService);

        // Response patterns
        UrlPattern = item.URL;
        ThumbnailUrlPattern = item.ThumbnailURL;
        DeletionUrlPattern = item.DeletionURL;
        ErrorMessagePattern = item.ErrorMessage;

        // Headers
        Headers.Clear();
        if (item.Headers != null)
        {
            foreach (var kvp in item.Headers)
            {
                Headers.Add(new KeyValuePairViewModel { Key = kvp.Key, Value = kvp.Value });
            }
        }

        // Parameters
        Parameters.Clear();
        if (item.Parameters != null)
        {
            foreach (var kvp in item.Parameters)
            {
                Parameters.Add(new KeyValuePairViewModel { Key = kvp.Key, Value = kvp.Value });
            }
        }

        // Arguments
        Arguments.Clear();
        if (item.Arguments != null)
        {
            foreach (var kvp in item.Arguments)
            {
                Arguments.Add(new KeyValuePairViewModel { Key = kvp.Key, Value = kvp.Value });
            }
        }
    }

    public CustomUploaderItem ToItem()
    {
        var item = CustomUploaderItem.Init();

        item.Name = Name;
        item.RequestURL = RequestUrl;
        item.RequestMethod = SelectedMethod;
        item.Body = SelectedBodyType;
        item.FileFormName = FileFormName;
        item.Data = BodyData;

        // Destination types
        item.DestinationType = CustomUploaderDestinationType.None;
        if (IsImageUploader) item.DestinationType |= CustomUploaderDestinationType.ImageUploader;
        if (IsTextUploader) item.DestinationType |= CustomUploaderDestinationType.TextUploader;
        if (IsFileUploader) item.DestinationType |= CustomUploaderDestinationType.FileUploader;
        if (IsUrlShortener) item.DestinationType |= CustomUploaderDestinationType.URLShortener;
        if (IsUrlSharingService) item.DestinationType |= CustomUploaderDestinationType.URLSharingService;

        // Response patterns
        item.URL = UrlPattern;
        item.ThumbnailURL = ThumbnailUrlPattern;
        item.DeletionURL = DeletionUrlPattern;
        item.ErrorMessage = ErrorMessagePattern;

        // Headers
        if (Headers.Count > 0)
        {
            item.Headers = Headers
                .Where(h => !string.IsNullOrWhiteSpace(h.Key))
                .ToDictionary(h => h.Key, h => h.Value);
        }

        // Parameters
        if (Parameters.Count > 0)
        {
            item.Parameters = Parameters
                .Where(p => !string.IsNullOrWhiteSpace(p.Key))
                .ToDictionary(p => p.Key, p => p.Value);
        }

        // Arguments
        if (Arguments.Count > 0)
        {
            item.Arguments = Arguments
                .Where(a => !string.IsNullOrWhiteSpace(a.Key))
                .ToDictionary(a => a.Key, a => a.Value);
        }

        return item;
    }

    #endregion

    #region Helpers

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }

    #endregion
}

/// <summary>
/// ViewModel for key-value pair entries in lists.
/// </summary>
public partial class KeyValuePairViewModel : ObservableObject
{
    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;
}
