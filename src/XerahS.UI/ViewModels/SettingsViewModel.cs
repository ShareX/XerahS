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
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Core.Managers;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private bool _isLoading = true;

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
            WatchFolderManager.Instance.UpdateWatchers();

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
                e.PropertyName != nameof(HasWatchFolders))
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

            WatchFolderEnabled = WatchFolders.Any(folder => folder.Enabled);
            taskSettings.WatchFolderEnabled = WatchFolderEnabled;
            taskSettings.WatchFolderList = WatchFolders.Select(item => item.ToSettings()).ToList();

            SettingsManager.SaveApplicationConfig();
            SettingsManager.SaveWorkflowsConfigAsync();
            WatchFolderManager.Instance.UpdateWatchers();
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
                Enabled = SelectedWatchFolder.Enabled
            };
            PopulateEditViewModel(editVm, SelectedWatchFolder.WorkflowId);

            var saved = await EditWatchFolderRequester(editVm);
            if (!saved)
            {
                return;
            }

            SelectedWatchFolder.FolderPath = editVm.FolderPath;
            SelectedWatchFolder.Filter = string.IsNullOrWhiteSpace(editVm.Filter) ? "*.*" : editVm.Filter;
            SelectedWatchFolder.IncludeSubdirectories = editVm.IncludeSubdirectories;
            SelectedWatchFolder.MoveFilesToScreenshotsFolder = editVm.MoveFilesToScreenshotsFolder;
            SelectedWatchFolder.Enabled = editVm.Enabled;
            SelectedWatchFolder.WorkflowId = editVm.SelectedWorkflowId;
            SelectedWatchFolder.WorkflowName = editVm.SelectedWorkflow?.Name ?? "Unassigned";
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
                if (e.PropertyName == nameof(WatchFolderSettingsViewModel.FolderPath) ||
                    e.PropertyName == nameof(WatchFolderSettingsViewModel.Filter) ||
                    e.PropertyName == nameof(WatchFolderSettingsViewModel.IncludeSubdirectories) ||
                    e.PropertyName == nameof(WatchFolderSettingsViewModel.MoveFilesToScreenshotsFolder) ||
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
    }
}
