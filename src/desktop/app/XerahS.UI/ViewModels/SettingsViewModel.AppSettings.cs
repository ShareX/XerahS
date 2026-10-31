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

using XerahS.Core;

namespace XerahS.UI.ViewModels
{
    public partial class SettingsViewModel
    {
        public bool IsMacOS => OperatingSystem.IsMacOS();
        public bool IsWindowsPlatform => OperatingSystem.IsWindows();
        public bool ShowUseModernCaptureSetting => OperatingSystem.IsWindows();

        // True on platforms where system panels use dark backgrounds (macOS menu bar, GNOME/KDE top bar).
        // Controls visibility of the white tray icon option.
        public bool IsLinuxOrMacOS => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

        public bool UseWhiteShareXIcon
        {
            get => SettingsManager.Settings.UseWhiteShareXIcon;
            set
            {
                if (SettingsManager.Settings.UseWhiteShareXIcon != value)
                {
                    SettingsManager.Settings.UseWhiteShareXIcon = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool LinuxUseWaylandPortalServices
        {
            get => SettingsManager.Settings.LinuxUseWaylandPortalServices ?? ResolveLegacyLinuxWaylandPortalServicesSetting();
            set
            {
                bool currentValue = SettingsManager.Settings.LinuxUseWaylandPortalServices ?? ResolveLegacyLinuxWaylandPortalServicesSetting();
                if (currentValue != value || SettingsManager.Settings.LinuxUseWaylandPortalServices == null)
                {
                    SettingsManager.Settings.LinuxUseWaylandPortalServices = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool PersistClipboardAfterExit
        {
            get => SettingsManager.Settings.PersistClipboardAfterExit ?? ResolveDefaultPersistClipboardAfterExit();
            set
            {
                bool current = SettingsManager.Settings.PersistClipboardAfterExit ?? ResolveDefaultPersistClipboardAfterExit();
                if (current == value && SettingsManager.Settings.PersistClipboardAfterExit.HasValue)
                {
                    return;
                }

                SettingsManager.Settings.PersistClipboardAfterExit = value;
                OnPropertyChanged();
            }
        }

        private static bool ResolveDefaultPersistClipboardAfterExit()
        {
            bool isWayland = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) ||
                             string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase);
            return isWayland;
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

        public bool ScreenshotContentSearchEnabled
        {
            get => SettingsManager.Settings.ScreenshotContentSearchEnabled;
            set
            {
                if (SettingsManager.Settings.ScreenshotContentSearchEnabled != value)
                {
                    SettingsManager.Settings.ScreenshotContentSearchEnabled = value;
                    OnPropertyChanged();
                }
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

        public bool RememberMainWindowSize
        {
            get => SettingsManager.Settings.RememberMainFormSize;
            set
            {
                if (SettingsManager.Settings.RememberMainFormSize != value)
                {
                    SettingsManager.Settings.RememberMainFormSize = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool RememberMainWindowPosition
        {
            get => SettingsManager.Settings.RememberMainFormPosition;
            set
            {
                if (SettingsManager.Settings.RememberMainFormPosition != value)
                {
                    SettingsManager.Settings.RememberMainFormPosition = value;
                    OnPropertyChanged();
                }
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
                if (SettingsManager.Settings.EnableContextMenuIntegration == value)
                {
                    return;
                }

                bool previousValue = SettingsManager.Settings.EnableContextMenuIntegration;
                SettingsManager.Settings.EnableContextMenuIntegration = value;
                OnPropertyChanged();

                if (!ApplyContextMenuPreference(value))
                {
                    SettingsManager.Settings.EnableContextMenuIntegration = previousValue;
                    OnPropertyChanged(nameof(EnableContextMenuIntegration));
                }
            }
        }

        private static bool ResolveLegacyLinuxWaylandPortalServicesSetting()
        {
            return SettingsManager.DefaultTaskSettings?.CaptureSettings?.UseModernCapture ?? true;
        }

        public bool EnableSendToIntegration
        {
            get => SettingsManager.Settings.EnableSendToIntegration;
            set
            {
                if (SettingsManager.Settings.EnableSendToIntegration == value)
                {
                    return;
                }

                bool previousValue = SettingsManager.Settings.EnableSendToIntegration;
                SettingsManager.Settings.EnableSendToIntegration = value;
                OnPropertyChanged();

                if (!ApplySendToPreference(value))
                {
                    SettingsManager.Settings.EnableSendToIntegration = previousValue;
                    OnPropertyChanged(nameof(EnableSendToIntegration));
                }
            }
        }

        public bool NetworkMonitorEnabled
        {
            get => SettingsManager.Settings.NetworkMonitorEnabled;
            set
            {
                if (SettingsManager.Settings.NetworkMonitorEnabled == value) return;
                SettingsManager.Settings.NetworkMonitorEnabled = value;
                OnPropertyChanged();
                if (value) XerahS.Common.NetworkMonitor.NetworkMonitorHost.Shared.EnsureStarted();
                else XerahS.Common.NetworkMonitor.NetworkMonitorHost.Shared.Stop();
            }
        }

        public bool EnableClipboardContentViewer
        {
            get => SettingsManager.Settings.ShowClipboardContentViewer;
            set
            {
                if (SettingsManager.Settings.ShowClipboardContentViewer == value)
                {
                    return;
                }

                SettingsManager.Settings.ShowClipboardContentViewer = value;
                OnPropertyChanged();
                App.SetClipboardMonitorEnabled(value);
            }
        }
    }
}
