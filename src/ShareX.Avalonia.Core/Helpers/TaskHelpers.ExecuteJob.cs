#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
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

using ShareX.Avalonia.Common;
using ShareX.Avalonia.Core;
using ShareX.Avalonia.Core.Hotkeys;
using System;
using System.Threading.Tasks;

using ShareX.Avalonia.Platform.Abstractions;
using ShareX.Avalonia.Core.Tasks;
using System.Drawing;

namespace ShareX.Avalonia.Core.Helpers;

public static partial class TaskHelpers
{
    public static async Task ExecuteJob(HotkeyType job, TaskSettings? taskSettings = null)
    {
        DebugHelper.WriteLine($"Executing job: {job}");

        Image? capturedImage = null;

        try 
        {
            if (!PlatformServices.IsInitialized)
            {
                DebugHelper.WriteLine("Platform services not initialized.");
                return;
            }

            switch (job)
            {
                case HotkeyType.RectangleRegion:
                    DebugHelper.WriteLine("Execute: Rectangle Region Capture");
                    capturedImage = await PlatformServices.ScreenCapture.CaptureRegionAsync();
                    break;

                case HotkeyType.PrintScreen:
                    DebugHelper.WriteLine("Execute: Fullscreen Capture");
                    capturedImage = await PlatformServices.ScreenCapture.CaptureFullScreenAsync();
                    break;

                case HotkeyType.ActiveWindow:
                    DebugHelper.WriteLine("Execute: Active Window Capture");
                    capturedImage = await PlatformServices.ScreenCapture.CaptureActiveWindowAsync(PlatformServices.Window);
                    break;

                case HotkeyType.ClipboardUpload:
                    DebugHelper.WriteLine("Execute: Clipboard Upload");
                    // await UploadManager.ClipboardUpload(taskSettings);
                    break;
                    
                default:
                    DebugHelper.WriteLine($"Job type {job} not implemented yet.");
                    break;
            }

            if (capturedImage != null)
            {
                DebugHelper.WriteLine("Capture successful. Starting workflow...");
                // Create a basic workflow task for now (Capture -> Upload -> Clipboard)
                // In the future this should respect TaskSettings.AfterCaptureJob
                var workflow = WorkflowTask.CreateImageUploadTask(capturedImage, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                await workflow.ExecuteAsync();
                
                if (workflow.Result?.IsSuccess == true)
                {
                    DebugHelper.WriteLine($"Job completed successfully. URL: {workflow.Result.URL}");
                }
                else
                {
                    DebugHelper.WriteLine($"Job failed. Errors: {string.Join(", ", workflow.Result?.Errors ?? new())}");
                }
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"Error executing job {job}");
        }
    }
}
