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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels
{
    public partial class SettingsViewModel
    {
        private const int WatchFolderDaemonStopTimeoutSeconds = 30;
        private const int WatchFolderDaemonStateRetryCount = 8;
        private const int WatchFolderDaemonStateRetryDelayMs = 250;

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

                bool targetRunningState;
                WatchFolderDaemonResult result;
                if (WatchFolderDaemonRunning)
                {
                    targetRunningState = false;
                    result = await daemonService.StopAsync(scope, TimeSpan.FromSeconds(WatchFolderDaemonStopTimeoutSeconds));
                }
                else
                {
                    targetRunningState = true;
                    SaveSettings();
                    result = await daemonService.StartAsync(scope, SettingsManager.PersonalFolder, WatchFolderDaemonStartAtStartup);
                }

                ApplyWatchFolderDaemonOperationResult(result);
                if (result.Success)
                {
                    await RefreshWatchFolderDaemonStatusWithRetryAsync(targetRunningState, clearLastError: true);
                }
                else
                {
                    RefreshWatchFolderDaemonStatusCore(clearLastError: false);
                }

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

        private async Task RefreshWatchFolderDaemonStatusWithRetryAsync(bool targetRunningState, bool clearLastError)
        {
            for (int attempt = 0; attempt < WatchFolderDaemonStateRetryCount; attempt++)
            {
                bool isRunning = RefreshWatchFolderDaemonStatusCore(clearLastError);
                if (isRunning == targetRunningState)
                {
                    return;
                }

                if (attempt < WatchFolderDaemonStateRetryCount - 1)
                {
                    await Task.Delay(WatchFolderDaemonStateRetryDelayMs);
                }
            }
        }

        [RelayCommand]
        private Task RefreshWatchFolderDaemonStatus()
        {
            RefreshWatchFolderDaemonStatusCore();
            return Task.CompletedTask;
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
    }
}
