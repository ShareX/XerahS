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
using System.Runtime.InteropServices;

namespace XerahS.CLI.Commands
{
    public static class WorkflowCommand
    {


        public static Command Create()
        {
            // ... (rest of method same)
            var runCommand = new Command("run", "Execute a workflow by ID");

            var workflowIdArg = new Argument<string>(
                name: "workflow-id",
                description: "Workflow ID (e.g., WF01)");

            var durationOption = new Option<int>(
                name: "--duration",
                description: "Duration in seconds to record (only for recording tasks)",
                getDefaultValue: () => 0);

            var dumpFrameOption = new Option<bool>(
                name: "--dump-frame",
                description: "Dump the first captured frame to disk for debugging",
                getDefaultValue: () => false);

            var exitOnCompleteOption = new Option<bool>(
                name: "--exit-on-complete",
                description: "Exit the CLI process immediately after workflow completion",
                getDefaultValue: () => false);

            runCommand.AddOption(durationOption);
            runCommand.AddOption(dumpFrameOption);
            runCommand.AddOption(exitOnCompleteOption);
            runCommand.AddArgument(workflowIdArg);

            runCommand.SetHandler(async (string workflowId, int duration, bool dumpFrame, bool exitOnComplete) =>
            {
                Environment.ExitCode = await RunWorkflowAsync(workflowId, duration, dumpFrame, exitOnComplete);
            }, workflowIdArg, durationOption, dumpFrameOption, exitOnCompleteOption);

            return runCommand;
        }

        private static async Task<int> RunWorkflowAsync(string workflowId, int duration, bool dumpFrame, bool exitOnComplete)
        {
            try
            {
                var workflow = SettingManager.WorkflowsConfig?.Hotkeys?
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
                Console.WriteLine($"CLI flags: workflowId={workflowId}, duration={duration}s, dumpFrame={dumpFrame}, exitOnComplete={exitOnComplete}, started={runStart:O}");
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "CLI", $"Workflow run flags: workflowId={workflowId}, duration={duration}s, dumpFrame={dumpFrame}, exitOnComplete={exitOnComplete}, started={runStart:O}");

                // [2026-01-10T14:24:00+08:00] Enable first-frame dump when requested to diagnose orientation; disable by default.
                XerahS.ScreenCapture.ScreenRecording.ScreenRecorderService.DebugDumpFirstFrame = dumpFrame;

                Console.WriteLine($"Executing workflow: {workflow.Name} ({workflowId})");

                // Create a task completion source to wait for workflow completion
                var tcs = new TaskCompletionSource<bool>();

                EventHandler<WorkerTask>? handler = null;
                handler = async (sender, task) =>
                {
                    if (task.Info.TaskSettings.WorkflowId == workflowId)
                    {
                        // Check if it's a recording task and handle duration
                        if (task.Info.TaskSettings.Job == HotkeyType.ScreenRecorder && duration > 0)
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
                
                // Use the same entry point as UI
                await Core.Helpers.TaskHelpers.ExecuteWorkflow(workflow, workflowId);

                // Wait for completion (with timeout backup)
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(30000 + (duration * 1000)));

                if (completedTask != tcs.Task)
                {
                    Console.Error.WriteLine("Workflow execution timed out");
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
                        return 0;
                    }
                    
                    return 0;
                }
                
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Workflow execution failed: {ex.Message}");
                DebugHelper.WriteException(ex);
                return 1;
            }
        }
    }
}
