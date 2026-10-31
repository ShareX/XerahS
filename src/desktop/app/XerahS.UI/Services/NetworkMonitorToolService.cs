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
using Avalonia.Platform.Storage;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.UI.Services;

public static class NetworkMonitorToolService
{
    private static NetworkMonitorWindow? _window;

    public static Task HandleWorkflowAsync(WorkflowType job, Window? owner)
    {
        if (job == WorkflowType.NetworkMonitor)
        {
            ShowWindow(owner);
        }

        return Task.CompletedTask;
    }

    private static void ShowWindow(Window? owner)
    {
        if (_window != null)
        {
            try
            {
                _window.Show();
                _window.Activate();
                return;
            }
            catch
            {
                _window = null;
            }
        }

        var viewModel = new NetworkMonitorViewModel(keepMonitoring: () => SettingsManager.Settings.NetworkMonitorEnabled);
        _window = new NetworkMonitorWindow
        {
            DataContext = viewModel
        };

        viewModel.CopyToClipboardRequested = async text =>
        {
            try
            {
                await PlatformServices.Clipboard.SetTextAsync(text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to copy network monitor log: {ex.Message}");
            }
        };

        viewModel.OpenLogRequested = path =>
        {
            try
            {
                PlatformServices.System.OpenFile(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open network monitor log: {ex.Message}");
            }

            return Task.CompletedTask;
        };

        viewModel.SaveFileRequested = async (fileName, _) =>
        {
            if (_window == null)
            {
                return null;
            }

            try
            {
                IStorageFile? file = await _window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Export network monitor log",
                    SuggestedFileName = fileName,
                    FileTypeChoices =
                    [
                        new FilePickerFileType("Text files") { Patterns = ["*.txt"] }
                    ]
                });

                return file?.TryGetLocalPath();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to export network monitor log: {ex.Message}");
                return null;
            }
        };

        _window.Closed += (sender, _) =>
        {
            if (ReferenceEquals(_window, sender))
            {
                _window = null;
            }
        };

        if (owner is { IsVisible: true })
        {
            _window.Show(owner);
        }
        else
        {
            _window.Show();
        }
    }
}
