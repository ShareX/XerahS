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
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Helpers;
using XerahS.Core.Managers;
using XerahS.Core.Tasks;

namespace XerahS.CLI.Commands
{
    public static class CaptureCommand
    {
        public static Command Create()
        {
            var captureCommand = new Command("capture", "Screen capture operations");

            // Screen capture subcommand
            var screenCommand = new Command("screen", "Capture full screen");
            var outputOption = new Option<string?>(
                name: "--output",
                description: "Output file path");
            screenCommand.AddOption(outputOption);
            screenCommand.SetHandler(async (string? output) =>
            {
                Environment.ExitCode = await CaptureScreenAsync(output);
            }, outputOption);

            // Window capture subcommand
            var windowCommand = new Command("window", "Capture active window");
            windowCommand.AddOption(outputOption);
            windowCommand.SetHandler(async (string? output) =>
            {
                Environment.ExitCode = await CaptureWindowAsync(output);
            }, outputOption);

            // Region capture subcommand
            var regionCommand = new Command("region", "Capture specific region");
            var regionOption = new Option<string>(
                name: "--region",
                description: "Region in format 'x,y,width,height'");
            regionCommand.AddOption(regionOption);
            regionCommand.AddOption(outputOption);
            regionCommand.SetHandler(async (string region, string? output) =>
            {
                Environment.ExitCode = await CaptureRegionAsync(region, output);
            }, regionOption, outputOption);

            captureCommand.AddCommand(screenCommand);
            captureCommand.AddCommand(windowCommand);
            captureCommand.AddCommand(regionCommand);

            return captureCommand;
        }

        private static async Task<int> CaptureScreenAsync(string? output)
        {
            try
            {
                Console.WriteLine("Capturing full screen...");

                var taskSettings = new TaskSettings();
                taskSettings.Job = HotkeyType.PrintScreen;

                if (!string.IsNullOrEmpty(output))
                {
                    taskSettings.AfterCaptureJob = AfterCaptureTasks.SaveImageToFile;
                    // Note: Output path will be set by the task execution logic
                }

                var tcs = new TaskCompletionSource<bool>();

                EventHandler<WorkerTask>? handler = null;
                handler = (sender, task) =>
                {
                    TaskManager.Instance.TaskCompleted -= handler;
                    bool success = task.Status == Core.TaskStatus.Completed;
                    tcs.SetResult(success);

                    if (success && !string.IsNullOrEmpty(task.Info.FilePath))
                    {
                        Console.WriteLine($"Screenshot saved: {task.Info.FilePath}");
                    }
                    else if (!success)
                    {
                        var errorMsg = task.Error?.Message ?? task.Status.ToString();
                        Console.Error.WriteLine($"Capture failed: {errorMsg}");
                    }
                };

                TaskManager.Instance.TaskCompleted += handler;

                await Core.Helpers.TaskHelpers.ExecuteJob(taskSettings.Job, taskSettings);

                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));

                if (completedTask != tcs.Task)
                {
                    Console.Error.WriteLine("Capture operation timed out");
                    return 1;
                }

                return await tcs.Task ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to capture screen: {ex.Message}");
                DebugHelper.WriteException(ex);
                return 1;
            }
        }

        private static async Task<int> CaptureWindowAsync(string? output)
        {
            try
            {
                Console.WriteLine("Capturing active window...");

                var taskSettings = new TaskSettings();
                taskSettings.Job = HotkeyType.ActiveWindow;

                if (!string.IsNullOrEmpty(output))
                {
                    taskSettings.AfterCaptureJob = AfterCaptureTasks.SaveImageToFile;
                    // Note: Output path will be set by the task execution logic
                }

                var tcs = new TaskCompletionSource<bool>();

                EventHandler<WorkerTask>? handler = null;
                handler = (sender, task) =>
                {
                    TaskManager.Instance.TaskCompleted -= handler;
                    bool success = task.Status == Core.TaskStatus.Completed;
                    tcs.SetResult(success);

                    if (success && !string.IsNullOrEmpty(task.Info.FilePath))
                    {
                        Console.WriteLine($"Screenshot saved: {task.Info.FilePath}");
                    }
                    else if (!success)
                    {
                        var errorMsg = task.Error?.Message ?? task.Status.ToString();
                        Console.Error.WriteLine($"Capture failed: {errorMsg}");
                    }
                };

                TaskManager.Instance.TaskCompleted += handler;

                await Core.Helpers.TaskHelpers.ExecuteJob(taskSettings.Job, taskSettings);

                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));

                if (completedTask != tcs.Task)
                {
                    Console.Error.WriteLine("Capture operation timed out");
                    return 1;
                }

                return await tcs.Task ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to capture window: {ex.Message}");
                DebugHelper.WriteException(ex);
                return 1;
            }
        }

        private static async Task<int> CaptureRegionAsync(string region, string? output)
        {
            try
            {
                Console.WriteLine($"Capturing region: {region}");
                Console.Error.WriteLine("Note: Region capture from CLI is not fully implemented yet.");
                Console.Error.WriteLine("Please use 'capture screen' or 'capture window' instead.");
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to capture region: {ex.Message}");
                DebugHelper.WriteException(ex);
                return 1;
            }
        }
    }
}
