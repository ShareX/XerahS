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
using System.CommandLine.Invocation;
using XerahS.Core;

namespace XerahS.CLI.Commands
{
    public static class ConfigCommand
    {
        public static Command Create()
        {
            var configCommand = new Command("config", "Configuration operations");

            // Show config subcommand
            var showCommand = new Command("show", "Show current configuration summary");
            showCommand.SetAction((parseResult) =>
            {
                Environment.ExitCode = ShowConfig();
            });

            // Path subcommand
            var pathCommand = new Command("path", "Show configuration file paths");
            pathCommand.SetAction((parseResult) =>
            {
                Environment.ExitCode = ShowPaths();
            });

            configCommand.Add(showCommand);
            configCommand.Add(pathCommand);

            return configCommand;
        }

        private static int ShowConfig()
        {
            try
            {
                Console.WriteLine("ShareX Configuration Summary:");
                Console.WriteLine();

                var appConfig = SettingsManager.Settings;
                if (appConfig != null)
                {
                    Console.WriteLine("Application Settings:");
                    Console.WriteLine($"  Language: {appConfig.Language}");
                    Console.WriteLine($"  Show Tray Icon: {appConfig.ShowTray}");
                    Console.WriteLine($"  Silent Run: {appConfig.SilentRun}");
                    Console.WriteLine($"  Theme: {appConfig.SelectedTheme}");
                    Console.WriteLine();
                }

                var workflowsConfig = SettingsManager.WorkflowsConfig;
                if (workflowsConfig != null)
                {
                    var enabledCount = workflowsConfig.Hotkeys?.Count(w => w.Enabled) ?? 0;
                    var totalCount = workflowsConfig.Hotkeys?.Count ?? 0;
                    Console.WriteLine($"Workflows: {enabledCount} enabled / {totalCount} total");
                    Console.WriteLine();
                }

                Console.WriteLine("Use 'xerahs config path' to see configuration file locations.");
                Console.WriteLine("Use 'xerahs list workflows' to see all configured workflows.");

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to show configuration: {ex.Message}");
                return 1;
            }
        }

        private static int ShowPaths()
        {
            try
            {
                Console.WriteLine("Configuration File Paths:");
                Console.WriteLine();

                Console.WriteLine($"Personal Folder:");
                Console.WriteLine($"  {SettingsManager.PersonalFolder}");
                Console.WriteLine();

                Console.WriteLine($"Settings Folder:");
                Console.WriteLine($"  {SettingsManager.SettingsFolder}");
                Console.WriteLine();

                Console.WriteLine($"Configuration Files:");
                Console.WriteLine($"  ApplicationConfig: {SettingsManager.ApplicationConfigFilePath}");
                Console.WriteLine($"  WorkflowsConfig:   {SettingsManager.WorkflowsConfigFilePath}");
                Console.WriteLine($"  UploadersConfig:   {SettingsManager.UploadersConfigFilePath}");
                Console.WriteLine();

                Console.WriteLine($"Backup Folder:");
                Console.WriteLine($"  {SettingsManager.BackupFolder}");

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to show paths: {ex.Message}");
                return 1;
            }
        }
    }
}
