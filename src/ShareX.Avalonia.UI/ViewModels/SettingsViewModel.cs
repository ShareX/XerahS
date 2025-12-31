using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Avalonia.Core;

namespace ShareX.Avalonia.UI.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _screenshotsFolder;

        [ObservableProperty]
        private string _saveImageSubFolderPattern;

        [ObservableProperty]
        private bool _useCustomScreenshotsPath;

        [ObservableProperty]
        private bool _showTray;

        [ObservableProperty]
        private bool _silentRun;

        [ObservableProperty]
        private int _selectedTheme;

        // Task Settings - General
        [ObservableProperty]
        private bool _playSoundAfterCapture;

        [ObservableProperty]
        private bool _showToastNotification;

        // Task Settings - Capture
        [ObservableProperty]
        private bool _showCursor;

        [ObservableProperty]
        private double _screenshotDelay;

        [ObservableProperty]
        private bool _captureTransparent;

        [ObservableProperty]
        private bool _captureShadow;

        [ObservableProperty]
        private bool _captureClientArea;

        public SettingsViewModel()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = SettingManager.Settings;
            
            ScreenshotsFolder = settings.CustomScreenshotsPath ?? 
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ShareX");
            SaveImageSubFolderPattern = settings.SaveImageSubFolderPattern ?? "%y-%mo";
            UseCustomScreenshotsPath = settings.UseCustomScreenshotsPath;
            ShowTray = settings.ShowTray;
            SilentRun = settings.SilentRun;
            SelectedTheme = settings.SelectedTheme;

            // Task Settings Defaults (Placeholder for now until TaskSettings object is fully integrated)
            PlaySoundAfterCapture = true;
            ShowToastNotification = true;
            ShowCursor = true;
            ScreenshotDelay = 0.0;
            CaptureTransparent = true;
            CaptureShadow = true;
            CaptureClientArea = false;
        }

        [RelayCommand]
        private void SaveSettings()
        {
            var settings = SettingManager.Settings;
            
            settings.CustomScreenshotsPath = ScreenshotsFolder;
            settings.SaveImageSubFolderPattern = SaveImageSubFolderPattern;
            settings.UseCustomScreenshotsPath = UseCustomScreenshotsPath;
            settings.ShowTray = ShowTray;
            settings.SilentRun = SilentRun;
            settings.SelectedTheme = SelectedTheme;
            
            // TODO: Save Task settings to appropriate object (e.g., TaskSettings)
            // For now, these are just tied to the view model state.
            
            // TODO: Save to disk
            // SettingManager.Save();
        }

        [RelayCommand]
        private void BrowseFolder()
        {
            // TODO: Implement folder picker dialog
        }

        [RelayCommand]
        private void ResetToDefaults()
        {
            ScreenshotsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ShareX");
            SaveImageSubFolderPattern = "%y-%mo";
            UseCustomScreenshotsPath = false;
            ShowTray = true;
            SilentRun = false;
            SelectedTheme = 0;
        }
    }
}
