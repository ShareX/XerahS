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
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Uploaders;
using XerahS.Uploaders.PluginSystem;
using System.Diagnostics;

namespace XerahS.UI.ViewModels
{
    public partial class DebugViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _logText = "";

        [ObservableProperty]
        private string _secretStoreBackendName = "Unknown";

        [ObservableProperty]
        private string _secretStoreBackendDetails = "Secrets store diagnostics not available.";

        [ObservableProperty]
        private string _secretStoreBackendStatus = "Unknown";

        public DebugViewModel()
        {
            if (DebugHelper.Logger != null)
            {
                LogText = DebugHelper.Logger.ToString() ?? "";
                DebugHelper.Logger.MessageAdded += Logger_MessageAdded;
            }

            var context = ProviderContextManager.EnsureProviderContext();
            if (context.Secrets is ISecretStoreInfo info)
            {
                SecretStoreBackendName = info.BackendName;
                SecretStoreBackendDetails = info.BackendDetails;
                SecretStoreBackendStatus = info.IsFallback ? "Fallback" : "Primary";
            }
        }

        private void Logger_MessageAdded(string message)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                LogText += message;
            });
        }

        [RelayCommand]
        private void Clear()
        {
            DebugHelper.Logger?.Clear();
            LogText = "";
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task Copy()
        {
            if (!string.IsNullOrEmpty(LogText) && Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var clipboard = desktop.MainWindow?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(LogText);
                }
            }
        }

        [RelayCommand]
        private void Upload()
        {
            // Placeholder for upload functionality
        }

        [RelayCommand]
        private void OpenLogsFolder()
        {
            try
            {
                string logsFolder = System.IO.Path.Combine(SettingsManager.PersonalFolder, "Logs");

                if (System.IO.Directory.Exists(logsFolder))
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = logsFolder,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                else
                {
                    DebugHelper.WriteLine($"Logs folder does not exist: {logsFolder}");
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Error opening logs folder: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenSettingsFolder()
        {
            try
            {
                string settingsFolder = SettingsManager.SettingsFolder;

                if (System.IO.Directory.Exists(settingsFolder))
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = settingsFolder,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Error opening settings folder: {ex.Message}");
            }
        }
    }
}
