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
using XerahS.Common;
using XerahS.Uploaders.CustomUploader;
using XerahS.Uploaders.PluginSystem;
using System.Collections.ObjectModel;
using XerahS.UI.Services;

namespace XerahS.UI.ViewModels;

/// <summary>
/// ViewModel for a single uploader instance in the list
/// </summary>
public partial class UploaderInstanceViewModel : ViewModelBase
{
    private bool _isSynchronizingConfigViewModel;

    [ObservableProperty]
    private string _instanceId = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomUploaderInstance))]
    [NotifyPropertyChangedFor(nameof(CanExportDestinationConfig))]
    private string _providerId = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTextFilesOption))]
    private UploaderCategory _category;

    [ObservableProperty]
    private bool _isDefault;

    [ObservableProperty]
    private bool _isAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExplorerEnabled))]
    private string _settingsJson = "{}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanManageDefinition))]
    private IUploaderConfigViewModel? _configViewModel;

    [ObservableProperty]
    private object? _configView;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanManageDefinition))]
    private bool _hasDefinitionBinding;

    [ObservableProperty]
    private string _definitionFilePath = string.Empty;

    [ObservableProperty]
    private string _boundInstanceIdsDisplay = string.Empty;

    [ObservableProperty]
    private int _boundInstanceCount;

    [ObservableProperty]
    private string _definitionBindingUnavailableReason = "This instance cannot be mapped back to a .sxcu source file.";

    [ObservableProperty]
    private string _fileTypeScopeDisplay = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FileTypeItemViewModel> _availableFileTypes = new();

    [ObservableProperty]
    private bool _isAllFileTypes;

    [ObservableProperty]
    private bool _isTextFiles;

    /// <summary>Comma-separated extensions that "Text files" must not claim.</summary>
    [ObservableProperty]
    private string _textFileExceptionsText = string.Empty;

    public bool ShowTextFileExceptions => ShowTextFilesOption && IsTextFiles;

    /// <summary>"Text files" only routes file uploads; text and image uploads have their own categories.</summary>
    public bool ShowTextFilesOption => Category == UploaderCategory.File;

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
    /// True if this instance's provider implements <see cref="IUploaderExplorer"/>.
    /// Controls visibility of the "Browse Files" button in the config panel.
    /// </summary>
    [ObservableProperty]
    private bool _supportsExplorer;

    /// <summary>
    /// True when <see cref="SupportsExplorer"/> is true AND the provider's
    /// <c>ValidateSettings</c> returns true for the current <see cref="SettingsJson"/>.
    /// Controls whether the "Browse Files" button is enabled.
    /// </summary>
    public bool IsExplorerEnabled =>
        SupportsExplorer && (ProviderCatalog.GetProvider(ProviderId)?.ValidateSettings(SettingsJson) == true);

    public bool IsCustomUploaderInstance => ProviderId.StartsWith("custom_", StringComparison.OrdinalIgnoreCase);

    public bool CanExportDestinationConfig => string.Equals(ProviderId, "amazons3", StringComparison.OrdinalIgnoreCase);

    public bool CanManageDefinition => HasDefinitionBinding && ConfigViewModel is CustomUploaderEditorViewModel;

    public string DefinitionSaveHelpText =>
        "Instance-only edits update uploader-instances.json until you use Save definition to Plugins.";

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
            VerificationMessage = $"Cleaned {deletedCount} duplicate DLL(s). Please restart the application.";
        }
        else
        {
            VerificationMessage = "No duplicate files found to clean";
        }
    }

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsTextFiles))
        {
            OnPropertyChanged(nameof(ShowTextFileExceptions));
        }

        if (e.PropertyName == nameof(IsAllFileTypes) || e.PropertyName == nameof(IsTextFiles) ||
            e.PropertyName == nameof(TextFileExceptionsText) || e.PropertyName == nameof(SelectedFileExtensions))
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

            try
            {
                ConfigViewModel = provider.CreateConfigViewModel();
                ConfigView = provider.CreateConfigView();
            }
            catch (Exception ex)
            {
                Common.DebugHelper.WriteException(ex, $"Failed to create config UI for {ProviderId}");
                ConfigViewModel = null;
                ConfigView = null;
            }

            if (ConfigViewModel == null && ConfigView == null)
            {
                UploaderConfigSchema? schema = provider.GetConfigSchema();
                if (schema != null && schema.Fields.Count > 0)
                {
                    ConfigViewModel = new SchemaConfigViewModel(schema);
                    ConfigView = new Views.SchemaConfigView();
                }
            }

            // Custom uploaders use the full editor form inline in the provider settings area.
            if (ConfigViewModel == null && ConfigView == null && ProviderId.StartsWith("custom_", StringComparison.OrdinalIgnoreCase))
            {
                Common.DebugHelper.WriteLine("[UploaderInstanceVM] Creating inline custom uploader editor view/viewmodel");
                ConfigViewModel = new CustomUploaderEditorViewModel();
                ConfigView = new Views.CustomUploaderEditorFormView();
            }

            Common.DebugHelper.WriteLine($"[UploaderInstanceVM] ConfigViewModel created: {ConfigViewModel?.GetType().Name ?? "null"}");
            Common.DebugHelper.WriteLine($"[UploaderInstanceVM] ConfigView created: {ConfigView?.GetType().Name ?? "null"}");

            // Set explorer support flag after provider is resolved
            SupportsExplorer = provider is IUploaderExplorer;
            RefreshDefinitionBindingInfo(provider as CustomUploaderProvider);

            if (ConfigViewModel is IProviderContextAware contextAware)
            {
                var context = ProviderCatalog.GetProviderContext();
                if (context != null)
                {
                    try
                    {
                        contextAware.SetContext(context);
                    }
                    catch (Exception ex)
                    {
                        Common.DebugHelper.WriteException(ex, $"Failed to apply provider context for {ProviderId}");
                    }
                }
            }
        }
        else
        {
            Common.DebugHelper.WriteLine($"[UploaderInstanceVM] WARNING: Provider not found for ProviderId: {ProviderId}");
            RefreshDefinitionBindingInfo();
        }

        if (ConfigViewModel != null)
        {
            Common.DebugHelper.WriteLine($"[UploaderInstanceVM] Loading settings from JSON for {ProviderId}");

            try
            {
                if (ConfigViewModel is CustomUploaderEditorViewModel customUploaderConfigViewModel)
                {
                    customUploaderConfigViewModel.SetFallbackName(provider?.Name);
                    customUploaderConfigViewModel.IsNameReadOnly = true;
                }

                SynchronizeConfigViewModel(() => ConfigViewModel.LoadFromJson(SettingsJson));
            }
            catch (Exception ex)
            {
                Common.DebugHelper.WriteException(ex, $"Failed to load settings JSON for {ProviderId}");
            }

            if (ConfigViewModel is ObservableObject obs)
            {
                obs.PropertyChanged += (s, e) =>
                {
                    if (_isSynchronizingConfigViewModel)
                    {
                        return;
                    }

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
        else if (provider != null)
        {
            // Some providers (e.g. "auto") intentionally have no config UI.
            Common.DebugHelper.WriteLine($"[UploaderInstanceVM] No config UI for provider: {ProviderId}");
        }
    }

    private void InitializeFileTypeScope()
    {
        // Load current file type scope from instance
        IsAllFileTypes = Instance.FileTypeRouting.AllFileTypes;
        IsTextFiles = Instance.FileTypeRouting.TextFiles;
        TextFileExceptionsText = string.Join(", ", Instance.FileTypeRouting.EffectiveTextFileExceptions);

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
        Instance.FileTypeRouting.TextFiles = !IsAllFileTypes && IsTextFiles;
        Instance.FileTypeRouting.TextFileExceptions = ParseExtensionList(TextFileExceptionsText);
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

    internal static List<string> ParseExtensionList(string? text) =>
        (text ?? string.Empty)
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.TrimStart('.').ToLowerInvariant())
            .Where(x => x.Length > 0)
            .Distinct()
            .ToList();

    private void UpdateFileTypeScopeDisplay()
    {
        if (IsAllFileTypes)
        {
            FileTypeScopeDisplay = "All File Types";
        }
        else if (IsTextFiles || SelectedFileExtensions.Any())
        {
            var parts = SelectedFileExtensions.OrderBy(x => x).ToList();
            if (IsTextFiles)
            {
                parts.Insert(0, "Text files");
            }

            FileTypeScopeDisplay = string.Join(", ", parts);
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

        if (ConfigViewModel is CustomUploaderEditorViewModel customUploaderConfigViewModel)
        {
            customUploaderConfigViewModel.SetFallbackName(ProviderCatalog.GetProvider(ProviderId)?.Name);
        }

        if (ConfigViewModel != null)
        {
            SynchronizeConfigViewModel(() => ConfigViewModel.LoadFromJson(SettingsJson));
        }

        RefreshDefinitionBindingInfo();
    }

    partial void OnDisplayNameChanged(string value)
    {
        Instance.DisplayName = value;
        InstanceManager.Instance.UpdateInstance(Instance);
    }

    partial void OnProviderIdChanged(string value)
    {
        SupportsExplorer = ProviderCatalog.GetProvider(value) is IUploaderExplorer;
        RefreshDefinitionBindingInfo();
        OnPropertyChanged(nameof(IsExplorerEnabled));
    }

    [RelayCommand]
    private async Task SaveDefinitionToPluginsAsync()
    {
        try
        {
            if (!TryGetCustomUploaderDefinitionContext(out var factory, out var provider, out var editorViewModel))
            {
                var unavailableFactory = UiViewModelFactoryAccessor.GetRequired();
                await unavailableFactory.CoreDialogService.ShowWarningAsync("Save Unavailable", DefinitionBindingUnavailableReason);
                return;
            }

            if (!editorViewModel.Validate())
            {
                await factory.CoreDialogService.ShowWarningAsync(
                    "Validation Failed",
                    "Fix the current validation errors before saving the shared .sxcu definition.");
                return;
            }

            var bindingInfo = CustomUploaderDefinitionBindingService.GetBindingInfo(provider, InstanceId);
            if (bindingInfo.HasMultipleBindings)
            {
                bool confirmed = await factory.CoreDialogService.ShowConfirmationAsync(
                    "Save Shared Definition",
                    $"This definition is shared by {bindingInfo.BoundInstanceIds.Count} instances. Saving it will update the shared .sxcu file for all of them.{Environment.NewLine}{Environment.NewLine}Continue?");

                if (!confirmed)
                {
                    return;
                }
            }

            var item = editorViewModel.ToItem();
            if (!CustomUploaderDefinitionBindingService.SaveDefinition(
                    item,
                    provider.FilePath,
                    bindingInfo.BoundInstanceIds,
                    bindingInfo.PrimaryInstanceId))
            {
                await factory.CoreDialogService.ShowErrorAsync("Save Failed", "Failed to write the .sxcu definition to disk.");
                return;
            }

            if (!ProviderCatalog.ReloadCustomUploader(provider.FilePath))
            {
                await factory.CoreDialogService.ShowWarningAsync(
                    "Saved with Warning",
                    "The .sxcu file was saved, but the provider catalog did not reload immediately.");
            }

            RefreshDefinitionBindingInfo();
            await factory.CoreDialogService.ShowMessageAsync("Definition Saved", $"Saved changes to:{Environment.NewLine}{provider.FilePath}");
        }
        catch (Exception ex)
        {
            Common.DebugHelper.WriteException(ex, "Failed to save custom uploader definition");
        }
    }

    [RelayCommand]
    private async Task SaveDefinitionAsNewAsync()
    {
        try
        {
            if (!TryGetCustomUploaderDefinitionContext(out var factory, out var provider, out var editorViewModel))
            {
                var unavailableFactory = UiViewModelFactoryAccessor.GetRequired();
                await unavailableFactory.CoreDialogService.ShowWarningAsync("Save Unavailable", DefinitionBindingUnavailableReason);
                return;
            }

            if (!editorViewModel.Validate())
            {
                await factory.CoreDialogService.ShowWarningAsync(
                    "Validation Failed",
                    "Fix the current validation errors before saving a new .sxcu definition.");
                return;
            }

            string newFilePath = CustomUploaderDefinitionBindingService.BuildForkFilePath(provider.FilePath, InstanceId);
            var item = editorViewModel.ToItem();

            if (!CustomUploaderDefinitionBindingService.SaveDefinition(item, newFilePath, new[] { InstanceId }, InstanceId))
            {
                await factory.CoreDialogService.ShowErrorAsync("Save Failed", "Failed to create the new .sxcu definition.");
                return;
            }

            if (!ProviderCatalog.ReloadCustomUploader(newFilePath))
            {
                await factory.CoreDialogService.ShowErrorAsync(
                    "Reload Failed",
                    "The new .sxcu definition was written, but the provider catalog could not load it.");
                return;
            }

            var newProvider = CustomUploaderDefinitionBindingService.GetProviderByFilePath(newFilePath);
            if (newProvider == null)
            {
                await factory.CoreDialogService.ShowErrorAsync(
                    "Reload Failed",
                    "The new .sxcu definition was written, but no matching provider was found after reload.");
                return;
            }

            var previousBindingInfo = CustomUploaderDefinitionBindingService.GetBindingInfo(provider, InstanceId);
            var remainingInstanceIds = previousBindingInfo.BoundInstanceIds
                .Where(instanceId => !string.Equals(instanceId, InstanceId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            string? remainingPrimaryInstanceId =
                string.Equals(previousBindingInfo.PrimaryInstanceId, InstanceId, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : previousBindingInfo.PrimaryInstanceId;

            if (!CustomUploaderDefinitionBindingService.SaveDefinition(
                    provider.Item,
                    provider.FilePath,
                    remainingInstanceIds,
                    remainingPrimaryInstanceId))
            {
                await factory.CoreDialogService.ShowWarningAsync(
                    "Fork Saved with Warning",
                    "A new .sxcu definition was created for this instance, but the original file metadata could not be updated.");
            }
            else
            {
                ProviderCatalog.ReloadCustomUploader(provider.FilePath);
            }

            editorViewModel.SetFallbackName(newProvider.Name);
            SettingsJson = CustomUploaderSettingsSerializer.SerializeForInstance(item, newProvider.Name);
            Instance.ProviderId = newProvider.ProviderId;
            Instance.SettingsJson = SettingsJson;
            ProviderId = newProvider.ProviderId;
            InstanceManager.Instance.UpdateInstance(Instance);
            RefreshDefinitionBindingInfo();

            await factory.CoreDialogService.ShowMessageAsync(
                "Definition Saved As New",
                $"Saved a dedicated .sxcu definition for this instance:{Environment.NewLine}{newFilePath}");
        }
        catch (Exception ex)
        {
            Common.DebugHelper.WriteException(ex, "Failed to fork custom uploader definition");
        }
    }

    private void SynchronizeConfigViewModel(Action action)
    {
        _isSynchronizingConfigViewModel = true;

        try
        {
            action();
        }
        finally
        {
            _isSynchronizingConfigViewModel = false;
        }
    }

    private void RefreshDefinitionBindingInfo(CustomUploaderProvider? provider = null)
    {
        provider ??= ProviderCatalog.GetProvider(ProviderId) as CustomUploaderProvider;

        if (provider == null || string.IsNullOrWhiteSpace(provider.FilePath))
        {
            HasDefinitionBinding = false;
            DefinitionFilePath = string.Empty;
            BoundInstanceIdsDisplay = InstanceId;
            BoundInstanceCount = string.IsNullOrWhiteSpace(InstanceId) ? 0 : 1;
            DefinitionBindingUnavailableReason = "This instance cannot be mapped back to a .sxcu source file.";
            return;
        }

        var bindingInfo = CustomUploaderDefinitionBindingService.GetBindingInfo(provider, InstanceId);

        HasDefinitionBinding = true;
        DefinitionFilePath = bindingInfo.FilePath;
        BoundInstanceIdsDisplay = bindingInfo.BoundInstanceIds.Count > 0
            ? string.Join(Environment.NewLine, bindingInfo.BoundInstanceIds)
            : InstanceId;
        BoundInstanceCount = bindingInfo.BoundInstanceIds.Count > 0 ? bindingInfo.BoundInstanceIds.Count : 1;
        DefinitionBindingUnavailableReason = string.Empty;
    }

    private bool TryGetCustomUploaderDefinitionContext(
        out IUiViewModelFactory factory,
        out CustomUploaderProvider provider,
        out CustomUploaderEditorViewModel editorViewModel)
    {
        factory = null!;
        provider = null!;
        editorViewModel = null!;

        if (ConfigViewModel is not CustomUploaderEditorViewModel customUploaderEditorViewModel)
        {
            return false;
        }

        if (ProviderCatalog.GetProvider(ProviderId) is not CustomUploaderProvider customUploaderProvider ||
            string.IsNullOrWhiteSpace(customUploaderProvider.FilePath))
        {
            return false;
        }

        factory = UiViewModelFactoryAccessor.GetRequired();
        provider = customUploaderProvider;
        editorViewModel = customUploaderEditorViewModel;
        return true;
    }

    [RelayCommand]
    private void OpenPluginsFolder()
    {
        try
        {
            var customUploaderProvider = ProviderCatalog.GetProvider(ProviderId) as CustomUploaderProvider;
            var pluginsPath = customUploaderProvider != null
                ? Path.GetDirectoryName(customUploaderProvider.FilePath) ?? PathsManager.PluginsFolder
                : ProviderCatalog.GetPluginMetadata(ProviderId)?.PluginDirectory ?? PathsManager.GetUserPluginDirectory(ProviderId);
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

    /// <summary>
    /// Opens the Media Browser on this instance's storage (other browsable destinations stay
    /// one click away in its account switcher).
    /// </summary>
    [RelayCommand]
    private Task OpenExplorer() => Services.MediaBrowserToolService.OpenAsync(Instance);
}
