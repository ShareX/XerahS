using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Common = ShareX.Ava.Common;
using ShareX.Ava.Core;
using ShareX.Ava.UI.Views;
using ShareX.Ava.Uploaders.PluginSystem;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ShareX.Ava.UI.ViewModels;

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
}
