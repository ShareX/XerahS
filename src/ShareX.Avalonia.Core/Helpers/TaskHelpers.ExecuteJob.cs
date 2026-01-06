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

using ShareX.Ava.Common;
using ShareX.Ava.Core;
using ShareX.Ava.Core.Hotkeys;
using ShareX.Ava.Core.Managers;
using System;
using System.Threading.Tasks;

using ShareX.Ava.Platform.Abstractions;
using ShareX.Ava.Core.Tasks;
using System.Drawing;

namespace ShareX.Ava.Core.Helpers;

public static partial class TaskHelpers
{
    public static async Task ExecuteJob(HotkeyType job, TaskSettings? taskSettings = null)
    {
        DebugHelper.WriteLine($"Executing job: {job}");

        if (!PlatformServices.IsInitialized)
        {
            DebugHelper.WriteLine("Platform services not initialized.");
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

        // Ensure the job type in settings matches the requested job
        if (taskSettings.Job != job && job != HotkeyType.None)
        {
            taskSettings.Job = job;
        }

        if (taskSettings.UseDefaultAfterCaptureJob && SettingManager.Settings?.DefaultTaskSettings != null)
        {
            taskSettings.AfterCaptureJob = SettingManager.Settings.DefaultTaskSettings.AfterCaptureJob;
            DebugHelper.WriteLine($"Applied default AfterCaptureJob: {taskSettings.AfterCaptureJob}");
        }

        if (taskSettings.UseDefaultAfterUploadJob && SettingManager.Settings?.DefaultTaskSettings != null)
        {
            taskSettings.AfterUploadJob = SettingManager.Settings.DefaultTaskSettings.AfterUploadJob;
            DebugHelper.WriteLine($"Applied default AfterUploadJob: {taskSettings.AfterUploadJob}");
        }

        if (taskSettings.UseDefaultDestinations && SettingManager.Settings?.DefaultTaskSettings != null)
        {
            var defaults = SettingManager.Settings.DefaultTaskSettings;
            taskSettings.ImageDestination = defaults.ImageDestination;
            taskSettings.ImageFileDestination = defaults.ImageFileDestination;
            taskSettings.TextDestination = defaults.TextDestination;
            taskSettings.TextFileDestination = defaults.TextFileDestination;
            taskSettings.FileDestination = defaults.FileDestination;
            taskSettings.URLShortenerDestination = defaults.URLShortenerDestination;
            taskSettings.URLSharingServiceDestination = defaults.URLSharingServiceDestination;
            DebugHelper.WriteLine($"Applied default destinations: Image={taskSettings.ImageDestination}, Text={taskSettings.TextDestination}, File={taskSettings.FileDestination}");
        }

        if (taskSettings.UseDefaultCaptureSettings && SettingManager.Settings?.DefaultTaskSettings != null)
        {
            var defaults = SettingManager.Settings.DefaultTaskSettings.CaptureSettings;
            // Create a new instance to avoid reference modification
            taskSettings.CaptureSettings = new TaskSettingsCapture
            {
                UseModernCapture = defaults.UseModernCapture,
                ShowCursor = defaults.ShowCursor,
                ScreenshotDelay = defaults.ScreenshotDelay,
                CaptureTransparent = defaults.CaptureTransparent,
                CaptureShadow = defaults.CaptureShadow,
                CaptureShadowOffset = defaults.CaptureShadowOffset,
                CaptureClientArea = defaults.CaptureClientArea,
                CaptureAutoHideTaskbar = defaults.CaptureAutoHideTaskbar,
                CaptureAutoHideDesktopIcons = defaults.CaptureAutoHideDesktopIcons,
                CaptureCustomRegion = defaults.CaptureCustomRegion,
                CaptureCustomWindow = defaults.CaptureCustomWindow
            };
            DebugHelper.WriteLine($"Applied default CaptureSettings: UseModernCapture={taskSettings.CaptureSettings.UseModernCapture}");
        }

        DebugHelper.WriteLine(
            $"Task settings: AfterCaptureJob={taskSettings.AfterCaptureJob}, " +
            $"UploadImageToHost={taskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.UploadImageToHost)}, " +
            $"UseDefaultAfterCaptureJob={taskSettings.UseDefaultAfterCaptureJob}, " +
            $"ImageDestination={taskSettings.ImageDestination}");

        try 
        {
            // Start the task via TaskManager
            // This ensures it appears in the UI and follows the standard lifecycle
            await TaskManager.Instance.StartTask(taskSettings);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"Error starting job {job}");
        }
    }
}
