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
using XerahS.Core.Services.Privacy;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.History;
using XerahS.Platform.Abstractions;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;
using XerahS.Core.Services;

namespace XerahS.Core.Tasks.Processors
{
    public class UploadJobProcessor : IJobProcessor
    {
        public async Task<bool> ProcessAsync(TaskInfo info, CancellationToken token)
        {
            if (!info.IsUploadJob) return true;
            if (token.IsCancellationRequested) return true;

            if (info.Result != null && !info.Result.IsError && !string.IsNullOrEmpty(info.Result.URL))
            {
                DebugHelper.WriteLine("Upload already completed during capture; running after-upload tasks.");
                await HandleAfterUploadTasksAsync(info, info.Result, token);
                return true;
            }

            // TODO: Handle URL Shortening, URL Sharing logic separate? Or combined?
            // For now, focus on File/Image/Text upload.

            UploadResult? result = null;

            token.ThrowIfCancellationRequested();

            DebugHelper.WriteLine($"[UploadTrace {info.CorrelationId}] Starting upload; dataType={info.DataType}, filePath=\"{info.FilePath}\", fileName=\"{info.FileName}\"");
            result = await UploadAsync(info, token).ConfigureAwait(false);

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
                    
                        if (PlatformServices.IsInitialized && PlatformServices.IsToastServiceInitialized)
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

            return true;
        }

        internal Task<UploadResult?> UploadCaptureImageAsync(TaskInfo info, CancellationToken token) => UploadAsync(info, token);

        private async Task<UploadResult?> UploadAsync(TaskInfo info, CancellationToken token)
        {
            if (!await PrivacyUploadGuard.PrepareAsync(info, token).ConfigureAwait(false)) return info.Result;
            if ((info.DataType is EDataType.Image or EDataType.File) && !string.IsNullOrEmpty(info.FilePath))
            {
                FileReadinessStatus readiness = await FileReadiness.WaitUntilReadyAsync(info.FilePath, cancellationToken: token).ConfigureAwait(false);
                if (readiness != FileReadinessStatus.Ready)
                {
                    if (readiness == FileReadinessStatus.Missing)
                    {
                        string sourcePath = info.FilePath;
                        UploadResult? recorded = AdoptRecordedUpload(
                            info,
                            WatchedFileUploadRecovery.FindRecordedUpload(sourcePath, DateTime.Now - WatchedFileUploadRecovery.RecentWindow));
                        if (recorded != null)
                        {
                            return recorded;
                        }

                        string? movedPath = WatchedFileUploadRecovery.FindMovedPath(sourcePath);
                        if (movedPath != null)
                        {
                            recorded = AdoptRecordedUpload(info, WatchedFileUploadRecovery.FindRecordedUpload(sourcePath));
                            if (recorded != null)
                            {
                                return recorded;
                            }

                            DebugHelper.WriteLine($"[UploadTrace {info.CorrelationId}] Source was moved to {movedPath}. Waiting for the watch-folder upload.");
                            recorded = await WaitForRecordedUploadAsync(info, sourcePath, token).ConfigureAwait(false);
                            if (recorded != null)
                            {
                                return recorded;
                            }

                            info.FilePath = movedPath;
                            readiness = await FileReadiness.WaitUntilReadyAsync(movedPath, TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
                            if (readiness == FileReadinessStatus.Ready)
                            {
                                DebugHelper.WriteLine($"[UploadTrace {info.CorrelationId}] Watch-folder upload was not recorded. Uploading {movedPath}.");
                            }
                        }
                    }

                    if (readiness != FileReadinessStatus.Ready)
                    {
                        string message = FileReadiness.Describe(readiness, info.FilePath);
                        DebugHelper.WriteLine($"[UploadTrace {info.CorrelationId}] Upload skipped: {message}");
                        return new UploadResult { IsSuccess = false, Response = message };
                    }
                }
            }

            ApplyNamingConvention(info);

            return await StartUploadAsync(info, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Generic file uploads keep their source name, so different files sent with the same name
        /// end up at one constant URL. Apply the task's name pattern (as screenshots do) and keep the extension.
        /// </summary>
        private static void ApplyNamingConvention(TaskInfo info)
        {
            if (info.DataType != EDataType.File || string.IsNullOrEmpty(info.FilePath) || PrivacyUploadGuard.IsRequired(info))
            {
                return;
            }

            try
            {
                string extension = Path.GetExtension(info.FilePath);
                string newName = TaskHelpers.GetFileName(info.TaskSettings, extension, info.Metadata);
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    DebugHelper.WriteLine($"[UploadTrace {info.CorrelationId}] Renaming upload \"{info.FileName}\" -> \"{newName}\"");
                    info.SetFileName(newName);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to apply upload naming convention; keeping original file name");
            }
        }

        private static UploadResult? AdoptRecordedUpload(TaskInfo info, HistoryItem? recorded)
        {
            UploadResult? result = WatchedFileUploadRecovery.ToAdoptedResult(recorded);
            if (result == null || recorded == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(recorded.FilePath))
            {
                info.FilePath = recorded.FilePath;
            }

            DebugHelper.WriteLine($"[UploadTrace {info.CorrelationId}] Upload already recorded for {Path.GetFileName(recorded.FilePath)}: {result.URL}");
            return result;
        }

        private static async Task<UploadResult?> WaitForRecordedUploadAsync(TaskInfo info, string sourcePath, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow + WatchedFileUploadRecovery.InFlightGrace;
            DateTime notBefore = DateTime.Now - WatchedFileUploadRecovery.RecentWindow;
            while (true)
            {
                UploadResult? recorded = AdoptRecordedUpload(info, WatchedFileUploadRecovery.FindRecordedUpload(sourcePath, notBefore));
                if (recorded != null)
                {
                    return recorded;
                }

                if (DateTime.UtcNow >= deadline)
                {
                    return null;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), token).ConfigureAwait(false);
            }
        }

        private Task<UploadResult?> StartUploadAsync(TaskInfo info, CancellationToken token)
        {
            try
            {
                return info.DataType switch
                {
                    EDataType.Image => UploadWithPluginSystemAsync(info, UploaderCategory.Image, token),
                    EDataType.Text => UploadWithPluginSystemAsync(info, UploaderCategory.Text, token),
                    EDataType.File => UploadWithPluginSystemAsync(info, UploaderCategory.File, token),
                    _ => Task.FromResult<UploadResult?>(null)
                };
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "UploadJobProcessor");
                return Task.FromResult<UploadResult?>(new UploadResult { IsSuccess = false, Response = ex.Message });
            }
        }

        private async Task<UploadResult?> UploadWithPluginSystemAsync(TaskInfo info, UploaderCategory category, CancellationToken token)
        {
            EnsurePluginsLoaded();

            var instanceManager = InstanceManager.Instance;
            var allowCrossCategoryFallback = AllowsCrossCategoryFallback(info);
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
                targetInstance = ResolveRequestedInstance(instanceManager, targetInstanceId, category, allowCrossCategoryFallback, info.TaskSettings.RoutedImageFileDestinationId, info.FileName);
            }

            // Check if Auto destination is selected
            if (targetInstance != null && InstanceManager.IsAutoProvider(targetInstance.ProviderId))
            {
                return await TryUploadWithFallbackAsync(instanceManager, category, info, targetInstanceId, token).ConfigureAwait(false);
            }

            // Not Auto - use the configured instance directly
            targetInstance ??= ResolveDefaultInstance(instanceManager, category);

            if (allowCrossCategoryFallback)
            {
                targetInstance = ApplyFileTypeRoute(instanceManager, category, targetInstance, info.FileName);
            }

            if (targetInstance == null && category == UploaderCategory.File)
            {
                targetInstance = EnsureAutoFileDestinationInstance(instanceManager);
            }

            if (targetInstance != null && InstanceManager.IsAutoProvider(targetInstance.ProviderId))
            {
                return await TryUploadWithFallbackAsync(instanceManager, category, info, null, token).ConfigureAwait(false);
            }

            if (targetInstance == null)
            {
                // No instance found for the requested category.
                // For Image uploads, fall back to File-category instances (multi-category providers
                // like Amazon S3 support both Image and File even when configured as a File uploader).
                if (allowCrossCategoryFallback && category == UploaderCategory.Image)
                {
                    DebugHelper.WriteLine("No Image uploader configured; falling back to File-category instances.");
                    return await TryUploadWithFallbackAsync(instanceManager, category, info, null, token).ConfigureAwait(false);
                }

                var errorMsg = $"No uploader instance configured (plugin system) for category {category}.";
                DebugHelper.WriteLine(errorMsg);
                return new UploadResult { IsSuccess = false, Response = errorMsg };
            }

            var primaryResult = await TryUploadWithInstanceAsync(targetInstance, info, token).ConfigureAwait(false);

            if (info.PrivacyReviewRequired) return primaryResult;

            if (IsSuccessfulUploadResult(primaryResult))
            {
                return primaryResult;
            }

            var primaryError = primaryResult?.Errors?.ToString() ?? primaryResult?.Response ?? "Unknown error";
            DebugHelper.WriteLine(
                $"Primary {category} uploader '{targetInstance.DisplayName}' failed ({primaryError}). " +
                "Trying fallback uploaders.");

            var attemptedInstanceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                targetInstance.InstanceId
            };

            var fallbackResult = await TryUploadWithFallbackAsync(
                instanceManager,
                category,
                info,
                targetInstance.InstanceId,
                token,
                attemptedInstanceIds).ConfigureAwait(false);

            return IsSuccessfulUploadResult(fallbackResult) ? fallbackResult : fallbackResult ?? primaryResult;
        }

        internal static UploaderInstance? ResolveRequestedInstance(
            InstanceManager instanceManager,
            string targetInstanceId,
            UploaderCategory category,
            bool allowCrossCategoryFallback = true,
            string? routedImageFileDestinationId = null,
            string? fileName = null)
        {
            var targetInstance = instanceManager.GetInstance(targetInstanceId);
            if (targetInstance == null)
            {
                DebugHelper.WriteLine($"Configured destination instance not found: {targetInstanceId}");
                return null;
            }

            if (!InstanceManager.IsAutoProvider(targetInstance.ProviderId) && targetInstance.Category != category)
            {
                bool explicitlyRoutedImage = category == UploaderCategory.Image && targetInstance.Category == UploaderCategory.File &&
                    string.Equals(targetInstance.InstanceId, routedImageFileDestinationId, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(fileName) && targetInstance.FileTypeRouting != null &&
                    (targetInstance.FileTypeRouting.AllFileTypes || (targetInstance.FileTypeRouting.FileExtensions ?? []).Any(ext =>
                        string.Equals(ext?.Trim().TrimStart('.'), Path.GetExtension(fileName).TrimStart('.'), StringComparison.OrdinalIgnoreCase)));
                if (!allowCrossCategoryFallback && !explicitlyRoutedImage)
                {
                    DebugHelper.WriteLine($"Configured destination category mismatch. Expected {category}, got {targetInstance.Category}. Rejecting instance.");
                    return null;
                }

                DebugHelper.WriteLine($"Configured destination category mismatch. Expected {category}, got {targetInstance.Category}. Continuing with configured instance.");
            }

            if (!targetInstance.IsAvailable)
            {
                DebugHelper.WriteLine($"Configured destination instance is unavailable: {targetInstance.DisplayName} ({targetInstance.InstanceId}). Falling back to other uploaders.");
                return null;
            }

            return targetInstance;
        }

        /// <summary>
        /// Hands a file to the destination that claims its type (a listed extension or "Text files") when the
        /// chosen destination only covers it as a catch-all, so e.g. .md goes to a docs host while the
        /// default stays object storage. Auto destinations already route by type.
        /// </summary>
        internal static UploaderInstance? ApplyFileTypeRoute(
            InstanceManager instanceManager,
            UploaderCategory category,
            UploaderInstance? targetInstance,
            string? fileName)
        {
            if (category != UploaderCategory.File || string.IsNullOrWhiteSpace(fileName) ||
                (targetInstance != null && InstanceManager.IsAutoProvider(targetInstance.ProviderId)))
            {
                return targetInstance;
            }

            string extension = Path.GetExtension(fileName);
            if (targetInstance != null && ClaimsFileTypeSpecifically(targetInstance, extension))
            {
                return targetInstance;
            }

            var specialist = instanceManager.GetSpecificDestinationForFile(category, extension);
            if (specialist == null ||
                (targetInstance != null && string.Equals(specialist.InstanceId, targetInstance.InstanceId, StringComparison.OrdinalIgnoreCase)))
            {
                return targetInstance;
            }

            DebugHelper.WriteLine(
                $"File type route: '{extension}' goes to {specialist.DisplayName} ({specialist.InstanceId}) " +
                $"instead of {targetInstance?.DisplayName ?? "(no destination)"}.");
            return specialist;
        }

        private static bool ClaimsFileTypeSpecifically(UploaderInstance instance, string extension)
        {
            var routing = instance.FileTypeRouting;
            if (routing == null || routing.AllFileTypes)
            {
                return false;
            }

            string ext = extension.Trim().TrimStart('.');
            return (routing.FileExtensions ?? []).Any(e => string.Equals(e?.Trim().TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase)) ||
                routing.ClaimsAsTextFile(ext);
        }

        private static bool AllowsCrossCategoryFallback(TaskInfo info)
        {
            return info.TaskSettings?.AllowCrossCategoryFallback ?? true;
        }

        internal static UploaderInstance? ResolveDefaultInstance(InstanceManager instanceManager, UploaderCategory category)
        {
            var targetInstance = instanceManager.GetDefaultInstance(category);
            if (targetInstance != null && !targetInstance.IsAvailable)
            {
                DebugHelper.WriteLine($"Default destination instance is unavailable: {targetInstance.DisplayName} ({targetInstance.InstanceId}). Falling back to other uploaders.");
                return null;
            }

            return targetInstance;
        }

        private static UploaderInstance? EnsureAutoFileDestinationInstance(InstanceManager instanceManager)
        {
            try
            {
                var existingDefault = instanceManager.GetDefaultInstance(UploaderCategory.File);
                if (existingDefault != null && InstanceManager.IsAutoProvider(existingDefault.ProviderId))
                {
                    DebugHelper.WriteLine($"[UploadAutoBootstrap] Reusing existing default auto file instance: {existingDefault.InstanceId}");
                    return existingDefault;
                }

                var existingAuto = instanceManager.GetInstancesByCategory(UploaderCategory.File)
                    .FirstOrDefault(i => InstanceManager.IsAutoProvider(i.ProviderId));
                if (existingAuto != null)
                {
                    instanceManager.SetDefaultInstance(UploaderCategory.File, existingAuto.InstanceId);
                    DebugHelper.WriteLine($"[UploadAutoBootstrap] Selected existing auto file instance as default: {existingAuto.InstanceId}");
                    return existingAuto;
                }

                var autoProvider = ProviderCatalog.GetProvider(ProviderIds.Auto);
                if (autoProvider == null)
                {
                    DebugHelper.WriteLine("[UploadAutoBootstrap] Auto provider is not available; cannot bootstrap File destination.");
                    return null;
                }

                var autoInstance = new UploaderInstance
                {
                    ProviderId = ProviderIds.Auto,
                    Category = UploaderCategory.File,
                    DisplayName = "Auto (File)",
                    SettingsJson = autoProvider.GetDefaultSettings(UploaderCategory.File),
                    IsAvailable = true
                };

                instanceManager.AddInstance(autoInstance);
                instanceManager.SetDefaultInstance(UploaderCategory.File, autoInstance.InstanceId);
                DebugHelper.WriteLine($"[UploadAutoBootstrap] Created and selected auto file instance: {autoInstance.InstanceId} (category={UploaderCategory.File})");
                return autoInstance;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "[UploadAutoBootstrap] Failed to bootstrap auto file destination");
                return null;
            }
        }

        /// <summary>
        /// Tries to upload using multiple instances with fallback logic.
        /// When one instance fails, it tries the next available instance.
        /// Falls back to File category uploaders if the primary category fails.
        /// </summary>
        private static async Task<UploadResult?> TryUploadWithFallbackAsync(InstanceManager instanceManager, UploaderCategory category, TaskInfo info, string? excludeInstanceId, CancellationToken token, HashSet<string>? attemptedInstanceIds = null)
        {
            if (info.PrivacyReviewRequired) return info.Result;
            attemptedInstanceIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            DebugHelper.WriteLine($"Auto destination selected; trying uploaders with fallback for category {category}.");

            // Get all available instances for this category that haven't been attempted yet
            var allInstances = GetPrioritizedInstances(instanceManager, category, excludeInstanceId, info.FileName)
                .Where(i => !attemptedInstanceIds.Contains(i.InstanceId))
                .ToList();

            if (allInstances.Count == 0)
            {
                // Ensure we have an auto destination for File category
                var targetInstance = category == UploaderCategory.File ? EnsureAutoFileDestinationInstance(instanceManager) : null;

                // Prevent infinite recursion: if the auto instance was already attempted, don't try again
                if (targetInstance != null && attemptedInstanceIds.Contains(targetInstance.InstanceId))
                {
                    DebugHelper.WriteLine($"Auto instance {targetInstance.InstanceId} already attempted; skipping to avoid recursion.");
                    return new UploadResult { IsSuccess = false, Response = $"All uploaders failed for category {category} and fallback." };
                }

                if (targetInstance == null)
                {
                    DebugHelper.WriteLine($"No available uploaders for category {category} (excluding already attempted).");
                }
                else
                {
                    // Try the auto instance as a last resort
                    DebugHelper.WriteLine($"Trying auto destination: {targetInstance.InstanceId}");
                    attemptedInstanceIds.Add(targetInstance.InstanceId);
                    var primaryResult = await TryUploadWithInstanceAsync(targetInstance, info, token).ConfigureAwait(false);
                    if (IsSuccessfulUploadResult(primaryResult))
                    {
                        return primaryResult;
                    }

                    var errorMsg = primaryResult?.Errors?.ToString() ?? primaryResult?.Response ?? "Unknown error";
                    DebugHelper.WriteLine($"Auto uploader failed ({errorMsg}), no more fallbacks available.");
                }
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

                    var result = await TryUploadWithInstanceAsync(instance, info, token).ConfigureAwait(false);
                    if (info.PrivacyReviewRequired) return result;

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

            var allowCrossCategoryFallback = AllowsCrossCategoryFallback(info);

            // If primary category failed (or had no uploaders), try cross-category fallback
            if (allowCrossCategoryFallback && category != UploaderCategory.File)
            {
                DebugHelper.WriteLine($"Trying File category uploaders as fallback...");
                var fileFallbackResult = await TryUploadWithFallbackAsync(instanceManager, UploaderCategory.File, info, excludeInstanceId, token, attemptedInstanceIds).ConfigureAwait(false);
                if (fileFallbackResult != null && !fileFallbackResult.IsError && !string.IsNullOrEmpty(fileFallbackResult.URL))
                {
                    return fileFallbackResult;
                }
            }

            // If File category failed and the file is an image, try Image-category uploaders
            if (allowCrossCategoryFallback && category == UploaderCategory.File && !string.IsNullOrEmpty(info.FileName) && FileHelpers.IsImageFile(info.FileName))
            {
                DebugHelper.WriteLine("File is an image; trying Image category uploaders as fallback...");
                var imageFallbackResult = await TryUploadWithFallbackAsync(instanceManager, UploaderCategory.Image, info, excludeInstanceId, token, attemptedInstanceIds).ConfigureAwait(false);
                if (imageFallbackResult != null && !imageFallbackResult.IsError && !string.IsNullOrEmpty(imageFallbackResult.URL))
                {
                    return imageFallbackResult;
                }
            }

            // If File category failed and the file is text-based, try Text-category uploaders
            if (allowCrossCategoryFallback && category == UploaderCategory.File && !string.IsNullOrEmpty(info.FileName) && FileHelpers.IsTextFile(info.FileName))
            {
                DebugHelper.WriteLine("File is text-based; trying Text category uploaders as fallback...");
                var textFallbackResult = await TryUploadWithFallbackAsync(instanceManager, UploaderCategory.Text, info, excludeInstanceId, token, attemptedInstanceIds).ConfigureAwait(false);
                if (textFallbackResult != null && !textFallbackResult.IsError && !string.IsNullOrEmpty(textFallbackResult.URL))
                {
                    return textFallbackResult;
                }
            }

            return new UploadResult { IsSuccess = false, Response = $"All uploaders failed for category {category} and fallback." };
        }

        /// <summary>
        /// Gets all available instances for a category, prioritized by:
        /// 1. Default instance first
        /// 2. Other instances sorted by creation time (newest first)
        /// </summary>
        private static List<UploaderInstance> GetPrioritizedInstances(InstanceManager instanceManager, UploaderCategory category, string? excludeInstanceId, string? fileName)
        {
            var allInstances = instanceManager.GetInstancesByCategory(category)
                .Where(i => i.IsAvailable)
                .Where(i => !InstanceManager.IsAutoProvider(i.ProviderId))
                .Where(i => excludeInstanceId == null || !string.Equals(i.InstanceId, excludeInstanceId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var preferredInstance = ResolvePreferredInstance(instanceManager, category, fileName);

            // Sort: default first, then by creation time (newest first)
            var ordered = allInstances
                .OrderByDescending(i => preferredInstance != null && i.InstanceId == preferredInstance.InstanceId)
                .ThenByDescending(i => i.CreatedAt)
                .ToList();

            return ordered;
        }

        private static UploaderInstance? ResolvePreferredInstance(InstanceManager instanceManager, UploaderCategory category, string? fileName)
        {
            UploaderInstance? preferredInstance = null;

            string? extension = string.IsNullOrWhiteSpace(fileName) ? null : Path.GetExtension(fileName);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                preferredInstance = instanceManager.GetDestinationForFile(category, extension);
                if (preferredInstance != null && InstanceManager.IsAutoProvider(preferredInstance.ProviderId))
                {
                    preferredInstance = instanceManager.ResolveAutoInstance(category, preferredInstance.InstanceId);
                }
            }

            preferredInstance ??= instanceManager.GetDefaultInstance(category);
            if (preferredInstance != null && InstanceManager.IsAutoProvider(preferredInstance.ProviderId))
            {
                preferredInstance = instanceManager.ResolveAutoInstance(category, preferredInstance.InstanceId);
            }

            return preferredInstance;
        }

        /// <summary>
        /// Attempts to upload using a specific instance. Prefers <see cref="IUploadHandler"/>;
        /// otherwise adapts legacy <see cref="GenericUploader"/>.
        /// </summary>
        private static async Task<UploadResult?> TryUploadWithInstanceAsync(
            UploaderInstance instance,
            TaskInfo info,
            CancellationToken token)
        {
            // Freeze the reviewed destination settings before creating a plugin. A concurrent UI edit
            // cannot redirect an approved image after the final guard check.
            instance = new UploaderInstance { InstanceId = instance.InstanceId, ProviderId = instance.ProviderId,
                Category = instance.Category, DisplayName = instance.DisplayName, SettingsJson = instance.SettingsJson,
                IsAvailable = instance.IsAvailable };
            if (!await PrivacyUploadGuard.ConfirmDestinationAsync(info, instance, token).ConfigureAwait(false)) return info.Result;
            var provider = ProviderCatalog.GetProvider(instance.ProviderId);
            if (provider == null)
            {
                DebugHelper.WriteLine($"Provider not found in catalog: {instance.ProviderId}");
                return null;
            }

            object uploader;
            try
            {
                uploader = provider.CreateInstance(instance.SettingsJson);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, $"Failed to create uploader instance for {instance.DisplayName}");
                return new UploadResult { IsSuccess = false, Response = ex.Message };
            }

            try
            {
                await using Stream? content = OpenUploadContent(info, instance, out string fileName, out UploaderCategory category);
                if (content == null)
                {
                    return new UploadResult { IsSuccess = false, Response = "No content to upload." };
                }

                long? length = content.CanSeek ? content.Length : null;
                ProgressManager progressManager = new(length is > 0 ? length.Value : 1);
                IProgress<UploadProgressReport> progress = new Progress<UploadProgressReport>(report =>
                {
                    if (progressManager.UpdateProgress(report.BytesTransferred))
                    {
                        info.ReportUploadProgress(progressManager);
                    }
                });

                UploadRequest request = new()
                {
                    Content = content,
                    FileName = fileName,
                    ContentType = MimeTypes.GetMimeTypeFromFileName(fileName),
                    ContentLength = length,
                    Category = category,
                    CorrelationId = info.CorrelationId,
                    Progress = progress,
                    Host = ProviderCatalog.GetProviderContext() as IDestinationHost
                };

                UploadOutcome outcome = await UploaderUploadAdapter.UploadAsync(uploader, request, token).ConfigureAwait(false);
                UploadResult result = outcome.ToUploadResult();
                ApplyResolvedUploaderHost(info, instance, result);
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                // The file vanished between queueing and upload (moved or deleted by the program
                // that handed it over): report it plainly instead of logging a stack trace.
                string missingMessage = $"The file to upload no longer exists: {(ex as FileNotFoundException)?.FileName ?? ex.Message}";
                DebugHelper.WriteLine($"Upload failed for {instance.DisplayName}: {missingMessage}");
                return new UploadResult { IsSuccess = false, Response = missingMessage };
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, $"Upload failed for {instance.DisplayName}");
                return new UploadResult { IsSuccess = false, Response = ex.Message };
            }
        }

        private static Stream? OpenUploadContent(TaskInfo info, UploaderInstance instance, out string fileName, out UploaderCategory category)
        {
            if (PrivacyUploadGuard.IsRequired(info))
            {
                fileName = Path.GetFileName(info.PrivacyApproval?.FilePath ?? throw new InvalidOperationException("Privacy review required."));
                category = UploaderCategory.Image;
                return PrivacyUploadGuard.OpenApprovedContent(info, instance);
            }
            if (!string.IsNullOrEmpty(info.FilePath))
            {
                fileName = string.IsNullOrWhiteSpace(info.FileName) ? Path.GetFileName(info.FilePath) : info.FileName;
                category = info.DataType switch
                {
                    EDataType.Image => UploaderCategory.Image,
                    EDataType.Text => UploaderCategory.Text,
                    _ => UploaderCategory.File
                };
                return new FileStream(info.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
            }

            if (info.DataType == EDataType.Text && !string.IsNullOrEmpty(info.TextContent))
            {
                string extension = info.TaskSettings.AdvancedSettings.TextFileExtension;
                fileName = string.IsNullOrWhiteSpace(info.FileName)
                    ? TaskHelpers.GetFileName(info.TaskSettings, extension, info.Metadata)
                    : info.FileName;
                category = UploaderCategory.Text;
                DebugHelper.WriteLine(
                    $"[UploadContentDebug] Text upload dispatch: textLength={info.TextContent.Length}, fileName=\"{fileName}\"");
                return new MemoryStream(Encoding.UTF8.GetBytes(info.TextContent));
            }

            if (info.Metadata?.Image != null)
            {
                fileName = info.FileName;
                category = UploaderCategory.Image;
                MemoryStream? ms = TaskHelpers.SaveImageAsStream(info.Metadata.Image, info.TaskSettings.ImageSettings.ImageFormat, info.TaskSettings);
                if (ms != null)
                {
                    ms.Position = 0;
                }

                return ms;
            }

            fileName = info.FileName ?? "upload";
            category = UploaderCategory.File;
            return null;
        }

        internal static void ApplyResolvedUploaderHost(TaskInfo info, UploaderInstance instance, UploadResult? result)
        {
            if (IsSuccessfulUploadResult(result))
            {
                info.ResolvedUploaderHost = instance.DisplayName;
                info.ResolvedUploaderInstanceId = instance.InstanceId;
            }
        }

        private static bool IsSuccessfulUploadResult(UploadResult? result)
        {
            return result != null && (result.IsSuccess || (!result.IsError && !string.IsNullOrWhiteSpace(result.URL)));
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

                string sourceName = Path.GetFileName(info.FilePath ?? string.Empty);
                HistoryItem? alreadyRecorded = historyManager.FindBySourceFileName(sourceName);
                if (alreadyRecorded != null && string.Equals(alreadyRecorded.URL, url, StringComparison.Ordinal))
                {
                    DebugHelper.WriteLine($"History already records {url} for {sourceName}");
                    return;
                }

                var historyItem = CreateHistoryItem(info, url);

                if (historyManager.AppendHistoryItem(historyItem))
                {
                    info.HistoryItemId = historyItem.Id;
                    DebugHelper.WriteLine($"Added upload to history: {historyItem.FileName} (URL: {historyItem.URL})");
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to add upload history item");
            }
        }

        internal static HistoryItem CreateHistoryItem(TaskInfo info, string url)
        {
            var historyItem = new HistoryItem
            {
                FilePath = info.FilePath ?? string.Empty,
                FileName = TaskHelpers.GetHistoryFileName(info.FileName, info.FilePath, url),
                DateTime = DateTime.Now,
                Type = GetHistoryType(info),
                URL = url
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

            ApplyUploadResult(historyItem, info);
            return historyItem;
        }

        /// <summary>
        /// Copies upload details (host, thumbnail/deletion/shortened URLs, result metadata, uploader
        /// instance, errors) onto a history item. Shared by upload jobs and capture-then-upload.
        /// </summary>
        internal static void ApplyUploadResult(HistoryItem historyItem, TaskInfo info)
        {
            var uploadResult = info.Result;
            historyItem.Host = info.ResolvedUploaderHost ?? info.UploaderHost ?? string.Empty;
            historyItem.ThumbnailURL = uploadResult?.ThumbnailURL ?? string.Empty;
            historyItem.DeletionURL = uploadResult?.DeletionURL ?? string.Empty;
            historyItem.ShortenedURL = uploadResult?.ShortenedURL ?? string.Empty;
            historyItem.Tags ??= new Dictionary<string, string?>();

            if (uploadResult?.Metadata?.Count > 0)
            {
                foreach (var pair in uploadResult.Metadata)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Key))
                    {
                        historyItem.Tags[UploadRemoteDeletionService.UploadResultTagPrefix + pair.Key] = pair.Value;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(info.ResolvedUploaderInstanceId))
            {
                historyItem.Tags[UploadRemoteDeletionService.UploaderInstanceIdTag] = info.ResolvedUploaderInstanceId;
            }

            // Store upload errors if any
            if (uploadResult?.Errors?.Count > 0)
            {
                historyItem.Errors = uploadResult.Errors.ToString();
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
                        PreviewImage = info.Metadata?.Image,
                        ErrorDetails = result.Errors?.Count > 0 ? result.Errors.ToString() : null
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
