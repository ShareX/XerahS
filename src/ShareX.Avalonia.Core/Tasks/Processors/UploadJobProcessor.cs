using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ShareX.Avalonia.Core;
using ShareX.Avalonia.Common;
using ShareX.Avalonia.Core.Tasks;
using ShareX.Avalonia.Uploaders;
using ShareX.Avalonia.Common.Helpers;
using ShareX.Avalonia.Platform.Abstractions;

namespace ShareX.Avalonia.Core.Tasks.Processors
{
    public class UploadJobProcessor : IJobProcessor
    {
        public async Task ProcessAsync(TaskInfo info, CancellationToken token)
        {
            if (!info.IsUploadJob) return;
            if (token.IsCancellationRequested) return;

            // TODO: Handle URL Shortening, URL Sharing logic separate? Or combined?
            // For now, focus on File/Image/Text upload.

            UploadResult? result = null;

            token.ThrowIfCancellationRequested();

            DebugHelper.WriteLine($"Starting upload for {info.FileName}...");
            
            // Wrap legacy synchronous upload in Task.Run
            result = await Task.Run(() => Upload(info), token);

            if (result != null)
            {
                info.Result = result;
                
                    if (result.IsSuccess)
                    {
                        DebugHelper.WriteLine($"Upload successful: {result.URL}");
                        
                        // Handle URL Shortening if requested
                        if (info.TaskSettings.AfterUploadJob.HasFlag(AfterUploadTasks.UseURLShortener))
                        {
                            // TODO: Implement URL Shortening logic using UploaderFactory
                            // string shortUrl = ShortenURL(result.URL);
                            // if (!string.IsNullOrEmpty(shortUrl)) result.URL = shortUrl;
                        }

                        // Handle Clipboard Copy
                        if (info.TaskSettings.AfterUploadJob.HasFlag(AfterUploadTasks.CopyURLToClipboard))
                        {
                            if (PlatformServices.IsInitialized)
                            {
                                await PlatformServices.Clipboard.SetTextAsync(result.URL);
                            }
                        }
                    }
                    else
                    {
                        DebugHelper.WriteLine($"Upload failed: {result.Response}");
                    }
            }
        }

        private UploadResult? Upload(TaskInfo info)
        {
            try
            {
                // 1. Determine Data Type and Destination
                if (info.DataType == EDataType.Image && info.Metadata?.Image != null)
                {
                    return UploadImage(info);
                }
                else if (info.DataType == EDataType.Text)
                {
                     // Return UploadText(info);
                     return null; // TODO implement text
                }
                else if (info.DataType == EDataType.File)
                {
                     // Return UploadFile(info);
                     return null; // TODO implement file
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "UploadJobProcessor");
                return new UploadResult { IsSuccess = false, Response = ex.Message };
            }

            return null;
        }

        private UploadResult? UploadImage(TaskInfo info)
        {
            var destination = info.TaskSettings.ImageDestination;
            
            if (UploaderFactory.ImageUploaderServices.TryGetValue(destination, out var service))
            {
                 // Create TaskReferenceHelper
                 var helper = new TaskReferenceHelper() 
                 {
                     DataType = EDataType.Image,
                     StopRequested = false, // TODO: Bind to cancellation token?
                     OverrideFTP = info.TaskSettings.OverrideFTP,
                     FTPIndex = info.TaskSettings.FTPIndex,
                     OverrideCustomUploader = info.TaskSettings.OverrideCustomUploader,
                     CustomUploaderIndex = info.TaskSettings.CustomUploaderIndex
                 };

                 var uploader = service.CreateUploader(SettingManager.UploadersConfig, helper); 
                 
                 if (uploader is GenericUploader genericUploader)
                 {
                     // Get image stream with correct format/quality settings
                     using (MemoryStream? ms = TaskHelpers.SaveImageAsStream(info.Metadata.Image, info.TaskSettings.ImageSettings.ImageFormat, info.TaskSettings))
                     {
                         if (ms != null)
                         {
                             ms.Position = 0;
                             return genericUploader.Upload(ms, info.FileName);
                         }
                     }
                 }
            }
            
            return new UploadResult { IsSuccess = false, Response = "Uploader service not found or initialization failed." };
        }
    }
}
