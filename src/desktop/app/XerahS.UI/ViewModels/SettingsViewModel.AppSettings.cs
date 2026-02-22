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
    }
}
