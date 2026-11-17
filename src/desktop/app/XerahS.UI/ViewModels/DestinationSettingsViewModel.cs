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

    /// <summary>File type overrides: extensions that always go to one instance.</summary>
    public FileTypeOverridesViewModel Overrides { get; } = new();

    /// <summary>True while the overrides pane is shown instead of a category.</summary>
    [ObservableProperty]
    private bool _showOverrides;

    [RelayCommand]
    private void OpenOverrides()
    {
        Overrides.Reload();
        SelectedCategory = null;
        ShowOverrides = true;
    }

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
        var imageCategory = new CategoryViewModel("Image Uploaders", UploaderCategory.Image);
        imageCategory.LoadInstances();
        Categories.Add(imageCategory);

        var textCategory = new CategoryViewModel("Text Uploaders", UploaderCategory.Text);
        textCategory.LoadInstances();
        Categories.Add(textCategory);

        var fileCategory = new CategoryViewModel("File Uploaders", UploaderCategory.File);
        fileCategory.LoadInstances();
        Categories.Add(fileCategory);

        var urlCategory = new CategoryViewModel("URL Shorteners", UploaderCategory.UrlShortener);
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
        if (value != null)
        {
            ShowOverrides = false;
        }
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
                var filePath = await _dialogService.ShowFilePickerAsync("Select ShareX UploadersConfig.json", new[] { "*UploadersConfig*.json", "*.json" });

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
                ? "Import Complete (With Warnings)"
                : "Import Complete";

            string summary = BuildImportSummary(configPath, result, customUploaderExport, builtinMigration);
            await ShowMessageDialogAsync(title, summary);
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync("Import Failed", $"Failed to import UploadersConfig:{Environment.NewLine}{ex.Message}");
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
                    await ShowMessageDialogAsync("Custom Uploader Already Exists",
                        $"A custom uploader with identical configuration as '{item.Name}' already exists.");
                    return;
                }

                if (CustomUploaderRepository.SaveToFile(item, filePath))
                {
                    var instanceResult = EnsureCustomUploaderInstances(filePath);
                    RefreshCategories(instanceResult.AffectedCategories);

                    await ShowMessageDialogAsync("Custom Uploader Created",
                        BuildCustomUploaderCreatedMessage(item.Name, instanceResult));
                }
                else
                {
                    await ShowMessageDialogAsync("Save Failed",
                        "Failed to save the custom uploader. Check the logs for details.");
                }
            }
        }
        catch (Exception ex)
        {
            Common.DebugHelper.WriteException(ex, "Failed to create custom uploader");
            await ShowMessageDialogAsync("Error", $"Failed to create custom uploader: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExportDestinationConfig(UploaderInstanceViewModel? instance)
    {
        if (instance == null)
        {
            await ShowMessageDialogAsync("Export Failed", "Select a destination instance to export.");
            return;
        }

        try
        {
            string? passphrase = await _dialogService.ShowSecretInputAsync(
                "Export Destination Config",
                "Enter a passphrase for this .xsdc file:");
            if (string.IsNullOrWhiteSpace(passphrase))
            {
                return;
            }

            string? confirmation = await _dialogService.ShowSecretInputAsync(
                "Confirm Passphrase",
                "Re-enter the passphrase:");
            if (!string.Equals(passphrase, confirmation, StringComparison.Ordinal))
            {
                await ShowMessageDialogAsync("Export Failed", "Passphrases do not match.");
                return;
            }

            string fileName = MakeSafeFileName(instance.DisplayName);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "DestinationConfig";
            }

            string? filePath = await _dialogService.ShowSaveFilePickerAsync(
                "Export XerahS Destination Config",
                fileName,
                "xsdc",
                new[] { "*.xsdc" });
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            string exportJson = DestinationConfigExportService.BuildEncryptedExport(instance.Instance, passphrase);
            await File.WriteAllTextAsync(filePath, exportJson, Encoding.UTF8);
            await ShowMessageDialogAsync("Destination Config Exported", $"Saved encrypted destination config:{Environment.NewLine}{filePath}");
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync("Export Failed", ex.Message);
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
        builder.AppendLine($"Custom uploader '{uploaderName}' has been saved to Plugins.");

        if (instanceResult.CreatedInstances.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"Created {instanceResult.CreatedInstances.Count} destination instance(s): {FormatCategoryList(instanceResult.CreatedInstances.Select(instance => instance.Category))}.");
        }

        if (instanceResult.SkippedCategories.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"Existing instance bindings were reused for: {FormatCategoryList(instanceResult.SkippedCategories)}.");
        }

        if (instanceResult.CreatedInstances.Count == 0 && instanceResult.SkippedCategories.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("No destination categories were updated.");
        }
        else
        {
            builder.AppendLine();
            builder.Append("The uploader is ready to use without Add from Catalog.");
        }

        return builder.ToString();
    }

    private static string FormatCategoryList(IEnumerable<UploaderCategory> categories)
    {
        var labels = categories
            .Distinct()
            .Select(category => category switch
            {
                UploaderCategory.Image => "Image Uploaders",
                UploaderCategory.Text => "Text Uploaders",
                UploaderCategory.File => "File Uploaders",
                UploaderCategory.UrlShortener => "URL Shorteners",
                UploaderCategory.UrlSharing => "URL Sharing",
                _ => category.ToString()
            })
            .ToList();

        return labels.Count > 0 ? string.Join(", ", labels) : "None";
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
        builder.AppendLine($"Source: {sourceConfigPath}");
        builder.AppendLine();
        builder.Append(importResult.GetSummary());

        if (importResult.TotalImportedCustomUploaders > 0)
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine("Custom uploader export:");
            builder.AppendLine($"- Imported from config: {importResult.TotalImportedCustomUploaders}");
            builder.AppendLine($"- Created .sxcu files: {customUploaderExport.ExportedCount}");
            builder.AppendLine($"- Skipped duplicates: {customUploaderExport.SkippedCount}");
            builder.AppendLine($"- Failed exports: {customUploaderExport.FailedCount}");
            builder.AppendLine($"- Plugins folder: {customUploaderExport.PluginsPath}");

            if (customUploaderExport.InstancesCreated > 0)
            {
                builder.AppendLine();
                builder.Append($"Auto-created {customUploaderExport.InstancesCreated} destination instance(s) - ready to use.");
            }

            if (customUploaderExport.InstancesSkipped > 0)
            {
                builder.AppendLine();
                builder.Append($"Reused {customUploaderExport.InstancesSkipped} existing destination binding(s) without creating duplicates.");
            }
        }

        if (builtinMigration.HasAnything)
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine("Built-in provider migration:");
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
