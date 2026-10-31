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
using XerahS.Core.Cloud;
using XerahS.Core.Hotkeys;
using XerahS.Core.Managers;
using XerahS.Platform.Abstractions;
using XerahS.UI.Helpers;

namespace XerahS.UI.ViewModels
{
    /// <summary>
    /// ViewModel for application-level settings.
    /// Split into partial-class files by concern:
    ///   SettingsViewModel.AppSettings.cs        — tray, history, recent tasks, OS integration
    ///   SettingsViewModel.TaskSettings.cs        — task settings forwarding (capture, upload, after-capture/upload)
    ///   SettingsViewModel.WatchFolders.cs         — watch folder list management (add/edit/remove)
    ///   SettingsViewModel.WatchFolderDaemon.cs    — daemon lifecycle orchestration
    ///   SettingsViewModel.Proxy.cs                — proxy settings + ApplyProxyAndResetClient
    ///   SettingsViewModel.Integration.cs          — plugin extension, file associations, startup
    /// </summary>
    public partial class SettingsViewModel : ViewModelBase
    {
        private bool _isLoading = true;
        private string _lastSavedWatchFolderSignature = string.Empty;
        private bool _suspendWatchFolderAutoSave;

        [ObservableProperty]
        private string _screenshotsFolder = string.Empty;

        [ObservableProperty]
        private string _saveImageSubFolderPattern = string.Empty;

        [ObservableProperty]
        private bool _useSaveImageSubFolderPattern = true;

        [ObservableProperty]
        private bool _useCustomScreenshotsPath;

        [ObservableProperty]
        private bool _showTray;

        [ObservableProperty]
        private bool _silentRun;

        partial void OnShowTrayChanged(bool value)
        {
            if (_isLoading) return;

            if (!value && SilentRun)
            {
                SilentRun = false;
            }
        }

        partial void OnSilentRunChanged(bool value)
        {
            if (_isLoading) return;

            if (value && !ShowTray)
            {
                ShowTray = true;
            }
        }

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
        private bool _hDRScreenshotColorCorrection;

        [ObservableProperty]
        private bool _trayIconProgressEnabled;

        [ObservableProperty]
        private bool _taskbarProgressEnabled;

        [ObservableProperty]
        private bool _disableToastNotification;

        [ObservableProperty]
        private bool _autoCheckUpdate;

        [ObservableProperty]
        private UpdateChannel _updateChannel;

        public UpdateChannel[] UpdateChannels => (UpdateChannel[])Enum.GetValues(typeof(UpdateChannel));

        [ObservableProperty]
        private PreReleaseUpdateSource _preReleaseUpdateSource;

        public PreReleaseUpdateSource[] PreReleaseUpdateSources => (PreReleaseUpdateSource[])Enum.GetValues(typeof(PreReleaseUpdateSource));

        [ObservableProperty]
        private string _customPreReleaseUpdateSource = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ManualUpdateCommand))]
        private bool _isManualUpdateInProgress;

        [ObservableProperty]
        private string _manualUpdateStatusText = "Use Update Now to check and install the latest release for your selected channel.";

        public string ManualUpdateButtonText => IsManualUpdateInProgress ? "Checking..." : "Update Now";
        public bool CanTriggerManualUpdate => !IsManualUpdateInProgress;

        public bool ShowPreReleaseSourceSettings => UpdateChannel == UpdateChannel.PreRelease;
        public bool ShowCustomPreReleaseSource => ShowPreReleaseSourceSettings && PreReleaseUpdateSource == PreReleaseUpdateSource.Custom;

        partial void OnAutoCheckUpdateChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowPreReleaseSourceSettings));
            OnPropertyChanged(nameof(ShowCustomPreReleaseSource));
        }

        partial void OnUpdateChannelChanged(UpdateChannel value)
        {
            OnPropertyChanged(nameof(ShowPreReleaseSourceSettings));
            OnPropertyChanged(nameof(ShowCustomPreReleaseSource));
        }

        partial void OnPreReleaseUpdateSourceChanged(PreReleaseUpdateSource value)
        {
            OnPropertyChanged(nameof(ShowCustomPreReleaseSource));
        }

        partial void OnIsManualUpdateInProgressChanged(bool value)
        {
            OnPropertyChanged(nameof(ManualUpdateButtonText));
            OnPropertyChanged(nameof(CanTriggerManualUpdate));
        }

        [ObservableProperty]
        private HotkeyDeliveryViewModel _hotkeyDelivery;

        public ApplicationConfig ApplicationConfig => SettingsManager.Settings;

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

        public Func<WatchFolderEditViewModel, Task<bool>>? EditWatchFolderRequester { get; set; }
        public Func<Task<string?>>? BrowseScreenshotsFolderRequester { get; set; }

        public SettingsViewModel(
            IXerahSCloudClient? cloudClient = null,
            IXerahSCloudOAuthCoordinator? cloudOAuthCoordinator = null)
        {
            _cloudClient = cloudClient;
            _cloudOAuthCoordinator = cloudOAuthCoordinator;
            HotkeyDelivery = new HotkeyDeliveryViewModel();
            WatchFolders.CollectionChanged += (_, _) =>
            {
                HasWatchFolders = WatchFolders.Count > 0;
                RefreshWatchFolderStatuses();
            };
            LoadSettings();
            InitializeCloudStatus();
            _isLoading = false;
        }

        /// <summary>
        /// Properties in this set are transient UI state that should NOT trigger auto-save
        /// when they change. Adding a new transient property only requires adding its name here
        /// instead of extending a fragile if-chain in OnPropertyChanged.
        /// </summary>
        private static readonly HashSet<string> AutoSaveExclusions = new()
        {
            nameof(HotkeyDelivery),
            nameof(SelectedWatchFolder),
            nameof(HasWatchFolders),
            nameof(HasMcpApiKey),
            nameof(McpApiKeyDisplay),
            nameof(McpApiKeyStatusText),
            nameof(McpManifestUrl),
            nameof(AssistantHotkeyText),
            nameof(AssistantHotkeyStatusText),
            nameof(CaptureCommandPaletteHotkeyText),
            nameof(CaptureCommandPaletteStatusText),
            nameof(AssistantProviderOptions),
            nameof(SelectedAssistantProvider),
            nameof(AssistantProviderModelId),
            nameof(AssistantProviderBaseUrl),
            nameof(AssistantProviderApiKey),
            nameof(AssistantProviderStatusText),
            nameof(AssistantProviderNeedsApiKey),
            nameof(AssistantProviderHasApiKey),
            nameof(AssistantProviderKeyStatus),
            nameof(WatchFolderDaemonSupported),
            nameof(ShowWatchFolderDaemonScopeSelector),
            nameof(WatchFolderDaemonRunning),
            nameof(WatchFolderDaemonInstalled),
            nameof(IsWatchFolderDaemonBusy),
            nameof(WatchFolderDaemonStatusText),
            nameof(WatchFolderDaemonButtonText),
            nameof(WatchFolderDaemonLastError),
            nameof(LinuxRegionSelectorCurrentSessionText),
            nameof(LinuxRegionSelectorPortalBackendText),
            nameof(LinuxRegionSelectorAvailableText),
            nameof(LinuxRegionSelectorAutomaticText),
            nameof(LinuxRegionSelectorLastDecisionText),
            nameof(IsManualUpdateInProgress),
            nameof(ManualUpdateStatusText),
            nameof(IsSettingsBackupBusy),
            nameof(SettingsBackupStatusText),
            nameof(CloudStatusText),
            nameof(CloudProfileUrl),
            nameof(IsCloudBusy),
            nameof(IsCloudSignedIn),
            nameof(IsCloudConfigured),
        };

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            // Auto-save when any property changes (after initial load),
            // except for transient UI-state properties listed in AutoSaveExclusions.
            if (!_isLoading && e.PropertyName != null && !AutoSaveExclusions.Contains(e.PropertyName))
            {
                SaveSettings();
            }
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings;

            ScreenshotsFolder = settings.CustomScreenshotsPath ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ShareX");
            SaveImageSubFolderPattern = settings.SaveImageSubFolderPattern ?? "%y-%mo";
            UseSaveImageSubFolderPattern = settings.UseSaveImageSubFolderPattern;
            UseCustomScreenshotsPath = settings.UseCustomScreenshotsPath;
            ShowTray = settings.ShowTray;
            SilentRun = settings.SilentRun;
            SelectedTheme = settings.SelectedTheme;
            ThemeMode = settings.ThemeMode;
            TrayIconProgressEnabled = settings.TrayIconProgressEnabled;
            TaskbarProgressEnabled = settings.TaskbarProgressEnabled;
            DisableToastNotification = settings.DisableToastNotification;
            AutoCheckUpdate = settings.AutoCheckUpdate;
            UpdateChannel = settings.UpdateChannel;
            PreReleaseUpdateSource = settings.PreReleaseUpdateSource;
            CustomPreReleaseUpdateSource = settings.CustomPreReleaseUpdateSource;
            // Migrate legacy Dev channel to PreRelease (Dev was removed)
            if ((int)UpdateChannel >= 2)
            {
                UpdateChannel = UpdateChannel.PreRelease;
                settings.UpdateChannel = UpdateChannel.PreRelease;
            }

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
            HDRScreenshotColorCorrection = taskSettings.CaptureSettings.HDRScreenshotColorCorrection;
            RefreshLinuxRegionSelectorDiagnostics();
            RefreshLinuxClipboardDiagnostics();
            LinuxRegionSelectorPreference = LinuxRegionSelectorPreferenceSupport.NormalizeForCurrentSession(
                taskSettings.CaptureSettings.LinuxRegionSelectorPreference);
            MacOSRegionSelectorPreference = taskSettings.CaptureSettings.MacOSRegionSelectorPreference;
            MacOSPlayCaptureSound = taskSettings.CaptureSettings.MacOSPlayCaptureSound;
            LinuxRecordingBackendPreference = ResolveLinuxRecordingBackendPreference(taskSettings.CaptureSettings);

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
            try
            {
                IStartupService startupService = PlatformServices.Startup;
                settings.RunAtStartup = startupService.IsRunAtStartupEnabled();
            }
            catch (InvalidOperationException)
            {
                settings.RunAtStartup = false;
            }

            IShellIntegrationService? shellIntegration = PlatformServices.GetShellIntegrationIfAvailable();
            SupportsFileAssociations = shellIntegration?.SupportsPluginExtensionRegistration == true;
            SupportsContextMenuIntegration = shellIntegration?.SupportsContextMenuIntegration == true;
            SupportsSendToIntegration = shellIntegration?.SupportsSendToIntegration == true;
            LoadAssistantProviderSettings();

            if (shellIntegration != null)
            {
                IsPluginExtensionRegistered = shellIntegration.SupportsPluginExtensionRegistration &&
                                              shellIntegration.IsPluginExtensionRegistered();
                settings.EnableContextMenuIntegration = shellIntegration.SupportsContextMenuIntegration &&
                                                        shellIntegration.IsContextMenuIntegrationEnabled();
                settings.EnableSendToIntegration = shellIntegration.SupportsSendToIntegration &&
                                                   shellIntegration.IsSendToIntegrationEnabled();
            }
            else
            {
                IsPluginExtensionRegistered = false;
                settings.EnableContextMenuIntegration = false;
                settings.EnableSendToIntegration = false;
            }
        }

        [RelayCommand]
        private void SaveSettings()
        {
            var settings = SettingsManager.Settings;

            settings.CustomScreenshotsPath = ScreenshotsFolder;
            settings.SaveImageSubFolderPattern = SaveImageSubFolderPattern;
            settings.UseSaveImageSubFolderPattern = UseSaveImageSubFolderPattern;
            settings.UseCustomScreenshotsPath = UseCustomScreenshotsPath;
            settings.ShowTray = ShowTray;
            settings.SilentRun = SilentRun;
            settings.SelectedTheme = SelectedTheme;
            settings.ThemeMode = ThemeMode;
            settings.TrayIconProgressEnabled = TrayIconProgressEnabled;
            settings.TaskbarProgressEnabled = TaskbarProgressEnabled;
            settings.DisableToastNotification = DisableToastNotification;
            settings.AutoCheckUpdate = AutoCheckUpdate;
            settings.UpdateChannel = UpdateChannel;
            settings.PreReleaseUpdateSource = PreReleaseUpdateSource;
            settings.CustomPreReleaseUpdateSource = CustomPreReleaseUpdateSource?.Trim() ?? string.Empty;

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
            taskSettings.CaptureSettings.HDRScreenshotColorCorrection = HDRScreenshotColorCorrection;
            taskSettings.CaptureSettings.LinuxRegionSelectorPreference = LinuxRegionSelectorPreference;
            taskSettings.CaptureSettings.MacOSRegionSelectorPreference = MacOSRegionSelectorPreference;
            taskSettings.CaptureSettings.MacOSPlayCaptureSound = MacOSPlayCaptureSound;
            taskSettings.CaptureSettings.LinuxRecordingBackendPreference = LinuxRecordingBackendPreference;

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
            _ = SettingsManager.SaveWorkflowsConfigAsync();
            App.ApplyMenuBarOnlyModeFromSettings();
            Services.UpdateService.Instance.RefreshConfigurationFromSettings();

            ApplyWatchFolderRuntimePolicy(
                watchFolderConfigurationChanged,
                refreshDaemonStatus: watchFolderConfigurationChanged);
        }

        private bool CanManualUpdate() => !IsManualUpdateInProgress;

        [RelayCommand(CanExecute = nameof(CanManualUpdate))]
        private async Task ManualUpdate()
        {
            IsManualUpdateInProgress = true;

            try
            {
                if (XerahS.UI.Services.UpdateService.IsRuntimeManagedByFlatpak)
                {
                    // The Flatpak runtime owns upgrade delivery; the in-app
                    // updater is intentionally a no-op here so it does not
                    // surface .deb / .rpm assets that the sandbox cannot
                    // install.
                    ManualUpdateStatusText = XerahS.UI.Services.UpdateService.RuntimeManagedUpdateMessage;
                    return;
                }

                ManualUpdateStatusText = "Checking for updates...";
                Services.UpdateService.Instance.Initialize();
                UpdateStatus status = await Services.UpdateService.Instance.CheckForUpdatesAsync();

                ManualUpdateStatusText = status switch
                {
                    UpdateStatus.UpdateAvailable => "Update available. Review the prompt to continue.",
                    UpdateStatus.UpToDate => "XerahS is already up to date.",
                    _ => "Update check failed. Check the debug log for details."
                };
            }
            catch (Exception ex)
            {
                ManualUpdateStatusText = $"Update check failed: {ex.Message}";
                DebugHelper.WriteException(ex, "Manual update check failed.");
            }
            finally
            {
                IsManualUpdateInProgress = false;
            }
        }

        [RelayCommand]
        private async Task BrowseFolder()
        {
            if (BrowseScreenshotsFolderRequester == null)
            {
                return;
            }

            string? path = await BrowseScreenshotsFolderRequester();
            if (!string.IsNullOrWhiteSpace(path))
            {
                ScreenshotsFolder = path;
            }
        }

        [RelayCommand]
        private void ResetToDefaults()
        {
            ScreenshotsFolder = PathsManager.ScreenshotsFolder;
            SaveImageSubFolderPattern = "%y-%mo";
            UseSaveImageSubFolderPattern = true;
            UseCustomScreenshotsPath = false;
            ShowTray = true;
            SilentRun = false;
            DisableToastNotification = false;
            SelectedTheme = 0;
            LinuxRegionSelectorPreference = LinuxInteractiveRegionSelectorPreference.Automatic;
            MacOSRegionSelectorPreference = MacOSInteractiveRegionSelectorPreference.Automatic;
            MacOSPlayCaptureSound = true;
            LinuxRecordingBackendPreference = XerahS.RegionCapture.ScreenRecording.LinuxRecordingBackendPreference.Automatic;
        }

        private static RegionCapture.ScreenRecording.LinuxRecordingBackendPreference ResolveLinuxRecordingBackendPreference(TaskSettingsCapture captureSettings)
        {
            return captureSettings.LinuxRecordingBackendPreference ??
                (captureSettings.UseModernCapture
                    ? RegionCapture.ScreenRecording.LinuxRecordingBackendPreference.Automatic
                    : RegionCapture.ScreenRecording.LinuxRecordingBackendPreference.FFmpeg);
        }

        private static string BuildWatchFolderConfigurationSignature(bool watchFolderEnabled, List<WatchFolderSettings> watchFolderSettings)
        {
            return JsonConvert.SerializeObject(new
            {
                WatchFolderEnabled = watchFolderEnabled,
                WatchFolderList = watchFolderSettings
            });
        }
    }
}
