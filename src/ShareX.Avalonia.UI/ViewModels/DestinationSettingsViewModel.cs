using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core;
using XerahS.UI.Views;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;
using System.Collections.ObjectModel;

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
        var pluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
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
        SettingManager.SaveUploadersConfigAsync();
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

            var result = UploadersConfigImporter.ImportFromFile(configPath, SettingManager.UploadersConfig);
            SettingManager.SaveUploadersConfig();

            OnPropertyChanged(string.Empty);

            await ShowMessageDialogAsync("Import Complete", result.GetSummary());
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync("Import Failed", $"Failed to import UploadersConfig:{Environment.NewLine}{ex.Message}");
        }
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
