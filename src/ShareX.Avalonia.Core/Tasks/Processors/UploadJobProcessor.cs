using System.IO;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Core.Tasks.Processors
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

            DebugHelper.WriteLine($"[UploadTrace {info.CorrelationId}] Starting upload; dataType={info.DataType}, filePath=\"{info.FilePath}\", fileName=\"{info.FileName}\"");
            // Wrap upload in Task.Run
            result = await Task.Run(() => Upload(info), token);

            if (result != null)
            {
                info.Result = result;

                if (result.IsSuccess)
                {
                    info.Metadata.UploadURL = result.URL;
                    DebugHelper.WriteLine($"[UploadTrace {info.CorrelationId}] Upload successful: {result.URL}");
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
                return info.DataType switch
                {
                    EDataType.Image => UploadWithPluginSystem(info, UploaderCategory.Image),
                    EDataType.Text => UploadWithPluginSystem(info, UploaderCategory.Text),
                    EDataType.File => UploadWithPluginSystem(info, UploaderCategory.File),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "UploadJobProcessor");
                return new UploadResult { IsSuccess = false, Response = ex.Message };
            }
        }

        private UploadResult? UploadWithPluginSystem(TaskInfo info, UploaderCategory category)
        {
            EnsurePluginsLoaded();

            var instanceManager = InstanceManager.Instance;
            var targetInstanceId = info.TaskSettings.GetDestinationInstanceIdForDataType(info.DataType);
            UploaderInstance? targetInstance = null;

            if (!string.IsNullOrEmpty(targetInstanceId))
            {
                targetInstance = instanceManager.GetInstance(targetInstanceId);
                if (targetInstance == null)
                {
                    DebugHelper.WriteLine($"Configured destination instance not found: {targetInstanceId}");
                }
                else if (targetInstance.Category != category)
                {
                    DebugHelper.WriteLine($"Configured destination category mismatch. Expected {category}, got {targetInstance.Category}. Continuing with configured instance.");
                }
            }

            var defaultInstance = targetInstance ?? instanceManager.GetDefaultInstance(category);
            if (defaultInstance == null)
            {
                DebugHelper.WriteLine($"No uploader instance configured (plugin system) for category {category}.");
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
