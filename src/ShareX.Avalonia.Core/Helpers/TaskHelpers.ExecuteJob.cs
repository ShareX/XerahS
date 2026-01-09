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

using XerahS.Common;
using XerahS.Core.Managers;

using XerahS.Platform.Abstractions;

namespace XerahS.Core.Helpers;

public static partial class TaskHelpers
{
    /// <summary>
    /// Execute a workflow using its complete settings.
    /// This is the preferred method - avoids ambiguity with HotkeyType lookup.
    /// </summary>
    /// <param name="workflow">The workflow to execute</param>
    /// <param name="workflowId">Optional workflow ID for troubleshooting (uses workflow.Id if not provided)</param>
    public static async Task ExecuteWorkflow(Core.Hotkeys.WorkflowSettings workflow, string? workflowId = null)
    {
        // Use provided ID or get from workflow
        var id = workflowId ?? workflow?.Id ?? "Unknown";

        // Create a user-friendly category combining job type and ID for better identification
        var logCategory = workflow != null
            ? $"{workflow.Job}_{id}"
            : id;

        TroubleshootingHelper.Log(logCategory, "EXECUTE_WORKFLOW", $"Entry: workflow={workflow?.Name ?? "null"}, Job={workflow?.Job.ToString() ?? "null"}, Id={id}");

        if (workflow == null)
        {
            DebugHelper.WriteLine("ExecuteWorkflow: workflow is null");
            TroubleshootingHelper.Log(logCategory, "EXECUTE_WORKFLOW", "ABORT: workflow is null");
            return;
        }

        // Store the workflow ID in task settings for troubleshooting
        if (workflow.TaskSettings != null)
        {
            workflow.TaskSettings.WorkflowId = id;
        }

        TroubleshootingHelper.Log(logCategory, "EXECUTE_WORKFLOW", $"Calling ExecuteJob, TaskSettings={workflow.TaskSettings != null}");
        await ExecuteJob(workflow.Job, workflow.TaskSettings, id);
    }

    public static async Task ExecuteJob(HotkeyType job, TaskSettings? taskSettings = null, string? workflowId = null)
    {
        // Create a user-friendly category combining job type and workflow ID for better identification
        var logCategory = !string.IsNullOrEmpty(workflowId)
            ? $"{job}_{workflowId}"
            : job.ToString();

        TroubleshootingHelper.Log(logCategory, "EXECUTE_JOB", $"Entry: taskSettings={taskSettings != null}, workflowId={workflowId ?? "null"}");
        DebugHelper.WriteLine($"Executing job: {job}");

        if (!PlatformServices.IsInitialized)
        {
            DebugHelper.WriteLine("Platform services not initialized.");
            TroubleshootingHelper.Log(logCategory, "EXECUTE_JOB", "ABORT: Platform services not initialized");
            return;
        }

        // Create default settings if none provided
        if (taskSettings == null)
        {
            taskSettings = new TaskSettings();

            // Apply job-specific defaults if needed
            if (taskSettings.Job == HotkeyType.None)
            {
                taskSettings.Job = job;
            }
        }

        // Store workflow ID in task settings
        if (!string.IsNullOrEmpty(workflowId))
        {
            taskSettings.WorkflowId = workflowId;
        }

        if (taskSettings.Job != job && job != HotkeyType.None)
        {
            taskSettings.Job = job;
        }

        DebugHelper.WriteLine(
            $"Task settings: AfterCaptureJob={taskSettings.AfterCaptureJob}, " +
            $"UploadImageToHost={taskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.UploadImageToHost)}, " +
            $"ImageDestination={taskSettings.ImageDestination}");

        try
        {
            // Start the task via TaskManager
            // This ensures it appears in the UI and follows the standard lifecycle
            TroubleshootingHelper.Log(logCategory, "EXECUTE_JOB", "Calling TaskManager.StartTask");
            await TaskManager.Instance.StartTask(taskSettings);
            TroubleshootingHelper.Log(logCategory, "EXECUTE_JOB", "TaskManager.StartTask completed");
        }
        catch (Exception ex)
        {
            TroubleshootingHelper.Log(logCategory, "EXECUTE_JOB", $"ERROR: {ex.Message}");
            DebugHelper.WriteException(ex, $"Error starting job {job}");
        }
    }
}
