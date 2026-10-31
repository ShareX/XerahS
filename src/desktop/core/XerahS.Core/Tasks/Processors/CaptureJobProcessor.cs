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
using XerahS.Services;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;
using ShareX.ImageEditor.Core.Persistence;
using ShareX.ImageEditor.Hosting;
using XerahS.Core.Services;
using XerahS.Core.Services.Privacy;
using SkiaSharp;

namespace XerahS.Core.Tasks.Processors
{
    public class CaptureJobProcessor : IJobProcessor
    {
        /// <summary>
        /// Callback to pin an image to the desktop. Set by the UI layer to dispatch to PinToScreenManager.
        /// Takes bitmap, location (object for cross-layer safety), and options.
        /// </summary>
        public static Func<SKBitmap, object?, PinToScreenOptions, Task>? PinToScreenCallback { get; set; }

        /// <summary>
        /// Callback to show the analyzer window. Set by the UI layer to dispatch to AvaloniaUIService.
        /// </summary>
        public static Func<SKBitmap, Task>? ShowAnalyzerCallback { get; set; }

        /// <summary>
        /// Executes after-capture tasks for the current job.
        /// </summary>
        /// <returns><c>true</c> to continue the pipeline; <c>false</c> if the user cancelled.</returns>
        public async Task<bool> ProcessAsync(TaskInfo info, CancellationToken token)
        {
            if (info.Metadata?.Image == null) return true;

            var settings = info.TaskSettings;
            DebugHelper.WriteLine(
                $"AfterCaptureJob={settings.AfterCaptureJob}, " +
                $"UploadImageToHost={settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.UploadImageToHost)}");
            ImageEditorSessionResult? editorResult = null;
            string? annotationSidecarPath = null;
            bool annotationSidecarSaveAttempted = false;
            bool imageChanged = false;

            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.ShowAfterCaptureWindow))
            {
                if (!PlatformServices.IsInitialized)
                {
                    DebugHelper.WriteLine("ShowAfterCaptureWindow requested but UI service is not initialized.");
                }
                else
                {
                    var originalAfterCapture = settings.AfterCaptureJob;
                    bool originalPrivacyMode = settings.PrivacySettings.Enabled;
                    var choices = await PlatformServices.UI.ShowAfterCaptureWindowWithPrivacyAsync(
                        info.Metadata.Image,
                        settings.AfterCaptureJob,
                        settings.AfterUploadJob, settings.PrivacySettings.Enabled);
                    var result = choices.Tasks;
                    settings.PrivacySettings.Enabled = choices.PrivacyEnabled;
                    if (result.Cancel)
                    {
                        DebugHelper.WriteLine("After capture window cancelled; aborting workflow.");
                        return false;
                    }

                    settings.AfterCaptureJob = GetAfterCaptureTasksForRun(result);
                    settings.AfterUploadJob = result.Upload;
                    if (originalPrivacyMode != settings.PrivacySettings.Enabled) PersistPrivacyModeSetting(settings.WorkflowId, settings.PrivacySettings.Enabled);
                    info.SuppressCompletionNotification = result.QuickAction != AfterCaptureQuickAction.None;

                    // Persist "Show after capture window" setting if user unchecked it
                    if (result.QuickAction == AfterCaptureQuickAction.None &&
                        originalAfterCapture.HasFlag(AfterCaptureTasks.ShowAfterCaptureWindow) &&
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
                        return true;
                    }

                    if (!ReferenceEquals(processed, info.Metadata.Image))
                    {
                        info.Metadata.Image.Dispose();
                    }

                    info.Metadata.Image = processed;
                    imageChanged = true;
                }
            }

            // Annotation should happen BEFORE save, so the saved file includes annotations
            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.AnnotateMedia))
            {
                // OmaSnap editor on Hyprland when selected in settings (XIP0088); otherwise the XerahS editor.
                var hostedAnnotation = info.Metadata?.Image != null
                    ? await HostedEditorAndPinService.TryAnnotateAsync(info.Metadata.Image, token)
                    : (Handled: false, Annotated: null);
                if (hostedAnnotation.Handled)
                {
                    if (hostedAnnotation.Annotated != null)
                    {
                        info.Metadata!.Image!.Dispose();
                        info.Metadata.Image = hostedAnnotation.Annotated;
                        imageChanged = true;
                    }
                }
                else if (info.Metadata?.Image != null && PlatformServices.UI != null)
                {
                    EditorSessionOutcome outcome = await PlatformServices.UI.ShowEditorSessionWithOutcomeAsync(info.Metadata.Image, taskMode: true);
                    if (outcome.Cancelled)
                    {
                        DebugHelper.WriteLine("Editor cancelled; aborting workflow.");
                        return false;
                    }

                    editorResult = outcome.Result;
                    if (editorResult?.RenderedImage == null)
                    {
                        // Cancelled or failed editor exports must not upload the unannotated input.
                        DebugHelper.WriteLine("Annotation editor returned no image; stopping after-capture tasks.");
                        return false;
                    }
                    if (info.Metadata.Image != editorResult.RenderedImage)
                    {
                        info.Metadata.Image.Dispose();
                    }
                    info.Metadata.Image = editorResult.RenderedImage;
                    imageChanged = true;
                }
                else
                {
                    DebugHelper.WriteLine("Annotation editor is unavailable; stopping after-capture tasks.");
                    return false;
                }
            }

            if (PrivacyUploadGuard.IsRequired(info))
            {
                // Source/annotation sidecars must never survive this upload path.
                if (editorResult?.SourceImage != null && !ReferenceEquals(editorResult.SourceImage, info.Metadata?.Image)) editorResult.SourceImage.Dispose();
                editorResult = null;
                if (!await PrivacyUploadGuard.PrepareAsync(info, token)) return false;
            }

            if (imageChanged && info.PrivacyApproval == null)
            {
                // FilePath may still identify an imported/pre-edit image. Force downstream
                // saving/uploading to materialize the current pixels without overwriting the input.
                // Keep the original path until privacy review has checked its source format.
                info.FilePath = string.Empty;
            }

            bool editedFileNeeded = imageChanged && (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.CopyFileToClipboard)
                || settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.CopyFilePathToClipboard));
            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.SaveImageToFile) || editedFileNeeded)
            {
                await SaveImageToFileAsync(info);
                annotationSidecarPath = await SaveAnnotationSidecarAsync(info, editorResult);
                annotationSidecarSaveAttempted = true;
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
                await UploadImageAsync(info, token);
                if (!annotationSidecarSaveAttempted)
                {
                    annotationSidecarPath = await SaveAnnotationSidecarAsync(info, editorResult);
                    annotationSidecarSaveAttempted = true;
                }
            }
            else
            {
                DebugHelper.WriteLine("UploadImageToHost flag not set; skipping upload.");
            }

            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.DoOCR))
            {
                await PerformOCRAsync(info);

                if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.CopyOcrTextToClipboard))
                {
                    TryCopyOcrTextToClipboard(info.Metadata?.OcrText);
                }
            }

            // ScanQRCode
            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.ScanQRCode))
            {
                if (info.Metadata?.Image == null)
                {
                    DebugHelper.WriteLine("ScanQRCode skipped: no image in metadata.");
                }
                else
                {
                    try
                    {
                        var results = QrCodeService.Decode(info.Metadata.Image, out var error);
                        if (!string.IsNullOrEmpty(error))
                        {
                            DebugHelper.WriteLine($"ScanQRCode error: {error}");
                        }
                        else if (results.Count > 0)
                        {
                            PlatformServices.Clipboard.SetText(string.Join(Environment.NewLine, results));
                            DebugHelper.WriteLine($"ScanQRCode decoded {results.Count} code(s) and copied to clipboard.");
                        }
                        else
                        {
                            DebugHelper.WriteLine("ScanQRCode: no QR codes detected.");
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteException(ex, "ScanQRCode");
                    }
                }
            }

            // PinToScreen
            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.PinToScreen))
            {
                if (info.Metadata?.Image == null)
                {
                    DebugHelper.WriteLine("PinToScreen skipped: no image in metadata.");
                }
                else if (PinToScreenCallback == null)
                {
                    DebugHelper.WriteLine("PinToScreen skipped: callback not set.");
                }
                else
                {
                    try
                    {
                        if (await HostedEditorAndPinService.TryPinAsync(info.Metadata.Image, token))
                        {
                            DebugHelper.WriteLine("PinToScreen: image pinned with OmaSnap.");
                        }
                        else
                        {
                            var options = SettingsManager.DefaultTaskSettings?.ToolsSettings?.PinToScreenOptions ?? new PinToScreenOptions();
                            await PinToScreenCallback(info.Metadata.Image, null, options);
                            DebugHelper.WriteLine("PinToScreen: image pinned to desktop.");
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteException(ex, "PinToScreen");
                    }
                }
            }

            // CopyFileToClipboard
            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.CopyFileToClipboard))
            {
                if (string.IsNullOrEmpty(info.FilePath))
                {
                    DebugHelper.WriteLine("CopyFileToClipboard skipped: no file path.");
                }
                else
                {
                    try
                    {
                        PlatformServices.Clipboard.SetFileDropList(new[] { info.FilePath });
                        DebugHelper.WriteLine($"CopyFileToClipboard: copied {info.FilePath}");
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteException(ex, "CopyFileToClipboard");
                    }
                }
            }

            // CopyFilePathToClipboard
            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.CopyFilePathToClipboard))
            {
                if (string.IsNullOrEmpty(info.FilePath))
                {
                    DebugHelper.WriteLine("CopyFilePathToClipboard skipped: no file path.");
                }
                else
                {
                    try
                    {
                        PlatformServices.Clipboard.SetText(info.FilePath);
                        DebugHelper.WriteLine($"CopyFilePathToClipboard: copied path {info.FilePath}");
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteException(ex, "CopyFilePathToClipboard");
                    }
                }
            }

            // ShowInExplorer
            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.ShowInExplorer))
            {
                if (string.IsNullOrEmpty(info.FilePath))
                {
                    DebugHelper.WriteLine("ShowInExplorer skipped: no file path.");
                }
                else
                {
                    try
                    {
                        PlatformServices.System.ShowFileInExplorer(info.FilePath);
                        DebugHelper.WriteLine($"ShowInExplorer: opened for {info.FilePath}");
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteException(ex, "ShowInExplorer");
                    }
                }
            }


            // AnalyzeImage
            if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.AnalyzeImage))
            {
                if (info.Metadata?.Image == null)
                {
                    DebugHelper.WriteLine("AnalyzeImage skipped: no image in metadata.");
                }
                else if (ShowAnalyzerCallback == null)
                {
                    DebugHelper.WriteLine("AnalyzeImage skipped: callback not set.");
                }
                else
                {
                    try
                    {
                        await ShowAnalyzerCallback(info.Metadata.Image);
                        DebugHelper.WriteLine("AnalyzeImage: analyzer window shown.");
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteException(ex, "AnalyzeImage");
                    }
                }
            }

            if (!annotationSidecarSaveAttempted)
            {
                editorResult?.SourceImage?.Dispose();
            }

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
                    historyItem.AnnotationSidecarPath = annotationSidecarPath;

                    var tags = info.GetTags();
                    if (tags != null)
                    {
                        historyItem.Tags = new Dictionary<string, string?>(tags.Count);
                        foreach (var pair in tags)
                        {
                            historyItem.Tags[pair.Key] = pair.Value;
                        }
                    }

                    // Screenshots uploaded by this workflow used to lose host, deletion URL and
                    // upload metadata here; record them like upload jobs do.
                    if (!string.IsNullOrWhiteSpace(historyItem.URL))
                    {
                        UploadJobProcessor.ApplyUploadResult(historyItem, info);
                    }

                    bool appended = historyManager.AppendHistoryItem(historyItem);
                    DebugHelper.WriteLine($"Trace: History pipeline - AppendHistoryItem called for: {historyItem.FileName} (URL: {historyItem.URL})");
                    if (appended)
                    {
                        info.HistoryItemId = historyItem.Id;
                        DebugHelper.WriteLine($"Added to history: {historyItem.FileName}");

                        if (!string.IsNullOrWhiteSpace(info.Metadata?.OcrText))
                        {
                            await OcrIndexingService.PersistRecognizedTextAsync(
                                historyItem,
                                info.Metadata.OcrText,
                                "after-capture-ocr",
                                NormalizeOcrLanguage(info.TaskSettings.CaptureSettings.OCROptions?.Language),
                                token);
                        }
                        else
                        {
                            OcrIndexingService.QueueIndexHistoryItem(historyItem);
                        }
                    }
                    else
                    {
                        DebugHelper.WriteLine($"Failed to append history item: {historyItem.FileName}");
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteLine($"Failed to add to history: {ex.Message}");
                    DebugHelper.WriteException(ex);
                }
            }

            return true;
        }

        private static async Task<string?> SaveAnnotationSidecarAsync(TaskInfo info, ImageEditorSessionResult? editorResult)
        {
            if (editorResult == null)
            {
                return null;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(info.FilePath) ||
                    editorResult.Annotations.Count == 0 ||
                    editorResult.SourceImage == null)
                {
                    return null;
                }

                string? sidecarPath = await XannProjectFileService.SaveAsync(
                    info.FilePath,
                    editorResult.SourceImage,
                    editorResult.Annotations);
                DebugHelper.WriteLine($"Annotation sidecar saved: {sidecarPath}");
                return sidecarPath;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to save annotation sidecar: {ex.Message}");
                DebugHelper.WriteException(ex);
                return null;
            }
            finally
            {
                editorResult.SourceImage?.Dispose();
            }
        }

        private async Task SaveImageToFileAsync(TaskInfo info)
        {
            if (info.PrivacyApproval != null) return; // Guard already saved the exact approved PNG.
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
            }
            else
            {
                DebugHelper.WriteLine("Failed to save image.");
                // info.Status = TaskStatus.Failed; // Logic to handle failure
            }

            await Task.CompletedTask;
        }

        private async Task UploadImageAsync(TaskInfo info, CancellationToken token)
        {
            if (PrivacyUploadGuard.IsRequired(info))
            {
                HandleUploadResult(info, await new UploadJobProcessor().UploadCaptureImageAsync(info, token));
                return;
            }
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

        private async Task PerformOCRAsync(TaskInfo info)
        {
            if (info.Metadata?.Image == null)
            {
                DebugHelper.WriteLine("OCR skipped: no image in metadata.");
                return;
            }

            var ocrService = PlatformServices.Ocr;
            if (ocrService == null || !ocrService.IsSupported)
            {
                DebugHelper.WriteLine($"OCR skipped: {ocrService?.UnavailableReason ?? "OCR is not supported on this platform."}");
                return;
            }

            try
            {
                DebugHelper.WriteLine("Starting OCR on captured image...");
                var taskOcrOptions = info.TaskSettings.CaptureSettings.OCROptions ?? new OCROptions();
                var options = new OcrOptions
                {
                    Language = NormalizeOcrLanguage(taskOcrOptions.Language),
                    ScaleFactor = NormalizeOcrScaleFactor(taskOcrOptions.ScaleFactor),
                    SingleLine = taskOcrOptions.SingleLine
                };
                var result = await ocrService.RecognizeAsync(info.Metadata.Image, options);

                if (result.Success && !string.IsNullOrWhiteSpace(result.Text))
                {
                    info.Metadata.OcrText = result.Text;
                    DebugHelper.WriteLine($"OCR completed. Text length: {result.Text.Length} chars.");
                }
                else
                {
                    DebugHelper.WriteLine($"OCR completed but no text recognized: {result.ErrorMessage}");
                }

                // Show OCR window so user can review/adjust the result
                if (PlatformServices.IsInitialized)
                {
                    await PlatformServices.UI.ShowOcrWindowAsync(info.Metadata.Image);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "OCR error");
            }

            await Task.CompletedTask;
        }

        private static void TryCopyOcrTextToClipboard(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                DebugHelper.WriteLine("CopyOcrTextToClipboard skipped: OCR text is empty.");
                return;
            }

            XerahS.Platform.Abstractions.IClipboardService? clipboardService;
            try
            {
                clipboardService = PlatformServices.Clipboard;
            }
            catch (InvalidOperationException)
            {
                DebugHelper.WriteLine("CopyOcrTextToClipboard skipped: clipboard service unavailable.");
                return;
            }

            if (clipboardService == null)
            {
                DebugHelper.WriteLine("CopyOcrTextToClipboard skipped: clipboard service unavailable.");
                return;
            }

            try
            {
                clipboardService.SetText(text);
                DebugHelper.WriteLine($"CopyOcrTextToClipboard: copied {text.Length} chars.");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "CopyOcrTextToClipboard");
            }
        }

        private static string NormalizeOcrLanguage(string? language)
        {
            string? trimmedLanguage = language?.Trim();
            return string.IsNullOrEmpty(trimmedLanguage) ? "en" : trimmedLanguage;
        }

        private static float NormalizeOcrScaleFactor(float scaleFactor)
        {
            return float.IsFinite(scaleFactor) ? Math.Max(scaleFactor, 1f) : 1f;
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
                    return TryUploadWithFallback(instanceManager, UploaderCategory.Image, info.FilePath, configuredInstanceId);
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
                DebugHelper.WriteLine("No default image uploader instance configured; trying available uploaders.");
                return TryUploadWithFallback(instanceManager, UploaderCategory.Image, info.FilePath, configuredInstanceId);
            }

            var primaryResult = TryUploadWithInstance(targetInstance, info.FilePath);
            if (primaryResult != null && !primaryResult.IsError && !string.IsNullOrEmpty(primaryResult.URL))
            {
                return primaryResult;
            }

            var primaryError = primaryResult?.Errors?.ToString() ?? primaryResult?.Response ?? "Unknown error";
            DebugHelper.WriteLine(
                $"Primary capture uploader '{targetInstance.DisplayName}' failed ({primaryError}). Trying fallback uploaders.");

            return TryUploadWithFallback(instanceManager, UploaderCategory.Image, info.FilePath, targetInstance.InstanceId);
        }

        /// <summary>
        /// Tries to upload using multiple instances with fallback logic.
        /// When one instance fails, it tries the next available instance.
        /// Falls back to File category uploaders if the primary category fails.
        /// </summary>
        private static UploadResult? TryUploadWithFallback(InstanceManager instanceManager, UploaderCategory category, string filePath, string? excludeInstanceId, HashSet<string>? attemptedInstanceIds = null)
        {
            attemptedInstanceIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            DebugHelper.WriteLine($"Trying uploaders with fallback for category {category}.");

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
                uploader = (Uploader)provider.CreateInstance(instance.SettingsJson);
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
        /// Maps a terminal After Capture quick action to the tasks for this run while preserving
        /// the workflow's ShowAfterCaptureWindow flag for future captures.
        /// </summary>
        internal static AfterCaptureTasks GetAfterCaptureTasksForRun(
            (AfterCaptureTasks Capture, AfterUploadTasks Upload, bool Cancel, AfterCaptureQuickAction QuickAction) result)
        {
            if (result.QuickAction == AfterCaptureQuickAction.None)
            {
                return result.Capture;
            }

            return result.Capture | AfterCaptureTasks.ShowAfterCaptureWindow;
        }

        /// <summary>
        /// Persists the "Show after capture window" setting change back to the workflow configuration.
        /// Uses synchronous save to ensure settings are written before app exit.
        /// </summary>
        private static void PersistPrivacyModeSetting(string? workflowId, bool enabled)
        {
            try
            {
                var workflow = !string.IsNullOrEmpty(workflowId) ? SettingsManager.GetWorkflowById(workflowId) : null;
                var settings = workflow?.TaskSettings ?? SettingsManager.DefaultTaskSettings;
                if (settings == null) return;
                settings.PrivacySettings.Enabled = enabled;
                SettingsManager.SaveWorkflowsConfig();
                DebugHelper.WriteLine($"Persisted PrivacySettings.Enabled={enabled} to workflow '{workflowId}'");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to persist privacy mode setting");
            }
        }

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
                        // Use synchronous save to ensure the setting is persisted immediately
                        // Async save is fire-and-forget and may not complete before app exit
                        SettingsManager.SaveWorkflowsConfig();
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

                // Use synchronous save to ensure the setting is persisted immediately
                // Async save is fire-and-forget and may not complete before app exit
                SettingsManager.SaveWorkflowsConfig();
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
    }
}
