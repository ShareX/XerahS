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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Core;

namespace XerahS.UI.ViewModels
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

        [ObservableProperty]
        private bool _useModernCapture;

        [ObservableProperty]
        private bool _trayIconProgressEnabled;

        [ObservableProperty]
        private bool _taskbarProgressEnabled;

        [ObservableProperty]
        private bool _autoCheckUpdate;

        [ObservableProperty]
        private UpdateChannel _updateChannel;

        public UpdateChannel[] UpdateChannels => (UpdateChannel[])Enum.GetValues(typeof(UpdateChannel));

        private TaskSettings ActiveTaskSettings => SettingManager.GetOrCreateWorkflowTaskSettings(HotkeyType.None);

        // Tray Click Actions
        public HotkeyType TrayLeftClickAction
        {
            get => SettingManager.Settings.TrayLeftClickAction;
            set
            {
                if (SettingManager.Settings.TrayLeftClickAction != value)
                {
                    SettingManager.Settings.TrayLeftClickAction = value;
                    OnPropertyChanged();
                }
            }
        }

        public HotkeyType TrayLeftDoubleClickAction
        {
            get => SettingManager.Settings.TrayLeftDoubleClickAction;
            set
            {
                if (SettingManager.Settings.TrayLeftDoubleClickAction != value)
                {
                    SettingManager.Settings.TrayLeftDoubleClickAction = value;
                    OnPropertyChanged();
                }
            }
        }

        public HotkeyType TrayMiddleClickAction
        {
            get => SettingManager.Settings.TrayMiddleClickAction;
            set
            {
                if (SettingManager.Settings.TrayMiddleClickAction != value)
                {
                    SettingManager.Settings.TrayMiddleClickAction = value;
                    OnPropertyChanged();
                }
            }
        }

        public HotkeyType[] TrayClickActions => (HotkeyType[])Enum.GetValues(typeof(HotkeyType));

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

            XerahS.Core.Integration.IntegrationHelper.SetPluginExtensionRegistration(value);
        }

        // OS Integration Settings
        public bool RunAtStartup
        {
            get => SettingManager.Settings.RunAtStartup;
            set
            {
                if (SettingManager.Settings.RunAtStartup != value)
                {
                    SettingManager.Settings.RunAtStartup = value;
                    OnPropertyChanged();
                    // TODO: Call platform-specific startup registration service
                }
            }
        }

        public bool EnableContextMenuIntegration
        {
            get => SettingManager.Settings.EnableContextMenuIntegration;
            set
            {
                if (SettingManager.Settings.EnableContextMenuIntegration != value)
                {
                    SettingManager.Settings.EnableContextMenuIntegration = value;
                    OnPropertyChanged();
                    // TODO: Call platform-specific context menu registration service
                }
            }
        }

        public bool EnableSendToIntegration
        {
            get => SettingManager.Settings.EnableSendToIntegration;
            set
            {
                if (SettingManager.Settings.EnableSendToIntegration != value)
                {
                    SettingManager.Settings.EnableSendToIntegration = value;
                    OnPropertyChanged();
                    // TODO: Call platform-specific Send To registration service
                }
            }
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
            get => ActiveTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.SaveImageToFile);
            set
            {
                if (value)
                    ActiveTaskSettings.AfterCaptureJob |= AfterCaptureTasks.SaveImageToFile;
                else
                    ActiveTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.SaveImageToFile;
                OnPropertyChanged();
            }
        }

        public bool CopyImageToClipboard
        {
            get => ActiveTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.CopyImageToClipboard);
            set
            {
                if (value)
                    ActiveTaskSettings.AfterCaptureJob |= AfterCaptureTasks.CopyImageToClipboard;
                else
                    ActiveTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.CopyImageToClipboard;
                OnPropertyChanged();
            }
        }

        public bool UploadImageToHost
        {
            get => ActiveTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.UploadImageToHost);
            set
            {
                if (value)
                    ActiveTaskSettings.AfterCaptureJob |= AfterCaptureTasks.UploadImageToHost;
                else
                    ActiveTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.UploadImageToHost;
                OnPropertyChanged();
            }
        }

        public bool AnnotateImage
        {
            get => ActiveTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.AnnotateImage);
            set
            {
                if (value)
                    ActiveTaskSettings.AfterCaptureJob |= AfterCaptureTasks.AnnotateImage;
                else
                    ActiveTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.AnnotateImage;
                OnPropertyChanged();
            }
        }

        public bool ShowAfterCaptureWindow
        {
            get => ActiveTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.ShowAfterCaptureWindow);
            set
            {
                if (value)
                    ActiveTaskSettings.AfterCaptureJob |= AfterCaptureTasks.ShowAfterCaptureWindow;
                else
                    ActiveTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.ShowAfterCaptureWindow;
                OnPropertyChanged();
            }
        }

        // Task Settings - After Upload
        public bool CopyURLToClipboard
        {
            get => ActiveTaskSettings.AfterUploadJob.HasFlag(AfterUploadTasks.CopyURLToClipboard);
            set
            {
                if (value)
                    ActiveTaskSettings.AfterUploadJob |= AfterUploadTasks.CopyURLToClipboard;
                else
                    ActiveTaskSettings.AfterUploadJob &= ~AfterUploadTasks.CopyURLToClipboard;
                OnPropertyChanged();
            }
        }

        public bool UseURLShortener
        {
            get => ActiveTaskSettings.AfterUploadJob.HasFlag(AfterUploadTasks.UseURLShortener);
            set
            {
                if (value)
                    ActiveTaskSettings.AfterUploadJob |= AfterUploadTasks.UseURLShortener;
                else
                    ActiveTaskSettings.AfterUploadJob &= ~AfterUploadTasks.UseURLShortener;
                OnPropertyChanged();
            }
        }

        public bool ShareURL
        {
            get => ActiveTaskSettings.AfterUploadJob.HasFlag(AfterUploadTasks.ShareURL);
            set
            {
                if (value)
                    ActiveTaskSettings.AfterUploadJob |= AfterUploadTasks.ShareURL;
                else
                    ActiveTaskSettings.AfterUploadJob &= ~AfterUploadTasks.ShareURL;
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
            TrayIconProgressEnabled = settings.TrayIconProgressEnabled;
            TaskbarProgressEnabled = settings.TaskbarProgressEnabled;
            AutoCheckUpdate = settings.AutoCheckUpdate;
            UpdateChannel = settings.UpdateChannel;

            // Task Settings - General (from primary workflow)
            var taskSettings = ActiveTaskSettings;
            PlaySoundAfterCapture = taskSettings.GeneralSettings.PlaySoundAfterCapture;
            ShowToastNotification = taskSettings.GeneralSettings.ShowToastNotificationAfterTaskCompleted;

            // Task Settings - Capture
            ShowCursor = taskSettings.CaptureSettings.ShowCursor;
            ScreenshotDelay = (double)taskSettings.CaptureSettings.ScreenshotDelay;
            CaptureTransparent = taskSettings.CaptureSettings.CaptureTransparent;
            CaptureShadow = taskSettings.CaptureSettings.CaptureShadow;
            CaptureClientArea = taskSettings.CaptureSettings.CaptureClientArea;
            UseModernCapture = taskSettings.CaptureSettings.UseModernCapture;

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
            IsPluginExtensionRegistered = XerahS.Core.Integration.IntegrationHelper.IsPluginExtensionRegistered();
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
            settings.TrayIconProgressEnabled = TrayIconProgressEnabled;
            settings.TaskbarProgressEnabled = TaskbarProgressEnabled;
            settings.AutoCheckUpdate = AutoCheckUpdate;
            settings.UpdateChannel = UpdateChannel;

            // Save Task Settings
            var taskSettings = ActiveTaskSettings;
            taskSettings.GeneralSettings.PlaySoundAfterCapture = PlaySoundAfterCapture;
            taskSettings.GeneralSettings.ShowToastNotificationAfterTaskCompleted = ShowToastNotification;

            taskSettings.CaptureSettings.ShowCursor = ShowCursor;
            taskSettings.CaptureSettings.ScreenshotDelay = (decimal)ScreenshotDelay;
            taskSettings.CaptureSettings.CaptureTransparent = CaptureTransparent;
            taskSettings.CaptureSettings.CaptureShadow = CaptureShadow;
            taskSettings.CaptureSettings.CaptureClientArea = CaptureClientArea;
            taskSettings.CaptureSettings.UseModernCapture = UseModernCapture;

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
