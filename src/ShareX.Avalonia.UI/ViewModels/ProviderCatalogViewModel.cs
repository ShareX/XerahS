using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Avalonia.Common;
using ShareX.Avalonia.Uploaders.PluginSystem;
using System.Collections.ObjectModel;

namespace ShareX.Avalonia.UI.ViewModels;

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
        
        foreach (var provider in providers)
        {
            AvailableProviders.Add(new ProviderViewModel(provider));
        }
    }

    [RelayCommand]
    private void AddSelected()
    {
        if (SelectedProvider == null || !SelectedProvider.IsSelected)
            return;

        try
        {
            var provider = ProviderCatalog.GetProvider(SelectedProvider.ProviderId);
            if (provider == null) return;

            var instance = new UploaderInstance
            {
                ProviderId = provider.ProviderId,
                Category = Category,
                DisplayName = $"{provider.Name} ({Category})",
                SettingsJson = provider.GetDefaultSettings(Category)
            };

            InstanceManager.Instance.AddInstance(instance);
            OnInstancesAdded?.Invoke(new List<UploaderInstance> { instance });
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

    [ObservableProperty]
    private bool _isSelected;

    public UploaderCategory[] SupportedCategories { get; }

    public ProviderViewModel(IUploaderProvider provider)
    {
        _providerId = provider.ProviderId;
        _name = provider.Name;
        _description = provider.Description;
        SupportedCategories = provider.SupportedCategories;
    }
}
