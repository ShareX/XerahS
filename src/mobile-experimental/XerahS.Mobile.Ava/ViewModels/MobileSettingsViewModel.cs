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
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Threading;
using XerahS.Mobile.Core;
using Ava.Views;

namespace Ava.ViewModels;

/// <summary>
/// Represents a settings item in the list
/// </summary>
public class SettingsItem
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconPath { get; set; } = "";
    public bool IsConfigured { get; set; }
    public Func<Avalonia.Controls.Control> CreateView { get; set; } = () => new Avalonia.Controls.Border();
    public string BadgeText => IsConfigured ? "✓" : "!";
}

/// <summary>
/// ViewModel for mobile settings - lists available uploader configurations
/// </summary>
public class MobileSettingsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? RequestCloseSettings;

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

    private Avalonia.Controls.Control? _currentView;
    public Avalonia.Controls.Control? CurrentView
    {
        get => _currentView;
        set { _currentView = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsListView)); OnPropertyChanged(nameof(CurrentViewTitle)); }
    }

    public bool IsListView => CurrentView == null;

    public string CurrentViewTitle => CurrentView?.GetType().Name switch
    {
        "MobileAmazonS3ConfigView" => "Amazon S3",
        "MobileCustomUploaderConfigView" => "Custom Uploader",
        _ => "Settings"
    };

    public ICommand BackCommand { get; set; }
    public ICommand RefreshCommand { get; }
    public ICommand NavigateToConfigCommand { get; }

    public MobileSettingsViewModel()
    {
        BackCommand = new RelayCommand(_ => HandleBack());
        RefreshCommand = new RelayCommand(_ => RefreshItems());
        NavigateToConfigCommand = new RelayCommand<SettingsItem>(item =>
        {
            if (item != null) NavigateToConfig(item);
        });
        InitializeSettingsItems();
    }

    private async void InitializeSettingsItems()
    {
        await Task.Run(() =>
        {
            var items = new List<SettingsItem>();

            try
            {
                // Amazon S3 - the primary mobile uploader
                var s3Vm = new MobileAmazonS3ConfigViewModel();
                s3Vm.LoadConfig();
                items.Add(new SettingsItem
                {
                    Title = s3Vm.UploaderName,
                    Description = s3Vm.Description,
                    IconPath = s3Vm.IconPath,
                    IsConfigured = s3Vm.IsConfigured,
                    CreateView = () => 
                    {
                        var view = new MobileAmazonS3ConfigView();
                        // Ensure config is loaded when view is created (if not already via VM)
                        if (view.DataContext is XerahS.Mobile.Core.MobileAmazonS3ConfigViewModel vm)
                        {
                            vm.LoadConfig();
                        }
                        return view;
                    }
                });

                // Custom Uploader - add/manage custom image uploaders via .sxcu JSON
                var customVm = new MobileCustomUploaderConfigViewModel();
                customVm.LoadConfig();
                items.Add(new SettingsItem
                {
                    Title = customVm.UploaderName,
                    Description = customVm.Description,
                    IconPath = customVm.IconPath,
                    IsConfigured = customVm.IsConfigured,
                    CreateView = () => new MobileCustomUploaderConfigView()
                });
            }
            catch (Exception ex)
            {
                // Log but don't crash
                 System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex}");
            }

            Dispatcher.UIThread.Post(() =>
            {
                SettingsItems = new ObservableCollection<SettingsItem>(items);
            });
        });
    }

    public async void RefreshItems()
    {
        await Task.Run(() =>
        {
            try
            {
                // Reload each item to update IsConfigured status
                foreach (var item in SettingsItems)
                {
                    if (item.Title == "Amazon S3")
                    {
                        var vm = new MobileAmazonS3ConfigViewModel();
                        vm.LoadConfig();
                        Dispatcher.UIThread.Post(() =>
                        {
                            item.IsConfigured = vm.IsConfigured;
                            item.Description = vm.Description;
                        });
                    }
                    else if (item.Title == "Custom Uploader")
                    {
                        var vm = new MobileCustomUploaderConfigViewModel();
                        vm.LoadConfig();
                        Dispatcher.UIThread.Post(() =>
                        {
                            item.IsConfigured = vm.IsConfigured;
                            item.Description = vm.Description;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing items: {ex}");
            }

            Dispatcher.UIThread.Post(() =>
            {
                // Force refresh
                var temp = SettingsItems.ToList();
                SettingsItems = new ObservableCollection<SettingsItem>(temp);
            });
        });
    }

    public void NavigateToConfig(SettingsItem item)
    {
        CurrentView = item.CreateView();
        SelectedItem = null; // Reset selection
    }

    public void HandleBack()
    {
        if (CurrentView == null)
        {
            // At top level settings list - close settings entirely
            RequestCloseSettings?.Invoke();
        }
        else
        {
            // In a config view - go back to settings list
            CurrentView = null;
            RefreshItems();
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
