using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Ava.Uploaders.PluginSystem;
using System.Collections.ObjectModel;
using System.IO;

namespace ShareX.Ava.UI.ViewModels;

/// <summary>
/// ViewModel for a single uploader instance in the list
/// </summary>
public partial class UploaderInstanceViewModel : ViewModelBase
{
    [ObservableProperty]
    private Guid _instanceId;

    [ObservableProperty]
    private string _providerId = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private UploaderCategory _category;

    [ObservableProperty]
    private bool _isDefault;

    [ObservableProperty]
    private bool _isAvailable;

    [ObservableProperty]
    private string _settingsJson = "{}";

    [ObservableProperty]
    private IUploaderConfigViewModel? _configViewModel;

    [ObservableProperty]
    private object? _configView;

    [ObservableProperty]
    private string _fileTypeScopeDisplay = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FileTypeItemViewModel> _availableFileTypes = new();

    [ObservableProperty]
    private bool _isAllFileTypes;

    [ObservableProperty]
    private ObservableCollection<string> _selectedFileExtensions = new();

    [ObservableProperty]
    private ConflictWarningViewModel _conflictWarning = new();

    [ObservableProperty]
    private string _verificationStatus = string.Empty;

    [ObservableProperty]
    private string _verificationMessage = string.Empty;

    [ObservableProperty]
    private List<string> _verificationIssues = new();

    [ObservableProperty]
    private bool _hasVerificationWarning;

    [ObservableProperty]
    private bool _hasVerificationError;

    /// <summary>
    /// The actual instance model
    /// </summary>
    public UploaderInstance Instance { get; }

    public UploaderInstanceViewModel(UploaderInstance instance)
    {
        Instance = instance;
        _instanceId = instance.InstanceId;
        _providerId = instance.ProviderId;
        _displayName = instance.DisplayName;
        _category = instance.Category;
        _settingsJson = instance.SettingsJson;
        _isAvailable = instance.IsAvailable;

        InitializeConfigViewModel();
        InitializeFileTypeScope();
        VerifyPluginConfiguration();
        
        // Subscribe to file type changes
        PropertyChanged += OnPropertyChanged;
    }

    private void VerifyPluginConfiguration()
    {
        var result = PluginConfigurationVerifier.VerifyPluginConfiguration(ProviderId);
        
        VerificationMessage = result.Message;
        VerificationIssues = result.Issues;
        VerificationStatus = result.Status.ToString();
        HasVerificationWarning = result.Status == PluginVerificationStatus.Warning;
        HasVerificationError = result.Status == PluginVerificationStatus.Error;
        
        Common.DebugHelper.WriteLine($"[UploaderInstanceVM] Plugin verification for {ProviderId}: {result.Status} - {result.Message}");
    }

    [RelayCommand]
    private void CleanDuplicates()
    {
        Common.DebugHelper.WriteLine($"[UploaderInstanceVM] Cleaning duplicate DLLs for {ProviderId}");
        
        var deletedCount = PluginConfigurationVerifier.CleanDuplicateFrameworkDlls(ProviderId);
        
        if (deletedCount > 0)
        {
            Common.DebugHelper.WriteLine($"[UploaderInstanceVM] Deleted {deletedCount} duplicate DLL(s)");
            
            // Re-verify after cleanup
            VerifyPluginConfiguration();
            
            // Update status message to show success
            VerificationMessage = $"✓ Cleaned {deletedCount} duplicate DLL(s) - Please restart the application";
        }
        else
        {
            VerificationMessage = "No duplicate files found to clean";
        }
    }

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsAllFileTypes) || e.PropertyName == nameof(SelectedFileExtensions))
        {
            UpdateFileTypeScope();
            UpdateFileTypeScopeDisplay();
            ValidateConfiguration();
        }
    }

    private void InitializeConfigViewModel()
    {
        Common.DebugHelper.WriteLine($"[UploaderInstanceVM] InitializeConfigViewModel for ProviderId: {ProviderId}");
        
        var provider = ProviderCatalog.GetProvider(ProviderId);
        if (provider != null)
        {
            Common.DebugHelper.WriteLine($"[UploaderInstanceVM] Provider found: {provider.Name}");
            
            ConfigViewModel = provider.CreateConfigViewModel();
            Common.DebugHelper.WriteLine($"[UploaderInstanceVM] ConfigViewModel created: {ConfigViewModel?.GetType().Name ?? "null"}");
            
            ConfigView = provider.CreateConfigView();
            Common.DebugHelper.WriteLine($"[UploaderInstanceVM] ConfigView created: {ConfigView?.GetType().Name ?? "null"}");
        }
        else
        {
            Common.DebugHelper.WriteLine($"[UploaderInstanceVM] WARNING: Provider not found for ProviderId: {ProviderId}");
        }

        if (ConfigViewModel != null)
        {
            Common.DebugHelper.WriteLine($"[UploaderInstanceVM] Loading settings from JSON for {ProviderId}");
            ConfigViewModel.LoadFromJson(SettingsJson);
            
            if (ConfigViewModel is ObservableObject obs)
            {
                obs.PropertyChanged += (s, e) =>
                {
                    // Sync settings back to JSON when any property changes
                    SettingsJson = ConfigViewModel.ToJson();
                    Instance.SettingsJson = SettingsJson;
                    
                    // Persist changes to disk
                    InstanceManager.Instance.UpdateInstance(Instance);
                };
            }

            if (ConfigView is Avalonia.Controls.Control control)
            {
                Common.DebugHelper.WriteLine($"[UploaderInstanceVM] Setting DataContext on ConfigView for {ProviderId}");
                control.DataContext = ConfigViewModel;
            }
            else
            {
                Common.DebugHelper.WriteLine($"[UploaderInstanceVM] WARNING: ConfigView is not an Avalonia Control");
            }
        }
        else
        {
            Common.DebugHelper.WriteLine($"[UploaderInstanceVM] WARNING: ConfigViewModel is null for {ProviderId}");
        }
    }

    private void InitializeFileTypeScope()
    {
        // Load current file type scope from instance
        IsAllFileTypes = Instance.FileTypeRouting.AllFileTypes;
        
        SelectedFileExtensions.Clear();
        foreach (var ext in Instance.FileTypeRouting.FileExtensions)
        {
            SelectedFileExtensions.Add(ext);
        }

        LoadAvailableFileTypes();
        UpdateFileTypeScopeDisplay();
    }

    private void LoadAvailableFileTypes()
    {
        AvailableFileTypes.Clear();
        
        var provider = ProviderCatalog.GetProvider(ProviderId);
        if (provider == null) return;

        var supportedTypes = provider.GetSupportedFileTypes();
        if (!supportedTypes.TryGetValue(Category, out var fileTypes)) return;

        var blockedTypes = InstanceManager.Instance.GetBlockedFileTypes(Category, InstanceId);

        foreach (var fileType in fileTypes)
        {
            bool isBlocked = blockedTypes.ContainsKey(fileType);
            string? blockedBy = isBlocked ? blockedTypes[fileType] : null;
            bool isSelected = SelectedFileExtensions.Contains(fileType, StringComparer.OrdinalIgnoreCase);

            var item = new FileTypeItemViewModel
            {
                Extension = fileType,
                IsBlocked = isBlocked,
                BlockedByInstance = blockedBy,
                IsSelected = isSelected
            };

            // Subscribe to selection changes
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FileTypeItemViewModel.IsSelected) && s is FileTypeItemViewModel fileTypeItem)
                {
                    if (fileTypeItem.IsSelected && !SelectedFileExtensions.Contains(fileTypeItem.Extension, StringComparer.OrdinalIgnoreCase))
                    {
                        SelectedFileExtensions.Add(fileTypeItem.Extension);
                    }
                    else if (!fileTypeItem.IsSelected && SelectedFileExtensions.Contains(fileTypeItem.Extension, StringComparer.OrdinalIgnoreCase))
                    {
                        SelectedFileExtensions.Remove(fileTypeItem.Extension);
                    }
                }
            };

            AvailableFileTypes.Add(item);
        }
    }

    private void UpdateFileTypeScope()
    {
        Instance.FileTypeRouting.AllFileTypes = IsAllFileTypes;
        Instance.FileTypeRouting.FileExtensions.Clear();
        
        if (!IsAllFileTypes)
        {
            foreach (var ext in SelectedFileExtensions)
            {
                Instance.FileTypeRouting.FileExtensions.Add(ext);
            }
        }

        InstanceManager.Instance.UpdateInstance(Instance);
    }

    private void UpdateFileTypeScopeDisplay()
    {
        if (IsAllFileTypes)
        {
            FileTypeScopeDisplay = "All File Types";
        }
        else if (SelectedFileExtensions.Any())
        {
            FileTypeScopeDisplay = string.Join(", ", SelectedFileExtensions.OrderBy(x => x));
        }
        else
        {
            FileTypeScopeDisplay = "No file types selected";
        }
    }

    private void ValidateConfiguration()
    {
        var validationError = InstanceManager.Instance.ValidateFileTypeConfiguration(Instance);
        ConflictWarning.SetWarning(validationError);
    }

    public void RefreshAvailableFileTypes()
    {
        LoadAvailableFileTypes();
    }

    public void UpdateFromInstance(UploaderInstance instance)
    {
        DisplayName = instance.DisplayName;
        SettingsJson = instance.SettingsJson;
        IsAvailable = instance.IsAvailable;
        
        ConfigViewModel?.LoadFromJson(SettingsJson);
    }

    [RelayCommand]
    private void OpenPluginsFolder()
    {
        try
        {
            var pluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", ProviderId);
            Common.DebugHelper.WriteLine($"[UploaderInstanceVM] Opening plugins folder: {pluginsPath}");

            if (!Directory.Exists(pluginsPath))
            {
                Common.DebugHelper.WriteLine("[UploaderInstanceVM] Plugins folder does not exist, creating...");
                Directory.CreateDirectory(pluginsPath);
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = pluginsPath,
                UseShellExecute = true,
                Verb = "open"
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            Common.DebugHelper.WriteException(ex, "Failed to open plugins folder");
        }
    }
}
