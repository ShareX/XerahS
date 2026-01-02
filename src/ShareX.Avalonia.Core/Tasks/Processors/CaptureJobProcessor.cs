using System;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using ShareX.Ava.Core;
using ShareX.Ava.Common;
using ShareX.Ava.Core.Tasks;
using ShareX.Ava.Common.Helpers;
using ShareX.Ava.Platform.Abstractions;
using ShareX.Ava.History;

namespace ShareX.Ava.Core.Tasks.Processors
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
                 
                // Add to History
                try
                {
                    DebugHelper.WriteLine("Trace: History pipeline - Starting history item creation.");
                    
                    // Use centralized settings folder
                    var historyPath = Path.Combine(SettingManager.SettingsFolder, ShareXResources.HistoryFileName);

                    DebugHelper.WriteLine($"Trace: History pipeline - History file path: {historyPath}");
                    
                    var historyManager = new HistoryManagerXML(historyPath);
                    var historyItem = new HistoryItem
                    {
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath),
                        DateTime = DateTime.Now,
                        Type = "Image"
                    };
                    
                    historyManager.AppendHistoryItem(historyItem);
                    DebugHelper.WriteLine($"Trace: History pipeline - AppendHistoryItem called for: {historyItem.FileName}");
                    DebugHelper.WriteLine($"Added to history: {historyItem.FileName}");
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteLine($"Failed to add to history: {ex.Message}");
                    DebugHelper.WriteException(ex);
                }
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
