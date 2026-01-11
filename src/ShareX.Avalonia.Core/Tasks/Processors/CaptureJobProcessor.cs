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

using XerahS.Common;
using XerahS.History;
using XerahS.Platform.Abstractions;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Core.Tasks.Processors
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
            DebugHelper.WriteLine(
                $"AfterCaptureJob={settings.AfterCaptureJob}, " +
                $"UploadImageToHost={settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.UploadImageToHost)}");

            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.ShowAfterCaptureWindow))
            {
                if (!PlatformServices.IsInitialized)
                {
                    DebugHelper.WriteLine("ShowAfterCaptureWindow requested but UI service is not initialized.");
                }
                else
                {
                    var result = await PlatformServices.UI.ShowAfterCaptureWindowAsync(
                        info.Metadata.Image,
                        settings.AfterCaptureJob,
                        settings.AfterUploadJob);
                    if (result.Cancel)
                    {
                        DebugHelper.WriteLine("After capture window cancelled; aborting workflow.");
                        return;
                    }

                    settings.AfterCaptureJob = result.Capture;
                    settings.AfterUploadJob = result.Upload;
                }
            }

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
                    await PlatformServices.UI.ShowEditorAsync(info.Metadata.Image);
                }
            }

            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.UploadImageToHost))
            {
                await UploadImageAsync(info);
            }
            else
            {
                DebugHelper.WriteLine("UploadImageToHost flag not set; skipping upload.");
            }

            // TODO: Add other tasks

            await Task.CompletedTask;
        }

        private async Task SaveImageToFileAsync(TaskInfo info)
        {
            if (info.Metadata?.Image == null) return;

            SkiaSharp.SKBitmap bmp = info.Metadata.Image;

            // TaskHelpers contains the logic for folder resolution, naming, and file exists handling.
            // It runs synchronously (SkiaSharp limitation), so wrap in Task.Run if needed, 
            // though here we are already on background thread from WorkerTask.

            string? filePath = TaskHelpers.SaveImageAsFile(bmp, info.TaskSettings);
            if (!string.IsNullOrEmpty(filePath))
            {
                var directory = Path.GetDirectoryName(filePath) ?? "";
                var fileName = Path.GetFileName(filePath);
                var extension = Path.GetExtension(filePath);
                DebugHelper.WriteLine($"[PathTrace {info.CorrelationId}] SaveImageToFile: dir=\"{directory}\", fileName=\"{fileName}\", ext=\"{extension}\", fullPath=\"{filePath}\"");
            }

            if (!string.IsNullOrEmpty(filePath))
            {
                info.FilePath = filePath;
                DebugHelper.WriteLine($"Image saved: {filePath}");

                // Add to History
                try
                {
                    DebugHelper.WriteLine("Trace: History pipeline - Starting history item creation.");

                    // Use centralized history file path
                    var historyPath = SettingManager.GetHistoryFilePath();

                    DebugHelper.WriteLine($"Trace: History pipeline - History file path: {historyPath}");

                    using var historyManager = new HistoryManagerSQLite(historyPath);
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
                var pluginResult = TryUploadWithPluginSystem(info);
                if (pluginResult == null)
                {
                    DebugHelper.WriteLine("Plugin upload did not return a result.");
                    return;
                }

                HandleUploadResult(info, pluginResult);
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

        private static void HandleUploadResult(TaskInfo info, UploadResult? result)
        {
            if (result != null && !result.IsError && !string.IsNullOrEmpty(result.URL))
            {
                info.Metadata!.UploadURL = result.URL;
                info.Result = result;
                info.DataType = EDataType.Image;
                DebugHelper.WriteLine($"Upload successful: {result.URL}");
                DebugHelper.WriteLine("Upload complete.");
                return;
            }

            string? errorText = result?.Errors?.Errors?.FirstOrDefault()?.Text ?? result?.Errors?.ToString();
            DebugHelper.WriteLine($"Upload failed: {errorText ?? "Unknown upload error."}");
        }

        private static UploadResult? TryUploadWithPluginSystem(TaskInfo info)
        {
            EnsurePluginsLoaded();

            var instanceManager = InstanceManager.Instance;
            var configuredInstanceId = info.TaskSettings.GetDestinationInstanceIdForDataType(EDataType.Image);
            UploaderInstance? targetInstance = null;

            if (!string.IsNullOrEmpty(configuredInstanceId))
            {
                targetInstance = instanceManager.GetInstance(configuredInstanceId);
                if (targetInstance == null)
                {
                    DebugHelper.WriteLine($"Configured image uploader instance not found: {configuredInstanceId}");
                }
            }

            if (targetInstance == null)
            {
                targetInstance = instanceManager.GetDefaultInstance(UploaderCategory.Image);
            }

            if (targetInstance == null)
            {
                DebugHelper.WriteLine("No image uploader instance configured.");
                return null;
            }

            DebugHelper.WriteLine($"Plugin instance selected: {targetInstance.DisplayName} ({targetInstance.ProviderId})");

            var provider = ProviderCatalog.GetProvider(targetInstance.ProviderId);
            if (provider == null)
            {
                DebugHelper.WriteLine($"Provider not found in catalog: {targetInstance.ProviderId}");
                return null;
            }

            DebugHelper.WriteLine($"Provider loaded: {provider.Name} ({provider.ProviderId})");

            Uploader uploader;
            try
            {
                uploader = provider.CreateInstance(targetInstance.SettingsJson);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to create uploader instance");
                return null;
            }

            return uploader switch
            {
                FileUploader fileUploader => fileUploader.UploadFile(info.FilePath),
                GenericUploader genericUploader => UploadWithGenericUploader(genericUploader, info.FilePath),
                _ => null
            };
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
    }
}
