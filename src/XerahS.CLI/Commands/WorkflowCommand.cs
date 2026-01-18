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
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Helpers;
using XerahS.Core.Managers;
using XerahS.Core.Tasks;
using System.Runtime.InteropServices;

namespace XerahS.CLI.Commands
{
    public static class WorkflowCommand
    {
        public static Command Create()
        {
            var runCommand = new Command("run", "Execute a workflow by ID");

            var workflowIdArg = new Argument<string>("workflow-id");
            var durationOption = new Option<int>("--duration");
            durationOption.Description = "Duration in seconds to record (only for recording tasks)";

            var dumpFrameOption = new Option<bool>("--dump-frame") { Description = "Dump the first captured frame to disk for debugging" };
            var exitOnCompleteOption = new Option<bool>("--exit-on-complete") { Description = "Exit the CLI process immediately after workflow completion" };
            var regionOption = new Option<string?>("--region") { Description = "Region override in format 'x,y,width,height' (e.g. '0,0,500,500')" };

            runCommand.Add(durationOption);
            runCommand.Add(dumpFrameOption);
            runCommand.Add(exitOnCompleteOption);
            runCommand.Add(regionOption);
            runCommand.Add(workflowIdArg);

            runCommand.SetAction((parseResult) =>
            {
                var workflowId = parseResult.GetValue(workflowIdArg);
                if (string.IsNullOrEmpty(workflowId))
                {
                    Console.Error.WriteLine("Error: workflow-id is required");
                    Environment.ExitCode = 1;
                    return;
                }
                var duration = parseResult.GetValue(durationOption);
                var dumpFrame = parseResult.GetValue(dumpFrameOption);
                var exitOnComplete = parseResult.GetValue(exitOnCompleteOption);
                var region = parseResult.GetValue(regionOption);

                Environment.ExitCode = RunWorkflowAsync(workflowId, duration, dumpFrame, exitOnComplete, region).GetAwaiter().GetResult();
            });

            return runCommand;
        }

        private static async Task<int> RunWorkflowAsync(string workflowId, int duration, bool dumpFrame, bool exitOnComplete, string? region)
        {
            try
            {
                var workflow = SettingsManager.WorkflowsConfig?.Hotkeys?
                    .FirstOrDefault(w => w.Id == workflowId);

                if (workflow == null)
                {
                    Console.Error.WriteLine($"Workflow not found: {workflowId}");
                    Console.WriteLine("Use 'xerahs list workflows' to see available workflows.");
                    return 1;
                }

                if (!workflow.Enabled)
                {
                    Console.Error.WriteLine($"Workflow is disabled: {workflowId}");
                    return 1;
                }

                var runStart = DateTime.Now;
                Console.WriteLine($"CLI flags: workflowId={workflowId}, duration={duration}s, dumpFrame={dumpFrame}, exitOnComplete={exitOnComplete}, region={region ?? "null"}, started={runStart:O}");
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "CLI", $"Workflow run flags: workflowId={workflowId}, duration={duration}s, dumpFrame={dumpFrame}, exitOnComplete={exitOnComplete}, region={region ?? "null"}, started={runStart:O}");

                // [2026-01-10T14:24:00+08:00] Enable first-frame dump when requested to diagnose orientation; disable by default.
                XerahS.RegionCapture.ScreenRecording.ScreenRecorderService.DebugDumpFirstFrame = dumpFrame;

                Console.WriteLine($"Executing workflow: {workflow.Name} ({workflowId})");

                // Create a task completion source to wait for workflow completion
                var tcs = new TaskCompletionSource<bool>();
                
                // Need to keep reference to the specific task ID we start
                string? expectedTaskId = null;

                EventHandler<WorkerTask>? handler = null;
                handler = async (sender, task) =>
                {
                    // If we have an expected Task ID, use it. Otherwise fall back to WorkflowID match (legacy)
                    if ((expectedTaskId != null && task.Info.CorrelationId == expectedTaskId) || 
                        (expectedTaskId == null && task.Info.TaskSettings.WorkflowId == workflowId))
                    {
                        // Check if it's a recording task and handle duration
                        if (task.Info.TaskSettings.Job == HotkeyType.ScreenRecorderActiveWindow && duration > 0)
                        {
                            Console.WriteLine($"Recording started. Waiting for {duration} seconds...");
                            await Task.Delay(duration * 1000);
                            Console.WriteLine("Stopping recording...");
                            await ScreenRecordingManager.Instance.StopRecordingAsync();
                        }

                        TaskManager.Instance.TaskCompleted -= handler;
                        bool success = task.Status == Core.TaskStatus.Completed;
                        tcs.SetResult(success);

                        if (!success)
                        {
                            var errorMsg = task.Error?.Message ?? task.Status.ToString();
                            Console.Error.WriteLine($"Workflow failed: {errorMsg}");
                        }
                    }
                };

                TaskManager.Instance.TaskCompleted += handler;
                
                if (!string.IsNullOrEmpty(region))
                {
                     // Region Override Mode
                    try 
                    {
                        var parts = region.Split(',');
                        if (parts.Length != 4) throw new ArgumentException("Region must be x,y,width,height");
                        int x = int.Parse(parts[0]);
                        int y = int.Parse(parts[1]);
                        int w = int.Parse(parts[2]);
                        int h = int.Parse(parts[3]);
                        
                        Console.WriteLine($"Capturing override region: {region}");
                        var rect = new SkiaSharp.SKRect(x, y, x + w, y + h);
                        var image = await XerahS.Platform.Abstractions.PlatformServices.ScreenCapture.CaptureRectAsync(rect);
                        
                        if (image == null)
                        {
                            Console.Error.WriteLine("Region capture failed (returned null)");
                            return 1;
                        }
                        
                        Console.WriteLine("Region captured. Starting worker task with workflow settings...");
                        
                        // Ensure WorkflowId is set in settings
                        workflow.TaskSettings.WorkflowId = workflowId;
                        
                        var worker = WorkerTask.Create(workflow.TaskSettings, image);
                        expectedTaskId = worker.Info.CorrelationId;
                        
                        // We must start it manually since we bypassed TaskManager.StartTask(settings)
                        // But we want TaskManager events to fire? 
                        // WorkerTask triggers TaskManager events via TaskManager instance usually? No, TaskManager listens to new tasks?
                        // TaskManager.Instance doesn't automatically pick up manually created tasks unless we add them
                        // BUT WorkerTask internally calls TaskManager events? No.
                        // TaskManager wraps WorkerTask.
                        
                        // Workaround: Use TaskManager logic if possible.
                        // TaskManager doesn't expose "StartTask(settings, image)".
                        // So we will just run the worker and manually fire/wait.
                        // Wait, my handler listens to TaskManager.Instance.TaskCompleted.
                        // Does WorkerTask fire TaskManager.TaskCompleted?
                        // WorkerTask fires its own TaskCompleted event.
                        
                        // Let's attach to the worker directly
                        worker.TaskCompleted += (s, e) => 
                        {
                             // Bridge to our handler logic if needed, or just set tcs directly
                             bool success = worker.Status == Core.TaskStatus.Completed;
                             tcs.TrySetResult(success);
                        };
                        
                        await worker.StartAsync();
                    }
                    catch (Exception ex)
                    {
                         Console.Error.WriteLine($"Region capture/execution error: {ex.Message}");
                         DebugHelper.WriteException(ex);
                         return 1;
                    }
                }
                else
                {
                    // Standard Execution
                    await Core.Helpers.TaskHelpers.ExecuteWorkflow(workflow, workflowId);
                }

                // Wait for completion (with timeout backup)
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(30000 + (duration * 1000)));

                if (completedTask != tcs.Task)
                {
                    Console.Error.WriteLine("Workflow execution timed out");
                    if (exitOnComplete) Environment.Exit(1);
                    return 1;
                }

                bool success = await tcs.Task;
                if (success)
                {
                    // If manually stopped via duration, it might already be stopped.
                    if (ScreenRecordingManager.Instance.IsRecording && duration == 0)
                    {
                        Console.WriteLine("Recording active. Waiting 5 seconds before stopping (default)...");
                        await Task.Delay(5000);
                        await ScreenRecordingManager.Instance.StopRecordingAsync();
                    }

                    Console.WriteLine($"Workflow completed successfully: {workflowId}");
                    
                    if (exitOnComplete)
                    {
                        Console.WriteLine("Exiting (--exit-on-complete specified).");
                        Environment.Exit(0);
                        return 0;
                    }
                    
                    return 0;
                }
                
                if (exitOnComplete) Environment.Exit(1);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Workflow execution failed: {ex.Message}");
                DebugHelper.WriteException(ex);
                if (exitOnComplete) Environment.Exit(1);
                return 1;
            }
        }
    }
}
