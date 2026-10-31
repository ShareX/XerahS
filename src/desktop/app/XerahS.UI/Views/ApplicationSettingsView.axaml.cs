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
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.UI.Helpers;
using XerahS.UI.Services;
using XerahS.UI.Controls;

namespace XerahS.UI.Views
{
    public partial class ApplicationSettingsView : PageView
    {
        private TextBox? _debugTextBox;

        public ApplicationSettingsView()
        {
            InitializeComponent();
            TextBox? subfolderPattern = this.FindControl<TextBox>("SaveImageSubFolderPatternTextBox");
            if (subfolderPattern != null)
            {
                NamePatternMenu.Attach(subfolderPattern, CodeMenuEntryFilename.n);
            }

            var uiFactory = UiViewModelFactoryAccessor.GetRequired();
            var vm = uiFactory.CreateApplicationSettingsViewModel();
            DataContext = vm;

            var propertyGrid = this.FindControl<PropertyGrid>("ApplicationConfigPropertyGrid");
            if (propertyGrid != null)
            {
                propertyGrid.PropertyValueChanged += (_, _) => SettingsManager.SaveApplicationConfig();
            }

            vm.EditWatchFolderRequester = async (editVm) =>
            {
                return await uiFactory.ViewDialogService.ShowWatchFolderEditorAsync(editVm);
            };

            vm.BrowseScreenshotsFolderRequester = BrowseScreenshotsFolderAsync;
            vm.BackupSettingsFileRequester = () => uiFactory.ViewDialogService.ShowSaveFilePickerAsync(
                "Create Portable Settings Backup",
                PortableSettingsBackupService.DefaultFileName,
                PortableSettingsBackupService.FileExtension,
                new[] { $"*.{PortableSettingsBackupService.FileExtension}" });
            vm.RestoreSettingsFileRequester = () => uiFactory.ViewDialogService.ShowFilePickerAsync(
                "Restore Portable Settings Backup",
                new[] { $"*.{PortableSettingsBackupService.FileExtension}" });
            var settingsBackupDialogs = new AvaloniaDialogServiceAdapter();
            vm.SettingsBackupConfirmationRequester = settingsBackupDialogs.ShowConfirmationAsync;
            vm.SettingsBackupMessageRequester = settingsBackupDialogs.ShowMessageAsync;
            vm.SettingsBackupErrorRequester = settingsBackupDialogs.ShowErrorAsync;
            // Find debug TextBox and connect it to the HotkeySelectionControl's static debug log
            Loaded += (s, e) =>
            {
                _debugTextBox = this.FindControl<TextBox>("DebugLogTextBox");
                if (_debugTextBox != null)
                {
                    // Set up the debug log callback
                    Controls.HotkeySelectionControl.SetDebugCallback((msg) =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            _debugTextBox.Text = (_debugTextBox.Text ?? "") + msg + "\n";
                            _debugTextBox.CaretIndex = _debugTextBox.Text?.Length ?? 0;
                        });
                    });

                    _debugTextBox.Text = "Debug log initialized. Try clicking a hotkey button...\n";
                }
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public bool SelectTabByHeader(string? tabHeader)
        {
            if (string.IsNullOrWhiteSpace(tabHeader))
            {
                return false;
            }

            TabControl? tabs = this.FindControl<TabControl>("ApplicationSettingsTabs");
            if (tabs?.Items == null)
            {
                return false;
            }

            foreach (object? item in tabs.Items)
            {
                if (item is not TabItem tabItem)
                {
                    continue;
                }

                string header = tabItem.Header switch
                {
                    string text => text,
                    TextBlock textBlock => textBlock.Text ?? string.Empty,
                    _ => tabItem.Header?.ToString() ?? string.Empty
                };

                if (string.Equals(header.Trim(), tabHeader.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    tabs.SelectedItem = tabItem;
                    return true;
                }
            }

            return false;
        }

        private async Task<string?> BrowseScreenshotsFolderAsync()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider == null)
            {
                return null;
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Screenshots Folder",
                AllowMultiple = false,
                SuggestedStartLocation = await topLevel.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Pictures)
            });

            return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        }
    }
}
