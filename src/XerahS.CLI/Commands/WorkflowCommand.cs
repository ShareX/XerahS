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
    public static class WorkflowCommand
    {
        public static Command Create()
        {
            var runCommand = new Command("run", "Execute a workflow by ID");

            var workflowIdArg = new Argument<string>(
                name: "workflow-id",
                description: "Workflow ID (e.g., WF01)");

            runCommand.AddArgument(workflowIdArg);

            runCommand.SetHandler(async (string workflowId) =>
            {
                Environment.ExitCode = await RunWorkflowAsync(workflowId);
            }, workflowIdArg);

            return runCommand;
        }

        private static async Task<int> RunWorkflowAsync(string workflowId)
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

                Console.WriteLine($"Executing workflow: {workflow.Name} ({workflowId})");

                // Create a task completion source to wait for workflow completion
                var tcs = new TaskCompletionSource<bool>();

                EventHandler<WorkerTask>? handler = null;
                handler = (sender, task) =>
                {
                    if (task.Info.TaskSettings.WorkflowId == workflowId)
                    {
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

                // Wait for completion with timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));

                if (completedTask != tcs.Task)
                {
                    Console.Error.WriteLine("Workflow execution timed out");
                    return 1;
                }

                bool success = await tcs.Task;
                if (success)
                {
                    Console.WriteLine($"Workflow completed successfully: {workflowId}");
                    return 0;
                }
                else
                {
                    return 1;
                }
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
