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

using XerahS.Common;
using XerahS.Core.Managers;

using XerahS.Platform.Abstractions;

namespace XerahS.Core.Helpers;

public static partial class TaskHelpers
{
    /// <summary>
    /// Execute a workflow using its complete settings.
    /// This is the preferred method - avoids ambiguity with WorkflowType lookup.
    /// </summary>
    /// <param name="workflow">The workflow to execute</param>
    /// <param name="workflowId">Optional workflow ID for troubleshooting (uses workflow.Id if not provided)</param>
    /// <param name="hideMainWindow">If true, minimizes main window before capture (for navbar clicks, not hotkeys)</param>
    public static async Task ExecuteWorkflow(Core.Hotkeys.WorkflowSettings workflow, string? workflowId = null, bool hideMainWindow = false)
    {
        // Use provided ID or get from workflow
        var id = workflowId ?? workflow?.Id ?? "Unknown";

        // Use job type as category (for folder/file naming), log workflow ID in content
        var logCategory = workflow?.Job.ToString() ?? "Unknown";

        TroubleshootingHelper.Log(logCategory, "EXECUTE_WORKFLOW", $"Entry: workflowId={id}, workflow={workflow?.Name ?? "null"}, Job={workflow?.Job.ToString() ?? "null"}");

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
        await ExecuteJob(workflow.Job, workflow.TaskSettings, id, hideMainWindow);
    }

    /// <summary>
    /// Execute a job with optional window hiding for UI-triggered captures.
    /// </summary>
    /// <param name="job">The workflow type to execute</param>
    /// <param name="taskSettings">Optional task settings</param>
    /// <param name="workflowId">Optional workflow ID for troubleshooting</param>
    /// <param name="hideMainWindow">If true, minimizes main window before capture (for navbar clicks, not hotkeys)</param>
    public static async Task ExecuteJob(WorkflowType job, TaskSettings? taskSettings = null, string? workflowId = null, bool hideMainWindow = false)
    {
        // Use job type as category (for folder/file naming), log workflow ID in content
        var logCategory = job.ToString();

        TroubleshootingHelper.Log(logCategory, "EXECUTE_JOB", $"Entry: workflowId={workflowId ?? "null"}, taskSettings={taskSettings != null}");
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
            if (taskSettings.Job == WorkflowType.None)
            {
                taskSettings.Job = job;
            }
        }

        // Store workflow ID in task settings
        if (!string.IsNullOrEmpty(workflowId))
        {
            taskSettings.WorkflowId = workflowId;
        }

        if (taskSettings.Job != job && job != WorkflowType.None)
        {
            taskSettings.Job = job;
        }

        DebugHelper.WriteLine(
            $"Task settings: AfterCaptureJob={taskSettings.AfterCaptureJob}, " +
            $"UploadImageToHost={taskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.UploadImageToHost)}");

        // Only hide window for UI-triggered captures (navbar clicks), not hotkeys
        // This allows users to capture the app itself when using hotkeys
        bool shouldHideWindow = hideMainWindow && IsCaptureWorkflow(job);

        try
        {
            // Hide main window before capture to avoid capturing the app itself
            if (shouldHideWindow)
            {
                try
                {
                    await PlatformServices.UI.HideMainWindowAsync();
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "Failed to hide main window before capture");
                }
            }

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
        finally
        {
            // Restore main window after capture completes
            if (shouldHideWindow)
            {
                try
                {
                    await PlatformServices.UI.RestoreMainWindowAsync();
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "Failed to restore main window after capture");
                }
            }
        }
    }

    /// <summary>
    /// Determines if the workflow type is a capture operation.
    /// </summary>
    private static bool IsCaptureWorkflow(WorkflowType job)
    {
        return job switch
        {
            WorkflowType.PrintScreen => true,
            WorkflowType.ActiveWindow => true,
            WorkflowType.RectangleRegion => true,
            WorkflowType.RectangleTransparent => true,
            WorkflowType.CustomWindow => true,
            WorkflowType.ScreenRecorder => true,
            WorkflowType.ScreenRecorderActiveWindow => true,
            WorkflowType.ScreenRecorderCustomRegion => true,
            WorkflowType.ScreenRecorderGIF => true,
            WorkflowType.ScreenRecorderGIFActiveWindow => true,
            WorkflowType.ScreenRecorderGIFCustomRegion => true,
            WorkflowType.StartScreenRecorder => true,
            WorkflowType.StartScreenRecorderGIF => true,
            WorkflowType.ScrollingCapture => true,
            WorkflowType.OCR => true,
            WorkflowType.ActiveMonitor => true,
            WorkflowType.CustomRegion => true,
            WorkflowType.LastRegion => true,
            _ => false
        };
    }
}
