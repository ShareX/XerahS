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
using System.IO;
using System.Text;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.History;
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

                if (result.IsSuccess || (!result.IsError && !string.IsNullOrEmpty(result.URL)))
                {
                    info.Metadata.UploadURL = result.URL;
                    DebugHelper.WriteLine($"[UploadTrace {info.CorrelationId}] Upload successful: {result.URL}");
                    await HandleAfterUploadTasksAsync(info, result, token);
                }
                else
                {
                    var errorMsg = result.Response ?? "Unknown error";
                    // If URL is present but we fell here, it means IsError is true
                    if (!string.IsNullOrEmpty(result.URL))
                    {
                         DebugHelper.WriteLine($"Upload finished with errors but URL present: {result.URL}. (Error: {errorMsg})");
                         // If we have a URL, let's treat it as partial success for metadata purposes
                         info.Metadata.UploadURL = result.URL;
                    }
                    else
                    {
                        DebugHelper.WriteLine($"Upload failed: {errorMsg}");
                    
                        if (PlatformServices.IsInitialized && PlatformServices.Toast != null)
                        {
                            PlatformServices.Toast.ShowToast(new Platform.Abstractions.ToastConfig
                            {
                                Title = "Upload Failed",
                                Text = errorMsg,
                                Duration = 4f,
                                AutoHide = true
                            });
                        }
                    }
                }

                TryAppendHistoryItem(info);
            }
            else
            {
                DebugHelper.WriteLine("Upload result was null.");
                info.Result = new UploadResult
                {
                    IsSuccess = false,
                    Response = "Upload failed: uploader returned no result."
                };
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
            DebugHelper.WriteLine(
                $"[UploadContentDebug] UploadWithPluginSystem: category={category}, dataType={info.DataType}, " +
                $"taskSettingsJob={info.TaskSettings.Job}, destinationInstanceId=\"{info.TaskSettings.DestinationInstanceId ?? string.Empty}\", " +
                $"resolvedTargetInstanceId=\"{targetInstanceId ?? string.Empty}\"");

            if (category == UploaderCategory.Text)
            {
                var textInstances = instanceManager.GetInstancesByCategory(UploaderCategory.Text);
                var defaultTextInstance = instanceManager.GetDefaultInstance(UploaderCategory.Text);
                var textInstanceSummary = textInstances.Count > 0
                    ? string.Join(", ", textInstances.Select(i => $"{i.DisplayName}({i.InstanceId})"))
                    : "(none)";

                DebugHelper.WriteLine(
                    $"[UploadContentDebug] Text instances: count={textInstances.Count}, " +
                    $"default=\"{defaultTextInstance?.DisplayName ?? "(none)"}\", list={textInstanceSummary}");
            }

            UploaderInstance? targetInstance = null;

            if (!string.IsNullOrEmpty(targetInstanceId))
            {
                targetInstance = instanceManager.GetInstance(targetInstanceId);
                if (targetInstance == null)
                {
                    DebugHelper.WriteLine($"Configured destination instance not found: {targetInstanceId}");
                }
                else if (!InstanceManager.IsAutoProvider(targetInstance.ProviderId) && targetInstance.Category != category)
                {
                    DebugHelper.WriteLine($"Configured destination category mismatch. Expected {category}, got {targetInstance.Category}. Continuing with configured instance.");
                }
            }

            // Check if Auto destination is selected
            if (targetInstance != null && InstanceManager.IsAutoProvider(targetInstance.ProviderId))
            {
                return TryUploadWithFallback(instanceManager, category, info, targetInstanceId);
            }

            // Not Auto - use the configured instance directly
            targetInstance ??= instanceManager.GetDefaultInstance(category);
            
            if (targetInstance != null && InstanceManager.IsAutoProvider(targetInstance.ProviderId))
            {
                return TryUploadWithFallback(instanceManager, category, info, null);
            }

            if (targetInstance == null)
            {
                var errorMsg = $"No uploader instance configured (plugin system) for category {category}.";
                DebugHelper.WriteLine(errorMsg);
                return new UploadResult { IsSuccess = false, Response = errorMsg };
            }

            return TryUploadWithInstance(targetInstance, info);
        }

        /// <summary>
        /// Tries to upload using multiple instances with fallback logic.
        /// When one instance fails, it tries the next available instance.
        /// Falls back to File category uploaders if the primary category fails.
        /// </summary>
        private static UploadResult? TryUploadWithFallback(InstanceManager instanceManager, UploaderCategory category, TaskInfo info, string? excludeInstanceId)
        {
            DebugHelper.WriteLine($"Auto destination selected; trying uploaders with fallback for category {category}.");

            // Get all available instances for this category, ordered by priority (default first)
            var allInstances = GetPrioritizedInstances(instanceManager, category, excludeInstanceId);

            if (allInstances.Count == 0)
            {
                DebugHelper.WriteLine($"No available uploaders for category {category}.");
            }
            else
            {
                DebugHelper.WriteLine($"Found {allInstances.Count} potential uploaders to try in category {category}.");

                List<string> failedInstances = new();

                foreach (var instance in allInstances)
                {
                    DebugHelper.WriteLine($"Trying uploader: {instance.DisplayName} ({instance.ProviderId})");

                    var result = TryUploadWithInstance(instance, info);

                    if (result != null && !result.IsError && !string.IsNullOrEmpty(result.URL))
                    {
                        DebugHelper.WriteLine($"Upload successful with {instance.DisplayName}.");
                        return result;
                    }

                    // Track failed instance
                    var errorMsg = result?.Errors?.ToString() ?? result?.Response ?? "Unknown error";
                    failedInstances.Add($"{instance.DisplayName}: {errorMsg}");
                    DebugHelper.WriteLine($"Uploader {instance.DisplayName} failed ({errorMsg}), trying next...");
                }

                var allErrors = string.Join("; ", failedInstances);
                DebugHelper.WriteLine($"All uploaders in category {category} failed: {allErrors}");
            }

            // If primary category failed (or had no uploaders), try File category as fallback
            if (category != UploaderCategory.File)
            {
                DebugHelper.WriteLine($"Trying File category uploaders as fallback...");
                var fileFallbackResult = TryUploadWithFallback(instanceManager, UploaderCategory.File, info, excludeInstanceId);
                if (fileFallbackResult != null && !fileFallbackResult.IsError && !string.IsNullOrEmpty(fileFallbackResult.URL))
                {
                    return fileFallbackResult;
                }
            }

            return new UploadResult { IsSuccess = false, Response = $"All uploaders failed for category {category} and File fallback." };
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
        private static UploadResult? TryUploadWithInstance(UploaderInstance instance, TaskInfo info)
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
                return new UploadResult { IsSuccess = false, Response = ex.Message };
            }

            Uploader.ProgressEventHandler progressHandler = progress => info.ReportUploadProgress(progress);
            uploader.ProgressChanged += progressHandler;

            try
            {
                if (!string.IsNullOrEmpty(info.FilePath))
                {
                    return uploader switch
                    {
                        FileUploader fileUploader => fileUploader.UploadFile(info.FilePath),
                        GenericUploader genericUploader => UploadWithGenericUploader(genericUploader, info.FilePath),
                        _ => new UploadResult { IsSuccess = false, Response = "Uploader type not supported." }
                    };
                }

                if (info.DataType == EDataType.Text && !string.IsNullOrEmpty(info.TextContent))
                {
                    DebugHelper.WriteLine(
                        $"[UploadContentDebug] Text upload dispatch: provider={instance.ProviderId}, " +
                        $"textLength={info.TextContent.Length}, fileName=\"{info.FileName}\"");

                    if (uploader is GenericUploader genericUploader)
                    {
                        string extension = info.TaskSettings.AdvancedSettings.TextFileExtension;
                        string fileName = string.IsNullOrWhiteSpace(info.FileName)
                            ? TaskHelpers.GetFileName(info.TaskSettings, extension, info.Metadata)
                            : info.FileName;

                        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(info.TextContent));
                        ms.Position = 0;
                        return genericUploader.Upload(ms, fileName);
                    }

                    return new UploadResult
                    {
                        IsSuccess = false,
                        Response = "Resolved uploader does not support text uploads."
                    };
                }

                if (info.Metadata?.Image == null)
                {
                    return new UploadResult { IsSuccess = false, Response = "No content to upload." };
                }

                using (MemoryStream? ms = TaskHelpers.SaveImageAsStream(info.Metadata.Image, info.TaskSettings.ImageSettings.ImageFormat, info.TaskSettings))
                {
                    if (ms == null) return new UploadResult { IsSuccess = false, Response = "Failed to create image stream." };
                    ms.Position = 0;
                    return uploader is GenericUploader genericUploader
                        ? genericUploader.Upload(ms, info.FileName)
                        : new UploadResult { IsSuccess = false, Response = "Uploader type not supported for images." };
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, $"Upload failed for {instance.DisplayName}");
                return new UploadResult { IsSuccess = false, Response = ex.Message };
            }
            finally
            {
                uploader.ProgressChanged -= progressHandler;
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
                XerahS.Core.Uploaders.ProviderContextManager.EnsureProviderContext();
                ProviderCatalog.InitializeBuiltInProviders();

                var pluginPaths = PathsManager.GetPluginDirectories();

                DebugHelper.WriteLine($"Loading plugins from: {string.Join(", ", pluginPaths)}");
                ProviderCatalog.LoadPlugins(pluginPaths);
                DebugHelper.WriteLine($"Plugin providers available: {ProviderCatalog.GetAllProviders().Count}");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to load plugins");
            }
        }

        private static void TryAppendHistoryItem(TaskInfo info)
        {
            var url = info.Metadata?.UploadURL;
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            var category = info.TaskSettings.Job.GetHotkeyCategory();
            if (category == EnumExtensions.WorkflowType_Category_ScreenCapture ||
                category == EnumExtensions.WorkflowType_Category_ScreenRecord)
            {
                return;
            }

            try
            {
                var historyPath = SettingsManager.GetHistoryFilePath();
                using var historyManager = new HistoryManagerSQLite(historyPath);

                var historyItem = new HistoryItem
                {
                    FilePath = info.FilePath ?? string.Empty,
                    FileName = TaskHelpers.GetHistoryFileName(info.FileName, info.FilePath, url),
                    DateTime = DateTime.Now,
                    Type = GetHistoryType(info),
                    Host = info.UploaderHost ?? string.Empty,
                    URL = url ?? string.Empty
                };

                var tags = info.GetTags();
                if (tags != null)
                {
                    historyItem.Tags = new Dictionary<string, string?>(tags.Count);
                    foreach (var pair in tags)
                    {
                        historyItem.Tags[pair.Key] = pair.Value;
                    }
                }

                historyManager.AppendHistoryItem(historyItem);
                DebugHelper.WriteLine($"Added upload to history: {historyItem.FileName} (URL: {historyItem.URL})");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to add upload history item");
            }
        }

        private static string GetHistoryType(TaskInfo info)
        {
            return info.DataType switch
            {
                EDataType.Image => "Image",
                EDataType.Text => "Text",
                EDataType.File => "File",
                EDataType.URL => "URL",
                _ => "Unknown"
            };
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

            // Show After Upload window (non-blocking)
            if (info.TaskSettings.AfterUploadJob.HasFlag(AfterUploadTasks.ShowAfterUploadWindow))
            {
                if (!PlatformServices.IsInitialized || PlatformServices.UI == null)
                {
                    DebugHelper.WriteLine("AfterUpload: ShowAfterUploadWindow requested but UI service is not initialized.");
                }
                else if (!string.IsNullOrEmpty(result.URL) && !result.IsError)
                {
                    var advancedSettings = info.TaskSettings.AdvancedSettings;
                    var windowInfo = new Platform.Abstractions.AfterUploadWindowInfo
                    {
                        Url = result.URL ?? string.Empty,
                        ShortenedUrl = result.ShortenedURL,
                        ThumbnailUrl = result.ThumbnailURL,
                        DeletionUrl = result.DeletionURL,
                        FilePath = info.FilePath,
                        FileName = info.FileName,
                        DataType = info.DataType.ToString(),
                        UploaderHost = info.UploaderHost,
                        ClipboardContentFormat = advancedSettings?.ClipboardContentFormat,
                        OpenUrlFormat = advancedSettings?.OpenURLFormat,
                        AutoCloseAfterUploadForm = advancedSettings?.AutoCloseAfterUploadForm ?? false,
                        PreviewImage = info.Metadata?.Image
                    };

                    _ = PlatformServices.UI.ShowAfterUploadWindowAsync(windowInfo).ContinueWith(task =>
                    {
                        if (task.Exception != null)
                        {
                            DebugHelper.WriteException(task.Exception, "AfterUpload: Failed to show window");
                        }
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
                else
                {
                    DebugHelper.WriteLine("AfterUpload: ShowAfterUploadWindow skipped (URL empty or result error).");
                }
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
