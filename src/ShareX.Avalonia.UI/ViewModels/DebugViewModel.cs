using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core;
using System.Diagnostics;

namespace XerahS.UI.ViewModels
{
    public partial class DebugViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _logText = "";

        public DebugViewModel()
        {
            if (DebugHelper.Logger != null)
            {
                LogText = DebugHelper.Logger.ToString() ?? "";
                DebugHelper.Logger.MessageAdded += Logger_MessageAdded;
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
                string logsFolder = System.IO.Path.Combine(SettingManager.PersonalFolder, "Logs");

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
                string settingsFolder = SettingManager.SettingsFolder;

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
