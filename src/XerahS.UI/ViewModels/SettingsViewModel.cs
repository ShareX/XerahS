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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Core.Managers;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private const int WatchFolderDaemonStopTimeoutSeconds = 30;
        private bool _isLoading = true;
        private string _lastSavedWatchFolderSignature = string.Empty;
        private bool _suspendWatchFolderAutoSave;

        [ObservableProperty]
        private string _screenshotsFolder = string.Empty;

        [ObservableProperty]
        private string _saveImageSubFolderPattern = string.Empty;

        [ObservableProperty]
        private bool _useCustomScreenshotsPath;

        [ObservableProperty]
        private bool _showTray;

        [ObservableProperty]
        private bool _silentRun;

        [ObservableProperty]
        private int _selectedTheme;

        [ObservableProperty]
        private AppThemeMode _themeMode;

        public AppThemeMode[] ThemeModes => (AppThemeMode[])Enum.GetValues(typeof(AppThemeMode));

        partial void OnThemeModeChanged(AppThemeMode value)
        {
            if (_isLoading) return;

            // Apply theme change immediately
            Services.ThemeService.ApplyTheme(value);
        }

        partial void OnProxyMethodChanged(ProxyMethod value)
        {
            if (_isLoading) return;
            OnPropertyChanged(nameof(IsManualProxy));
            ApplyProxyAndResetClient();
        }

        partial void OnProxyHostChanged(string value)
        {
            if (_isLoading) return;
            ApplyProxyAndResetClient();
        }

        partial void OnProxyPortChanged(int value)
        {
            if (_isLoading) return;
            ApplyProxyAndResetClient();
        }

        partial void OnProxyUsernameChanged(string value)
        {
            if (_isLoading) return;
            ApplyProxyAndResetClient();
        }

        partial void OnProxyPasswordChanged(string value)
        {
            if (_isLoading) return;
            ApplyProxyAndResetClient();
        }

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

        // Proxy Settings
        [ObservableProperty]
        private ProxyMethod _proxyMethod;

        [ObservableProperty]
        private string _proxyHost = string.Empty;

        [ObservableProperty]
        private int _proxyPort = 8080;

        [ObservableProperty]
        private string _proxyUsername = string.Empty;

        [ObservableProperty]
        private string _proxyPassword = string.Empty;

        public ProxyMethod[] ProxyMethods => (ProxyMethod[])Enum.GetValues(typeof(ProxyMethod));

        public bool IsManualProxy => ProxyMethod == ProxyMethod.Manual;

        public ApplicationConfig ApplicationConfig => SettingsManager.Settings;

        [ObservableProperty]
        private bool _watchFolderEnabled;

        public ObservableCollection<WatchFolderSettingsViewModel> WatchFolders { get; } = new();

        public ObservableCollection<WorkflowOptionViewModel> WatchFolderWorkflows { get; } = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(EditWatchFolderCommand))]
        [NotifyCanExecuteChangedFor(nameof(RemoveWatchFolderCommand))]
        private WatchFolderSettingsViewModel? _selectedWatchFolder;

        [ObservableProperty]
        private bool _hasWatchFolders;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ToggleWatchFolderDaemonCommand))]
        private bool _watchFolderDaemonSupported;

        [ObservableProperty]
        private bool _showWatchFolderDaemonScopeSelector;

        [ObservableProperty]
        private WatchFolderDaemonScope _watchFolderDaemonScope;

        [ObservableProperty]
        private bool _watchFolderDaemonStartAtStartup;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WatchFolderDaemonButtonText))]
        private bool _watchFolderDaemonRunning;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WatchFolderDaemonButtonText))]
        private bool _watchFolderDaemonInstalled;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ToggleWatchFolderDaemonCommand))]
        private bool _isWatchFolderDaemonBusy;

        [ObservableProperty]
        private string _watchFolderDaemonStatusText = "Unknown";

        [ObservableProperty]
        private string _watchFolderDaemonLastError = string.Empty;

        public WatchFolderDaemonScope[] WatchFolderDaemonScopes { get; } =
        {
            WatchFolderDaemonScope.User,
            WatchFolderDaemonScope.System
        };

        public string WatchFolderDaemonButtonText => WatchFolderDaemonRunning ? "Stop" : (WatchFolderDaemonInstalled ? "Start" : "Install + Start");

        public Func<WatchFolderEditViewModel, Task<bool>>? EditWatchFolderRequester { get; set; }

        private TaskSettings ActiveTaskSettings
        {
            get
            {
                var taskSettings = SettingsManager.DefaultTaskSettings;
                if (taskSettings.Job != WorkflowType.None)
                {
                    taskSettings.Job = WorkflowType.None;
                }

                return taskSettings;
            }
        }

        // Tray Click Actions
        public WorkflowType TrayLeftClickAction
        {
            get => SettingsManager.Settings.TrayLeftClickAction;
            set
            {
                if (SettingsManager.Settings.TrayLeftClickAction != value)
                {
                    SettingsManager.Settings.TrayLeftClickAction = value;
                    OnPropertyChanged();
                }
            }
        }

        public WorkflowType TrayLeftDoubleClickAction
        {
            get => SettingsManager.Settings.TrayLeftDoubleClickAction;
            set
            {
                if (SettingsManager.Settings.TrayLeftDoubleClickAction != value)
                {
                    SettingsManager.Settings.TrayLeftDoubleClickAction = value;
                    OnPropertyChanged();
                }
            }
        }

        public WorkflowType TrayMiddleClickAction
        {
            get => SettingsManager.Settings.TrayMiddleClickAction;
            set
            {
                if (SettingsManager.Settings.TrayMiddleClickAction != value)
                {
                    SettingsManager.Settings.TrayMiddleClickAction = value;
                    OnPropertyChanged();
                }
            }
        }

        public WorkflowType[] TrayClickActions => (WorkflowType[])Enum.GetValues(typeof(WorkflowType));

        // History Settings
        public bool HistorySaveTasks
        {
            get => SettingsManager.Settings.HistorySaveTasks;
            set
            {
                SettingsManager.Settings.HistorySaveTasks = value;
                OnPropertyChanged();
            }
        }

        public bool HistoryCheckURL
        {
            get => SettingsManager.Settings.HistoryCheckURL;
            set
            {
                SettingsManager.Settings.HistoryCheckURL = value;
                OnPropertyChanged();
            }
        }

        // Recent Tasks Settings
        public bool RecentTasksSave
        {
            get => SettingsManager.Settings.RecentTasksSave;
            set
            {
                SettingsManager.Settings.RecentTasksSave = value;
                OnPropertyChanged();
            }
        }

        public int RecentTasksMaxCount
        {
            get => SettingsManager.Settings.RecentTasksMaxCount;
            set
            {
                SettingsManager.Settings.RecentTasksMaxCount = value;
                OnPropertyChanged();
            }
        }

        public bool RecentTasksShowInMainWindow
        {
            get => SettingsManager.Settings.RecentTasksShowInMainWindow;
            set
            {
                SettingsManager.Settings.RecentTasksShowInMainWindow = value;
                OnPropertyChanged();
            }
        }

        public bool RecentTasksShowInTrayMenu
        {
            get => SettingsManager.Settings.RecentTasksShowInTrayMenu;
            set
            {
                SettingsManager.Settings.RecentTasksShowInTrayMenu = value;
                OnPropertyChanged();
            }
        }

        public bool RecentTasksTrayMenuMostRecentFirst
        {
            get => SettingsManager.Settings.RecentTasksTrayMenuMostRecentFirst;
            set
            {
                SettingsManager.Settings.RecentTasksTrayMenuMostRecentFirst = value;
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

            try
            {
                PlatformServices.ShellIntegration.SetPluginExtensionRegistration(value);
            }
            catch (InvalidOperationException)
            {
                // Shell integration not available on this platform
            }
        }

        // OS Integration Settings
        public bool RunAtStartup
        {
            get => SettingsManager.Settings.RunAtStartup;
            set
            {
                if (SettingsManager.Settings.RunAtStartup == value)
                {
                    return;
                }

                var previousValue = SettingsManager.Settings.RunAtStartup;
                SettingsManager.Settings.RunAtStartup = value;
                OnPropertyChanged();

                if (!ApplyStartupPreference(value))
                {
                    SettingsManager.Settings.RunAtStartup = previousValue;
                    OnPropertyChanged(nameof(RunAtStartup));
                }
            }
        }

        public bool EnableContextMenuIntegration
        {
            get => SettingsManager.Settings.EnableContextMenuIntegration;
            set
            {
                if (SettingsManager.Settings.EnableContextMenuIntegration != value)
                {
                    SettingsManager.Settings.EnableContextMenuIntegration = value;
                    OnPropertyChanged();
                    // TODO: Call platform-specific context menu registration service
                }
            }
        }

        public bool EnableSendToIntegration
        {
            get => SettingsManager.Settings.EnableSendToIntegration;
            set
            {
                if (SettingsManager.Settings.EnableSendToIntegration != value)
                {
                    SettingsManager.Settings.EnableSendToIntegration = value;
                    OnPropertyChanged();
                    // TODO: Call platform-specific Send To registration service
                }
            }
        }

        private static bool ApplyStartupPreference(bool enable)
        {
            try
            {
                if (!PlatformServices.IsInitialized)
                {
                    return false;
                }

                return PlatformServices.Startup.SetRunAtStartup(enable);
            }
            catch (InvalidOperationException ex)
            {
                DebugHelper.WriteException(ex, "SettingsViewModel: RunAtStartup platform services not ready.");
                return false;
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
        private string _nameFormatPattern = string.Empty;

        [ObservableProperty]
        private string _nameFormatPatternActiveWindow = string.Empty;

        [ObservableProperty]
        private bool _fileUploadUseNamePattern;

        [ObservableProperty]
        private bool _fileUploadReplaceProblematicCharacters;

        [ObservableProperty]
        private bool _uRLRegexReplace;

        [ObservableProperty]
        private string _uRLRegexReplacePattern = string.Empty;

        [ObservableProperty]
        private string _uRLRegexReplaceReplacement = string.Empty;

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
        public bool ShowAfterUploadWindow
        {
            get => ActiveTaskSettings.AfterUploadJob.HasFlag(AfterUploadTasks.ShowAfterUploadWindow);
            set
            {
                if (value)
                    ActiveTaskSettings.AfterUploadJob |= AfterUploadTasks.ShowAfterUploadWindow;
                else
                    ActiveTaskSettings.AfterUploadJob &= ~AfterUploadTasks.ShowAfterUploadWindow;
                OnPropertyChanged();
            }
        }

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
            WatchFolders.CollectionChanged += (_, _) =>
            {
                HasWatchFolders = WatchFolders.Count > 0;
                RefreshWatchFolderStatuses();
            };
            LoadSettings();
            _isLoading = false;
        }

        partial void OnWatchFolderEnabledChanged(bool value)
        {
            if (_isLoading)
            {
                return;
            }

            RefreshWatchFolderStatuses();
        }

        partial void OnWatchFolderDaemonScopeChanged(WatchFolderDaemonScope value)
        {
            if (_isLoading)
            {
                return;
            }

            WatchFolderDaemonScope normalizedScope = NormalizeWatchFolderDaemonScope(value);
            if (normalizedScope != value)
            {
                WatchFolderDaemonScope = normalizedScope;
                return;
            }

            RefreshWatchFolderDaemonStatusCore();
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings;

            ScreenshotsFolder = settings.CustomScreenshotsPath ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ShareX");
            SaveImageSubFolderPattern = settings.SaveImageSubFolderPattern ?? "%y-%mo";
            UseCustomScreenshotsPath = settings.UseCustomScreenshotsPath;
            ShowTray = settings.ShowTray;
            SilentRun = settings.SilentRun;
            SelectedTheme = settings.SelectedTheme;
            ThemeMode = settings.ThemeMode;
            TrayIconProgressEnabled = settings.TrayIconProgressEnabled;
            TaskbarProgressEnabled = settings.TaskbarProgressEnabled;
            AutoCheckUpdate = settings.AutoCheckUpdate;
            UpdateChannel = settings.UpdateChannel;

            // Proxy Settings
            ProxyMethod = settings.ProxySettings.ProxyMethod;
            ProxyHost = settings.ProxySettings.Host;
            ProxyPort = settings.ProxySettings.Port;
            ProxyUsername = settings.ProxySettings.Username;
            ProxyPassword = settings.ProxySettings.Password;

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

            WatchFolderEnabled = taskSettings.WatchFolderEnabled;
            WatchFolders.Clear();
            WatchFolderWorkflows.Clear();
            LoadWatchFolderWorkflows();
            foreach (var folder in taskSettings.WatchFolderList)
            {
                var vm = WatchFolderSettingsViewModel.FromSettings(folder);
                vm.WorkflowName = GetWorkflowName(folder.WorkflowId);
                WatchFolders.Add(vm);
                AttachWatchFolder(vm);
            }
            HasWatchFolders = WatchFolders.Count > 0;
            WatchFolderEnabled = WatchFolders.Any(folder => folder.Enabled);
            RefreshWatchFolderStatuses();

            InitializeWatchFolderDaemonSettings(settings);
            _lastSavedWatchFolderSignature = BuildWatchFolderConfigurationSignature(
                WatchFolderEnabled,
                taskSettings.WatchFolderList);

            ApplyWatchFolderRuntimePolicy(watchFolderConfigurationChanged: false, refreshDaemonStatus: true);

            // Integration Settings
            SupportsFileAssociations = OperatingSystem.IsWindows();
            try
            {
                IsPluginExtensionRegistered = PlatformServices.ShellIntegration.IsPluginExtensionRegistered();
            }
            catch (InvalidOperationException)
            {
                // Shell integration not available on this platform
                IsPluginExtensionRegistered = false;
            }
        }

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            // Auto-save when any property changes (after initial load)
            if (!_isLoading &&
                e.PropertyName != nameof(HotkeySettings) &&
                e.PropertyName != nameof(SelectedWatchFolder) &&
                e.PropertyName != nameof(HasWatchFolders) &&
                e.PropertyName != nameof(WatchFolderDaemonSupported) &&
                e.PropertyName != nameof(ShowWatchFolderDaemonScopeSelector) &&
                e.PropertyName != nameof(WatchFolderDaemonRunning) &&
                e.PropertyName != nameof(WatchFolderDaemonInstalled) &&
                e.PropertyName != nameof(IsWatchFolderDaemonBusy) &&
                e.PropertyName != nameof(WatchFolderDaemonStatusText) &&
                e.PropertyName != nameof(WatchFolderDaemonButtonText) &&
                e.PropertyName != nameof(WatchFolderDaemonLastError))
            {
                SaveSettings();
            }
        }

        [RelayCommand]
        private void SaveSettings()
        {
            var settings = SettingsManager.Settings;

            settings.CustomScreenshotsPath = ScreenshotsFolder;
            settings.SaveImageSubFolderPattern = SaveImageSubFolderPattern;
            settings.UseCustomScreenshotsPath = UseCustomScreenshotsPath;
            settings.ShowTray = ShowTray;
            settings.SilentRun = SilentRun;
            settings.SelectedTheme = SelectedTheme;
            settings.ThemeMode = ThemeMode;
            settings.TrayIconProgressEnabled = TrayIconProgressEnabled;
            settings.TaskbarProgressEnabled = TaskbarProgressEnabled;
            settings.AutoCheckUpdate = AutoCheckUpdate;
            settings.UpdateChannel = UpdateChannel;

            // Proxy Settings
            settings.ProxySettings.ProxyMethod = ProxyMethod;
            settings.ProxySettings.Host = ProxyHost;
            settings.ProxySettings.Port = ProxyPort;
            settings.ProxySettings.Username = ProxyUsername;
            settings.ProxySettings.Password = ProxyPassword;

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

            settings.WatchFolderDaemonScope = NormalizeWatchFolderDaemonScope(WatchFolderDaemonScope);
            settings.WatchFolderDaemonStartAtStartup = WatchFolderDaemonStartAtStartup;

            WatchFolderEnabled = WatchFolders.Any(folder => folder.Enabled);
            List<WatchFolderSettings> watchFolderSettings = WatchFolders.Select(item => item.ToSettings()).ToList();
            taskSettings.WatchFolderEnabled = WatchFolderEnabled;
            taskSettings.WatchFolderList = watchFolderSettings;

            string currentWatchFolderSignature = BuildWatchFolderConfigurationSignature(WatchFolderEnabled, watchFolderSettings);
            bool watchFolderConfigurationChanged = _lastSavedWatchFolderSignature != currentWatchFolderSignature;
            _lastSavedWatchFolderSignature = currentWatchFolderSignature;

            SettingsManager.SaveApplicationConfig();
            SettingsManager.SaveWorkflowsConfigAsync();

            ApplyWatchFolderRuntimePolicy(
                watchFolderConfigurationChanged,
                refreshDaemonStatus: watchFolderConfigurationChanged);
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

        [RelayCommand]
        private async Task AddWatchFolder()
        {
            if (EditWatchFolderRequester == null)
            {
                return;
            }

            var editVm = new WatchFolderEditViewModel
            {
                Title = "Add Watch Folder",
                Filter = "*.*"
            };
            PopulateEditViewModel(editVm, null);

            var saved = await EditWatchFolderRequester(editVm);
            if (!saved)
            {
                return;
            }

            var item = new WatchFolderSettingsViewModel
            {
                FolderPath = editVm.FolderPath,
                Filter = string.IsNullOrWhiteSpace(editVm.Filter) ? "*.*" : editVm.Filter,
                IncludeSubdirectories = editVm.IncludeSubdirectories,
                MoveFilesToScreenshotsFolder = editVm.MoveFilesToScreenshotsFolder,
                ConvertMovToMp4BeforeProcessing = editVm.ConvertMovToMp4BeforeProcessing,
                Enabled = editVm.Enabled,
                WorkflowId = editVm.SelectedWorkflowId,
                WorkflowName = editVm.SelectedWorkflow?.Name ?? "Unassigned"
            };

            WatchFolders.Add(item);
            AttachWatchFolder(item);
            RefreshWatchFolderStatuses();
            SaveSettings();
        }

        [RelayCommand(CanExecute = nameof(CanEditWatchFolder))]
        private async Task EditWatchFolder()
        {
            if (SelectedWatchFolder == null || EditWatchFolderRequester == null)
            {
                return;
            }

            var editVm = new WatchFolderEditViewModel
            {
                Title = "Edit Watch Folder",
                FolderPath = SelectedWatchFolder.FolderPath,
                Filter = SelectedWatchFolder.Filter,
                IncludeSubdirectories = SelectedWatchFolder.IncludeSubdirectories,
                MoveFilesToScreenshotsFolder = SelectedWatchFolder.MoveFilesToScreenshotsFolder,
                ConvertMovToMp4BeforeProcessing = SelectedWatchFolder.ConvertMovToMp4BeforeProcessing,
                Enabled = SelectedWatchFolder.Enabled
            };
            PopulateEditViewModel(editVm, SelectedWatchFolder.WorkflowId);

            var saved = await EditWatchFolderRequester(editVm);
            if (!saved)
            {
                return;
            }

            _suspendWatchFolderAutoSave = true;
            try
            {
                SelectedWatchFolder.FolderPath = editVm.FolderPath;
                SelectedWatchFolder.Filter = string.IsNullOrWhiteSpace(editVm.Filter) ? "*.*" : editVm.Filter;
                SelectedWatchFolder.IncludeSubdirectories = editVm.IncludeSubdirectories;
                SelectedWatchFolder.MoveFilesToScreenshotsFolder = editVm.MoveFilesToScreenshotsFolder;
                SelectedWatchFolder.ConvertMovToMp4BeforeProcessing = editVm.ConvertMovToMp4BeforeProcessing;
                SelectedWatchFolder.Enabled = editVm.Enabled;
                SelectedWatchFolder.WorkflowId = editVm.SelectedWorkflowId;
                SelectedWatchFolder.WorkflowName = editVm.SelectedWorkflow?.Name ?? "Unassigned";
            }
            finally
            {
                _suspendWatchFolderAutoSave = false;
            }

            RefreshWatchFolderStatuses();
            SaveSettings();
        }

        [RelayCommand(CanExecute = nameof(CanEditWatchFolder))]
        private void RemoveWatchFolder()
        {
            if (SelectedWatchFolder == null)
            {
                return;
            }

            WatchFolders.Remove(SelectedWatchFolder);
            SelectedWatchFolder = null;
            RefreshWatchFolderStatuses();
            SaveSettings();
        }

        private bool CanEditWatchFolder()
        {
            return SelectedWatchFolder != null;
        }

        private bool CanToggleWatchFolderDaemon()
        {
            return WatchFolderDaemonSupported && !IsWatchFolderDaemonBusy;
        }

        [RelayCommand(CanExecute = nameof(CanToggleWatchFolderDaemon))]
        private async Task ToggleWatchFolderDaemon()
        {
            if (!TryGetWatchFolderDaemonService(out IWatchFolderDaemonService daemonService))
            {
                return;
            }

            IsWatchFolderDaemonBusy = true;
            WatchFolderDaemonLastError = string.Empty;

            try
            {
                WatchFolderDaemonScope scope = NormalizeWatchFolderDaemonScope(WatchFolderDaemonScope);
                if (!daemonService.SupportsScope(scope))
                {
                    WatchFolderDaemonLastError = $"Selected scope '{scope}' is not supported on this platform.";
                    WatchFolderDaemonStatusText = "Unsupported scope.";
                    WatchFolderDaemonRunning = false;
                    WatchFolderManager.Instance.StartOrReloadFromCurrentSettings();
                    return;
                }

                WatchFolderDaemonResult result;
                if (WatchFolderDaemonRunning)
                {
                    result = await daemonService.StopAsync(scope, TimeSpan.FromSeconds(WatchFolderDaemonStopTimeoutSeconds));
                }
                else
                {
                    SaveSettings();
                    result = await daemonService.StartAsync(scope, SettingsManager.PersonalFolder, WatchFolderDaemonStartAtStartup);
                }

                ApplyWatchFolderDaemonOperationResult(result);
                RefreshWatchFolderDaemonStatusCore(clearLastError: result.Success);
                ApplyWatchFolderRuntimePolicy(watchFolderConfigurationChanged: false, refreshDaemonStatus: false);
            }
            catch (Exception ex)
            {
                WatchFolderDaemonLastError = ex.Message;
                WatchFolderDaemonStatusText = "Failed to control daemon.";
                DebugHelper.WriteException(ex, "SettingsViewModel: failed to toggle watch folder daemon.");
            }
            finally
            {
                IsWatchFolderDaemonBusy = false;
            }
        }

        [RelayCommand]
        private Task RefreshWatchFolderDaemonStatus()
        {
            RefreshWatchFolderDaemonStatusCore();
            return Task.CompletedTask;
        }

        private void RefreshWatchFolderStatuses()
        {
            var workflows = SettingsManager.WorkflowsConfig?.Hotkeys;
            foreach (var folder in WatchFolders)
            {
                bool workflowValid = workflows?.Any(w => w.Id == folder.WorkflowId) == true;
                folder.UpdateStatus(WatchFolderEnabled, workflowValid);
            }
        }

        private void AttachWatchFolder(WatchFolderSettingsViewModel folder)
        {
            folder.PropertyChanged += (_, e) =>
            {
                if (_suspendWatchFolderAutoSave)
                {
                    return;
                }

                if (e.PropertyName == nameof(WatchFolderSettingsViewModel.FolderPath) ||
                    e.PropertyName == nameof(WatchFolderSettingsViewModel.Filter) ||
                    e.PropertyName == nameof(WatchFolderSettingsViewModel.IncludeSubdirectories) ||
                    e.PropertyName == nameof(WatchFolderSettingsViewModel.MoveFilesToScreenshotsFolder) ||
                    e.PropertyName == nameof(WatchFolderSettingsViewModel.ConvertMovToMp4BeforeProcessing) ||
                    e.PropertyName == nameof(WatchFolderSettingsViewModel.WorkflowId) ||
                    e.PropertyName == nameof(WatchFolderSettingsViewModel.Enabled))
                {
                    RefreshWatchFolderStatuses();
                    SaveSettings();
                }
            };
        }

        private void LoadWatchFolderWorkflows()
        {
            var workflows = SettingsManager.WorkflowsConfig?.Hotkeys;
            if (workflows == null)
            {
                return;
            }

            foreach (var workflow in workflows.Where(w => w.Job != WorkflowType.None))
            {
                WatchFolderWorkflows.Add(new WorkflowOptionViewModel(workflow.Id, GetWorkflowName(workflow)));
            }
        }

        private void PopulateEditViewModel(WatchFolderEditViewModel editVm, string? workflowId)
        {
            foreach (var workflow in WatchFolderWorkflows)
            {
                editVm.Workflows.Add(workflow);
            }

            string? preferredWorkflowId = workflowId;
            if (string.IsNullOrWhiteSpace(preferredWorkflowId))
            {
                preferredWorkflowId = SettingsManager.WorkflowsConfig?.Hotkeys
                    ?.FirstOrDefault(w => w.Job == WorkflowType.FileUpload)
                    ?.Id;
            }

            editVm.SelectedWorkflow = editVm.Workflows.FirstOrDefault(w => w.Id == preferredWorkflowId)
                                      ?? editVm.Workflows.FirstOrDefault();
        }

        private static string GetWorkflowName(string workflowId)
        {
            var workflow = SettingsManager.GetWorkflowById(workflowId);
            return workflow != null ? GetWorkflowName(workflow) : "Unknown workflow";
        }

        private static string GetWorkflowName(WorkflowSettings workflow)
        {
            if (!string.IsNullOrEmpty(workflow.TaskSettings?.Description))
            {
                return workflow.TaskSettings.Description;
            }

            return EnumExtensions.GetDescription(workflow.Job);
        }

        private void InitializeWatchFolderDaemonSettings(ApplicationConfig settings)
        {
            if (!PlatformServices.IsInitialized)
            {
                WatchFolderDaemonSupported = false;
                ShowWatchFolderDaemonScopeSelector = false;
                WatchFolderDaemonScope = NormalizeWatchFolderDaemonScope(settings.WatchFolderDaemonScope);
                WatchFolderDaemonStartAtStartup = settings.WatchFolderDaemonStartAtStartup;
                WatchFolderDaemonStatusText = "Platform services are not initialized.";
                WatchFolderDaemonLastError = string.Empty;
                WatchFolderDaemonRunning = false;
                WatchFolderDaemonInstalled = false;
                return;
            }

            IWatchFolderDaemonService daemonService = PlatformServices.WatchFolderDaemon;
            WatchFolderDaemonSupported = daemonService.IsSupported;
            ShowWatchFolderDaemonScopeSelector = daemonService.IsSupported &&
                                                 (PlatformServices.PlatformInfo.IsLinux || PlatformServices.PlatformInfo.IsMacOS);

            WatchFolderDaemonScope = NormalizeWatchFolderDaemonScope(settings.WatchFolderDaemonScope);
            WatchFolderDaemonStartAtStartup = settings.WatchFolderDaemonStartAtStartup;

            settings.WatchFolderDaemonScope = WatchFolderDaemonScope;
        }

        private void ApplyWatchFolderRuntimePolicy(bool watchFolderConfigurationChanged, bool refreshDaemonStatus)
        {
            bool daemonRunning = refreshDaemonStatus ? RefreshWatchFolderDaemonStatusCore() : WatchFolderDaemonRunning;
            if (daemonRunning)
            {
                WatchFolderManager.Instance.Stop();

                if (watchFolderConfigurationChanged && TryGetWatchFolderDaemonService(out IWatchFolderDaemonService daemonService))
                {
                    WatchFolderDaemonScope scope = NormalizeWatchFolderDaemonScope(WatchFolderDaemonScope);
                    if (daemonService.SupportsScope(scope))
                    {
                        WatchFolderDaemonResult restartResult = RunWatchFolderDaemonCall(() => daemonService.RestartAsync(
                            scope,
                            SettingsManager.PersonalFolder,
                            WatchFolderDaemonStartAtStartup,
                            TimeSpan.FromSeconds(WatchFolderDaemonStopTimeoutSeconds)));

                        ApplyWatchFolderDaemonOperationResult(restartResult);
                    }
                    else
                    {
                        WatchFolderDaemonLastError = $"Selected scope '{scope}' is not supported on this platform.";
                    }

                    daemonRunning = RefreshWatchFolderDaemonStatusCore();
                }
            }

            if (!daemonRunning)
            {
                WatchFolderManager.Instance.StartOrReloadFromCurrentSettings();
            }
        }

        private bool RefreshWatchFolderDaemonStatusCore(bool clearLastError = true)
        {
            try
            {
                if (!TryGetWatchFolderDaemonService(out IWatchFolderDaemonService daemonService))
                {
                    WatchFolderDaemonStatusText = "Watch folder daemon is not supported on this platform.";
                    WatchFolderDaemonRunning = false;
                    WatchFolderDaemonInstalled = false;
                    return false;
                }

                WatchFolderDaemonScope scope = NormalizeWatchFolderDaemonScope(WatchFolderDaemonScope);
                WatchFolderDaemonScope = scope;

                if (!daemonService.SupportsScope(scope))
                {
                    WatchFolderDaemonStatusText = $"Scope '{scope}' is not supported.";
                    WatchFolderDaemonRunning = false;
                    WatchFolderDaemonInstalled = false;
                    return false;
                }

                WatchFolderDaemonStatus status = RunWatchFolderDaemonCall(() => daemonService.GetStatusAsync(scope));
                WatchFolderDaemonInstalled = status.Installed;
                WatchFolderDaemonRunning = status.State == WatchFolderDaemonState.Running;
                WatchFolderDaemonStatusText = $"{status.State} ({status.Scope}) - {status.Message}";
                if (clearLastError)
                {
                    WatchFolderDaemonLastError = string.Empty;
                }
                return WatchFolderDaemonRunning;
            }
            catch (Exception ex)
            {
                WatchFolderDaemonRunning = false;
                WatchFolderDaemonInstalled = false;
                WatchFolderDaemonStatusText = "Failed to query daemon status.";
                WatchFolderDaemonLastError = ex.Message;
                DebugHelper.WriteException(ex, "SettingsViewModel: failed to query watch folder daemon status.");
                return false;
            }
        }

        private static T RunWatchFolderDaemonCall<T>(Func<Task<T>> daemonCall)
        {
            // Avoid UI-thread deadlocks caused by sync-over-async calls in settings workflows.
            return Task.Run(daemonCall).GetAwaiter().GetResult();
        }

        private bool TryGetWatchFolderDaemonService(out IWatchFolderDaemonService daemonService)
        {
            if (!PlatformServices.IsInitialized)
            {
                daemonService = new UnsupportedWatchFolderDaemonService();
                WatchFolderDaemonSupported = false;
                return false;
            }

            daemonService = PlatformServices.WatchFolderDaemon;
            WatchFolderDaemonSupported = daemonService.IsSupported;
            return daemonService.IsSupported;
        }

        private WatchFolderDaemonScope NormalizeWatchFolderDaemonScope(WatchFolderDaemonScope scope)
        {
            if (OperatingSystem.IsWindows())
            {
                return WatchFolderDaemonScope.System;
            }

            if (!PlatformServices.IsInitialized)
            {
                return scope;
            }

            IWatchFolderDaemonService daemonService = PlatformServices.WatchFolderDaemon;
            if (!daemonService.IsSupported)
            {
                return scope;
            }

            if (daemonService.SupportsScope(scope))
            {
                return scope;
            }

            if (daemonService.SupportsScope(WatchFolderDaemonScope.User))
            {
                return WatchFolderDaemonScope.User;
            }

            if (daemonService.SupportsScope(WatchFolderDaemonScope.System))
            {
                return WatchFolderDaemonScope.System;
            }

            return scope;
        }

        private void ApplyWatchFolderDaemonOperationResult(WatchFolderDaemonResult result)
        {
            if (result.Success)
            {
                WatchFolderDaemonLastError = string.Empty;
                return;
            }

            WatchFolderDaemonStatusText = $"Daemon operation failed ({result.ErrorCode}).";
            WatchFolderDaemonLastError = string.IsNullOrWhiteSpace(result.Message)
                ? $"Operation failed with error: {result.ErrorCode}"
                : result.Message;
        }

        private static string BuildWatchFolderConfigurationSignature(bool watchFolderEnabled, List<WatchFolderSettings> watchFolderSettings)
        {
            return JsonConvert.SerializeObject(new
            {
                WatchFolderEnabled = watchFolderEnabled,
                WatchFolderList = watchFolderSettings
            });
        }

        private void ApplyProxyAndResetClient()
        {
            var settings = SettingsManager.Settings;

            // Update ApplicationConfig
            settings.ProxySettings.ProxyMethod = ProxyMethod;
            settings.ProxySettings.Host = ProxyHost;
            settings.ProxySettings.Port = ProxyPort;
            settings.ProxySettings.Username = ProxyUsername;
            settings.ProxySettings.Password = ProxyPassword;

            // Sync to HelpersOptions
            HelpersOptions.SyncProxyFromConfig(settings.ProxySettings);

            // Reset HttpClient to pick up new proxy
            HttpClientFactory.Reset();
        }
    }
}
