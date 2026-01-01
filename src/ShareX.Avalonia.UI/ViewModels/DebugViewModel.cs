using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Ava.Common;
using System;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace ShareX.Ava.UI.ViewModels
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
    }
}
