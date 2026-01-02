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

using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShareX.Ava.Core;
using ShareX.Ava.Common;
using ShareX.Ava.Core.Tasks;
using ShareX.Ava.Common.Helpers;
using ShareX.Ava.Platform.Abstractions;
using ShareX.Ava.Uploaders;

namespace ShareX.Ava.Core.Tasks.Processors
{
    public class CaptureJobProcessor : IJobProcessor
    {
        /// <summary>
        /// Executes after-capture tasks for the current job.
        /// </summary>
        public async Task ProcessAsync(TaskInfo info, CancellationToken token)
        {
            if (info.Metadata?.Image == null) return;

            var settings = info.TaskSettings;

            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.SaveImageToFile))
            {
                await SaveImageToFileAsync(info);
            }

            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.CopyImageToClipboard))
            {
                 if (PlatformServices.IsInitialized && info.Metadata?.Image != null)
                 {
                     PlatformServices.Clipboard.SetImage(info.Metadata.Image);
                     DebugHelper.WriteLine("Image copied to clipboard.");
                 }
            }

            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.AnnotateImage))
            {
                 if (info.Metadata.Image != null)
                 {
                     // Open in Editor using UI Service
                     // This decouples Core from UI dependencies
                     await PlatformServices.UI.ShowEditorAsync(info.Metadata.Image);
                 }
            }

            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.UploadImageToHost))
            {
                await UploadImageAsync(info);
            }
            
            // TODO: Add other tasks
            
            await Task.CompletedTask;
        }

        private async Task SaveImageToFileAsync(TaskInfo info)
        {
             if (info.Metadata?.Image == null) return;
             
             Bitmap bmp = info.Metadata.Image;

             // TaskHelpers contains the logic for folder resolution, naming, and file exists handling.
             // It runs synchronously (System.Drawing limitation), so wrap in Task.Run if needed, 
             // though here we are already on background thread from WorkerTask.
             
             string? filePath = TaskHelpers.SaveImageAsFile(bmp, info.TaskSettings);
             
             if (!string.IsNullOrEmpty(filePath))
             {
                 info.FilePath = filePath;
                 DebugHelper.WriteLine($"Image saved: {filePath}");
             }
             else
             {
                 DebugHelper.WriteLine("Failed to save image.");
                 // info.Status = TaskStatus.Failed; // Logic to handle failure
             }

             await Task.CompletedTask;
        }

        private async Task UploadImageAsync(TaskInfo info)
        {
            if (string.IsNullOrEmpty(info.FilePath) && info.Metadata?.Image != null)
            {
                info.FilePath = TaskHelpers.SaveImageAsFile(info.Metadata.Image, info.TaskSettings);
            }

            if (string.IsNullOrEmpty(info.FilePath))
            {
                DebugHelper.WriteLine("Upload failed: No file to upload.");
                return;
            }

            DebugHelper.WriteLine($"Uploading image: {info.FilePath}");

            try
            {
                var destination = info.TaskSettings.ImageDestination;
                if (!UploaderFactory.ImageUploaderServices.TryGetValue(destination, out var uploaderService))
                {
                    DebugHelper.WriteLine($"No uploader found for destination: {destination}");
                    return;
                }

                var helper = new TaskReferenceHelper
                {
                    DataType = EDataType.Image,
                    StopRequested = false,
                    OverrideFTP = info.TaskSettings.OverrideFTP,
                    FTPIndex = info.TaskSettings.FTPIndex,
                    OverrideCustomUploader = info.TaskSettings.OverrideCustomUploader,
                    CustomUploaderIndex = info.TaskSettings.CustomUploaderIndex
                };

                var uploader = uploaderService.CreateUploader(SettingManager.UploadersConfig, helper);

                UploadResult? result = uploader switch
                {
                    FileUploader fileUploader => fileUploader.UploadFile(info.FilePath),
                    GenericUploader genericUploader => UploadWithGenericUploader(genericUploader, info.FilePath),
                    _ => null
                };

                if (result != null && !result.IsError && !string.IsNullOrEmpty(result.URL))
                {
                    info.Metadata!.UploadURL = result.URL;
                    DebugHelper.WriteLine($"Upload successful: {result.URL}");
                    DebugHelper.WriteLine("Upload complete.");
                }
                else
                {
                    string? errorText = result?.Errors?.Errors?.FirstOrDefault()?.Text ?? result?.Errors?.ToString();
                    DebugHelper.WriteLine($"Upload failed: {errorText ?? "Unknown upload error."}");
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Upload error");
            }

            await Task.CompletedTask;
        }

        private static UploadResult? UploadWithGenericUploader(GenericUploader uploader, string filePath)
        {
            using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return uploader.Upload(stream, Path.GetFileName(filePath));
        }
    }
}
