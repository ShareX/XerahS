using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ShareX.Ava.Core;
using ShareX.Ava.Common;
using ShareX.Ava.Core.Tasks;
using ShareX.Ava.Uploaders;
using ShareX.Ava.Common.Helpers;
using ShareX.Ava.Platform.Abstractions;
using ShareX.Ava.Uploaders.PluginSystem;

namespace ShareX.Ava.Core.Tasks.Processors
{
    public class UploadJobProcessor : IJobProcessor
    {
        public async Task ProcessAsync(TaskInfo info, CancellationToken token)
        {
            if (!info.IsUploadJob) return;
            if (token.IsCancellationRequested) return;

            if (info.Result != null && !info.Result.IsError && !string.IsNullOrEmpty(info.Result.URL))
            {
                DebugHelper.WriteLine("Upload already completed during capture; running after-upload tasks.");
                await HandleAfterUploadTasksAsync(info, info.Result, token);
                return;
            }

            // TODO: Handle URL Shortening, URL Sharing logic separate? Or combined?
            // For now, focus on File/Image/Text upload.

            UploadResult? result = null;

            token.ThrowIfCancellationRequested();

            DebugHelper.WriteLine($"Starting upload for {info.FileName}...");
            DebugHelper.WriteLine($"Upload data type: {info.DataType}, FilePath: {info.FilePath}");
            DebugHelper.WriteLine($"Image destination: {info.TaskSettings.ImageDestination}");
            
            // Wrap legacy synchronous upload in Task.Run
            result = await Task.Run(() => Upload(info), token);

            if (result != null)
            {
                info.Result = result;
                
                if (result.IsSuccess)
                {
                    DebugHelper.WriteLine($"Upload successful: {result.URL}");
                    await HandleAfterUploadTasksAsync(info, result, token);
                }
                else
                {
                    DebugHelper.WriteLine($"Upload failed: {result.Response}");
                }
            }
            else
            {
                DebugHelper.WriteLine("Upload result was null.");
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

            DebugHelper.WriteLine($"No legacy uploader service found for destination: {destination}");
            return UploadImageWithPluginSystem(info) ??
                new UploadResult { IsSuccess = false, Response = "Uploader service not found or initialization failed." };
            
        }

        private UploadResult? UploadImageWithPluginSystem(TaskInfo info)
        {
            EnsurePluginsLoaded();

            var instanceManager = InstanceManager.Instance;
            var defaultInstance = instanceManager.GetDefaultInstance(UploaderCategory.Image);
            if (defaultInstance == null)
            {
                DebugHelper.WriteLine("No default image uploader instance configured (plugin system).");
                return null;
            }

            DebugHelper.WriteLine($"Plugin instance selected: {defaultInstance.DisplayName} ({defaultInstance.ProviderId})");
            var provider = ProviderCatalog.GetProvider(defaultInstance.ProviderId);
            if (provider == null)
            {
                DebugHelper.WriteLine($"Provider not found in catalog: {defaultInstance.ProviderId}");
                return null;
            }

            DebugHelper.WriteLine($"Provider loaded: {provider.Name} ({provider.ProviderId})");

            Uploader uploader;
            try
            {
                uploader = provider.CreateInstance(defaultInstance.SettingsJson);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to create uploader instance");
                return null;
            }

            if (!string.IsNullOrEmpty(info.FilePath))
            {
                return uploader switch
                {
                    FileUploader fileUploader => fileUploader.UploadFile(info.FilePath),
                    GenericUploader genericUploader => UploadWithGenericUploader(genericUploader, info.FilePath),
                    _ => null
                };
            }

            if (info.Metadata?.Image == null)
            {
                return null;
            }

            using (MemoryStream? ms = TaskHelpers.SaveImageAsStream(info.Metadata.Image, info.TaskSettings.ImageSettings.ImageFormat, info.TaskSettings))
            {
                if (ms == null) return null;
                ms.Position = 0;
                return uploader is GenericUploader genericUploader
                    ? genericUploader.Upload(ms, info.FileName)
                    : null;
            }
        }

        private static UploadResult UploadWithGenericUploader(GenericUploader uploader, string filePath)
        {
            using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return uploader.Upload(stream, Path.GetFileName(filePath));
        }

        private static void EnsurePluginsLoaded()
        {
            if (ProviderCatalog.ArePluginsLoaded())
            {
                return;
            }

            try
            {
                ProviderCatalog.InitializeBuiltInProviders();
                string pluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                DebugHelper.WriteLine($"Loading plugins from: {pluginsPath}");
                ProviderCatalog.LoadPlugins(pluginsPath);
                DebugHelper.WriteLine($"Plugin providers available: {ProviderCatalog.GetAllProviders().Count}");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to load plugins");
            }
        }

        private async Task HandleAfterUploadTasksAsync(TaskInfo info, UploadResult result, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            // Handle URL Shortening if requested
            if (info.TaskSettings.AfterUploadJob.HasFlag(AfterUploadTasks.UseURLShortener))
            {
                DebugHelper.WriteLine("AfterUpload: URL shortener requested (not implemented).");
                // TODO: Implement URL Shortening logic using UploaderFactory
            }

            // Handle Clipboard Copy
            if (info.TaskSettings.AfterUploadJob.HasFlag(AfterUploadTasks.CopyURLToClipboard))
            {
                if (PlatformServices.IsInitialized && !string.IsNullOrEmpty(result.URL))
                {
                    await PlatformServices.Clipboard.SetTextAsync(result.URL);
                    DebugHelper.WriteLine($"Copied URL to clipboard: {result.URL}");
                }
                else
                {
                    DebugHelper.WriteLine("CopyURLToClipboard skipped: clipboard not available or URL empty.");
                }
            }
        }
    }
}
