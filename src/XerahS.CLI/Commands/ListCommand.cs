#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
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

using System.CommandLine;
using XerahS.Core;

namespace XerahS.CLI.Commands
{
    public static class ListCommand
    {
        public static Command Create()
        {
            var listCommand = new Command("list", "List workflows and configuration");

            // List workflows subcommand
            var workflowsCommand = new Command("workflows", "List all configured workflows");

            var enabledOnlyOption = new Option<bool>(
                name: "--enabled-only",
                description: "Show only enabled workflows");

            workflowsCommand.AddOption(enabledOnlyOption);

            workflowsCommand.SetHandler((bool enabledOnly) =>
            {
                Environment.ExitCode = ListWorkflows(enabledOnly);
            }, enabledOnlyOption);

            listCommand.AddCommand(workflowsCommand);

            return listCommand;
        }

        private static int ListWorkflows(bool enabledOnly)
        {
            try
            {
                var workflows = SettingManager.WorkflowsConfig?.Hotkeys;

                if (workflows == null || workflows.Count == 0)
                {
                    Console.WriteLine("No workflows configured.");
                    return 0;
                }

                var filteredWorkflows = enabledOnly
                    ? workflows.Where(w => w.Enabled).ToList()
                    : workflows;

                if (filteredWorkflows.Count == 0)
                {
                    Console.WriteLine("No enabled workflows found.");
                    return 0;
                }

                Console.WriteLine($"Workflows ({filteredWorkflows.Count}):");
                Console.WriteLine();

                foreach (var workflow in filteredWorkflows)
                {
                    var status = workflow.Enabled ? "enabled" : "disabled";
                    var hotkey = workflow.HotkeyInfo?.ToString() ?? "none";

                    Console.WriteLine($"  {workflow.Id} - {workflow.Name}");
                    Console.WriteLine($"    Job: {workflow.Job}");
                    Console.WriteLine($"    Hotkey: {hotkey}");
                    Console.WriteLine($"    Status: {status}");
                    Console.WriteLine();
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to list workflows: {ex.Message}");
                return 1;
            }
        }
    }
}
