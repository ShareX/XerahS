using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Uploaders.PluginSystem;
using ShareX.Editor.ViewModels;
using System.Collections.ObjectModel;

namespace XerahS.UI.ViewModels;

/// <summary>
/// ViewModel for a category with its instances
/// </summary>
public partial class CategoryViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private UploaderCategory _category;

    [ObservableProperty]
    private ObservableCollection<UploaderInstanceViewModel> _instances = new();

    [ObservableProperty]
    private UploaderInstanceViewModel? _selectedInstance;

    [ObservableProperty]
    private UploaderInstanceViewModel? _defaultInstance;

    public CategoryViewModel(string name, UploaderCategory category)
    {
        _name = name;
        _category = category;
    }

    [RelayCommand]
    private void AddFromCatalog()
    {
        try
        {
            var viewModel = new ProviderCatalogViewModel(Category);
            var mainVm = MainViewModel.Current;

            if (mainVm != null)
            {
                // Unsubscribing helper
                void Cleanup()
                {
                    if (mainVm.ModalContent == viewModel)
                        mainVm.CloseModalCommand.Execute(null);
                }

                viewModel.OnInstancesAdded += instances =>
                {
                    LoadInstances();
                    Cleanup();
                };

                viewModel.OnCancelled += Cleanup;

                // Show Modal (set ViewModel, DataTemplate handles View)
                mainVm.ModalContent = viewModel;
                mainVm.IsModalOpen = true;
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to open catalog");
        }
    }

    [RelayCommand]
    private void SetAsDefault(UploaderInstanceViewModel? instance)
    {
        if (instance == null) return;

        try
        {
            InstanceManager.Instance.SetDefaultInstance(Category, instance.InstanceId);

            // Update UI
            if (DefaultInstance != null)
            {
                DefaultInstance.IsDefault = false;
            }

            DefaultInstance = instance;
            instance.IsDefault = true;
        }
        catch (Exception ex)
        {
            // TODO: Show error to user
            DebugHelper.WriteException(ex, "Failed to set default");
        }
    }

    [RelayCommand]
    private void DuplicateInstance(UploaderInstanceViewModel? instance)
    {
        if (instance == null) return;

        try
        {
            var duplicate = InstanceManager.Instance.DuplicateInstance(instance.InstanceId);
            var duplicateVm = new UploaderInstanceViewModel(duplicate);
            Instances.Add(duplicateVm);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to duplicate");
        }
    }

    [RelayCommand]
    private void RemoveInstance(UploaderInstanceViewModel? instance)
    {
        if (instance == null) return;

        try
        {
            InstanceManager.Instance.RemoveInstance(instance.InstanceId);
            Instances.Remove(instance);

            if (DefaultInstance == instance)
            {
                DefaultInstance = null;
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to remove");
        }
    }

    public void LoadInstances()
    {
        Instances.Clear();

        var instances = InstanceManager.Instance.GetInstancesByCategory(Category);
        var defaultInstance = InstanceManager.Instance.GetDefaultInstance(Category);

        foreach (var instance in instances)
        {
            var vm = new UploaderInstanceViewModel(instance);

            if (defaultInstance != null && instance.InstanceId == defaultInstance.InstanceId)
            {
                vm.IsDefault = true;
                DefaultInstance = vm;
            }

            Instances.Add(vm);
        }
    }
}
