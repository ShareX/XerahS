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

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core;
using XerahS.Uploaders;
using XerahS.Uploaders.CustomUploader;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Mobile.Core;

/// <summary>
/// Mobile-friendly ViewModel for Custom Uploader configuration.
/// Provides a form-based editor (recreated from the desktop CustomUploaderEditorDialog)
/// and a list view to manage custom uploaders.
/// </summary>
[MobileUploaderConfig("customuploader", "Custom Uploader", 2)]
public class MobileCustomUploaderConfigViewModel : IMobileUploaderConfig, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    #region Static Data

    public static IReadOnlyList<string> HttpMethods { get; } = new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD" };
    public static IReadOnlyList<string> BodyTypes { get; } = new[] { "No body", "Form data (multipart)", "Form URL encoded", "JSON", "XML", "Binary" };

    #endregion

    #region IMobileUploaderConfig Properties

    public string UploaderName => "Custom Uploader";
    public string IconPath => "CloudUpload";
    public string Description => IsConfigured
        ? $"Active: {ActiveUploaderName}"
        : "Not configured - tap to set up";

    private bool _isConfigured;
    public bool IsConfigured
    {
        get => _isConfigured;
        private set { _isConfigured = value; OnPropertyChanged(); OnPropertyChanged(nameof(Description)); }
    }

    #endregion

    #region List View Properties

    private ObservableCollection<CustomUploaderListItem> _customUploaders = new();
    public ObservableCollection<CustomUploaderListItem> CustomUploaders
    {
        get => _customUploaders;
        set { _customUploaders = value; OnPropertyChanged(); }
    }

    private int _selectedUploaderIndex = -1;
    public int SelectedUploaderIndex
    {
        get => _selectedUploaderIndex;
        set { _selectedUploaderIndex = value; OnPropertyChanged(); }
    }

    public string ActiveUploaderName
    {
        get
        {
            var activeIndex = SettingsManager.UploadersConfig?.CustomImageUploaderSelected ?? -1;
            var list = SettingsManager.UploadersConfig?.CustomUploadersList;
            if (list != null && activeIndex >= 0 && activeIndex < list.Count)
                return list[activeIndex].ToString();
            return "None";
        }
    }

    private bool _isTesting;
    public bool IsTesting
    {
        get => _isTesting;
        set { _isTesting = value; OnPropertyChanged(); }
    }

    #endregion

    #region Editor Properties

    private bool _isEditorVisible;
    public bool IsEditorVisible
    {
        get => _isEditorVisible;
        set { _isEditorVisible = value; OnPropertyChanged(); }
    }

    private string _editorTitle = "Add Custom Uploader";
    public string EditorTitle
    {
        get => _editorTitle;
        set { _editorTitle = value; OnPropertyChanged(); }
    }

    private bool _isEditMode;
    public bool IsEditMode
    {
        get => _isEditMode;
        set { _isEditMode = value; OnPropertyChanged(); }
    }

    private int _editingIndex = -1;
    public int EditingIndex
    {
        get => _editingIndex;
        set { _editingIndex = value; OnPropertyChanged(); }
    }

    private string _editorName = "";
    public string EditorName
    {
        get => _editorName;
        set { _editorName = value; HasNameError = false; OnPropertyChanged(); }
    }

    private bool _isImageUploader = true;
    public bool IsImageUploader
    {
        get => _isImageUploader;
        set { _isImageUploader = value; HasDestinationError = false; OnPropertyChanged(); }
    }

    private bool _isTextUploader;
    public bool IsTextUploader
    {
        get => _isTextUploader;
        set { _isTextUploader = value; HasDestinationError = false; OnPropertyChanged(); }
    }

    private bool _isFileUploader;
    public bool IsFileUploader
    {
        get => _isFileUploader;
        set { _isFileUploader = value; HasDestinationError = false; OnPropertyChanged(); }
    }

    private bool _isUrlShortener;
    public bool IsUrlShortener
    {
        get => _isUrlShortener;
        set { _isUrlShortener = value; HasDestinationError = false; OnPropertyChanged(); }
    }

    private bool _isUrlSharingService;
    public bool IsUrlSharingService
    {
        get => _isUrlSharingService;
        set { _isUrlSharingService = value; HasDestinationError = false; OnPropertyChanged(); }
    }

    private int _selectedMethodIndex = 1;
    public int SelectedMethodIndex
    {
        get => _selectedMethodIndex;
        set { _selectedMethodIndex = value; OnPropertyChanged(); }
    }

    private string _requestUrl = "";
    public string RequestUrl
    {
        get => _requestUrl;
        set { _requestUrl = value; HasUrlError = false; OnPropertyChanged(); }
    }

    private ObservableCollection<KeyValuePairItem> _headers = new();
    public ObservableCollection<KeyValuePairItem> Headers
    {
        get => _headers;
        set { _headers = value; OnPropertyChanged(); }
    }

    private ObservableCollection<KeyValuePairItem> _parameters = new();
    public ObservableCollection<KeyValuePairItem> Parameters
    {
        get => _parameters;
        set { _parameters = value; OnPropertyChanged(); }
    }

    private int _selectedBodyTypeIndex = 1;
    public int SelectedBodyTypeIndex
    {
        get => _selectedBodyTypeIndex;
        set { _selectedBodyTypeIndex = value; OnPropertyChanged(); }
    }

    private string _fileFormName = "file";
    public string FileFormName
    {
        get => _fileFormName;
        set { _fileFormName = value; OnPropertyChanged(); }
    }

    private ObservableCollection<KeyValuePairItem> _arguments = new();
    public ObservableCollection<KeyValuePairItem> Arguments
    {
        get => _arguments;
        set { _arguments = value; OnPropertyChanged(); }
    }

    private string _bodyData = "";
    public string BodyData
    {
        get => _bodyData;
        set { _bodyData = value; OnPropertyChanged(); }
    }

    private string _urlPattern = "";
    public string UrlPattern
    {
        get => _urlPattern;
        set { _urlPattern = value; OnPropertyChanged(); }
    }

    private string _thumbnailUrlPattern = "";
    public string ThumbnailUrlPattern
    {
        get => _thumbnailUrlPattern;
        set { _thumbnailUrlPattern = value; OnPropertyChanged(); }
    }

    private string _deletionUrlPattern = "";
    public string DeletionUrlPattern
    {
        get => _deletionUrlPattern;
        set { _deletionUrlPattern = value; OnPropertyChanged(); }
    }

    private string _errorMessagePattern = "";
    public string ErrorMessagePattern
    {
        get => _errorMessagePattern;
        set { _errorMessagePattern = value; OnPropertyChanged(); }
    }

    private string _jsonInput = "";
    public string JsonInput
    {
        get => _jsonInput;
        set { _jsonInput = value; OnPropertyChanged(); }
    }

    private bool _hasNameError;
    public bool HasNameError
    {
        get => _hasNameError;
        set { _hasNameError = value; OnPropertyChanged(); }
    }

    private bool _hasUrlError;
    public bool HasUrlError
    {
        get => _hasUrlError;
        set { _hasUrlError = value; OnPropertyChanged(); }
    }

    private bool _hasDestinationError;
    public bool HasDestinationError
    {
        get => _hasDestinationError;
        set { _hasDestinationError = value; OnPropertyChanged(); }
    }

    public Action? ScrollToFirstError { get; set; }

    #endregion

    #region Commands

    public ICommand AddNewCommand { get; }
    public ICommand EditSelectedCommand { get; }
    public ICommand SetActiveCommand { get; }
    public ICommand RemoveSelectedCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand TestCommand { get; }
    public ICommand SaveEditorCommand { get; }
    public ICommand CancelEditorCommand { get; }
    public ICommand ImportJsonCommand { get; }
    public ICommand AddHeaderCommand { get; }
    public ICommand RemoveHeaderCommand { get; }
    public ICommand AddParameterCommand { get; }
    public ICommand RemoveParameterCommand { get; }
    public ICommand AddArgumentCommand { get; }
    public ICommand RemoveArgumentCommand { get; }

    #endregion

    public MobileCustomUploaderConfigViewModel()
    {
        AddNewCommand = new RelayCommand(ShowAddNew);
        EditSelectedCommand = new RelayCommand(ShowEditSelected);
        SetActiveCommand = new RelayCommand(SetActive);
        RemoveSelectedCommand = new RelayCommand(RemoveSelected);
        SaveCommand = new RelayCommand(() => SaveConfig());
        TestCommand = new AsyncRelayCommand(TestConfigAsync);
        SaveEditorCommand = new RelayCommand(SaveFromEditor);
        CancelEditorCommand = new RelayCommand(CancelEditor);
        ImportJsonCommand = new RelayCommand(ImportJson);
        AddHeaderCommand = new RelayCommand(() => AddKeyValueItem(Headers));
        RemoveHeaderCommand = new RelayCommand<KeyValuePairItem>(item => { if (item != null) Headers.Remove(item); });
        AddParameterCommand = new RelayCommand(() => AddKeyValueItem(Parameters));
        RemoveParameterCommand = new RelayCommand<KeyValuePairItem>(item => { if (item != null) Parameters.Remove(item); });
        AddArgumentCommand = new RelayCommand(() => AddKeyValueItem(Arguments));
        RemoveArgumentCommand = new RelayCommand<KeyValuePairItem>(item => { if (item != null) Arguments.Remove(item); });
    }

    #region IMobileUploaderConfig Implementation

    public void LoadConfig()
    {
        try
        {
            var list = SettingsManager.UploadersConfig?.CustomUploadersList;
            var activeIndex = SettingsManager.UploadersConfig?.CustomImageUploaderSelected ?? -1;
            var items = new ObservableCollection<CustomUploaderListItem>();
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var item = list[i];
                    items.Add(new CustomUploaderListItem
                    {
                        Name = item.ToString(),
                        HostName = URLHelpers.GetHostName(item.RequestURL) ?? item.RequestURL,
                        Index = i,
                        IsActive = i == activeIndex
                    });
                }
            }
            CustomUploaders = items;
            UpdateIsConfigured();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[MobileCustomUploaderConfig] LoadConfig failed");
        }
    }

    public bool SaveConfig()
    {
        try
        {
            SettingsManager.SaveUploadersConfig();
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[MobileCustomUploaderConfig] SaveConfig failed");
            return false;
        }
    }

    public async Task<bool> TestConfigAsync()
    {
        IsTesting = true;
        try
        {
            await Task.Delay(300);
            var list = SettingsManager.UploadersConfig?.CustomUploadersList;
            var activeIndex = SettingsManager.UploadersConfig?.CustomImageUploaderSelected ?? -1;
            if (list == null || activeIndex < 0 || activeIndex >= list.Count)
            {
                IsTesting = false;
                return false;
            }
            var item = list[activeIndex];
            var validationError = CustomUploaderRepository.ValidateItem(item);
            if (validationError != null)
            {
                IsTesting = false;
                return false;
            }
            IsTesting = false;
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[MobileCustomUploaderConfig] TestConfig failed");
            IsTesting = false;
            return false;
        }
    }

    #endregion

    #region Editor Methods

    private void ShowAddNew()
    {
        ResetEditor();
        EditorTitle = "Add Custom Uploader";
        IsEditMode = false;
        EditingIndex = -1;
        IsEditorVisible = true;
    }

    private void ShowEditSelected()
    {
        if (SelectedUploaderIndex < 0 || SelectedUploaderIndex >= CustomUploaders.Count)
            return;
        var listItem = CustomUploaders[SelectedUploaderIndex];
        var config = SettingsManager.UploadersConfig;
        if (config == null) return;
        var itemIndex = listItem.Index;
        if (itemIndex < 0 || itemIndex >= config.CustomUploadersList.Count) return;
        var item = config.CustomUploadersList[itemIndex];
        LoadItemIntoEditor(item);
        EditorTitle = $"Edit: {item}";
        IsEditMode = true;
        EditingIndex = itemIndex;
        IsEditorVisible = true;
    }

    private void ResetEditor()
    {
        EditorName = "";
        IsImageUploader = true;
        IsTextUploader = false;
        IsFileUploader = false;
        IsUrlShortener = false;
        IsUrlSharingService = false;
        SelectedMethodIndex = 1;
        RequestUrl = "";
        Headers = new ObservableCollection<KeyValuePairItem>();
        Parameters = new ObservableCollection<KeyValuePairItem>();
        SelectedBodyTypeIndex = 1;
        FileFormName = "file";
        Arguments = new ObservableCollection<KeyValuePairItem>();
        BodyData = "";
        UrlPattern = "";
        ThumbnailUrlPattern = "";
        DeletionUrlPattern = "";
        ErrorMessagePattern = "";
        JsonInput = "";
        HasNameError = false;
        HasUrlError = false;
        HasDestinationError = false;
    }

    private void LoadItemIntoEditor(CustomUploaderItem item)
    {
        EditorName = item.Name;
        IsImageUploader = item.DestinationType.HasFlag(CustomUploaderDestinationType.ImageUploader);
        IsTextUploader = item.DestinationType.HasFlag(CustomUploaderDestinationType.TextUploader);
        IsFileUploader = item.DestinationType.HasFlag(CustomUploaderDestinationType.FileUploader);
        IsUrlShortener = item.DestinationType.HasFlag(CustomUploaderDestinationType.URLShortener);
        IsUrlSharingService = item.DestinationType.HasFlag(CustomUploaderDestinationType.URLSharingService);
        SelectedMethodIndex = (int)item.RequestMethod;
        RequestUrl = item.RequestURL;
        var headers = new ObservableCollection<KeyValuePairItem>();
        if (item.Headers != null)
            foreach (var kvp in item.Headers)
                headers.Add(CreateKeyValueItem(kvp.Key, kvp.Value, headers));
        Headers = headers;
        var parameters = new ObservableCollection<KeyValuePairItem>();
        if (item.Parameters != null)
            foreach (var kvp in item.Parameters)
                parameters.Add(CreateKeyValueItem(kvp.Key, kvp.Value, parameters));
        Parameters = parameters;
        SelectedBodyTypeIndex = (int)item.Body;
        FileFormName = string.IsNullOrEmpty(item.FileFormName) ? "file" : item.FileFormName;
        var arguments = new ObservableCollection<KeyValuePairItem>();
        if (item.Arguments != null)
            foreach (var kvp in item.Arguments)
                arguments.Add(CreateKeyValueItem(kvp.Key, kvp.Value, arguments));
        Arguments = arguments;
        BodyData = item.Data;
        UrlPattern = item.URL;
        ThumbnailUrlPattern = item.ThumbnailURL;
        DeletionUrlPattern = item.DeletionURL;
        ErrorMessagePattern = item.ErrorMessage;
        JsonInput = "";
    }

    private CustomUploaderItem BuildItemFromEditor()
    {
        var item = CustomUploaderItem.Init();
        item.Name = EditorName?.Trim() ?? "";
        item.RequestURL = RequestUrl?.Trim() ?? "";
        item.RequestMethod = (XerahS.Uploaders.HttpMethod)SelectedMethodIndex;
        item.Body = (CustomUploaderBody)SelectedBodyTypeIndex;
        item.FileFormName = FileFormName?.Trim() ?? "";
        item.Data = BodyData?.Trim() ?? "";
        var destType = CustomUploaderDestinationType.None;
        if (IsImageUploader) destType |= CustomUploaderDestinationType.ImageUploader;
        if (IsTextUploader) destType |= CustomUploaderDestinationType.TextUploader;
        if (IsFileUploader) destType |= CustomUploaderDestinationType.FileUploader;
        if (IsUrlShortener) destType |= CustomUploaderDestinationType.URLShortener;
        if (IsUrlSharingService) destType |= CustomUploaderDestinationType.URLSharingService;
        item.DestinationType = destType;
        var headers = new Dictionary<string, string>();
        foreach (var h in Headers)
            if (!string.IsNullOrWhiteSpace(h.Key))
                headers[h.Key] = h.Value ?? "";
        item.Headers = headers.Count > 0 ? headers : null;
        var parameters = new Dictionary<string, string>();
        foreach (var p in Parameters)
            if (!string.IsNullOrWhiteSpace(p.Key))
                parameters[p.Key] = p.Value ?? "";
        item.Parameters = parameters.Count > 0 ? parameters : null;
        var arguments = new Dictionary<string, string>();
        foreach (var a in Arguments)
            if (!string.IsNullOrWhiteSpace(a.Key))
                arguments[a.Key] = a.Value ?? "";
        item.Arguments = arguments.Count > 0 ? arguments : null;
        item.URL = UrlPattern?.Trim() ?? "";
        item.ThumbnailURL = ThumbnailUrlPattern?.Trim() ?? "";
        item.DeletionURL = DeletionUrlPattern?.Trim() ?? "";
        item.ErrorMessage = ErrorMessagePattern?.Trim() ?? "";
        return item;
    }

    private bool ValidateEditor()
    {
        bool valid = true;
        if (string.IsNullOrWhiteSpace(EditorName)) { HasNameError = true; valid = false; }
        if (string.IsNullOrWhiteSpace(RequestUrl)) { HasUrlError = true; valid = false; }
        if (!IsImageUploader && !IsTextUploader && !IsFileUploader && !IsUrlShortener && !IsUrlSharingService)
        { HasDestinationError = true; valid = false; }
        return valid;
    }

    private void SaveFromEditor()
    {
        if (!ValidateEditor()) { ScrollToFirstError?.Invoke(); return; }
        try
        {
            var item = BuildItemFromEditor();
            var config = SettingsManager.UploadersConfig;
            if (config == null) return;
            var pluginsFolder = PathsManager.PluginsFolder;
            if (!Directory.Exists(pluginsFolder))
                Directory.CreateDirectory(pluginsFolder);
            if (IsEditMode && EditingIndex >= 0 && EditingIndex < config.CustomUploadersList.Count)
            {
                var oldItem = config.CustomUploadersList[EditingIndex];
                config.CustomUploadersList[EditingIndex] = item;
                var sxcuFiles = Directory.Exists(pluginsFolder) ? Directory.GetFiles(pluginsFolder, "*.sxcu") : Array.Empty<string>();
                string? filePath = null;
                foreach (var file in sxcuFiles)
                {
                    var loaded = CustomUploaderRepository.LoadFromFile(file);
                    if (loaded.IsValid && loaded.Item.Name == oldItem.Name && loaded.Item.RequestURL == oldItem.RequestURL)
                    { filePath = file; break; }
                }
                if (filePath != null)
                    CustomUploaderRepository.SaveToFile(item, filePath);
                else
                {
                    filePath = Path.Combine(pluginsFolder, item.GetFileName());
                    CustomUploaderRepository.SaveToFile(item, filePath);
                }
                if (EditingIndex == config.CustomImageUploaderSelected)
                    ActivateCustomUploader(item, filePath, EditingIndex);
            }
            else
            {
                config.CustomUploadersList.Add(item);
                var fileName = item.GetFileName();
                var filePath = Path.Combine(pluginsFolder, fileName);
                int suffix = 1;
                while (File.Exists(filePath))
                {
                    filePath = Path.Combine(pluginsFolder, Path.GetFileNameWithoutExtension(fileName) + $"_{suffix}" + Path.GetExtension(fileName));
                    suffix++;
                }
                CustomUploaderRepository.SaveToFile(item, filePath);
                if (config.CustomUploadersList.Count == 1)
                {
                    config.CustomImageUploaderSelected = 0;
                    ActivateCustomUploader(item, filePath, 0);
                }
            }
            SettingsManager.SaveUploadersConfig();
            IsEditorVisible = false;
            LoadConfig();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[MobileCustomUploaderConfig] SaveFromEditor failed");
        }
    }

    private void CancelEditor() => IsEditorVisible = false;

    private void ImportJson()
    {
        if (string.IsNullOrWhiteSpace(JsonInput)) return;
        try
        {
            var loaded = CustomUploaderRepository.LoadFromJson(JsonInput);
            if (!loaded.IsValid) return;
            LoadItemIntoEditor(loaded.Item);
            JsonInput = "";
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[MobileCustomUploaderConfig] ImportJson failed");
        }
    }

    #endregion

    #region List Management

    private void SetActive()
    {
        if (SelectedUploaderIndex < 0 || SelectedUploaderIndex >= CustomUploaders.Count) return;
        try
        {
            var listItem = CustomUploaders[SelectedUploaderIndex];
            var config = SettingsManager.UploadersConfig;
            if (config == null) return;
            var itemIndex = listItem.Index;
            if (itemIndex < 0 || itemIndex >= config.CustomUploadersList.Count) return;
            var item = config.CustomUploadersList[itemIndex];
            config.CustomImageUploaderSelected = itemIndex;
            var pluginsFolder = PathsManager.PluginsFolder;
            var sxcuFiles = Directory.Exists(pluginsFolder) ? Directory.GetFiles(pluginsFolder, "*.sxcu") : Array.Empty<string>();
            string? filePath = null;
            foreach (var file in sxcuFiles)
            {
                var loaded = CustomUploaderRepository.LoadFromFile(file);
                if (loaded.IsValid && loaded.Item.Name == item.Name && loaded.Item.RequestURL == item.RequestURL)
                { filePath = file; break; }
            }
            if (filePath == null)
            {
                if (!Directory.Exists(pluginsFolder))
                    Directory.CreateDirectory(pluginsFolder);
                filePath = Path.Combine(pluginsFolder, item.GetFileName());
                CustomUploaderRepository.SaveToFile(item, filePath);
            }
            ActivateCustomUploader(item, filePath, itemIndex);
            SettingsManager.SaveUploadersConfig();
            LoadConfig();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[MobileCustomUploaderConfig] SetActive failed");
        }
    }

    private void ActivateCustomUploader(CustomUploaderItem item, string filePath, int index)
    {
        try
        {
            var loadedUploader = new LoadedCustomUploader(item, filePath);
            var provider = new CustomUploaderProvider(loadedUploader);
            ProviderCatalog.RegisterProvider(provider);
            var instanceManager = InstanceManager.Instance;
            var existingInstances = instanceManager.GetInstancesByCategory(UploaderCategory.Image);
            var existing = existingInstances.FirstOrDefault(i => i.ProviderId == provider.ProviderId);
            if (existing == null)
            {
                var instance = new UploaderInstance
                {
                    ProviderId = provider.ProviderId,
                    Category = UploaderCategory.Image,
                    DisplayName = item.ToString(),
                    SettingsJson = provider.GetDefaultSettings(UploaderCategory.Image)
                };
                instanceManager.AddInstance(instance);
                existing = instanceManager.GetInstancesByCategory(UploaderCategory.Image).FirstOrDefault(i => i.ProviderId == provider.ProviderId);
            }
            if (existing != null)
            {
                instanceManager.SetDefaultInstance(UploaderCategory.Image, existing.InstanceId);
                SettingsManager.DefaultTaskSettings.DestinationInstanceId = existing.InstanceId;
                SettingsManager.SaveWorkflowsConfig();
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[MobileCustomUploaderConfig] ActivateCustomUploader failed");
        }
    }

    private void RemoveSelected()
    {
        if (SelectedUploaderIndex < 0 || SelectedUploaderIndex >= CustomUploaders.Count) return;
        try
        {
            var listItem = CustomUploaders[SelectedUploaderIndex];
            var config = SettingsManager.UploadersConfig;
            if (config == null) return;
            var itemIndex = listItem.Index;
            if (itemIndex < 0 || itemIndex >= config.CustomUploadersList.Count) return;
            var item = config.CustomUploadersList[itemIndex];
            var wasActive = itemIndex == config.CustomImageUploaderSelected;
            config.CustomUploadersList.RemoveAt(itemIndex);
            var pluginsFolder = PathsManager.PluginsFolder;
            if (Directory.Exists(pluginsFolder))
            {
                foreach (var file in Directory.GetFiles(pluginsFolder, "*.sxcu"))
                {
                    var loaded = CustomUploaderRepository.LoadFromFile(file);
                    if (loaded.IsValid && loaded.Item.Name == item.Name && loaded.Item.RequestURL == item.RequestURL)
                    {
                        try { File.Delete(file); } catch { }
                        CustomUploaderRepository.RemoveFile(file);
                        break;
                    }
                }
            }
            if (config.CustomUploadersList.Count == 0)
                config.CustomImageUploaderSelected = 0;
            else if (itemIndex <= config.CustomImageUploaderSelected)
                config.CustomImageUploaderSelected = Math.Max(0, config.CustomImageUploaderSelected - 1);
            if (wasActive)
            {
                SettingsManager.DefaultTaskSettings.DestinationInstanceId = null;
                SettingsManager.SaveWorkflowsConfig();
            }
            SettingsManager.SaveUploadersConfig();
            LoadConfig();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[MobileCustomUploaderConfig] RemoveSelected failed");
        }
    }

    #endregion

    #region Private Methods

    private void UpdateIsConfigured()
    {
        var list = SettingsManager.UploadersConfig?.CustomUploadersList;
        var activeIndex = SettingsManager.UploadersConfig?.CustomImageUploaderSelected ?? -1;
        IsConfigured = list != null && list.Count > 0 && activeIndex >= 0 && activeIndex < list.Count;
        OnPropertyChanged(nameof(ActiveUploaderName));
    }

    private void AddKeyValueItem(ObservableCollection<KeyValuePairItem> collection)
    {
        KeyValuePairItem? item = null;
        item = new KeyValuePairItem
        {
            RemoveCommand = new RelayCommand(() => collection.Remove(item!))
        };
        collection.Add(item);
    }

    private KeyValuePairItem CreateKeyValueItem(string key, string value, ObservableCollection<KeyValuePairItem> collection)
    {
        var item = new KeyValuePairItem { Key = key, Value = value };
        item.RemoveCommand = new RelayCommand(() => collection.Remove(item));
        return item;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    #endregion
}
