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

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace XerahS.Mobile.Maui.ViewModels;

/// <summary>
/// Represents a settings item in the list
/// </summary>
public class SettingsItem
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconPath { get; set; } = "";
    public bool IsConfigured { get; set; }
    public string Route { get; set; } = "";
    public string BadgeText => IsConfigured ? "\u2713" : "!";
}

/// <summary>
/// ViewModel for mobile settings - lists available uploader configurations.
/// Navigation is handled by MAUI Shell routes instead of embedded views.
/// </summary>
public class MobileSettingsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private ObservableCollection<SettingsItem> _settingsItems = new();
    public ObservableCollection<SettingsItem> SettingsItems
    {
        get => _settingsItems;
        set { _settingsItems = value; OnPropertyChanged(); }
    }

    private SettingsItem? _selectedItem;
    public SettingsItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;
            OnPropertyChanged();
            if (value != null)
            {
                NavigateToConfig(value);
            }
        }
    }

    public ICommand BackCommand { get; set; }
    public ICommand RefreshCommand { get; }
    public ICommand NavigateToConfigCommand { get; }

    public MobileSettingsViewModel()
    {
        BackCommand = new AsyncRelayCommand(HandleBack);
        RefreshCommand = new RelayCommand(RefreshItems);
        NavigateToConfigCommand = new RelayCommand<SettingsItem>(item =>
        {
            if (item != null) NavigateToConfig(item);
        });
        InitializeSettingsItems();
    }

    private void InitializeSettingsItems()
    {
        var items = new List<SettingsItem>();

        // Amazon S3 - the primary mobile uploader
        var s3Vm = new MobileAmazonS3ConfigViewModel();
        items.Add(new SettingsItem
        {
            Title = s3Vm.UploaderName,
            Description = s3Vm.Description,
            IconPath = s3Vm.IconPath,
            IsConfigured = s3Vm.IsConfigured,
            Route = "AmazonS3"
        });

        // Custom Uploader - add/manage custom image uploaders via .sxcu JSON
        var customVm = new MobileCustomUploaderConfigViewModel();
        items.Add(new SettingsItem
        {
            Title = customVm.UploaderName,
            Description = customVm.Description,
            IconPath = customVm.IconPath,
            IsConfigured = customVm.IsConfigured,
            Route = "CustomUploader"
        });

        SettingsItems = new ObservableCollection<SettingsItem>(items);
    }

    public void RefreshItems()
    {
        // Reload each item to update IsConfigured status
        foreach (var item in SettingsItems)
        {
            if (item.Title == "Amazon S3")
            {
                var vm = new MobileAmazonS3ConfigViewModel();
                item.IsConfigured = vm.IsConfigured;
                item.Description = vm.Description;
            }
            else if (item.Title == "Custom Uploader")
            {
                var vm = new MobileCustomUploaderConfigViewModel();
                item.IsConfigured = vm.IsConfigured;
                item.Description = vm.Description;
            }
        }

        // Force refresh
        var temp = SettingsItems.ToList();
        SettingsItems = new ObservableCollection<SettingsItem>(temp);
    }

    public async void NavigateToConfig(SettingsItem item)
    {
        await Shell.Current.GoToAsync(item.Route);
        SelectedItem = null; // Reset selection
    }

    public async Task HandleBack()
    {
        await Shell.Current.GoToAsync("..");
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
