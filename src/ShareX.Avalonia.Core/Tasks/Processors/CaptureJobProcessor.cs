using System;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using ShareX.Avalonia.Core;
using ShareX.Avalonia.Common;
using ShareX.Avalonia.Core.Tasks;
using ShareX.Avalonia.Common.Helpers;

namespace ShareX.Avalonia.Core.Tasks.Processors
{
    public class CaptureJobProcessor : IJobProcessor
    {
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
                 // TODO: Use IClipboardService
                 DebugHelper.WriteLine("CopyImageToClipboard requested");
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
    }
}
