using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Ava.Common;
using ShareX.Ava.Uploaders.PluginSystem;
using System.Collections.ObjectModel;

namespace ShareX.Ava.UI.ViewModels;

/// <summary>
/// ViewModel for the provider catalog dialog
/// </summary>
public partial class ProviderCatalogViewModel : ViewModelBase
{
    [ObservableProperty]
    private UploaderCategory _category;

    [ObservableProperty]
    private ObservableCollection<ProviderViewModel> _availableProviders = new();

    [ObservableProperty]
    private ProviderViewModel? _selectedProvider;

    public Action<List<UploaderInstance>>? OnInstancesAdded { get; set; }
    public Action? OnCancelled { get; set; }

    public ProviderCatalogViewModel(UploaderCategory category)
    {
        _category = category;
        LoadProviders();
    }

    private void LoadProviders()
    {
        AvailableProviders.Clear();
        
        var providers = ProviderCatalog.GetProvidersByCategory(Category);
        DebugHelper.WriteLine($"[ProviderCatalog] Loading {providers.Count} providers for category {Category}");
        
        foreach (var provider in providers)
        {
            var vm = new ProviderViewModel(provider, Category, OnProviderSelected);
            AvailableProviders.Add(vm);
            DebugHelper.WriteLine($"[ProviderCatalog] Added provider: {provider.Name} (ID: {provider.ProviderId}), IsSelected: {vm.IsSelected}");
        }
        
        DebugHelper.WriteLine($"[ProviderCatalog] Total providers in AvailableProviders: {AvailableProviders.Count}");
    }

    private void OnProviderSelected(ProviderViewModel selected)
    {
        DebugHelper.WriteLine($"[ProviderCatalog] OnProviderSelected called for: {selected.Name}");
        
        // Deselect all others
        foreach (var provider in AvailableProviders)
        {
            if (provider != selected && provider.IsSelected)
            {
                provider.IsSelected = false;
            }
        }
        
        // Select this one
        selected.IsSelected = true;
    }

    [RelayCommand]
    private void AddSelected()
    {
        DebugHelper.WriteLine($"[ProviderCatalog] AddSelected called, AvailableProviders count: {AvailableProviders.Count}");
        
        // Log state of all providers
        for (int i = 0; i < AvailableProviders.Count; i++)
        {
            var p = AvailableProviders[i];
            DebugHelper.WriteLine($"[ProviderCatalog]   Provider {i}: {p.Name} (ID: {p.ProviderId}), IsSelected: {p.IsSelected}");
        }
        
        var selectedProvider = AvailableProviders.FirstOrDefault(p => p.IsSelected);

        if (selectedProvider == null)
        {
            DebugHelper.WriteLine("[ProviderCatalog] No provider selected (all IsSelected are false)");
            return;
        }

        try
        {
            DebugHelper.WriteLine($"[ProviderCatalog] Selected provider found: {selectedProvider.Name}");
            
            var provider = ProviderCatalog.GetProvider(selectedProvider.ProviderId);
            if (provider == null)
            {
                DebugHelper.WriteLine($"[ProviderCatalog] ERROR: Provider not found in catalog: {selectedProvider.ProviderId}");
                return;
            }

            DebugHelper.WriteLine($"[ProviderCatalog] Adding new instance for provider: {provider.Name}");

            var instance = new UploaderInstance
            {
                ProviderId = provider.ProviderId,
                Category = Category,
                DisplayName = $"{provider.Name} ({Category})",
                SettingsJson = provider.GetDefaultSettings(Category)
            };

            InstanceManager.Instance.AddInstance(instance);
            DebugHelper.WriteLine($"[ProviderCatalog] Instance added, invoking OnInstancesAdded callback...");
            OnInstancesAdded?.Invoke(new List<UploaderInstance> { instance });
            DebugHelper.WriteLine($"[ProviderCatalog] OnInstancesAdded callback invoked");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to add provider");
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        OnCancelled?.Invoke();
    }
}

/// <summary>
/// ViewModel for a provider in the catalog
/// </summary>
public partial class ProviderViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _providerId = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                DebugHelper.WriteLine($"[ProviderViewModel] IsSelected changed to {value} for provider: {Name}");
                OnPropertyChanged();
            }
        }
    }

    [ObservableProperty]
    private string _supportedFileTypesDisplay = string.Empty;

    public UploaderCategory[] SupportedCategories { get; }

    private readonly Action<ProviderViewModel>? _onSelect;

    public ProviderViewModel(IUploaderProvider provider, UploaderCategory? filterCategory = null, Action<ProviderViewModel>? onSelect = null)
    {
        _providerId = provider.ProviderId;
        _name = provider.Name;
        _description = provider.Description;
        SupportedCategories = provider.SupportedCategories;
        _onSelect = onSelect;

        // Display supported file types for the filter category if provided
        if (filterCategory.HasValue)
        {
            var fileTypes = provider.GetSupportedFileTypes();
            if (fileTypes.TryGetValue(filterCategory.Value, out var types))
            {
                var displayTypes = types.Take(8).Select(t => $".{t}");  
                var typeStr = string.Join(", ", displayTypes);
                SupportedFileTypesDisplay = types.Length > 8 ? $"{typeStr}, +{types.Length - 8} more" : typeStr;
            }
        }
    }

    [RelayCommand]
    private void Select()
    {
        DebugHelper.WriteLine($"[ProviderViewModel] Select command called for: {Name}");
        _onSelect?.Invoke(this);
    }
}
