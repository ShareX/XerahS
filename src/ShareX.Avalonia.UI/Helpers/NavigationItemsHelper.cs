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

using Avalonia;
using FluentAvalonia.UI.Controls;
using XerahS.Core;
using XerahS.Core.Hotkeys;

namespace XerahS.UI.Helpers
{
    /// <summary>
    /// Helper class for updating navigation items with workflow information.
    /// Shared between MainWindow and TrayIcon to ensure consistent workflow display.
    /// </summary>
    public static class NavigationItemsHelper
    {
        /// <summary>
        /// Update navigation items with the first 3 workflows.
        /// Returns a list of workflow IDs and their display names.
        /// </summary>
        public static List<(string Id, string DisplayName)> GetTop3Workflows()
        {
            List<WorkflowSettings> workflows;
            if (Application.Current is App app && app.WorkflowManager != null)
            {
                workflows = app.WorkflowManager.Workflows.Take(3).ToList();
            }
            else
            {
                workflows = SettingManager.WorkflowsConfig.Hotkeys.Take(3).ToList();
            }

            var result = new List<(string Id, string DisplayName)>();
            foreach (var workflow in workflows)
            {
                var description = string.IsNullOrEmpty(workflow.TaskSettings.Description)
                    ? XerahS.Common.EnumExtensions.GetDescription(workflow.Job)
                    : workflow.TaskSettings.Description;

                result.Add((workflow.Id, description));
            }

            return result;
        }

        /// <summary>
        /// Update navigation menu items for capture workflows.
        /// This method is used by MainWindow to populate the Capture submenu.
        /// </summary>
        /// <param name="captureItem">The parent navigation item for capture</param>
        public static void UpdateCaptureNavigationItems(NavigationViewItem captureItem)
        {
            if (captureItem == null) return;

            // Clear existing items
            captureItem.MenuItems.Clear();

            // Get first 3 workflows
            var workflows = GetTop3Workflows();

            for (int i = 0; i < workflows.Count; i++)
            {
                var (id, displayName) = workflows[i];
                var navItem = new NavigationViewItem
                {
                    Content = displayName,
                    Tag = $"Capture_{id}" // Use ID-based tag instead of index
                };

                captureItem.MenuItems.Add(navItem);
            }
        }
    }
}
