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

using System.CommandLine;
using System.CommandLine.Invocation;
using XerahS.CLI;
using System.CommandLine.Binding;
using System.CommandLine.Parsing;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Helpers;
using XerahS.Core.Managers;
using XerahS.Core.Tasks;
using XerahS.History;
using XerahS.Platform.Abstractions;

namespace XerahS.CLI.Commands
{
    public static class CaptureCommand
    {
        public static Command Create()
        {
            var captureCommand = new Command("capture", "Screen capture operations");
            
            var uploadOption = new Option<bool>("--upload") { Description = "Upload the captured image/file" };

            // Screen capture subcommand
            var screenCommand = new Command("screen", "Capture full screen");
            var outputOption = new Option<string?>("--output") { Description = "Output file path" };
            screenCommand.Add(outputOption);
            screenCommand.Add(uploadOption);
            screenCommand.SetAction((parseResult) =>
            {
                var output = parseResult.GetValue(outputOption);
                var upload = parseResult.GetValue(uploadOption);
                Environment.ExitCode = CaptureScreenAsync(output, upload).GetAwaiter().GetResult();
            });

            // Window capture subcommand
            var windowCommand = new Command("window", "Capture active window");
            windowCommand.Add(outputOption);
            windowCommand.Add(uploadOption);
            windowCommand.SetAction((parseResult) =>
            {
                var output = parseResult.GetValue(outputOption);
                var upload = parseResult.GetValue(uploadOption);
                Environment.ExitCode = CaptureWindowAsync(output, upload).GetAwaiter().GetResult();
            });

            // Region capture subcommand
            var regionCommand = new Command("region", "Capture specific region");
            var regionOption = new Option<string>("--region") { Description = "Region in format 'x,y,width,height' (e.g. '0,0,400,400')" };
            regionCommand.Add(regionOption);
            regionCommand.Add(outputOption);
            regionCommand.Add(uploadOption);
            regionCommand.SetAction((parseResult) =>
            {
                var region = parseResult.GetValue(regionOption);
                var output = parseResult.GetValue(outputOption);
                var upload = parseResult.GetValue(uploadOption);
                Environment.ExitCode = CaptureRegionAsync(region!, output, upload).GetAwaiter().GetResult();
            });

            // Transparent region capture subcommand
            var transparentCommand = new Command("transparent", "Capture transparent region");
            transparentCommand.Add(outputOption);
            transparentCommand.Add(uploadOption);
            transparentCommand.SetAction((parseResult) =>
            {
                var output = parseResult.GetValue(outputOption);
                var upload = parseResult.GetValue(uploadOption);
                Environment.ExitCode = CaptureTransparentAsync(output, upload).GetAwaiter().GetResult();
            });

            captureCommand.Add(screenCommand);
            captureCommand.Add(windowCommand);
            captureCommand.Add(regionCommand);
            captureCommand.Add(transparentCommand);

            return captureCommand;
        }

        private static void ConfigureTask(TaskSettings settings, string? output, bool upload)
        {
            if (!string.IsNullOrEmpty(output))
            {
                settings.AfterCaptureJob |= AfterCaptureTasks.SaveImageToFile;
                settings.OverrideScreenshotsFolder = true;
                settings.ScreenshotsFolder = Path.GetDirectoryName(output) ?? string.Empty;
                settings.UploadSettings.NameFormatPattern = Path.GetFileNameWithoutExtension(output);
            }
            
            if (upload)
            {
                settings.AfterCaptureJob |= AfterCaptureTasks.UploadImageToHost;
                settings.AfterUploadJob |= AfterUploadTasks.CopyURLToClipboard; // To match user scenario
            }
        }

        private static async Task<int> RunTask(TaskSettings taskSettings, SkiaSharp.SKBitmap? image = null)
        {
            var tcs = new TaskCompletionSource<bool>();

            EventHandler<WorkerTask>? handler = null;
            handler = (sender, task) =>
            {
                TaskManager.Instance.TaskCompleted -= handler;
                
                // Wait a bit for async history/clipboard ops (though they should be awaited in task)
                // But History is added at end of task.
                
                bool success = task.Status == Core.TaskStatus.Completed;
                
                if (success)
                {
                    if (!string.IsNullOrEmpty(task.Info.FilePath))
                        Console.WriteLine($"Saved: {task.Info.FilePath}");
                    
                    if (!string.IsNullOrEmpty(task.Info.Metadata.UploadURL))
                        Console.WriteLine($"URL: {task.Info.Metadata.UploadURL}");
                        
                    // Check History
                     try
                    {
                        var historyPath = SettingsManager.GetHistoryFilePath();
                        using var historyManager = new HistoryManagerSQLite(historyPath);
                        // Simple check: get latest item and see if it matches
                        var items = historyManager.GetHistoryItems(0, 1);
                        if (items.Count > 0)
                        {
                            var latest = items[0];
                            Console.WriteLine($"[History Verification] Latest Item: {latest.FileName}");
                            Console.WriteLine($"[History Verification] URL: '{latest.URL}'");
                            if (latest.FilePath == task.Info.FilePath && latest.URL == task.Info.Metadata.UploadURL)
                            {
                                 Console.WriteLine($"[History Verification] SUCCESS: History matches task output.");
                            }
                            else
                            {
                                 Console.WriteLine($"[History Verification] WARNING: History item mismatch.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[History Verification] Failed to read history: {ex.Message}");
                    }
                }
                else
                {
                    var errorMsg = task.Error?.Message ?? task.Status.ToString();
                    Console.Error.WriteLine($"Task failed: {errorMsg}");
                }
                
                tcs.SetResult(success);
            };

            TaskManager.Instance.TaskCompleted += handler;

            // Execute
            if (image != null)
            {
                // Bypass capture, start worker directly
                var worker = WorkerTask.Create(taskSettings, image);
                await worker.StartAsync();
            }
            else
            {
                await Core.Helpers.TaskHelpers.ExecuteJob(taskSettings.Job, taskSettings);
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));

            if (completedTask != tcs.Task)
            {
                Console.Error.WriteLine("Operation timed out");
                return 1;
            }

            return await tcs.Task ? 0 : 1;
        }

        private static async Task<int> CaptureScreenAsync(string? output, bool upload)
        {
            try
            {
                Console.WriteLine("Capturing full screen...");
                var taskSettings = new TaskSettings();
                taskSettings.Job = WorkflowType.PrintScreen;
                ConfigureTask(taskSettings, output, upload);
                return await RunTask(taskSettings);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to capture screen: {ex.Message}");
                DebugHelper.WriteException(ex);
                return 1;
            }
        }

        private static async Task<int> CaptureWindowAsync(string? output, bool upload)
        {
            try
            {
                Console.WriteLine("Capturing active window...");
                var taskSettings = new TaskSettings();
                taskSettings.Job = WorkflowType.ActiveWindow;
                ConfigureTask(taskSettings, output, upload);
                return await RunTask(taskSettings);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to capture window: {ex.Message}");
                DebugHelper.WriteException(ex);
                return 1;
            }
        }

        private static async Task<int> CaptureRegionAsync(string region, string? output, bool upload)
        {
            try
            {
                Console.WriteLine($"Capturing region: {region}");
                
                var parts = region.Split(',');
                if (parts.Length != 4) throw new ArgumentException("Region must be x,y,width,height");
                
                int x = int.Parse(parts[0]);
                int y = int.Parse(parts[1]);
                int w = int.Parse(parts[2]);
                int h = int.Parse(parts[3]);
                
                var rect = new SkiaSharp.SKRect(x, y, x + w, y + h);
                var image = await PlatformServices.ScreenCapture.CaptureRectAsync(rect);
                
                if (image == null)
                {
                    Console.Error.WriteLine("Capture returned null");
                    return 1;
                }
                
                var taskSettings = new TaskSettings();
                taskSettings.Job = WorkflowType.RectangleRegion;
                ConfigureTask(taskSettings, output, upload);
                
                return await RunTask(taskSettings, image);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to capture region: {ex.Message}");
                DebugHelper.WriteException(ex);
                return 1;
            }
        }

        private static async Task<int> CaptureTransparentAsync(string? output, bool upload)
        {
            try
            {
                Console.WriteLine("Starting transparent region capture...");
                var taskSettings = new TaskSettings();
                taskSettings.Job = WorkflowType.RectangleTransparent;
                ConfigureTask(taskSettings, output, upload);
                return await RunTask(taskSettings);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to capture transparent region: {ex.Message}");
                DebugHelper.WriteException(ex);
                return 1;
            }
        }
    }
}
