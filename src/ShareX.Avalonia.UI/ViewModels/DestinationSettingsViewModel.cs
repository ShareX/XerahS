using CommunityToolkit.Mvvm.ComponentModel;
using ShareX.Ava.Uploaders.PluginSystem;
using System.Collections.ObjectModel;

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

        // Load external plugins from Plugins folder
        var pluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
        Common.DebugHelper.WriteLine($"[DestinationSettings] App base directory: {AppDomain.CurrentDomain.BaseDirectory}");
        Common.DebugHelper.WriteLine($"[DestinationSettings] Plugins path: {pluginsPath}");
        Common.DebugHelper.WriteLine($"[DestinationSettings] Plugins folder exists: {Directory.Exists(pluginsPath)}");
        
        if (Directory.Exists(pluginsPath))
        {
            var subdirs = Directory.GetDirectories(pluginsPath);
            Common.DebugHelper.WriteLine($"[DestinationSettings] Found {subdirs.Length} subdirectory(ies) in Plugins folder");
            foreach (var dir in subdirs)
            {
                Common.DebugHelper.WriteLine($"[DestinationSettings]   - {Path.GetFileName(dir)}");
            }
            
            try
            {
                ProviderCatalog.LoadPlugins(pluginsPath);
            }
            catch (Exception ex)
            {
                Common.DebugHelper.WriteException(ex, "Failed to load plugins");
            }
        }
        else
        {
            Common.DebugHelper.WriteLine("[DestinationSettings] Plugins folder does not exist, skipping plugin loading");
        }

        var allProviders = ProviderCatalog.GetAllProviders();
        Common.DebugHelper.WriteLine($"[DestinationSettings] Total providers available: {allProviders.Count}");
        foreach (var p in allProviders)
        {
            Common.DebugHelper.WriteLine($"[DestinationSettings]   - {p.Name} ({p.ProviderId})");
        }
        
        Common.DebugHelper.WriteLine("[DestinationSettings] ========================================");

        // Force add providers for testing
        ForceAddTestInstance("imgur", UploaderCategory.Image);
        ForceAddTestInstance("amazons3", UploaderCategory.Image);
        
        LoadCategories();
    }

    private void ForceAddTestInstance(string providerId, UploaderCategory category)
    {
        var provider = ProviderCatalog.GetProvider(providerId);
        if (provider != null)
        {
            // Check if instance already exists
            var existing = InstanceManager.Instance.GetInstances().Any(i => i.ProviderId == providerId);
            if (!existing)
            {
                var instance = new UploaderInstance
                {
                    ProviderId = provider.ProviderId,
                    Category = category,
                    DisplayName = $"{provider.Name}",
                    SettingsJson = provider.GetDefaultSettings(category)
                };
                InstanceManager.Instance.AddInstance(instance);
                Common.DebugHelper.WriteLine($"[ForceAdd] Added test instance: {instance.DisplayName}");
            }
        }
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
}
