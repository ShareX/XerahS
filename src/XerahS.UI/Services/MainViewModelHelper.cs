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
using ShareX.ImageEditor.ViewModels;
using XerahS.Common;
using XerahS.Core;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.UI.Services;

/// <summary>
/// Helper class for wiring up MainViewModel events to host application infrastructure.
/// </summary>
public static class MainViewModelHelper
{
    /// <summary>
    /// Wires up the UploadRequested event to the XerahS upload pipeline.
    /// </summary>
    public static void WireUploadRequested(MainViewModel viewModel)
    {
        viewModel.UploadRequested += async (bitmap) =>
        {
            DebugHelper.WriteLine("MainViewModelHelper: UploadRequested received");
            try
            {
                // Convert Avalonia Bitmap to SKBitmap for upload pipeline
                using var skBitmap = ShareX.ImageEditor.Helpers.BitmapConversionHelpers.ToSKBitmap(bitmap);
                DebugHelper.WriteLine($"MainViewModelHelper: Converted bitmap {skBitmap.Width}x{skBitmap.Height}");

                // Get the default image uploader instance, or first available
                string? destinationInstanceId = null;
                var defaultInstance = InstanceManager.Instance.GetDefaultInstance(UploaderCategory.Image);
                if (defaultInstance != null)
                {
                    destinationInstanceId = defaultInstance.InstanceId;
                    DebugHelper.WriteLine($"MainViewModelHelper: Using default instance: {defaultInstance.DisplayName} ({destinationInstanceId})");
                }
                else
                {
                    // Fall back to first available image uploader
                    var imageInstances = InstanceManager.Instance.GetInstancesByCategory(UploaderCategory.Image);
                    var firstInstance = imageInstances.FirstOrDefault();
                    if (firstInstance != null)
                    {
                        destinationInstanceId = firstInstance.InstanceId;
                        DebugHelper.WriteLine($"MainViewModelHelper: Using first instance: {firstInstance.DisplayName} ({destinationInstanceId})");
                    }
                    else
                    {
                        DebugHelper.WriteLine("MainViewModelHelper: No image uploader instances available!");
                    }
                }

                // Create task settings with upload enabled and CopyURLToClipboard
                var taskSettings = new TaskSettings
                {
                    Job = WorkflowType.None,
                    AfterCaptureJob = AfterCaptureTasks.UploadImageToHost,
                    AfterUploadJob = AfterUploadTasks.CopyURLToClipboard,
                    DestinationInstanceId = destinationInstanceId
                };

                DebugHelper.WriteLine($"MainViewModelHelper: TaskSettings created - Job={taskSettings.Job}, AfterCapture={taskSettings.AfterCaptureJob}, AfterUpload={taskSettings.AfterUploadJob}, DestId={taskSettings.DestinationInstanceId}");

                // Start upload task via TaskManager
                DebugHelper.WriteLine("MainViewModelHelper: Calling TaskManager.StartTask...");
                await Core.Managers.TaskManager.Instance.StartTask(taskSettings, skBitmap.Copy());
                DebugHelper.WriteLine("MainViewModelHelper: TaskManager.StartTask completed");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Editor upload failed: {ex.Message}");
                DebugHelper.WriteException(ex);
            }
        };
    }
}

