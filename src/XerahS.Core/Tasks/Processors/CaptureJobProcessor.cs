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
                    var originalAfterCapture = settings.AfterCaptureJob;
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

                    // Persist "Show after capture window" setting if user unchecked it
                    if (originalAfterCapture.HasFlag(AfterCaptureTasks.ShowAfterCaptureWindow) &&
                        !result.Capture.HasFlag(AfterCaptureTasks.ShowAfterCaptureWindow))
                    {
                        PersistShowAfterCaptureWindowSetting(settings.WorkflowId, false);
                    }
                }
            }

            // Annotation should happen BEFORE save, so the saved file includes annotations
            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.AddImageEffects))
            {
                if (info.Metadata?.Image != null)
                {
                    var processed = TaskHelpers.ApplyImageEffects(info.Metadata.Image, settings.ImageSettings);
                    if (processed == null)
                    {
                        DebugHelper.WriteLine("Error: Applying image effects resulted in null image.");
                        return;
                    }

                    if (!ReferenceEquals(processed, info.Metadata.Image))
                    {
                        info.Metadata.Image.Dispose();
                    }

                    info.Metadata.Image = processed;
                }
            }

            // Annotation should happen BEFORE save, so the saved file includes annotations
            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.AnnotateImage))
            {
                if (info.Metadata?.Image != null && PlatformServices.UI != null)
                {
                    var editedImage = await PlatformServices.UI.ShowEditorAsync(info.Metadata.Image);
                    if (editedImage != null)
                    {
                        if (info.Metadata.Image != editedImage)
                        {
                            info.Metadata.Image.Dispose();
                        }
                        info.Metadata.Image = editedImage;
                    }
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

            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.UploadImageToHost))
            {
                await UploadImageAsync(info);
            }
            else
            {
                DebugHelper.WriteLine("UploadImageToHost flag not set; skipping upload.");
            }

            // TODO: Add other tasks

            // TODO: Add other tasks

            // Add to History (after all tasks, including upload, are complete)
            if (!string.IsNullOrEmpty(info.FilePath))
            {
                try
                {
                    DebugHelper.WriteLine("Trace: History pipeline - Starting history item creation.");

                    // Use centralized history file path
                    var historyPath = SettingsManager.GetHistoryFilePath();

                    DebugHelper.WriteLine($"Trace: History pipeline - History file path: {historyPath}");

                    using var historyManager = new HistoryManagerSQLite(historyPath);
                    var historyItem = new HistoryItem
                    {
                        FilePath = info.FilePath,
                        FileName = Path.GetFileName(info.FilePath),
                        DateTime = DateTime.Now,
                        Type = "Image",
                        URL = info.Metadata?.UploadURL ?? string.Empty
                    };

                    historyManager.AppendHistoryItem(historyItem);
                    DebugHelper.WriteLine($"Trace: History pipeline - AppendHistoryItem called for: {historyItem.FileName} (URL: {historyItem.URL})");
                    DebugHelper.WriteLine($"Added to history: {historyItem.FileName}");
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteLine($"Failed to add to history: {ex.Message}");
                    DebugHelper.WriteException(ex);
                }
            }

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
                info.FilePath = TaskHelpers.SaveImageAsFile(info.Metadata.Image, info.TaskSettings) ?? string.Empty;
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

            // Check if Auto destination is selected
            if (targetInstance != null && InstanceManager.IsAutoProvider(targetInstance.ProviderId))
            {
                return TryUploadWithFallback(instanceManager, UploaderCategory.Image, info.FilePath, configuredInstanceId);
            }

            // Not Auto - use the configured instance directly
            targetInstance ??= instanceManager.GetDefaultInstance(UploaderCategory.Image);
            
            if (targetInstance != null && InstanceManager.IsAutoProvider(targetInstance.ProviderId))
            {
                return TryUploadWithFallback(instanceManager, UploaderCategory.Image, info.FilePath, null);
            }

            if (targetInstance == null)
            {
                DebugHelper.WriteLine("No image uploader instance configured.");
                return null;
            }

            return TryUploadWithInstance(targetInstance, info.FilePath);
        }

        /// <summary>
        /// Tries to upload using multiple instances with fallback logic.
        /// When one instance fails, it tries the next available instance.
        /// Falls back to File category uploaders if the primary category fails.
        /// </summary>
        private static UploadResult? TryUploadWithFallback(InstanceManager instanceManager, UploaderCategory category, string filePath, string? excludeInstanceId, HashSet<string>? attemptedInstanceIds = null)
        {
            attemptedInstanceIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            DebugHelper.WriteLine($"Auto destination selected; trying uploaders with fallback for category {category}.");

            // Get all available instances for this category that haven't been attempted yet
            var allInstances = GetPrioritizedInstances(instanceManager, category, excludeInstanceId)
                .Where(i => !attemptedInstanceIds.Contains(i.InstanceId))
                .ToList();

            if (allInstances.Count == 0)
            {
                DebugHelper.WriteLine($"No available uploaders for category {category} (excluding already attempted).");
            }
            else
            {
                DebugHelper.WriteLine($"Found {allInstances.Count} potential uploaders to try in category {category}.");

                List<string> failedInstances = new();

                foreach (var instance in allInstances)
                {
                    // Mark as attempted to avoid retrying in fallback categories
                    attemptedInstanceIds.Add(instance.InstanceId);
                    
                    DebugHelper.WriteLine($"Trying uploader: {instance.DisplayName} ({instance.ProviderId})");

                    var result = TryUploadWithInstance(instance, filePath);

                    if (result != null && !result.IsError && !string.IsNullOrEmpty(result.URL))
                    {
                        DebugHelper.WriteLine($"Upload successful with {instance.DisplayName}.");
                        return result;
                    }

                    // Track failed instance
                    failedInstances.Add($"{instance.DisplayName} ({instance.ProviderId})");
                    DebugHelper.WriteLine($"Uploader {instance.DisplayName} failed, trying next...");
                }

                DebugHelper.WriteLine($"All uploaders in category {category} failed. Tried: {string.Join(", ", failedInstances)}");
            }

            // If primary category failed (or had no uploaders), try File category as fallback
            if (category != UploaderCategory.File)
            {
                DebugHelper.WriteLine($"Trying File category uploaders as fallback...");
                var fileFallbackResult = TryUploadWithFallback(instanceManager, UploaderCategory.File, filePath, excludeInstanceId, attemptedInstanceIds);
                if (fileFallbackResult != null)
                {
                    return fileFallbackResult;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets all available instances for a category, prioritized by:
        /// 1. Default instance first
        /// 2. Other instances sorted by creation time (newest first)
        /// </summary>
        private static List<UploaderInstance> GetPrioritizedInstances(InstanceManager instanceManager, UploaderCategory category, string? excludeInstanceId)
        {
            var allInstances = instanceManager.GetInstancesByCategory(category)
                .Where(i => !InstanceManager.IsAutoProvider(i.ProviderId))
                .Where(i => excludeInstanceId == null || !string.Equals(i.InstanceId, excludeInstanceId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var defaultInstance = instanceManager.GetDefaultInstance(category);

            // Sort: default first, then by creation time (newest first)
            var ordered = allInstances
                .OrderByDescending(i => defaultInstance != null && i.InstanceId == defaultInstance.InstanceId)
                .ThenByDescending(i => i.CreatedAt)
                .ToList();

            return ordered;
        }

        /// <summary>
        /// Attempts to upload using a specific instance. Returns null if creation or upload fails.
        /// </summary>
        private static UploadResult? TryUploadWithInstance(UploaderInstance instance, string filePath)
        {
            var provider = ProviderCatalog.GetProvider(instance.ProviderId);
            if (provider == null)
            {
                DebugHelper.WriteLine($"Provider not found in catalog: {instance.ProviderId}");
                return null;
            }

            Uploader uploader;
            try
            {
                uploader = provider.CreateInstance(instance.SettingsJson);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, $"Failed to create uploader instance for {instance.DisplayName}");
                return null;
            }

            try
            {
                return uploader switch
                {
                    FileUploader fileUploader => fileUploader.UploadFile(filePath),
                    GenericUploader genericUploader => UploadWithGenericUploader(genericUploader, filePath),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, $"Upload failed for {instance.DisplayName}");
                return null;
            }
        }

        /// <summary>
        /// Persists the "Show after capture window" setting change back to the workflow configuration.
        /// Uses async save to avoid blocking the capture thread.
        /// </summary>
        private static void PersistShowAfterCaptureWindowSetting(string? workflowId, bool showWindow)
        {
            try
            {
                // Find the workflow by ID
                var workflow = !string.IsNullOrEmpty(workflowId) ? SettingsManager.GetWorkflowById(workflowId) : null;
                if (workflow?.TaskSettings == null)
                {
                    // Fall back to default task settings if no workflow found
                    if (SettingsManager.DefaultTaskSettings != null)
                    {
                        if (showWindow)
                        {
                            SettingsManager.DefaultTaskSettings.AfterCaptureJob |= AfterCaptureTasks.ShowAfterCaptureWindow;
                        }
                        else
                        {
                            SettingsManager.DefaultTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.ShowAfterCaptureWindow;
                        }
                        // Use async save to avoid UI thread issues
                        SettingsManager.SaveWorkflowsConfigAsync();
                        DebugHelper.WriteLine($"Updated DefaultTaskSettings.AfterCaptureJob (ShowAfterCaptureWindow={showWindow})");
                    }
                    return;
                }

                // Update the workflow's task settings
                if (showWindow)
                {
                    workflow.TaskSettings.AfterCaptureJob |= AfterCaptureTasks.ShowAfterCaptureWindow;
                }
                else
                {
                    workflow.TaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.ShowAfterCaptureWindow;
                }

                // Use async save to avoid UI thread issues
                SettingsManager.SaveWorkflowsConfigAsync();
                DebugHelper.WriteLine($"Persisted ShowAfterCaptureWindow={showWindow} to workflow '{workflowId}'");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to persist ShowAfterCaptureWindow setting");
            }
        }

        private static void EnsurePluginsLoaded()
        {
            if (ProviderCatalog.ArePluginsLoaded())
            {
                return;
            }

            try
            {
                XerahS.Core.Uploaders.ProviderContextManager.EnsureProviderContext();
                ProviderCatalog.InitializeBuiltInProviders();
                string pluginsPath = PathsManager.PluginsFolder;
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
