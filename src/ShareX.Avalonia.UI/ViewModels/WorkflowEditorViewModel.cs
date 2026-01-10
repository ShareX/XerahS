using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace XerahS.UI.ViewModels;

public partial class WorkflowEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    private WorkflowSettings _model;

    [ObservableProperty]
    private Key _selectedKey;

    [ObservableProperty]
    private KeyModifiers _selectedModifiers;

    [ObservableProperty]
    private HotkeyType _selectedJob;



    // Destinations
    public ObservableCollection<UploaderInstanceViewModel> AvailableDestinations { get; } = new();

    [ObservableProperty]
    private UploaderInstanceViewModel? _selectedDestination;

    private CategoryViewModel _imageCategory;
    private CategoryViewModel _textCategory;
    private CategoryViewModel _fileCategory;
    private CategoryViewModel _urlCategory;

    public string WindowTitle
    {
        get
        {
            var baseTitle = Model.HotkeyInfo.Id == 0 ? "Add Workflow" : "Edit Workflow";
            var desc = Description;
            if (string.IsNullOrEmpty(desc))
            {
                desc = EnumExtensions.GetDescription(Model.Job);
            }
            return $"{baseTitle} - {desc}";
        }
    }

    public string Description
    {
        get => Model.TaskSettings.Description;
        set
        {
            if (Model.TaskSettings.Description != value)
            {
                Model.TaskSettings.Description = value;
                OnPropertyChanged(nameof(Description));
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    // Categories
    public ObservableCollection<JobCategoryViewModel> JobCategories { get; } = new();

    [ObservableProperty]
    private JobCategoryViewModel? _selectedJobCategory;

    [ObservableProperty]
    private HotkeyItemViewModel? _selectedJobItem;

    partial void OnSelectedJobItemChanged(HotkeyItemViewModel? value)
    {
        if (value != null)
        {
            SelectedJob = value.Model.Job;
        }
    }

    // Sub-ViewModels
    public TaskSettingsViewModel TaskSettings { get; private set; }

    public WorkflowEditorViewModel(WorkflowSettings model)
    {
        _model = model;
        _selectedKey = model.HotkeyInfo.Key;
        _selectedModifiers = model.HotkeyInfo.Modifiers;
        _selectedJob = model.Job;

        // Initialize TaskSettings VM
        if (model.TaskSettings == null)
            model.TaskSettings = new TaskSettings();

        TaskSettings = new TaskSettingsViewModel(model.TaskSettings);

        TaskSettings = new TaskSettingsViewModel(model.TaskSettings);

        LoadJobCategories();

        // Select the current job from the category tree
        SelectJobInCategories(model.Job);

        InitializeCategories();
        UpdateDestinations();
        LoadSelectedDestination();
    }

    private void InitializeCategories()
    {
        _imageCategory = new CategoryViewModel("Image Uploaders", UploaderCategory.Image);
        _imageCategory.LoadInstances();

        _textCategory = new CategoryViewModel("Text Uploaders", UploaderCategory.Text);
        _textCategory.LoadInstances();

        _fileCategory = new CategoryViewModel("File Uploaders", UploaderCategory.File);
        _fileCategory.LoadInstances();

        _urlCategory = new CategoryViewModel("URL Shorteners", UploaderCategory.UrlShortener);
        _urlCategory.LoadInstances();
    }

    partial void OnSelectedJobChanged(HotkeyType value)
    {
        UpdateDestinations();
    }

    private void UpdateDestinations()
    {
        if (_imageCategory == null || _textCategory == null || _fileCategory == null || _urlCategory == null)
        {
            InitializeCategories();
        }

        AvailableDestinations.Clear();

        string category = GetHotkeyCategory(SelectedJob);

        // Determine which destination types to show based on category
        bool showImageUploaders = false;
        bool showTextUploaders = false;
        bool showFileUploaders = false;

        switch (category)
        {
            case EnumExtensions.HotkeyType_Category_ScreenCapture:
            case EnumExtensions.HotkeyType_Category_ScreenRecord:
                showImageUploaders = true;
                showFileUploaders = true;
                break;

            case EnumExtensions.HotkeyType_Category_Upload:
                if (SelectedJob == HotkeyType.UploadText)
                {
                    showTextUploaders = true;
                    showFileUploaders = true;
                }
                else if (SelectedJob == HotkeyType.FileUpload || SelectedJob == HotkeyType.FolderUpload)
                {
                    showFileUploaders = true;
                }
                else
                {
                    showImageUploaders = true;
                    showFileUploaders = true;
                }
                break;

            case EnumExtensions.HotkeyType_Category_Tools:
                showImageUploaders = true;
                showFileUploaders = true;
                break;
        }

        if (showImageUploaders)
        {
            foreach (var instance in _imageCategory.Instances)
                AvailableDestinations.Add(instance);
        }

        if (showTextUploaders)
        {
            foreach (var instance in _textCategory.Instances)
                AvailableDestinations.Add(instance);
        }

        if (showFileUploaders)
        {
            foreach (var instance in _fileCategory.Instances)
                AvailableDestinations.Add(instance);
        }

        if (SelectedDestination == null)
        {
            SelectedDestination = AvailableDestinations.FirstOrDefault();
        }
    }



    private void LoadSelectedDestination()
    {
        // Logic to try and find the currently configured destination in the list
        // Simplified version of WorkflowWizardViewModel.LoadFromSettings

        UploaderInstanceViewModel? matched = null;
        var settings = Model;

        if (settings.TaskSettings.OverrideCustomUploader)
        {
            var customList = SettingManager.UploadersConfig.CustomUploadersList;
            if (settings.TaskSettings.CustomUploaderIndex >= 0 && settings.TaskSettings.CustomUploaderIndex < customList.Count)
            {
                var custom = customList[settings.TaskSettings.CustomUploaderIndex];
                matched = AvailableDestinations.FirstOrDefault(d => d.DisplayName == custom.Name);
            }
        }
        else if (settings.TaskSettings.OverrideFTP)
        {
            var ftpList = SettingManager.UploadersConfig.FTPAccountList;
            if (settings.TaskSettings.FTPIndex >= 0 && settings.TaskSettings.FTPIndex < ftpList.Count)
            {
                var ftp = ftpList[settings.TaskSettings.FTPIndex];
                matched = AvailableDestinations.FirstOrDefault(d => d.DisplayName == $"FTP: {ftp.Name}");
            }
        }
        else
        {
            var imgDest = settings.TaskSettings.ImageDestination;
            if (imgDest != ImageDestination.CustomImageUploader && imgDest != ImageDestination.FileUploader)
            {
                var candidate = AvailableDestinations.FirstOrDefault(d => d.Instance.ProviderId == imgDest.ToString());
                if (candidate != null) matched = candidate;
            }
        }

        if (matched != null)
            SelectedDestination = matched;
    }

    public void Save()
    {
        Model.HotkeyInfo.Key = SelectedKey;
        Model.HotkeyInfo.Modifiers = SelectedModifiers;
        Model.Job = SelectedJob;

        // Ensure TaskSettings knows its job too
        if (Model.TaskSettings != null)
        {
            Model.TaskSettings.Job = SelectedJob;

            // Save Destination if selected
            if (SelectedDestination != null)
            {
                // Reset overrides first to ensure clean state
                Model.TaskSettings.OverrideCustomUploader = false;
                Model.TaskSettings.OverrideFTP = false;

                // 1. Check if it's a Custom Uploader
                var customList = SettingManager.UploadersConfig.CustomUploadersList;
                var customIndex = customList.FindIndex(c => c.Name == SelectedDestination.DisplayName);
                
                // 2. Check if it's an FTP account (DisplayName format is "FTP: Name")
                var isFtp = SelectedDestination.DisplayName.StartsWith("FTP: ");
                
                if (customIndex >= 0)
                {
                    Model.TaskSettings.OverrideCustomUploader = true;
                    Model.TaskSettings.CustomUploaderIndex = customIndex;
                    DebugHelper.WriteLine($"Workflow saved with Custom Uploader: {SelectedDestination.DisplayName}");
                }
                else if (isFtp)
                {
                    var ftpName = SelectedDestination.DisplayName.Substring(5);
                    var ftpList = SettingManager.UploadersConfig.FTPAccountList;
                    var ftpIndex = ftpList.FindIndex(f => f.Name == ftpName);
                    
                    if (ftpIndex >= 0)
                    {
                        Model.TaskSettings.OverrideFTP = true;
                        Model.TaskSettings.FTPIndex = ftpIndex;
                        DebugHelper.WriteLine($"Workflow saved with FTP: {ftpName}");
                    }
                }
                else if (SelectedDestination.Instance != null && !string.IsNullOrEmpty(SelectedDestination.Instance.ProviderId))
                {
                    // 3. Standard Uploader - Try to update relevant destination enums
                    // We update the specific destination type based on what the provider ID parses to.
                    // This handles Image, File, Text, and URL Shorteners.
                    
                    bool typeFound = false;

                    if (Enum.TryParse<ImageDestination>(SelectedDestination.Instance.ProviderId, out var imgDest))
                    {
                        Model.TaskSettings.ImageDestination = imgDest;
                        typeFound = true;
                    }
                    
                    if (Enum.TryParse<FileDestination>(SelectedDestination.Instance.ProviderId, out var fileDest))
                    {
                        Model.TaskSettings.FileDestination = fileDest;
                         typeFound = true;
                    }
                    
                    if (Enum.TryParse<TextDestination>(SelectedDestination.Instance.ProviderId, out var textDest))
                    {
                        Model.TaskSettings.TextDestination = textDest;
                         typeFound = true;
                    }
                    
                     if (Enum.TryParse<UrlShortenerType>(SelectedDestination.Instance.ProviderId, out var urlDest))
                    {
                        Model.TaskSettings.URLShortenerDestination = urlDest;
                         typeFound = true;
                    }

                    if (typeFound)
                    {
                        DebugHelper.WriteLine($"Workflow saved with standard destination: {SelectedDestination.Instance.ProviderId}");
                    }
                    else
                    {
                        DebugHelper.WriteLine($"Warning: Could not map provider {SelectedDestination.Instance.ProviderId} to any destination enum.");
                    }
                }
            }
        }
    }

    [RelayCommand]
    private void Clear()
    {
        SelectedKey = Key.None;
        SelectedModifiers = KeyModifiers.None;
    }

    public string KeyText
    {
        get
        {
            if (SelectedKey == Key.None && SelectedModifiers == KeyModifiers.None)
                return "None";

            var info = new HotkeyInfo { Key = SelectedKey, Modifiers = SelectedModifiers };
            return info.ToString();
        }
    }

    partial void OnSelectedKeyChanged(Key value) => OnPropertyChanged(nameof(KeyText));
    partial void OnSelectedModifiersChanged(KeyModifiers value) => OnPropertyChanged(nameof(KeyText));

    private void LoadJobCategories()
    {
        // Group HotkeyTypes by their Category attribute
        var allTypes = Enum.GetValues(typeof(HotkeyType)).Cast<HotkeyType>()
            .Where(t => t != HotkeyType.None);

        var grouped = allTypes.GroupBy(GetHotkeyCategory)
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .OrderBy(g => GetCategoryOrder(g.Key));

        foreach (var group in grouped)
        {
            var category = new JobCategoryViewModel(GetCategoryDisplayName(group.Key), group);
            JobCategories.Add(category);
        }
    }

    private void SelectJobInCategories(HotkeyType job)
    {
        foreach (var category in JobCategories)
        {
            var item = category.Jobs.FirstOrDefault(j => j.Model.Job == job);
            if (item != null)
            {
                SelectedJobCategory = category;
                SelectedJobItem = item;
                break;
            }
        }

        // If not found (e.g. None), maybe select first generic
        if (SelectedJobItem == null && JobCategories.Count > 0)
        {
            SelectedJobCategory = JobCategories[0];
            SelectedJobItem = SelectedJobCategory.Jobs.FirstOrDefault();
        }
    }

    private string GetHotkeyCategory(HotkeyType type)
    {
        var field = type.GetType().GetField(type.ToString());
        if (field != null)
        {
            var attrs = (CategoryAttribute[])field.GetCustomAttributes(typeof(CategoryAttribute), false);
            if (attrs.Length > 0)
            {
                return attrs[0].Category;
            }
        }
        return string.Empty;
    }

    private string GetCategoryDisplayName(string category)
    {
        return category switch
        {
            EnumExtensions.HotkeyType_Category_Upload => "Upload",
            EnumExtensions.HotkeyType_Category_ScreenCapture => "Screen Capture",
            EnumExtensions.HotkeyType_Category_ScreenRecord => "Screen Record",
            EnumExtensions.HotkeyType_Category_Tools => "Tools",
            EnumExtensions.HotkeyType_Category_Other => "Other",
            _ => category
        };
    }

    private int GetCategoryOrder(string category)
    {
        return category switch
        {
            EnumExtensions.HotkeyType_Category_ScreenCapture => 0,
            EnumExtensions.HotkeyType_Category_ScreenRecord => 1,
            EnumExtensions.HotkeyType_Category_Upload => 2,
            EnumExtensions.HotkeyType_Category_Tools => 3,
            EnumExtensions.HotkeyType_Category_Other => 4,
            _ => 99
        };
    }
}
