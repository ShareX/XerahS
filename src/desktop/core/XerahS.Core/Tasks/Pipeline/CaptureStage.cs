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

using System.Diagnostics;
using System.Drawing;
using SkiaSharp;
using XerahS.Common;
using XerahS.Core.Capture;
using XerahS.Core.Helpers;
using XerahS.Core.Managers;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Core.Tasks.Pipeline
{
    /// <summary>
    /// Pipeline stage that handles the "capture" orchestration (fullscreen, region, window, etc).
    /// Extracted from the massive switch-case in WorkerTask.DoWorkAsync().
    /// </summary>
    public class CaptureStage : IPipelineStage
    {
        public string StageName => "Capture";

        /// <summary>
        /// Default delay in milliseconds after window activation before capture.
        /// Allows the window to settle after restore/activation operations.
        /// </summary>
        private const int WindowActivationDelayMs = 250;

        /// <summary>
        /// H.264/H.265 video encoders require dimensions divisible by this value.
        /// </summary>
        private const int VideoDimensionAlignment = 2;

        /// <summary>
        /// Minimum video width in pixels for recording.
        /// </summary>
        private const int MinVideoWidth = 2;

        /// <summary>
        /// Minimum video height in pixels for recording.
        /// </summary>
        private const int MinVideoHeight = 2;

        private readonly WorkerTask _workerTask;

        public CaptureStage(WorkerTask workerTask)
        {
            _workerTask = workerTask;
        }

        public async Task<PipelineStageResult> ExecuteAsync(PipelineContext context, CancellationToken token)
        {
            var taskSettings = context.Info.TaskSettings;
            var metadata = context.Info.Metadata;

            // Only capture if we don't already have an image (e.g. passed from UI)
            if (metadata!.Image != null || !PlatformServices.IsInitialized)
            {
                if (metadata.Image == null)
                {
                    // Platform services not ready - fail with clear error
                    DebugHelper.WriteLine("PlatformServices not initialized - cannot capture");
                    try
                    {
                        PlatformServices.Toast?.ShowToast(new Platform.Abstractions.ToastConfig
                        {
                            Title = "Capture Failed",
                            Text = "Platform services not ready. Please wait a moment and try again.",
                            Duration = 4f,
                            Size = new SizeI(400, 120),
                            AutoHide = true,
                            LeftClickAction = Platform.Abstractions.ToastClickAction.CloseNotification
                        });
                    }
                    catch
                    {
                        // Ignore toast errors if platform not ready
                    }

                    context.Status = TaskStatus.Failed;
                    context.Error = new InvalidOperationException("Platform services not initialized");
                    return PipelineStageResult.Failed;
                }

                return PipelineStageResult.Continue; // proceed to finalization
            }

            XerahS.Common.TroubleshootingHelper.Log(taskSettings!.Job.ToString(), "WORKER_TASK", "Entering capture phase (pipeline)");

            SKBitmap? image = null;
            var captureStopwatch = Stopwatch.StartNew();
            DebugHelper.WriteLine($"Capture start: Job={taskSettings.Job}");

            // Create capture options from task settings
            taskSettings.CaptureSettings ??= new TaskSettingsCapture();
            var captureSettings = taskSettings.CaptureSettings;

            var captureDelaySeconds = TaskHelpers.GetCaptureStartDelaySeconds(taskSettings, out var workflowCategory);
            var isScreenCaptureDelay = workflowCategory == EnumExtensions.WorkflowType_Category_ScreenCapture && captureDelaySeconds > 0;
            var isScreenRecordDelay = workflowCategory == EnumExtensions.WorkflowType_Category_ScreenRecord && captureDelaySeconds > 0;

            if (workflowCategory is EnumExtensions.WorkflowType_Category_ScreenCapture or EnumExtensions.WorkflowType_Category_ScreenRecord)
            {
                CaptureWindowMetadata(metadata, PlatformServices.Window);
            }

            var useTransparentOverlay = ShouldUseTransparentOverlay(taskSettings.Job);
            var linuxRegionSelectorPreference = ResolveLinuxRegionSelectorPreference(captureSettings);
            var macOSRegionSelectorPreference = ResolveMacOSRegionSelectorPreference(captureSettings);

            var captureOptions = new CaptureOptions
            {
                UseModernCapture = captureSettings.UseModernCapture,
                HDRScreenshotColorCorrection = captureSettings.HDRScreenshotColorCorrection,
                LinuxRegionSelectorPreference = linuxRegionSelectorPreference,
                MacOSRegionSelectorPreference = macOSRegionSelectorPreference,
                MacOSPlayCaptureSound = captureSettings.MacOSPlayCaptureSound,
                ShowCursor = captureSettings.ShowCursor,
                UseTransparentOverlay = useTransparentOverlay,
                CaptureShadow = captureSettings.CaptureShadow,
                CaptureClientArea = captureSettings.CaptureClientArea,
                WorkflowId = taskSettings.WorkflowId,
                WorkflowCategory = workflowCategory
            };

            // OmaSnap native capture on Hyprland (XIP0088). Returns null when the job keeps its normal
            // path; a failure falls through to the existing chain with the engine skipped.
            PipelineStageResult? hostedResult = await TryHostedCaptureAsync(
                context, taskSettings, captureSettings, captureOptions, linuxRegionSelectorPreference,
                isScreenCaptureDelay, captureDelaySeconds, workflowCategory, captureStopwatch, token);
            if (hostedResult.HasValue)
            {
                return hostedResult.Value;
            }

            // Upload Content queues its items as ClipboardUploadWithContentViewer tasks with the content preloaded;
            // those must upload, not reopen the Upload Content window.
            if (WorkflowCatalog.IsToolWorkflow(taskSettings.Job) && !HasPreloadedUploadContent(context))
            {
                await _workerTask.HandleToolWorkflowAsync(token);
                return PipelineStageResult.Stop;
            }

            switch (taskSettings.Job)
            {
                case WorkflowType.ClipboardUpload:
                    if (PlatformServices.Clipboard == null)
                    {
                        context.Status = TaskStatus.Failed;
                        context.Error = new Exception("Clipboard service is not available.");
                        return PipelineStageResult.Failed;
                    }

                    if (!_workerTask.TryLoadClipboardContent(taskSettings, metadata, out var clipboardFiles))
                    {
                        context.Status = TaskStatus.Failed;
                        context.Error = new Exception("Clipboard is empty or contains unsupported data.");
                        return PipelineStageResult.Failed;
                    }

                    if (clipboardFiles != null && clipboardFiles.Length > 1)
                    {
                        await _workerTask.UploadClipboardFilesAsync(taskSettings, clipboardFiles, token);
                        return PipelineStageResult.Stop;
                    }
                    break;

                case WorkflowType.ClipboardUploadWithContentViewer:
                    if (HasPreloadedUploadContent(context))
                    {
                        DebugHelper.WriteLine("ClipboardUploadWithContentViewer: preloaded content detected, bypassing tool callback.");
                        break;
                    }

                    await _workerTask.HandleToolWorkflowAsync(token);
                    return PipelineStageResult.Stop;

                case WorkflowType.PrintScreen:
                    if (isScreenCaptureDelay && !await _workerTask.ApplyCaptureStartDelayAsync(taskSettings, workflowCategory, captureDelaySeconds, token))
                    {
                        return PipelineStageResult.Stop;
                    }
                    image = await PlatformServices.ScreenCapture.CaptureFullScreenAsync(captureOptions);
                    break;

                case WorkflowType.RectangleTransparent:
                case WorkflowType.RectangleRegion:
                    if (isScreenCaptureDelay)
                    {
                        captureOptions.CaptureStartDelaySeconds = captureDelaySeconds;
                        captureOptions.CaptureStartDelayCancellationToken = token;
                    }
                    image = await PlatformServices.ScreenCapture.CaptureRegionAsync(captureOptions);
                    break;

                case WorkflowType.ActiveWindow:
                    if (isScreenCaptureDelay && !await _workerTask.ApplyCaptureStartDelayAsync(taskSettings, workflowCategory, captureDelaySeconds, token))
                    {
                        return PipelineStageResult.Stop;
                    }
                    if (PlatformServices.Window != null)
                    {
                        CaptureWindowMetadata(metadata, PlatformServices.Window);
                        image = await PlatformServices.ScreenCapture.CaptureActiveWindowAsync(PlatformServices.Window, captureOptions);
                    }
                    break;

                case WorkflowType.FileUpload:
                    if (string.IsNullOrEmpty(context.Info.FilePath) && WorkerTask.ShowOpenFileDialogCallback != null)
                    {
                        XerahS.Common.TroubleshootingHelper.Log(taskSettings.Job.ToString(), "UI", "Requesting file from user via dialog...");
                        var selectedFile = await WorkerTask.ShowOpenFileDialogCallback();
                        
                        if (!string.IsNullOrEmpty(selectedFile))
                        {
                            XerahS.Common.TroubleshootingHelper.Log(taskSettings.Job.ToString(), "UI", $"User selected file: {selectedFile}");
                            context.Info.FilePath = selectedFile;
                            context.Info.DataType = EDataType.File;
                        }
                        else
                        {
                            XerahS.Common.TroubleshootingHelper.Log(taskSettings.Job.ToString(), "UI", "User cancelled file selection");
                            context.Status = TaskStatus.Stopped;
                            return PipelineStageResult.Stop;
                        }
                    }
                    else if (!string.IsNullOrEmpty(context.Info.FilePath))
                    {
                         context.Info.DataType = EDataType.File;
                    }
                    else
                    {
                        DebugHelper.WriteLine("FileUpload job started but no file provided and no dialog callback available.");
                        context.Status = TaskStatus.Failed;
                        context.Error = new Exception("No file selected and dialog unavailable");
                        return PipelineStageResult.Failed;
                    }
                    context.Info.Job = TaskJob.FileUpload;
                    break;

                case WorkflowType.IndexFolder:
                    if (!_workerTask.TryIndexFolder(taskSettings, out string? indexPath))
                    {
                        context.Status = TaskStatus.Failed;
                        context.Error = new Exception("Index folder path is invalid or indexing failed.");
                        return PipelineStageResult.Failed;
                    }

                    context.Info.FilePath = indexPath ?? "";
                    context.Info.DataType = EDataType.File;
                    context.Info.Job = TaskJob.FileUpload;
                    break;

                case WorkflowType.CustomWindow:
                    if (PlatformServices.Window != null)
                    {
                        string targetWindow = captureSettings.CaptureCustomWindow;
                        if (string.IsNullOrEmpty(targetWindow))
                        {
                            if (WorkerTask.ShowWindowSelectorCallback != null)
                            {
                                var selectedWindow = await WorkerTask.ShowWindowSelectorCallback();
                                if (selectedWindow != null)
                                {
                                    if (PlatformServices.Window.IsWindowMinimized(selectedWindow.Handle))
                                    {
                                        PlatformServices.Window.ShowWindow(selectedWindow.Handle, 9); // SW_RESTORE = 9
                                        await Task.Delay(WindowActivationDelayMs, token);
                                    }

                                    PlatformServices.Window.ActivateWindow(selectedWindow.Handle);
                                    await Task.Delay(WindowActivationDelayMs, token); // Increased delay for activation to settle

                                    if (isScreenCaptureDelay && !await _workerTask.ApplyCaptureStartDelayAsync(taskSettings!, workflowCategory, captureDelaySeconds, token))
                                    {
                                        return PipelineStageResult.Stop;
                                    }

                                    CaptureWindowMetadata(metadata, PlatformServices.Window, selectedWindow.Handle);
                                    image = await PlatformServices.ScreenCapture.CaptureActiveWindowAsync(PlatformServices.Window, captureOptions);
                                }
                                else
                                {
                                    DebugHelper.WriteLine("Custom window capture cancelled by user");
                                }
                            }
                        }
                        else
                        {
                            IntPtr hWnd = PlatformServices.Window.SearchWindow(targetWindow);

                            if (hWnd != IntPtr.Zero)
                            {
                                if (PlatformServices.Window.IsWindowMinimized(hWnd))
                                {
                                    PlatformServices.Window.ShowWindow(hWnd, 9); // SW_RESTORE = 9
                                    await Task.Delay(WindowActivationDelayMs, token);
                                }

                                if (isScreenCaptureDelay && !await _workerTask.ApplyCaptureStartDelayAsync(taskSettings!, workflowCategory, captureDelaySeconds, token))
                                {
                                    return PipelineStageResult.Stop;
                                }

                                CaptureWindowMetadata(metadata, PlatformServices.Window, hWnd);
                                image = await PlatformServices.ScreenCapture.CaptureWindowAsync(hWnd, PlatformServices.Window, captureOptions);
                            }
                        }
                    }
                    break;

                // Screen Recording Workflow Cases (Extracted for brevity, calling internal helpers)
                case WorkflowType.ScreenRecorder:
                case WorkflowType.ScreenRecorderGIF:
                    await HandleScreenRecorderRegionAsync(context, captureOptions, isScreenRecordDelay, captureDelaySeconds, workflowCategory, token);
                    return PipelineStageResult.Stop;

                case WorkflowType.StartScreenRecorder:
                case WorkflowType.StartScreenRecorderGIF:
                    await HandleScreenRecorderLastRegionAsync(context, isScreenRecordDelay, captureDelaySeconds, workflowCategory, token);
                    return PipelineStageResult.Stop;

                case WorkflowType.ScreenRecorderActiveWindow:
                case WorkflowType.ScreenRecorderGIFActiveWindow:
                    await HandleScreenRecorderWindowAsync(context, isScreenRecordDelay, captureDelaySeconds, workflowCategory, token);
                    return PipelineStageResult.Stop;

                case WorkflowType.ScreenRecorderCustomRegion:
                case WorkflowType.ScreenRecorderGIFCustomRegion:
                    await HandleScreenRecorderCustomRegionAsync(context, captureOptions, isScreenRecordDelay, captureDelaySeconds, workflowCategory, token);
                    return PipelineStageResult.Stop;

                case WorkflowType.StopScreenRecording:
                    await _workerTask.HandleStopRecordingAsync();
                    return PipelineStageResult.Stop;

                case WorkflowType.PauseScreenRecording:
                    await _workerTask.HandlePauseRecordingAsync();
                    return PipelineStageResult.Stop;

                case WorkflowType.AbortScreenRecording:
                    await _workerTask.HandleAbortRecordingAsync();
                    return PipelineStageResult.Stop;

                // Quick-win capture workflows
                case WorkflowType.ActiveMonitor:
                    if (isScreenCaptureDelay && !await _workerTask.ApplyCaptureStartDelayAsync(taskSettings!, workflowCategory, captureDelaySeconds, token))
                    {
                        return PipelineStageResult.Stop;
                    }
                    var activeScreenBounds = PlatformServices.Screen.GetActiveScreenBounds();
                    image = await PlatformServices.ScreenCapture.CaptureRectAsync(
                        new SKRect(activeScreenBounds.X, activeScreenBounds.Y,
                            activeScreenBounds.Right, activeScreenBounds.Bottom),
                        captureOptions);
                    break;

                case WorkflowType.CustomRegion:
                    if (isScreenCaptureDelay && !await _workerTask.ApplyCaptureStartDelayAsync(taskSettings!, workflowCategory, captureDelaySeconds, token))
                    {
                        return PipelineStageResult.Stop;
                    }
                    var customRect = taskSettings!.CaptureSettings.CaptureCustomRegion;
                    if (TryCreateConfiguredCaptureRect(customRect, out var customCaptureRect))
                    {
                        image = await PlatformServices.ScreenCapture.CaptureRectAsync(customCaptureRect, captureOptions);
                    }
                    break;

                case WorkflowType.LastRegion:
                    if (isScreenCaptureDelay && !await _workerTask.ApplyCaptureStartDelayAsync(taskSettings!, workflowCategory, captureDelaySeconds, token))
                    {
                        return PipelineStageResult.Stop;
                    }
                    if (LastRegionStore.TryGet(out var lastRegionRect) &&
                        TryCreateConfiguredCaptureRect(lastRegionRect, out var lastRegionCaptureRect))
                    {
                        image = await PlatformServices.ScreenCapture.CaptureRectAsync(lastRegionCaptureRect, captureOptions);
                    }
                    break;

                // Quick-win "Other" workflows
                case WorkflowType.OpenScreenshotsFolder:
                    var screenshotsDir = TaskHelpers.GetScreenshotsFolder(taskSettings);
                    try
                    {
                        Directory.CreateDirectory(screenshotsDir);
                        PlatformServices.System.OpenFile(screenshotsDir);
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteException(ex, $"Failed to open screenshots folder: {screenshotsDir}");
                    }
                    return PipelineStageResult.Stop;

                case WorkflowType.OpenHistory:
                case WorkflowType.OpenImageHistory:
                    WorkerTask.OpenHistoryCallback?.Invoke(taskSettings.Job);
                    return PipelineStageResult.Stop;

                case WorkflowType.OpenMainWindow:
                    WorkerTask.OpenMainWindowCallback?.Invoke();
                    return PipelineStageResult.Stop;

                case WorkflowType.ExitShareX:
                    WorkerTask.ExitApplicationCallback?.Invoke();
                    return PipelineStageResult.Stop;

                case WorkflowType.DisableHotkeys:
                    WorkerTask.ToggleHotkeysCallback?.Invoke();
                    return PipelineStageResult.Stop;
            }

            return FinishCapture(context, image, captureStopwatch);
        }

        internal static void CaptureWindowMetadata(TaskMetadata metadata, IWindowService? windows, IntPtr windowHandle = default)
        {
            if (windows == null) return;
            try
            {
                IntPtr handle = windowHandle != IntPtr.Zero ? windowHandle : windows.GetForegroundWindow();
                if (handle == IntPtr.Zero) return;
                metadata.UpdateInfo(new WindowInfo
                {
                    Handle = handle,
                    Title = windows.GetWindowText(handle),
                    ProcessId = windows.GetWindowProcessId(handle)
                });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException or ArgumentException)
            {
                DebugHelper.WriteLine("Capture source window metadata is unavailable.");
            }
        }

        internal static bool HasPreloadedUploadContent(PipelineContext context)
        {
            return context.Info.TaskSettings?.Job == WorkflowType.ClipboardUploadWithContentViewer &&
                (context.Info.Metadata?.Image != null ||
                 !string.IsNullOrEmpty(context.Info.TextContent) ||
                 !string.IsNullOrEmpty(context.Info.FilePath));
        }

        private static PipelineStageResult FinishCapture(PipelineContext context, SKBitmap? image, Stopwatch captureStopwatch)
        {
            var taskSettings = context.Info.TaskSettings;
            var metadata = context.Info.Metadata!;
            captureStopwatch.Stop();

            bool hasClipboardPayload = taskSettings?.Job is WorkflowType.ClipboardUpload or WorkflowType.ClipboardUploadWithContentViewer
                && (metadata.Image != null || !string.IsNullOrEmpty(context.Info.TextContent) || !string.IsNullOrEmpty(context.Info.FilePath));

            if (image != null)
            {
                metadata.Image = image;
                DebugHelper.WriteLine($"Captured image: {image.Width}x{image.Height} in {captureStopwatch.ElapsedMilliseconds}ms");
            }
            else if (hasClipboardPayload)
            {
                DebugHelper.WriteLine($"Clipboard content loaded: dataType={context.Info.DataType}, filePath=\"{context.Info.FilePath}\", textLength={(context.Info.TextContent?.Length ?? 0)}");
            }
            else if ((taskSettings?.Job == WorkflowType.FileUpload || taskSettings?.Job == WorkflowType.IndexFolder) &&
                     !string.IsNullOrEmpty(context.Info.FilePath))
            {
                DebugHelper.WriteLine($"FileUpload selected file: {context.Info.FilePath}");
            }
            else if (!string.IsNullOrEmpty(context.Info.TextContent) && context.Info.Job == TaskJob.TextUpload)
            {
                DebugHelper.WriteLine($"Text content pre-loaded: textLength={context.Info.TextContent.Length}");
            }
            else
            {
                DebugHelper.WriteLine($"Capture returned null for job type: {taskSettings?.Job} (elapsed {captureStopwatch.ElapsedMilliseconds}ms)");
                
                context.Status = TaskStatus.Stopped;
                return PipelineStageResult.Stop;
            }

            return PipelineStageResult.Continue;
        }

        /// <summary>
        /// Runs the hosted capture engine for jobs it maps. Returns the stage result when the engine
        /// handled the job (captured or cancelled), or null to continue with the normal capture path.
        /// </summary>
        private async Task<PipelineStageResult?> TryHostedCaptureAsync(
            PipelineContext context,
            TaskSettings taskSettings,
            TaskSettingsCapture captureSettings,
            CaptureOptions captureOptions,
            LinuxInteractiveRegionSelectorPreference preference,
            bool isScreenCaptureDelay,
            double captureDelaySeconds,
            string workflowCategory,
            Stopwatch captureStopwatch,
            CancellationToken token)
        {
            IHostedCaptureEngine? engine = PlatformServices.HostedCaptureEngine;
            if (!OperatingSystem.IsLinux() || engine == null)
            {
                return null;
            }

            Rectangle? lastRegion = LastRegionStore.TryGet(out var last) ? last : null;
            HostedCaptureRequest? request = HostedCaptureWorkflowMapper.Map(
                taskSettings.Job,
                captureSettings.OmaSnapRegionOnly,
                captureSettings.CaptureCustomRegion,
                captureSettings.CaptureCustomWindow,
                lastRegion);
            if (request == null)
            {
                return null;
            }

            HostedCaptureEngineStatus status = await engine.GetStatusAsync(token).ConfigureAwait(false);
            if (!HostedCaptureWorkflowMapper.ShouldUseEngine(preference, status, captureOptions.LinuxSkipHostedCaptureEngine))
            {
                if (preference == LinuxInteractiveRegionSelectorPreference.OmaSnap)
                {
                    DebugHelper.WriteLine($"CaptureStage: OmaSnap selected but unavailable ({status.Summary}); using the existing capture chain.");
                }

                return null;
            }

            if (isScreenCaptureDelay && !await _workerTask.ApplyCaptureStartDelayAsync(taskSettings, workflowCategory, captureDelaySeconds, token))
            {
                return PipelineStageResult.Stop;
            }

            DebugHelper.WriteLine($"CaptureStage: {taskSettings.Job} via {engine.EngineId} (target={request.Target}).");
            HostedCaptureResult result = await engine.CaptureAsync(request, token).ConfigureAwait(false);

            if (result.Status == HostedCaptureStatus.Cancelled)
            {
                DebugHelper.WriteLine($"CaptureStage: {engine.EngineId} capture cancelled by the user.");
                context.Status = TaskStatus.Stopped;
                return PipelineStageResult.Stop;
            }

            SKBitmap? image = null;
            try
            {
                if (result.IsOk)
                {
                    image = SKBitmap.Decode(result.ImagePath);
                }
            }
            finally
            {
                engine.Release(result);
            }

            if (image == null)
            {
                // Failure (not cancel): fall through to the existing chain and keep the engine out of it.
                DebugHelper.WriteLine($"CaptureStage: {engine.EngineId} {result.Status}: {result.Error ?? "no image"}; falling back to the existing capture chain.");
                captureOptions.LinuxSkipHostedCaptureEngine = true;
                return null;
            }

            var metadata = context.Info.Metadata!;
            if (!string.IsNullOrWhiteSpace(result.WindowTitle))
            {
                metadata.WindowTitle = result.WindowTitle;
            }

            if (!string.IsNullOrWhiteSpace(result.WindowClass))
            {
                metadata.ProcessName = result.WindowClass;
            }

            if (result.Region is { Width: > 0, Height: > 0 } region &&
                request.Target is HostedCaptureTarget.Smart or HostedCaptureTarget.Region or HostedCaptureTarget.Window)
            {
                LastRegionStore.Set(region);
            }

            return FinishCapture(context, image, captureStopwatch);
        }

        private async Task HandleScreenRecorderRegionAsync(PipelineContext context, CaptureOptions captureOptions, bool isDelay, double delay, string category, CancellationToken token)
        {
            if (context.Info.Metadata.Image != null)
            {
                context.Info.Metadata.Image.Dispose();
                context.Info.Metadata.Image = null;
            }

            bool isLinuxWayland = OperatingSystem.IsLinux() &&
                Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")?.Equals("wayland", StringComparison.OrdinalIgnoreCase) == true;

            bool portalHandlesSourceSelection = isLinuxWayland &&
                ScreenRecorderService.NativeRecordingServiceFactory != null;

            if (portalHandlesSourceSelection)
            {
                if (isDelay && !await _workerTask.ApplyCaptureStartDelayAsync(context.Info.TaskSettings!, category, delay, token))
                    return;

                await _workerTask.HandleStartRecordingAsync(CaptureMode.Screen);
                return;
            }

            SKRectI selection;

            if (isLinuxWayland)
            {
                var slurpResult = await _workerTask.SelectRegionWithSlurpAsync();
                selection = slurpResult.Region;

                if (slurpResult.WasCancelled) { }
                else if (selection.IsEmpty || selection.Width <= 0 || selection.Height <= 0)
                {
                    selection = await PlatformServices.ScreenCapture.SelectRegionAsync(captureOptions);
                }
            }
            else
            {
                selection = await PlatformServices.ScreenCapture.SelectRegionAsync(captureOptions);
            }

            if (selection.IsEmpty || selection.Width <= 0 || selection.Height <= 0)
            {
                context.Status = TaskStatus.Stopped;
                return;
            }

            int adjustedWidth = selection.Width - (selection.Width % VideoDimensionAlignment);
            int adjustedHeight = selection.Height - (selection.Height % VideoDimensionAlignment);

            if (adjustedWidth < MinVideoWidth || adjustedHeight < MinVideoHeight)
            {
                context.Status = TaskStatus.Stopped;
                return;
            }

            var recordingRegion = new Rectangle(selection.Left, selection.Top, adjustedWidth, adjustedHeight);

            if (isDelay && !await _workerTask.ApplyCaptureStartDelayAsync(context.Info.TaskSettings!, category, delay, token))
                return;

            await _workerTask.HandleStartRecordingAsync(CaptureMode.Region, region: recordingRegion);
        }

        private async Task HandleScreenRecorderWindowAsync(PipelineContext context, bool isDelay, double delay, string category, CancellationToken token)
        {
            if (context.Info.Metadata.Image != null)
            {
                context.Info.Metadata.Image.Dispose();
                context.Info.Metadata.Image = null;
            }

            if (PlatformServices.Window != null)
            {
                var foregroundWindow = PlatformServices.Window.GetForegroundWindow();
                CaptureWindowMetadata(context.Info.Metadata, PlatformServices.Window, foregroundWindow);
                if (isDelay && !await _workerTask.ApplyCaptureStartDelayAsync(context.Info.TaskSettings!, category, delay, token))
                    return;
                await _workerTask.HandleStartRecordingAsync(CaptureMode.Window, foregroundWindow);
            }
        }

        private async Task HandleScreenRecorderCustomRegionAsync(
            PipelineContext context,
            CaptureOptions captureOptions,
            bool isDelay,
            double delay,
            string category,
            CancellationToken token)
        {
            if (context.Info.Metadata.Image != null)
            {
                context.Info.Metadata.Image.Dispose();
                context.Info.Metadata.Image = null;
            }

            var captureSettings = context.Info.TaskSettings!.CaptureSettings;
            var configuredRegion = captureSettings.CaptureCustomRegion;
            if (configuredRegion.IsEmpty || configuredRegion.Width <= 0 || configuredRegion.Height <= 0)
            {
                var selectedRegion = await PlatformServices.ScreenCapture.SelectRegionAsync(captureOptions);
                if (selectedRegion.IsEmpty || selectedRegion.Width <= 0 || selectedRegion.Height <= 0)
                {
                    context.Status = TaskStatus.Stopped;
                    return;
                }

                configuredRegion = new Rectangle(
                    selectedRegion.Left,
                    selectedRegion.Top,
                    selectedRegion.Width,
                    selectedRegion.Height);

                captureSettings.CaptureCustomRegion = configuredRegion;
            }

            int customAdjustedWidth = configuredRegion.Width - (configuredRegion.Width % VideoDimensionAlignment);
            int customAdjustedHeight = configuredRegion.Height - (configuredRegion.Height % VideoDimensionAlignment);

            if (customAdjustedWidth < MinVideoWidth || customAdjustedHeight < MinVideoHeight)
            {
                context.Status = TaskStatus.Stopped;
                return;
            }

            var configuredRecordingRegion = new Rectangle(
                configuredRegion.X,
                configuredRegion.Y,
                customAdjustedWidth,
                customAdjustedHeight);

            if (isDelay && !await _workerTask.ApplyCaptureStartDelayAsync(context.Info.TaskSettings!, category, delay, token))
                return;

            await _workerTask.HandleStartRecordingAsync(CaptureMode.Region, region: configuredRecordingRegion);
        }

        private async Task HandleScreenRecorderLastRegionAsync(
            PipelineContext context,
            bool isDelay,
            double delay,
            string category,
            CancellationToken token)
        {
            if (context.Info.Metadata.Image != null)
            {
                context.Info.Metadata.Image.Dispose();
                context.Info.Metadata.Image = null;
            }

            var lastRegion = context.Info.TaskSettings!.CaptureSettings.CaptureCustomRegion;
            if (lastRegion.IsEmpty || lastRegion.Width <= 0 || lastRegion.Height <= 0)
            {
                context.Status = TaskStatus.Stopped;
                return;
            }

            int adjustedWidth = lastRegion.Width - (lastRegion.Width % VideoDimensionAlignment);
            int adjustedHeight = lastRegion.Height - (lastRegion.Height % VideoDimensionAlignment);

            if (adjustedWidth < MinVideoWidth || adjustedHeight < MinVideoHeight)
            {
                context.Status = TaskStatus.Stopped;
                return;
            }

            var recordingRegion = new Rectangle(lastRegion.X, lastRegion.Y, adjustedWidth, adjustedHeight);

            if (isDelay && !await _workerTask.ApplyCaptureStartDelayAsync(context.Info.TaskSettings!, category, delay, token))
                return;

            await _workerTask.HandleStartRecordingAsync(CaptureMode.Region, region: recordingRegion);
        }

        internal static bool TryCreateConfiguredCaptureRect(Rectangle configuredRegion, out SKRect captureRect)
        {
            captureRect = default;

            if (configuredRegion.IsEmpty || configuredRegion.Width <= 0 || configuredRegion.Height <= 0)
            {
                return false;
            }

            long right = (long)configuredRegion.X + configuredRegion.Width;
            long bottom = (long)configuredRegion.Y + configuredRegion.Height;

            if (right > int.MaxValue || bottom > int.MaxValue)
            {
                return false;
            }

            captureRect = new SKRect(configuredRegion.X, configuredRegion.Y, right, bottom);
            return true;
        }

        private static LinuxInteractiveRegionSelectorPreference ResolveLinuxRegionSelectorPreference(TaskSettingsCapture captureSettings)
        {
            var taskPreference = captureSettings.LinuxRegionSelectorPreference;
            var defaultPreference = SettingsManager.DefaultTaskSettings.CaptureSettings?.LinuxRegionSelectorPreference ?? taskPreference;

            if (!OperatingSystem.IsLinux())
            {
                return taskPreference;
            }

            bool shouldUseDefaultPreference = ShouldUseDefaultLinuxRegionSelectorPreferenceForDesktop();
            DebugHelper.WriteLine(
                $"CaptureStage: Linux selector preference source={(shouldUseDefaultPreference ? "default task settings" : "task settings")} (task={taskPreference}, default={defaultPreference}).");

            return shouldUseDefaultPreference ? defaultPreference : taskPreference;
        }

        private static MacOSInteractiveRegionSelectorPreference ResolveMacOSRegionSelectorPreference(TaskSettingsCapture captureSettings)
        {
            var taskPreference = captureSettings.MacOSRegionSelectorPreference;
            var defaultPreference = SettingsManager.DefaultTaskSettings.CaptureSettings?.MacOSRegionSelectorPreference ??
                MacOSInteractiveRegionSelectorPreference.Automatic;

            if (!OperatingSystem.IsMacOS())
            {
                return taskPreference;
            }

            var effectivePreference = taskPreference == MacOSInteractiveRegionSelectorPreference.Automatic
                ? defaultPreference
                : taskPreference;

            if (effectivePreference == MacOSInteractiveRegionSelectorPreference.Automatic)
            {
                effectivePreference = MacOSInteractiveRegionSelectorPreference.XerahSOverlay;
            }

            DebugHelper.WriteLine(
                $"CaptureStage: macOS selector preference source={(taskPreference == MacOSInteractiveRegionSelectorPreference.Automatic ? "default task settings" : "task settings")} (task={taskPreference}, default={defaultPreference}, effective={effectivePreference}).");

            return effectivePreference;
        }

        private static bool ShouldUseTransparentOverlay(WorkflowType workflowType)
        {
            if (workflowType == WorkflowType.RectangleTransparent)
            {
                return true;
            }

            if (workflowType != WorkflowType.RectangleRegion)
            {
                return false;
            }

            if (!OperatingSystem.IsLinux())
            {
                return false;
            }

            if (RequiresLinuxTransparentOverlayForMixedDpi())
            {
                DebugHelper.WriteLine(
                    $"CaptureStage: forcing UseTransparentOverlay=true for workflow '{workflowType}' on Fedora GNOME mixed-DPI path.");
                return true;
            }

            return false;
        }

        private static bool RequiresLinuxTransparentOverlayForMixedDpi()
        {
            string? distroId = TryGetLinuxDistroId();
            if (!string.Equals(distroId, "fedora", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] desktopHints =
            {
                Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? string.Empty,
                Environment.GetEnvironmentVariable("XDG_SESSION_DESKTOP") ?? string.Empty,
                Environment.GetEnvironmentVariable("DESKTOP_SESSION") ?? string.Empty
            };

            foreach (string hint in desktopHints)
            {
                foreach (string token in hint.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    string normalized = token.ToUpperInvariant();
                    if (normalized.Contains("GNOME", StringComparison.Ordinal) ||
                        normalized.Contains("UBUNTU", StringComparison.Ordinal) ||
                        normalized.Contains("UNITY", StringComparison.Ordinal) ||
                        normalized.Contains("BUDGIE", StringComparison.Ordinal) ||
                        normalized.Contains("PANTHEON", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string? TryGetLinuxDistroId()
        {
            const string osReleasePath = "/etc/os-release";
            if (!File.Exists(osReleasePath))
            {
                return null;
            }

            try
            {
                foreach (string rawLine in File.ReadLines(osReleasePath))
                {
                    if (!rawLine.StartsWith("ID=", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string value = rawLine["ID=".Length..].Trim().Trim('"', '\'');
                    return value;
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"CaptureStage: unable to parse {osReleasePath}: {ex.Message}");
            }

            return null;
        }

        private static bool ShouldUseDefaultLinuxRegionSelectorPreferenceForDesktop()
        {
            string[] hints =
            {
                Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? string.Empty,
                Environment.GetEnvironmentVariable("XDG_SESSION_DESKTOP") ?? string.Empty,
                Environment.GetEnvironmentVariable("DESKTOP_SESSION") ?? string.Empty
            };

            foreach (string hint in hints)
            {
                foreach (string token in hint.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    string normalized = token.ToUpperInvariant();

                    if (normalized.Contains("KDE", StringComparison.Ordinal) || normalized.Contains("PLASMA", StringComparison.Ordinal))
                    {
                        return true;
                    }

                    if (normalized.Contains("GNOME", StringComparison.Ordinal) ||
                        normalized.Contains("UBUNTU", StringComparison.Ordinal) ||
                        normalized.Contains("UNITY", StringComparison.Ordinal) ||
                        normalized.Contains("BUDGIE", StringComparison.Ordinal) ||
                        normalized.Contains("PANTHEON", StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return false;
        }
    }
}
