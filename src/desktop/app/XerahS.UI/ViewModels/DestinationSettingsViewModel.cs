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
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core;
using XerahS.UI.Views;
using XerahS.Uploaders;
using XerahS.Uploaders.CustomUploader;
using XerahS.Uploaders.PluginSystem;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace XerahS.UI.ViewModels;

public partial class DestinationSettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<CategoryViewModel> _categories = new();

    [ObservableProperty]
    private CategoryViewModel? _selectedCategory;

    public DestinationSettingsViewModel()
    {
        // Constructor is now empty, initialization moved to Initialize()
    }

    public event Func<string, string, Task>? ShowMessageDialog;

    public async Task Initialize()
    {
        Common.DebugHelper.WriteLine("[DestinationSettings] ========================================");
        Common.DebugHelper.WriteLine("[DestinationSettings] Initializing destination settings...");

        // Initialize built-in providers
        Common.DebugHelper.WriteLine("[DestinationSettings] Initializing built-in providers...");
        ProviderCatalog.InitializeBuiltInProviders();

        // Load external plugins from Plugins folder (for third-party plugins)
        var pluginsPath = PathsManager.PluginsFolder;
        Common.DebugHelper.WriteLine($"[DestinationSettings] Checking for external plugins in: {pluginsPath}");

        if (Directory.Exists(pluginsPath))
        {
            try
            {
                ProviderCatalog.LoadPlugins(pluginsPath);
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
    }

    private void Provider_ConfigChanged(object? sender, EventArgs e)
    {
        // Save uploaders config when any provider's configuration changes
        SettingsManager.SaveUploadersConfigAsync();
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

    [RelayCommand]
    private async Task OpenPluginInstaller()
    {
        var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (mainWindow == null)
        {
            Common.DebugHelper.WriteLine("[DestinationSettings] Cannot open plugin installer (main window missing).");
            return;
        }

        try
        {
            var dialog = new PluginInstallerDialog();
            await dialog.ShowDialog<bool>(mainWindow);
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
                var topLevel = Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

                if (topLevel?.StorageProvider == null)
                {
                    await ShowMessageDialogAsync("Import Failed", "No window available to open the file picker.");
                    return;
                }

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select ShareX UploadersConfig.json",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("ShareX Config") { Patterns = new[] { "UploadersConfig.json" } },
                        new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } }
                    }
                });

                if (files.Count == 0)
                {
                    return;
                }

                configPath = files[0].Path.LocalPath;
            }

            var result = UploadersConfigImporter.ImportFromFile(configPath, SettingsManager.UploadersConfig);
            SettingsManager.SaveUploadersConfig();

            OnPropertyChanged(string.Empty);

            await ShowMessageDialogAsync("Import Complete", result.GetSummary());
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
        var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (mainWindow == null)
        {
            Common.DebugHelper.WriteLine("[DestinationSettings] Cannot open custom uploader editor (main window missing).");
            return;
        }

        try
        {
            var viewModel = new CustomUploaderEditorViewModel();
            var dialog = new CustomUploaderEditorDialog
            {
                DataContext = viewModel
            };

            var result = await dialog.ShowDialog<bool>(mainWindow);

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

                var filePath = Path.Combine(pluginsPath, $"{safeName}.sxcu");

                // Ensure unique filename
                int counter = 1;
                while (File.Exists(filePath))
                {
                    filePath = Path.Combine(pluginsPath, $"{safeName}_{counter++}.sxcu");
                }

                if (CustomUploaderRepository.SaveToFile(item, filePath))
                {
                    // Reload custom uploaders to include the new one
                    ProviderCatalog.LoadCustomUploaders(pluginsPath);

                    // Refresh all categories to show the new uploader
                    foreach (var category in Categories)
                    {
                        category.LoadInstances();
                    }

                    await ShowMessageDialogAsync("Custom Uploader Created",
                        $"Custom uploader '{item.Name}' has been saved and is now available in the catalog.");
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

    private async Task ShowMessageDialogAsync(string title, string message)
    {
        if (ShowMessageDialog != null)
        {
            await ShowMessageDialog.Invoke(title, message);
            return;
        }

        DebugHelper.WriteLine($"[DestinationSettings] {title}: {message}");
    }
}
