#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Ava.Core;

namespace ShareX.Ava.UI.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private bool _isLoading = true;

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

        // History Settings
        public bool HistorySaveTasks
        {
            get => SettingManager.Settings.HistorySaveTasks;
            set
            {
                SettingManager.Settings.HistorySaveTasks = value;
                OnPropertyChanged();
            }
        }

        public bool HistoryCheckURL
        {
            get => SettingManager.Settings.HistoryCheckURL;
            set
            {
                SettingManager.Settings.HistoryCheckURL = value;
                OnPropertyChanged();
            }
        }

        // Recent Tasks Settings
        public bool RecentTasksSave
        {
            get => SettingManager.Settings.RecentTasksSave;
            set
            {
                SettingManager.Settings.RecentTasksSave = value;
                OnPropertyChanged();
            }
        }

        public int RecentTasksMaxCount
        {
            get => SettingManager.Settings.RecentTasksMaxCount;
            set
            {
                SettingManager.Settings.RecentTasksMaxCount = value;
                OnPropertyChanged();
            }
        }

        public bool RecentTasksShowInMainWindow
        {
            get => SettingManager.Settings.RecentTasksShowInMainWindow;
            set
            {
                SettingManager.Settings.RecentTasksShowInMainWindow = value;
                OnPropertyChanged();
            }
        }

        public bool RecentTasksShowInTrayMenu
        {
            get => SettingManager.Settings.RecentTasksShowInTrayMenu;
            set
            {
                SettingManager.Settings.RecentTasksShowInTrayMenu = value;
                OnPropertyChanged();
            }
        }

        public bool RecentTasksTrayMenuMostRecentFirst
        {
            get => SettingManager.Settings.RecentTasksTrayMenuMostRecentFirst;
            set
            {
                SettingManager.Settings.RecentTasksTrayMenuMostRecentFirst = value;
                OnPropertyChanged();
            }
        }

        // Integration Settings
        [ObservableProperty]
        private bool _isPluginExtensionRegistered;

        [ObservableProperty]
        private bool _supportsFileAssociations;

        partial void OnIsPluginExtensionRegisteredChanged(bool value)
        {
            if (_isLoading) return; // Don't trigger during initial load
            
            ShareX.Ava.Core.Integration.IntegrationHelper.SetPluginExtensionRegistration(value);
        }

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

        [ObservableProperty]
        private HotkeySettingsViewModel _hotkeySettings;

        // Task Settings - Upload / File Naming
        [ObservableProperty]
        private string _nameFormatPattern;

        [ObservableProperty]
        private string _nameFormatPatternActiveWindow;

        [ObservableProperty]
        private bool _fileUploadUseNamePattern;

        [ObservableProperty]
        private bool _fileUploadReplaceProblematicCharacters;

        [ObservableProperty]
        private bool _uRLRegexReplace;

        [ObservableProperty]
        private string _uRLRegexReplacePattern;

        [ObservableProperty]
        private string _uRLRegexReplaceReplacement;

        // Task Settings - After Capture
        public bool SaveImageToFile
        {
            get => SettingManager.Settings.DefaultTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.SaveImageToFile);
            set
            {
                if (value)
                    SettingManager.Settings.DefaultTaskSettings.AfterCaptureJob |= AfterCaptureTasks.SaveImageToFile;
                else
                    SettingManager.Settings.DefaultTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.SaveImageToFile;
                OnPropertyChanged();
            }
        }

        public bool CopyImageToClipboard
        {
            get => SettingManager.Settings.DefaultTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.CopyImageToClipboard);
            set
            {
                if (value)
                    SettingManager.Settings.DefaultTaskSettings.AfterCaptureJob |= AfterCaptureTasks.CopyImageToClipboard;
                else
                    SettingManager.Settings.DefaultTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.CopyImageToClipboard;
                OnPropertyChanged();
            }
        }

        public bool UploadImageToHost
        {
            get => SettingManager.Settings.DefaultTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.UploadImageToHost);
            set
            {
                if (value)
                    SettingManager.Settings.DefaultTaskSettings.AfterCaptureJob |= AfterCaptureTasks.UploadImageToHost;
                else
                    SettingManager.Settings.DefaultTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.UploadImageToHost;
                OnPropertyChanged();
            }
        }

        public bool AnnotateImage
        {
            get => SettingManager.Settings.DefaultTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.AnnotateImage);
            set
            {
                if (value)
                    SettingManager.Settings.DefaultTaskSettings.AfterCaptureJob |= AfterCaptureTasks.AnnotateImage;
                else
                    SettingManager.Settings.DefaultTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.AnnotateImage;
                OnPropertyChanged();
            }
        }

        public bool ShowAfterCaptureWindow
        {
            get => SettingManager.Settings.DefaultTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.ShowAfterCaptureWindow);
            set
            {
                if (value)
                    SettingManager.Settings.DefaultTaskSettings.AfterCaptureJob |= AfterCaptureTasks.ShowAfterCaptureWindow;
                else
                    SettingManager.Settings.DefaultTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.ShowAfterCaptureWindow;
                OnPropertyChanged();
            }
        }

        // Task Settings - After Upload
        public bool CopyURLToClipboard
        {
            get => SettingManager.Settings.DefaultTaskSettings.AfterUploadJob.HasFlag(AfterUploadTasks.CopyURLToClipboard);
            set
            {
                if (value)
                    SettingManager.Settings.DefaultTaskSettings.AfterUploadJob |= AfterUploadTasks.CopyURLToClipboard;
                else
                    SettingManager.Settings.DefaultTaskSettings.AfterUploadJob &= ~AfterUploadTasks.CopyURLToClipboard;
                OnPropertyChanged();
            }
        }

        public bool UseURLShortener
        {
            get => SettingManager.Settings.DefaultTaskSettings.AfterUploadJob.HasFlag(AfterUploadTasks.UseURLShortener);
            set
            {
                if (value)
                    SettingManager.Settings.DefaultTaskSettings.AfterUploadJob |= AfterUploadTasks.UseURLShortener;
                else
                    SettingManager.Settings.DefaultTaskSettings.AfterUploadJob &= ~AfterUploadTasks.UseURLShortener;
                OnPropertyChanged();
            }
        }

        public bool ShareURL
        {
            get => SettingManager.Settings.DefaultTaskSettings.AfterUploadJob.HasFlag(AfterUploadTasks.ShareURL);
            set
            {
                if (value)
                    SettingManager.Settings.DefaultTaskSettings.AfterUploadJob |= AfterUploadTasks.ShareURL;
                else
                    SettingManager.Settings.DefaultTaskSettings.AfterUploadJob &= ~AfterUploadTasks.ShareURL;
                OnPropertyChanged();
            }
        }

        public SettingsViewModel()
        {
            HotkeySettings = new HotkeySettingsViewModel();
            LoadSettings();
            _isLoading = false;
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

            // Task Settings - General
            var taskSettings = settings.DefaultTaskSettings;
            PlaySoundAfterCapture = taskSettings.GeneralSettings.PlaySoundAfterCapture;
            ShowToastNotification = taskSettings.GeneralSettings.ShowToastNotificationAfterTaskCompleted;
            
            // Task Settings - Capture
            ShowCursor = taskSettings.CaptureSettings.ShowCursor;
            ScreenshotDelay = (double)taskSettings.CaptureSettings.ScreenshotDelay;
            CaptureTransparent = taskSettings.CaptureSettings.CaptureTransparent;
            CaptureShadow = taskSettings.CaptureSettings.CaptureShadow;
            CaptureClientArea = taskSettings.CaptureSettings.CaptureClientArea;

            // Task Settings - File Naming Defaults
            NameFormatPattern = taskSettings.UploadSettings.NameFormatPattern;
            NameFormatPatternActiveWindow = taskSettings.UploadSettings.NameFormatPatternActiveWindow;
            FileUploadUseNamePattern = taskSettings.UploadSettings.FileUploadUseNamePattern;
            FileUploadReplaceProblematicCharacters = taskSettings.UploadSettings.FileUploadReplaceProblematicCharacters;
            URLRegexReplace = taskSettings.UploadSettings.URLRegexReplace;
            URLRegexReplacePattern = taskSettings.UploadSettings.URLRegexReplacePattern;
            URLRegexReplaceReplacement = taskSettings.UploadSettings.URLRegexReplaceReplacement;
            
            // Integration Settings
            SupportsFileAssociations = OperatingSystem.IsWindows();
            IsPluginExtensionRegistered = ShareX.Ava.Core.Integration.IntegrationHelper.IsPluginExtensionRegistered();
        }

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            
            // Auto-save when any property changes (after initial load)
            if (!_isLoading && e.PropertyName != nameof(HotkeySettings))
            {
                SaveSettings();
            }
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
            
            // Save Task Settings
            var taskSettings = settings.DefaultTaskSettings;
            taskSettings.GeneralSettings.PlaySoundAfterCapture = PlaySoundAfterCapture;
            taskSettings.GeneralSettings.ShowToastNotificationAfterTaskCompleted = ShowToastNotification;
            
            taskSettings.CaptureSettings.ShowCursor = ShowCursor;
            taskSettings.CaptureSettings.ScreenshotDelay = (decimal)ScreenshotDelay;
            taskSettings.CaptureSettings.CaptureTransparent = CaptureTransparent;
            taskSettings.CaptureSettings.CaptureShadow = CaptureShadow;
            taskSettings.CaptureSettings.CaptureClientArea = CaptureClientArea;

            taskSettings.UploadSettings.NameFormatPattern = NameFormatPattern;
            taskSettings.UploadSettings.NameFormatPatternActiveWindow = NameFormatPatternActiveWindow;
            taskSettings.UploadSettings.FileUploadUseNamePattern = FileUploadUseNamePattern;
            taskSettings.UploadSettings.FileUploadReplaceProblematicCharacters = FileUploadReplaceProblematicCharacters;
            taskSettings.UploadSettings.URLRegexReplace = URLRegexReplace;
            taskSettings.UploadSettings.URLRegexReplacePattern = URLRegexReplacePattern;
            taskSettings.UploadSettings.URLRegexReplaceReplacement = URLRegexReplaceReplacement;
            
            SettingManager.SaveApplicationConfig();
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
