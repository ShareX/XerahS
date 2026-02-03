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
using XerahS.Uploaders.PluginSystem;
using XerahS.Editor.ViewModels;
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
