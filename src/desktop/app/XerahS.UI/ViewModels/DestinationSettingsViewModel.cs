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
using XerahS.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json.Linq;
using XerahS.Common;
using XerahS.Core;
using XerahS.Services.Abstractions;
using XerahS.Uploaders;
using XerahS.Uploaders.CustomUploader;
using XerahS.Uploaders.LegacySupport;
using XerahS.Uploaders.PluginSystem;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;

namespace XerahS.UI.ViewModels;

public partial class DestinationSettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<CategoryViewModel> _categories = new();

    [ObservableProperty]
    private CategoryViewModel? _selectedCategory;

    [ObservableProperty]
    private bool _showImportShareXConfig;

    private readonly IViewDialogService _dialogService;
    private readonly IDialogService _coreDialogService;
    private readonly IUiViewModelFactory _uiViewModelFactory;
    private bool _isInitialized;

    public DestinationSettingsViewModel(IUiViewModelFactory uiViewModelFactory)
    {
        _uiViewModelFactory = uiViewModelFactory;
        _dialogService = uiViewModelFactory.ViewDialogService;
        _coreDialogService = uiViewModelFactory.CoreDialogService;
    }

    public async Task Initialize()
    {
        if (_isInitialized)
        {
            Common.DebugHelper.WriteLine("[DestinationSettings] Initialize skipped (already initialized).");
            return;
        }

        Common.DebugHelper.WriteLine("[DestinationSettings] ========================================");
        Common.DebugHelper.WriteLine("[DestinationSettings] Initializing destination settings...");

        // Initialize built-in providers
        Common.DebugHelper.WriteLine("[DestinationSettings] Initializing built-in providers...");
        ProviderCatalog.InitializeBuiltInProviders();

        // Load external plugins from all configured plugin roots.
        var pluginPaths = PathsManager.GetPluginDirectories().ToList();
        Common.DebugHelper.WriteLine($"[DestinationSettings] Checking for external plugins in: {string.Join(", ", pluginPaths)}");

        if (pluginPaths.Count > 0)
        {
            try
            {
                ProviderCatalog.LoadPlugins(pluginPaths);
            }
            catch (Exception ex)
            {
                Common.DebugHelper.WriteException(ex, "Failed to load external plugins");
            }
        }

        var allProviders = ProviderCatalog.GetAllProviders();
        Common.DebugHelper.WriteLine($"[DestinationSettings] Total providers available: {allProviders.Count}");
        foreach (var p in allProviders)
        {
            Common.DebugHelper.WriteLine($"[DestinationSettings]   - {p.Name} ({p.ProviderId})");

            // Subscribe to config change events from each provider
            p.ConfigChanged += Provider_ConfigChanged;
        }

        Common.DebugHelper.WriteLine("[DestinationSettings] ========================================");

        LoadCategories();

        // Show the one-time legacy import button only on the first app run.
        ShowImportShareXConfig = SettingsManager.Settings.IsFirstTimeRun;

        _isInitialized = true;
    }

    private void Provider_ConfigChanged(object? sender, EventArgs e)
    {
        // Save uploaders config when any provider's configuration changes
        _ = SettingsManager.SaveUploadersConfigAsync();
    }

    private void LoadCategories()
    {
        var imageCategory = new CategoryViewModel(Resources.GetString("Destination_CategoryImageUploaders"), UploaderCategory.Image);
        imageCategory.LoadInstances();
        Categories.Add(imageCategory);

        var textCategory = new CategoryViewModel(Resources.GetString("Destination_CategoryTextUploaders"), UploaderCategory.Text);
        textCategory.LoadInstances();
        Categories.Add(textCategory);

        var fileCategory = new CategoryViewModel(Resources.GetString("Destination_CategoryFileUploaders"), UploaderCategory.File);
        fileCategory.LoadInstances();
        Categories.Add(fileCategory);

        var urlCategory = new CategoryViewModel(Resources.GetString("Destination_CategoryUrlShorteners"), UploaderCategory.UrlShortener);
        urlCategory.LoadInstances();
        Categories.Add(urlCategory);

        // Select first category by default
        SelectedCategory = Categories.FirstOrDefault();
    }

    public void RefreshCategory(UploaderCategory category)
    {
        var categoryVm = Categories.FirstOrDefault(c => c.Category == category);
        categoryVm?.LoadInstances();
    }

    partial void OnSelectedCategoryChanged(CategoryViewModel? value)
    {
        value?.LoadInstances();
    }

    [RelayCommand]
    private async Task OpenPluginInstaller()
    {
        try
        {
            var viewModel = _uiViewModelFactory.CreatePluginInstallerViewModel();
            await _dialogService.ShowPluginInstallerAsync(viewModel);
        }
        catch (Exception ex)
        {
            Common.DebugHelper.WriteException(ex, "Failed to open plugin installer");
        }
    }
    [RelayCommand]
    private async Task ImportShareXConfig()
    {
        try
        {
            string? configPath = UploadersConfigImporter.FindShareXUploadersConfig();

            if (configPath == null)
            {
                var filePath = await _dialogService.ShowFilePickerAsync(Resources.GetString("Destination_SelectShareXConfigTitle"), new[] { "*UploadersConfig*.json", "*.json" });

                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return;
                }

                configPath = filePath;
            }

            var result = UploadersConfigImporter.ImportFromFile(configPath, SettingsManager.UploadersConfig);
            SettingsManager.SaveUploadersConfig();

            var customUploaderExport = ExportImportedCustomUploaders(result.ImportedCustomUploaders);

            if (customUploaderExport.ExportedCount > 0 || customUploaderExport.SkippedCount > 0)
            {
                foreach (var filePath in customUploaderExport.ExportedFilePaths)
                {
                    var instanceResult = EnsureCustomUploaderInstances(filePath);
                    customUploaderExport.InstancesCreated += instanceResult.CreatedInstances.Count;
                    customUploaderExport.InstancesSkipped += instanceResult.SkippedCategories.Count;
                }

                RefreshCategories(Categories.Select(category => category.Category));
            }

            // Migrate built-in provider settings (S3, FTP, Pastebin, Imgur)
            var builtinMigration = BuiltinInstanceMigrator.Migrate(SettingsManager.UploadersConfig);

            if (builtinMigration.TotalCreated + builtinMigration.TotalUpdated > 0)
            {
                foreach (var category in Categories)
                {
                    category.LoadInstances();
                }
            }

            string title = customUploaderExport.FailedCount > 0
                ? Resources.GetString("Destination_ImportCompleteWithWarnings")
                : Resources.GetString("Destination_ImportComplete");

            string summary = BuildImportSummary(configPath, result, customUploaderExport, builtinMigration);
            await ShowMessageDialogAsync(title, summary);
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync(Resources.GetString("Destination_ImportFailed"), Resources.GetString("Destination_ImportFailedMessage", ex.Message));
        }
    }

    [RelayCommand]
    private void OpenPluginsFolder()
    {
        try
        {
            var pluginsPath = PathsManager.PluginsFolder;
            if (!Directory.Exists(pluginsPath))
            {
                Directory.CreateDirectory(pluginsPath);
            }

            var psi = new ProcessStartInfo
            {
                FileName = pluginsPath,
                UseShellExecute = true,
                Verb = "open"
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Common.DebugHelper.WriteException(ex, "Failed to open plugins folder");
        }
    }

    [RelayCommand]
    private async Task AddCustomUploader()
    {
        try
        {
            var viewModel = _uiViewModelFactory.CreateCustomUploaderEditorViewModel();
            var result = await _dialogService.ShowCustomUploaderEditorAsync(viewModel);

            if (result)
            {
                // Save the custom uploader to the Plugins folder
                var item = viewModel.ToItem();
                var safeName = MakeSafeFileName(item.Name);
                var pluginsPath = PathsManager.PluginsFolder;

                if (!Directory.Exists(pluginsPath))
                {
                    Directory.CreateDirectory(pluginsPath);
                }

                // Ensure unique filename (with duplicate detection)
                var filePath = ResolveCustomUploaderFilePath(pluginsPath, safeName, item, out bool isDuplicate);

                if (isDuplicate)
                {
                    await ShowMessageDialogAsync(Resources.GetString("Destination_CustomUploaderAlreadyExists"),
                        Resources.GetString("Destination_CustomUploaderAlreadyExistsMessage", item.Name));
                    return;
                }

                if (CustomUploaderRepository.SaveToFile(item, filePath))
                {
                    var instanceResult = EnsureCustomUploaderInstances(filePath);
                    RefreshCategories(instanceResult.AffectedCategories);

                    await ShowMessageDialogAsync(Resources.GetString("Destination_CustomUploaderCreated"),
                        BuildCustomUploaderCreatedMessage(item.Name, instanceResult));
                }
                else
                {
                    await ShowMessageDialogAsync(Resources.GetString("Destination_SaveFailed"),
                        Resources.GetString("Destination_SaveFailedMessage"));
                }
            }
        }
        catch (Exception ex)
        {
            Common.DebugHelper.WriteException(ex, "Failed to create custom uploader");
            await ShowMessageDialogAsync(Resources.GetString("Destination_Error"), Resources.GetString("Destination_CreateCustomUploaderFailed", ex.Message));
        }
    }

    [RelayCommand]
    private async Task ExportDestinationConfig(UploaderInstanceViewModel? instance)
    {
        if (instance == null)
        {
            await ShowMessageDialogAsync(Resources.GetString("Destination_ExportFailed"), Resources.GetString("Destination_ExportSelectInstance"));
            return;
        }

        try
        {
            string? passphrase = await _dialogService.ShowSecretInputAsync(
                Resources.GetString("Destination_ExportConfigTitle"),
                Resources.GetString("Destination_ExportConfigPrompt"));
            if (string.IsNullOrWhiteSpace(passphrase))
            {
                return;
            }

            string? confirmation = await _dialogService.ShowSecretInputAsync(
                Resources.GetString("Destination_ConfirmPassphraseTitle"),
                Resources.GetString("Destination_ConfirmPassphrasePrompt"));
            if (!string.Equals(passphrase, confirmation, StringComparison.Ordinal))
            {
                await ShowMessageDialogAsync(Resources.GetString("Destination_ExportFailed"), Resources.GetString("Destination_ExportPassphraseMismatch"));
                return;
            }

            string fileName = MakeSafeFileName(instance.DisplayName);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "DestinationConfig";
            }

            string? filePath = await _dialogService.ShowSaveFilePickerAsync(
                Resources.GetString("Destination_ExportSaveDialogTitle"),
                fileName,
                "xsdc",
                new[] { "*.xsdc" });
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            string exportJson = DestinationConfigExportService.BuildEncryptedExport(instance.Instance, passphrase);
            await File.WriteAllTextAsync(filePath, exportJson, Encoding.UTF8);
            await ShowMessageDialogAsync(Resources.GetString("Destination_ConfigExported"), Resources.GetString("Destination_ConfigExportedMessage", filePath));
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync(Resources.GetString("Destination_ExportFailed"), ex.Message);
        }
    }

    private CustomUploaderInstanceCreationResult EnsureCustomUploaderInstances(string filePath)
    {
        if (!ProviderCatalog.ReloadCustomUploader(filePath))
        {
            ProviderCatalog.LoadCustomUploaders(Path.GetDirectoryName(filePath) ?? PathsManager.PluginsFolder);
        }

        var provider = CustomUploaderDefinitionBindingService.GetProviderByFilePath(filePath);
        if (provider == null)
        {
            Common.DebugHelper.WriteLine($"[DestinationSettings] Failed to resolve custom uploader provider for: {filePath}");
            return new CustomUploaderInstanceCreationResult();
        }

        var result = CustomUploaderDefinitionBindingService.CreateMissingInstances(provider);

        try
        {
            var boundInstanceIds = CustomUploaderDefinitionBindingService.GetBoundInstanceIds(provider.ProviderId);
            CustomUploaderDefinitionBindingService.SaveDefinition(provider.Item, provider.FilePath, boundInstanceIds);
            ProviderCatalog.ReloadCustomUploader(provider.FilePath);
        }
        catch (Exception ex)
        {
            Common.DebugHelper.WriteException(ex, $"[DestinationSettings] Failed to update custom uploader metadata for {provider.ProviderId}");
        }

        return result;
    }

    private void RefreshCategories(IEnumerable<UploaderCategory> categories)
    {
        var categorySet = categories.Distinct().ToHashSet();

        if (categorySet.Count == 0)
        {
            foreach (var category in Categories)
            {
                category.LoadInstances();
            }

            return;
        }

        foreach (var category in Categories.Where(category => categorySet.Contains(category.Category)))
        {
            category.LoadInstances();
        }
    }

    private static string BuildCustomUploaderCreatedMessage(string uploaderName, CustomUploaderInstanceCreationResult instanceResult)
    {
        var builder = new StringBuilder();
        builder.AppendLine(Resources.GetString("Destination_CustomUploaderSaved", uploaderName));

        if (instanceResult.CreatedInstances.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine(Resources.GetString("Destination_CreatedInstances", instanceResult.CreatedInstances.Count, FormatCategoryList(instanceResult.CreatedInstances.Select(instance => instance.Category))));
        }

        if (instanceResult.SkippedCategories.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine(Resources.GetString("Destination_ReusedBindings", FormatCategoryList(instanceResult.SkippedCategories)));
        }

        if (instanceResult.CreatedInstances.Count == 0 && instanceResult.SkippedCategories.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine(Resources.GetString("Destination_NoCategoriesUpdated"));
        }
        else
        {
            builder.AppendLine();
            builder.Append(Resources.GetString("Destination_ReadyToUse"));
        }

        return builder.ToString();
    }

    private static string FormatCategoryList(IEnumerable<UploaderCategory> categories)
    {
        var labels = categories
            .Distinct()
            .Select(category => category switch
            {
                UploaderCategory.Image => Resources.GetString("Destination_CategoryImageUploaders"),
                UploaderCategory.Text => Resources.GetString("Destination_CategoryTextUploaders"),
                UploaderCategory.File => Resources.GetString("Destination_CategoryFileUploaders"),
                UploaderCategory.UrlShortener => Resources.GetString("Destination_CategoryUrlShorteners"),
                UploaderCategory.UrlSharing => Resources.GetString("Destination_CategoryUrlSharing"),
                _ => category.ToString()
            })
            .ToList();

        return labels.Count > 0 ? string.Join(", ", labels) : Resources.GetString("Destination_CategoryNone");
    }

    private static string MakeSafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "CustomUploader";

        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());

        // Replace spaces with underscores
        safeName = safeName.Replace(' ', '_');

        // Ensure not empty after sanitization
        return string.IsNullOrWhiteSpace(safeName) ? "CustomUploader" : safeName;
    }

    private CustomUploaderExportResult ExportImportedCustomUploaders(IReadOnlyCollection<CustomUploaderItem> customUploaders)
    {
        var result = new CustomUploaderExportResult
        {
            PluginsPath = PathsManager.PluginsFolder
        };

        if (customUploaders.Count == 0)
        {
            return result;
        }

        try
        {
            if (!Directory.Exists(result.PluginsPath))
            {
                Directory.CreateDirectory(result.PluginsPath);
            }
        }
        catch (Exception ex)
        {
            result.FailedCount = customUploaders.Count;
            DebugHelper.WriteException(ex, "Failed to prepare plugins directory for custom uploader import");
            return result;
        }

        foreach (var customUploader in customUploaders)
        {
            if (customUploader == null)
            {
                result.FailedCount++;
                continue;
            }

            string suggestedName = !string.IsNullOrWhiteSpace(customUploader.Name)
                ? customUploader.Name
                : customUploader.ToString();

            string safeName = MakeSafeFileName(suggestedName);
            string filePath = ResolveCustomUploaderFilePath(result.PluginsPath, safeName, customUploader, out bool isDuplicate);

            if (isDuplicate)
            {
                result.SkippedCount++;
                continue;
            }

            if (CustomUploaderRepository.SaveToFile(customUploader, filePath))
            {
                result.ExportedCount++;
                result.ExportedFilePaths.Add(filePath);
            }
            else
            {
                result.FailedCount++;
            }
        }

        return result;
    }

    private static string ResolveCustomUploaderFilePath(
        string pluginsPath,
        string safeName,
        CustomUploaderItem customUploader,
        out bool isDuplicate)
    {
        int counter = 0;

        while (true)
        {
            string fileName = counter == 0 ? $"{safeName}.sxcu" : $"{safeName}_{counter}.sxcu";
            string filePath = Path.Combine(pluginsPath, fileName);

            if (!File.Exists(filePath))
            {
                isDuplicate = false;
                return filePath;
            }

            if (IsEquivalentCustomUploaderFile(filePath, customUploader))
            {
                isDuplicate = true;
                return filePath;
            }

            counter++;
        }
    }

    private static bool IsEquivalentCustomUploaderFile(string filePath, CustomUploaderItem customUploader)
    {
        try
        {
            var existing = CustomUploaderRepository.LoadFromFile(filePath);
            if (!existing.IsValid)
            {
                return false;
            }

            JToken existingToken = JToken.FromObject(existing.Item);
            JToken incomingToken = JToken.FromObject(customUploader);

            return JToken.DeepEquals(existingToken, incomingToken);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"Failed to compare custom uploader file: {filePath}");
            return false;
        }
    }

    private static string BuildImportSummary(
        string sourceConfigPath,
        ImportResult importResult,
        CustomUploaderExportResult customUploaderExport,
        BuiltinMigrationResult builtinMigration)
    {
        var builder = new StringBuilder();
        builder.AppendLine(Resources.GetString("Destination_ImportSource", sourceConfigPath));
        builder.AppendLine();
        builder.Append(importResult.GetSummary());

        if (importResult.TotalImportedCustomUploaders > 0)
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine(Resources.GetString("Destination_ImportCustomUploaderExport"));
            builder.AppendLine(Resources.GetString("Destination_ImportFromConfig", importResult.TotalImportedCustomUploaders));
            builder.AppendLine(Resources.GetString("Destination_ImportCreatedFiles", customUploaderExport.ExportedCount));
            builder.AppendLine(Resources.GetString("Destination_ImportSkippedDuplicates", customUploaderExport.SkippedCount));
            builder.AppendLine(Resources.GetString("Destination_ImportFailedExports", customUploaderExport.FailedCount));
            builder.AppendLine(Resources.GetString("Destination_ImportPluginsFolder", customUploaderExport.PluginsPath));

            if (customUploaderExport.InstancesCreated > 0)
            {
                builder.AppendLine();
                builder.Append(Resources.GetString("Destination_AutoCreatedInstances", customUploaderExport.InstancesCreated));
            }

            if (customUploaderExport.InstancesSkipped > 0)
            {
                builder.AppendLine();
                builder.Append(Resources.GetString("Destination_ReusedBindingsSummary", customUploaderExport.InstancesSkipped));
            }
        }

        if (builtinMigration.HasAnything)
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine(Resources.GetString("Destination_BuiltInMigration"));
            builder.Append(builtinMigration.GetSummary());
        }

        return builder.ToString();
    }

    private sealed class CustomUploaderExportResult
    {
        public string PluginsPath { get; init; } = string.Empty;
        public int ExportedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> ExportedFilePaths { get; } = new();
        public int InstancesCreated { get; set; }
        public int InstancesSkipped { get; set; }
    }

    private async Task ShowMessageDialogAsync(string title, string message)
    {
        await _coreDialogService.ShowMessageAsync(title, message);
    }
}
