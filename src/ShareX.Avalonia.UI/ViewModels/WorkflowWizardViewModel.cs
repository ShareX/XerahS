using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using ShareX.Ava.Core;
using ShareX.Ava.Core.Hotkeys;
using ShareX.Ava.Common;
using ShareX.Ava.Uploaders;
using ShareX.UploadersLib;
using ShareX.Ava.Uploaders.PluginSystem;

namespace ShareX.Ava.UI.ViewModels;





public partial class WorkflowWizardViewModel : ObservableObject
{
    // Step 1: Job Selection
    public ObservableCollection<JobCategoryViewModel> JobCategories { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvailableDestinations))]
    [NotifyPropertyChangedFor(nameof(ReviewJobName))]
    private HotkeyItemViewModel? _selectedJob;

    // Step 2: Destination
    // Use UploaderInstanceViewModel directly for the list
    public ObservableCollection<UploaderInstanceViewModel> AvailableDestinations { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReviewDestinationName))]
    private UploaderInstanceViewModel? _selectedDestination;

    // Categories for loading instances (mirrors DestinationSettingsViewModel)
    private CategoryViewModel _imageCategory;
    private CategoryViewModel _textCategory;
    private CategoryViewModel _fileCategory;
    private CategoryViewModel _urlCategory;

    // Step 3: Tasks
    [ObservableProperty] public bool taskSaveToFile;
    [ObservableProperty] public bool taskCopyImage;
    [ObservableProperty] public bool taskUpload;
    [ObservableProperty] public bool taskCopyUrl;
    
    // Step 4: General
    [ObservableProperty] private string _workflowName;
    [ObservableProperty] private string _hotkeyString = "None";
    
    // Summary properties
    public string ReviewJobName => SelectedJob?.Description ?? "None";
    public string ReviewDestinationName => SelectedDestination?.DisplayName ?? "None";

    // UI properties
    [ObservableProperty] private string _title = "Create New Workflow";
    [ObservableProperty] private string _buttonText = "Create";

    public WorkflowWizardViewModel()
    {
        InitializeCategories();
        LoadJobCategories();
        // Default selection
        SelectedJob = JobCategories.FirstOrDefault()?.Jobs.FirstOrDefault();
        
        // Default workflow name from selected job
        WorkflowName = SelectedJob?.Description ?? "My New Workflow";
            
        // Default tasks
        TaskSaveToFile = true;
        TaskCopyImage = true;
    }

    private void InitializeCategories()
    {
        // Re-use logic from DestinationSettingsViewModel
        _imageCategory = new CategoryViewModel("Image Uploaders", UploaderCategory.Image);
        _imageCategory.LoadInstances();

        _textCategory = new CategoryViewModel("Text Uploaders", UploaderCategory.Text);
        _textCategory.LoadInstances();

        _fileCategory = new CategoryViewModel("File Uploaders", UploaderCategory.File);
        _fileCategory.LoadInstances();
        
        // URL Shorteners not typically a primary destination for workflows in this context, but good to have
        _urlCategory = new CategoryViewModel("URL Shorteners", UploaderCategory.UrlShortener);
        _urlCategory.LoadInstances();
    }

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
        // Map internal category names to display names
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
        // Define display order
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

    partial void OnSelectedJobChanged(HotkeyItemViewModel? value)
    {
        UpdateDestinations();
        
        // Auto-set reasonable defaults based on job category
        if (value != null)
        {
            // Update workflow name to match job description (if it's still default or empty)
            if (string.IsNullOrWhiteSpace(WorkflowName) || WorkflowName == "My New Workflow" || 
                JobCategories.SelectMany(c => c.Jobs).Any(j => j.Description == WorkflowName))
            {
                WorkflowName = value.Description;
            }
            
            string category = GetHotkeyCategory(value.Model.Job);
            
            if (category == EnumExtensions.HotkeyType_Category_Upload)
            {
                TaskUpload = true;
                TaskCopyUrl = true;
            }
            else if (category == EnumExtensions.HotkeyType_Category_ScreenCapture || 
                     category == EnumExtensions.HotkeyType_Category_ScreenRecord)
            {
                TaskSaveToFile = true;
                TaskUpload = true;
                TaskCopyUrl = true;
            }
        }
    }

    private void UpdateDestinations()
    {
        AvailableDestinations.Clear();

        if (SelectedJob == null) return;

        string category = GetHotkeyCategory(SelectedJob.Model.Job);
        
        // Determine which destination types to show based on category
        bool showImageUploaders = false;
        bool showTextUploaders = false;
        bool showFileUploaders = false;

        switch (category)
        {
            case EnumExtensions.HotkeyType_Category_ScreenCapture:
            case EnumExtensions.HotkeyType_Category_ScreenRecord:
                // Screen captures can go to Image or File uploaders
                showImageUploaders = true;
                showFileUploaders = true;
                break;
                
            case EnumExtensions.HotkeyType_Category_Upload:
                // Check specific upload type
                var job = SelectedJob.Model.Job;
                if (job == HotkeyType.UploadText)
                {
                    showTextUploaders = true;
                    showFileUploaders = true;
                }
                else if (job == HotkeyType.FileUpload || job == HotkeyType.FolderUpload)
                {
                    showFileUploaders = true;
                }
                else // ClipboardUpload, DragDropUpload, etc - could be image or file
                {
                    showImageUploaders = true;
                    showFileUploaders = true;
                }
                break;
                
            case EnumExtensions.HotkeyType_Category_Tools:
                // Tools generally don't upload, but offer option
                showImageUploaders = true;
                showFileUploaders = true;
                break;
        }

        // Add "None" / "Local Only" - Create a dummy instance for this
        // Or handle null selection as None. Let's create a dummy logic.
        // Actually, UploaderInstance can be "None" if we create a special one?
        // Or we just add a special ViewModel.
        // To keep it simple, let's just allow Null selection or explicit "Local Storage".
        // Let's assume the user picks *something* if TaskUpload is checked.
        // But for the list, we need UploaderInstanceViewModels.
        // Let's stick to valid instances only in the list for now, and maybe a "None" option if needed.
        // However, "TaskUpload" checkbox controls if it's used. 
        // So the list should just be "If you upload, where does it go?".
        
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
        
        // Select first valid destination (prefer Imgur for images, first for others)
        if (showImageUploaders)
        {
            SelectedDestination = AvailableDestinations.FirstOrDefault(d => d.Category == UploaderCategory.Image && d.DisplayName.Contains("Imgur")) 
                                  ?? AvailableDestinations.FirstOrDefault(d => d.Category == UploaderCategory.Image)
                                  ?? AvailableDestinations.FirstOrDefault();
        }
        else
        {
            SelectedDestination = AvailableDestinations.FirstOrDefault();
        }
    }

    public HotkeySettings ConstructHotkeySettings()
    {
        var settings = new HotkeySettings();
        settings.TaskSettings.Description = WorkflowName;
        settings.Job = SelectedJob?.Model.Job ?? HotkeyType.None;
        
        // Apply Destination Logic from Selected UploaderInstance
        // Only if Upload task is enabled!
        if (TaskUpload && SelectedDestination != null)
        {
            var instance = SelectedDestination.Instance;
            
            // Map the UploaderInstance back to HotkeySettings/TaskSettings properties
            // 1. Is it a built-in provider (Enum based)?
            // 2. Is it a Custom Uploader?
            // 3. Is it an FTP Account?
            // 4. Is it a Plugin? (Wait, TaskSettings implies plugins also use CustomUploader logic or just IDs?)
            // Actually, TaskSettings in Core currently relies on Enums (ImageDestination, TextDestination, FileDestination).
            // It assumes specific Enums for built-in providers.
            // Custom Uploaders work via OverrideCustomUploader + Index.
            // FTP works via OverrideFTP + Index.
            // Plugins? If the new system uses ProviderId, how does TaskSettings store it?
            // Looking at TaskSettings.cs, there is NO generic "ProviderId" or configuration string.
            // This means the current Core HotkeySettings is NOT fully compatible with the new Plugin system 
            // UNLESS the Plugin system maps back to "CustomFileUploader" or existing Enums.
            
            // However, the DestinationSettingsViewModel loads "Instances". 
            // Included "Built-in" ones are wrapped as UploaderInstance too.
            // So we need to reverse-map from UploaderInstance.ProviderId to the legacy Enum if possible.
            
            // Let's look at how UploaderInstance is constructed for built-ins.
            // If it's an Enum-based provider, the ProviderId matches the Enum/Name.
            
            // Plan:
            // 1. Check if it's a Custom Uploader (ProviderId starts with "CustomUploader" or we check the list)
            // 2. Check if it's FTP.
            // 3. Check if it matches an Enum value.
            
            // Find in Custom Uploaders List
            var customUploaders = SettingManager.UploadersConfig.CustomUploadersList;
            // int customIndex = customUploaders.FindIndex(c => c.Name == instance.DisplayName); 
            // Wait, UploaderInstance.InstanceId is unique. ProviderId is the type.
            // If the user created a specific Custom Uploader instance, it corresponds to an entry in CustomUploadersList.
            
            // Wait, CustomUploadersList IS the list of definitions.
            // Ideally UploaderInstance holds enough info.
            
            // Re-reading logic:
            // If UploaderInstance is a wrapper, we need to know WHICH wrapper it is.
            // But we don't have easy access to the exact backing store index here without search.
            
            // Let's try matching via Helper method or inspection.
            
            if (TryMapToCustomUploader(instance, settings))
            {
               // Mapped
            }
            else if (TryMapToFTP(instance, settings))
            {
               // Mapped
            }
            else
            {
                // Must be standard Enum
               MapToStandardEnum(instance, settings);
            }
        }

        // Apply Tasks
        AfterCaptureTasks captureTasks = 0;
        if (TaskSaveToFile) captureTasks |= AfterCaptureTasks.SaveImageToFile;
        if (TaskCopyImage) captureTasks |= AfterCaptureTasks.CopyImageToClipboard;
        if (TaskUpload) captureTasks |= AfterCaptureTasks.UploadImageToHost;
        
        settings.TaskSettings.AfterCaptureJob = captureTasks;
        settings.TaskSettings.UseDefaultAfterCaptureJob = false;

        AfterUploadTasks uploadTasks = 0;
        if (TaskCopyUrl) uploadTasks |= AfterUploadTasks.CopyURLToClipboard;
        
        settings.TaskSettings.AfterUploadJob = uploadTasks;
        settings.TaskSettings.UseDefaultAfterUploadJob = false;
        
        settings.TaskSettings.UseDefaultDestinations = false;

        return settings;
    }

    private bool TryMapToCustomUploader(UploaderInstance instance, HotkeySettings settings)
    {
        var list = SettingManager.UploadersConfig.CustomUploadersList;
        // Match by InstanceID (Guid) if we can link it?
        // UploaderInstance has InstanceId. 
        // Existing "CustomUploaderItem" doesn't necessarily have a Guid that matches UploaderInstance unless migrated.
        // But UploaderInstanceWrapper (if used) would wrap it.
        
        // Let's search by Name + some signature or ID.
        for(int i=0; i<list.Count; i++)
        {
             // If the uploader instance allows us to identify it.
             // ProviderId for custom uploader is usually "CustomImageUploader", "CustomTextUploader", "CustomFileUploader".
             if (instance.ProviderId == "CustomImageUploader" || 
                 instance.ProviderId == "CustomTextUploader" || 
                 instance.ProviderId == "CustomFileUploader")
             {
                 // Check if names match - weak link but standard for legacy
                 if (list[i].Name == instance.DisplayName)
                 {
                     settings.TaskSettings.OverrideCustomUploader = true;
                     settings.TaskSettings.CustomUploaderIndex = i;
                     
                     if (instance.ProviderId.Contains("Image")) settings.TaskSettings.ImageDestination = ImageDestination.CustomImageUploader;
                     if (instance.ProviderId.Contains("Text")) settings.TaskSettings.TextDestination = TextDestination.CustomTextUploader;
                     if (instance.ProviderId.Contains("File")) settings.TaskSettings.FileDestination = FileDestination.CustomFileUploader;
                     
                     return true;
                 }
             }
        }
        return false;
    }

    private bool TryMapToFTP(UploaderInstance instance, HotkeySettings settings)
    {
         if (instance.ProviderId == "FTP" || instance.DisplayName.StartsWith("FTP: "))
         {
            var list = SettingManager.UploadersConfig.FTPAccountList;
            for(int i=0; i<list.Count; i++)
            {
                if (instance.DisplayName.EndsWith(list[i].Name)) // Name format "FTP: {Name}"
                {
                    settings.TaskSettings.OverrideFTP = true;
                    settings.TaskSettings.FTPIndex = i;
                    settings.TaskSettings.FileDestination = FileDestination.FTP;
                    settings.TaskSettings.ImageDestination = ImageDestination.FileUploader; // FTP counts as FileUploader usually for images
                    return true;
                }
            }
         }
         return false;
    }

    private void MapToStandardEnum(UploaderInstance instance, HotkeySettings settings)
    {
        // ProviderId usually matches Enum name, e.g. "Imgur", "Dropbox"
        
        if (Enum.TryParse<ImageDestination>(instance.ProviderId, out var imgDest))
        {
            settings.TaskSettings.ImageDestination = imgDest;
        }
        
        if (Enum.TryParse<TextDestination>(instance.ProviderId, out var txtDest))
        {
            settings.TaskSettings.TextDestination = txtDest;
        }
        
        if (Enum.TryParse<FileDestination>(instance.ProviderId, out var fileDest))
        {
            settings.TaskSettings.FileDestination = fileDest;
        }
    }

    public void LoadFromSettings(HotkeySettings settings)
    {
        Title = "Edit Workflow";
        ButtonText = "Save";
        WorkflowName = settings.TaskSettings.Description;
        
        // 1. Select Job
        foreach (var category in JobCategories)
        {
            var job = category.Jobs.FirstOrDefault(j => j.Model.Job == settings.Job);
            if (job != null)
            {
                SelectedJob = job;
                break;
            }
        }

        // 2. Select Destination
        // We need to reverse-map from TaskSettings to UploaderInstanceViewModel
        // Try finding a matching instance based on settings
        
        // Strategy: 
        // Iterate available destinations and check if they match the current TaskSettings
        // This is tricky because TaskSettings stores Enums/Indices, but AvailableDestinations are UploaderInstances.
        // We should construct a temporary HotkeySettings from each candidate and compare? Too expensive.
        // Instead, check the properties directly.

        UpdateDestinations(); // Refresh list for the selected job

        UploaderInstanceViewModel? matched = null;

        // Custom Uploader
        if (settings.TaskSettings.OverrideCustomUploader)
        {
            var customList = SettingManager.UploadersConfig.CustomUploadersList;
            if (settings.TaskSettings.CustomUploaderIndex >= 0 && settings.TaskSettings.CustomUploaderIndex < customList.Count)
            {
                var custom = customList[settings.TaskSettings.CustomUploaderIndex];
                matched = AvailableDestinations.FirstOrDefault(d => 
                    d.DisplayName == custom.Name && 
                    (d.Instance.ProviderId == "CustomImageUploader" || d.Instance.ProviderId == "CustomTextUploader" || d.Instance.ProviderId == "CustomFileUploader"));
            }
        }
        // FTP
        else if (settings.TaskSettings.OverrideFTP)
        {
            var ftpList = SettingManager.UploadersConfig.FTPAccountList;
            if (settings.TaskSettings.FTPIndex >= 0 && settings.TaskSettings.FTPIndex < ftpList.Count)
            {
                var ftp = ftpList[settings.TaskSettings.FTPIndex];
                matched = AvailableDestinations.FirstOrDefault(d => d.DisplayName == $"FTP: {ftp.Name}");
            }
        }
        // Standard Enum
        else
        {
            // Determine relevant enum based on job category / what we have
            // Or look for any match
            
            // Check ImageDestination
            var imgDest = settings.TaskSettings.ImageDestination;
            if (imgDest != ImageDestination.CustomImageUploader && imgDest != ImageDestination.FileUploader)
            {
                 var candidate = AvailableDestinations.FirstOrDefault(d => d.Instance.ProviderId == imgDest.ToString());
                 if (candidate != null) matched = candidate;
            }
            
            // Check TextDestination
            if (matched == null)
            {
                var txtDest = settings.TaskSettings.TextDestination;
                if (txtDest != TextDestination.CustomTextUploader && txtDest != TextDestination.FileUploader)
                {
                    var candidate = AvailableDestinations.FirstOrDefault(d => d.Instance.ProviderId == txtDest.ToString());
                    if (candidate != null) matched = candidate;
                }
            }
            
            // Check FileDestination
            if (matched == null)
            {
                var fileDest = settings.TaskSettings.FileDestination;
                if (fileDest != FileDestination.CustomFileUploader && fileDest != FileDestination.SharedFolder && fileDest != FileDestination.Email)
                {
                    var candidate = AvailableDestinations.FirstOrDefault(d => d.Instance.ProviderId == fileDest.ToString());
                    if (candidate != null) matched = candidate;
                }
            }
        }

        if (matched != null)
        {
            SelectedDestination = matched;
        }

        // 3. Tasks
        var captureTasks = settings.TaskSettings.AfterCaptureJob;
        TaskSaveToFile = captureTasks.HasFlag(AfterCaptureTasks.SaveImageToFile);
        TaskCopyImage = captureTasks.HasFlag(AfterCaptureTasks.CopyImageToClipboard);
        TaskUpload = captureTasks.HasFlag(AfterCaptureTasks.UploadImageToHost);
        
        var uploadTasks = settings.TaskSettings.AfterUploadJob;
        TaskCopyUrl = uploadTasks.HasFlag(AfterUploadTasks.CopyURLToClipboard);
    }
}

public class JobCategoryViewModel
{
    public string Name { get; }
    public ObservableCollection<HotkeyItemViewModel> Jobs { get; }

    public JobCategoryViewModel(string name, IEnumerable<HotkeyType> jobs)
    {
        Name = name;
        Jobs = new ObservableCollection<HotkeyItemViewModel>(
            jobs.Select(j => new HotkeyItemViewModel(new HotkeySettings(j, Key.None)))
        );
    }
}
