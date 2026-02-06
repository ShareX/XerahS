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
using XerahS.Core.Helpers;
using XerahS.Core.Managers;
using XerahS.Core.Tasks.Processors;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture.ScreenRecording;
using SkiaSharp;
using System.Diagnostics;
using System.Linq;
using XerahS.History;
using Avalonia.Threading;
using System.Drawing;
using XerahS.Media;

namespace XerahS.Core.Tasks
{
    public partial class WorkerTask : IDisposable
    {
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

        public TaskInfo Info { get; private set; }
        public TaskStatus Status { get; private set; }
        public Exception? Error { get; private set; }
        public bool IsBusy => Status == TaskStatus.InQueue || IsWorking;
        public bool IsWorking => Status == TaskStatus.Preparing || Status == TaskStatus.Working || Status == TaskStatus.Stopping;

        /// <summary>
        /// Determines if the task completed successfully with a valid result.
        /// Returns true only if the task is not failed/canceled/stopped AND produced an artifact (Image, File, or URL).
        /// </summary>
        public bool IsSuccessful
        {
            get
            {
                if (Status == TaskStatus.Failed || Status == TaskStatus.Canceled || Status == TaskStatus.Stopped)
                    return false;

                // Check if we have any valid output
                bool hasImage = Info.Metadata?.Image != null;
                bool hasFile = !string.IsNullOrEmpty(Info.FilePath);
                bool hasUrl = !string.IsNullOrEmpty(Info.Metadata?.UploadURL);

                return hasImage || hasFile || hasUrl;
            }
        }

        private CancellationTokenSource _cancellationTokenSource;

        public event EventHandler? StatusChanged;
        public event EventHandler? TaskCompleted;

        /// <summary>
        /// Delegate to show window selector when CustomWindow capture has no target configured.
        /// Returns selected window or null if cancelled.
        /// </summary>
        public static Func<Task<XerahS.Platform.Abstractions.WindowInfo?>>? ShowWindowSelectorCallback { get; set; }

        /// <summary>
        /// Delegate to show open file dialog for FileUpload jobs.
        /// Returns selected file path or null if cancelled.
        /// </summary>
        public static Func<Task<string?>>? ShowOpenFileDialogCallback { get; set; }

        private WorkerTask(TaskSettings taskSettings, SKBitmap? inputImage = null)
        {
            Status = TaskStatus.InQueue;
            Info = new TaskInfo(taskSettings);
            if (inputImage != null)
            {
                Info.Metadata.Image = inputImage;
                Info.DataType = EDataType.Image;
            }
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public static WorkerTask Create(TaskSettings taskSettings, SKBitmap? inputImage = null)
        {
            return new WorkerTask(taskSettings, inputImage);
        }

        public async Task StartAsync()
        {
            if (Status != TaskStatus.InQueue) return;

            Info.TaskStartTime = DateTime.Now;
            DebugHelper.WriteLine($"Task started: Job={Info.TaskSettings.Job}");
            Status = TaskStatus.Preparing;
            OnStatusChanged();

            try
            {
                await Task.Run(() => DoWorkAsync(_cancellationTokenSource.Token));
            }
            catch (OperationCanceledException)
            {
                Status = TaskStatus.Stopped;
            }
            catch (Exception ex)
            {
                Status = TaskStatus.Failed;
                Error = ex;
                DebugHelper.WriteLine($"Task failed: {ex.Message}");

                // Show error toast to user for any task failure
                try
                {
                    var errorMessage = ex.InnerException?.Message ?? ex.Message;
                    if (errorMessage.Length > 150)
                    {
                        // Truncate at word boundary to avoid cutting mid-word
                        int cutoff = errorMessage.LastIndexOf(' ', 147);
                        if (cutoff <= 0) cutoff = 147; // Fallback if no space found
                        errorMessage = errorMessage.Substring(0, cutoff) + "...";
                    }

                    PlatformServices.Toast?.ShowToast(new Platform.Abstractions.ToastConfig
                    {
                        Title = $"{Info.TaskSettings.Job} Failed",
                        Text = errorMessage,
                        Duration = 5f,
                        Size = new SizeI(400, 120),
                        AutoHide = true,
                        LeftClickAction = Platform.Abstractions.ToastClickAction.CloseNotification
                    });
                }
                catch
                {
                    // Ignore toast errors
                }
            }
            finally
            {
                if (Status != TaskStatus.Failed && Status != TaskStatus.Stopped)
                {
                    Status = TaskStatus.Completed;
                }

                OnTaskCompleted();
                OnStatusChanged();
            }
        }

        private async Task DoWorkAsync(CancellationToken token)
        {
            // Ensure critical context is not null for the remainder of this task
            Info.TaskSettings ??= new TaskSettings();
            Info.Metadata ??= new TaskMetadata();

            TaskSettings taskSettings = Info.TaskSettings ?? new TaskSettings();
            TaskMetadata metadata = Info.Metadata ?? new TaskMetadata();
            Info.TaskSettings = taskSettings;
            Info.Metadata = metadata;

            TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", "DoWorkAsync Entry");
            
            Status = TaskStatus.Working;
            OnStatusChanged();

            // Perform Capture Phase based on Job Type
            // Only capture if we don't already have an image (e.g. passed from UI)
            if (metadata.Image == null && PlatformServices.IsInitialized)
            {
                TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", "Entering capture phase");
                
                SKBitmap? image = null;
                var captureStopwatch = Stopwatch.StartNew();
                DebugHelper.WriteLine($"Capture start: Job={taskSettings.Job}");

                // Create capture options from task settings
                taskSettings.CaptureSettings ??= new TaskSettingsCapture();
                var captureSettings = taskSettings.CaptureSettings;

                var captureDelaySeconds = TaskHelpers.GetCaptureStartDelaySeconds(taskSettings, out var workflowCategory);
                var isScreenCaptureDelay = workflowCategory == EnumExtensions.WorkflowType_Category_ScreenCapture && captureDelaySeconds > 0;
                var isScreenRecordDelay = workflowCategory == EnumExtensions.WorkflowType_Category_ScreenRecord && captureDelaySeconds > 0;

                // UseTransparentOverlay is true only for RectangleTransparent workflow
                // All other region capture workflows use frozen screenshot background
                var useTransparentOverlay = taskSettings.Job == WorkflowType.RectangleTransparent;

                var captureOptions = new CaptureOptions
                {
                    UseModernCapture = captureSettings.UseModernCapture,
                    ShowCursor = captureSettings.ShowCursor,
                    UseTransparentOverlay = useTransparentOverlay,
                    CaptureShadow = captureSettings.CaptureShadow,
                    CaptureClientArea = captureSettings.CaptureClientArea,
                    WorkflowId = taskSettings.WorkflowId,
                    WorkflowCategory = workflowCategory
                };

                switch (taskSettings.Job)
                {
                    case WorkflowType.ClipboardUpload:
                    case WorkflowType.ClipboardUploadWithContentViewer:
                        if (PlatformServices.Clipboard == null)
                        {
                            Status = TaskStatus.Failed;
                            Error = new Exception("Clipboard service is not available.");
                            OnStatusChanged();
                            return;
                        }

                        if (!TryLoadClipboardContent(taskSettings, metadata, out var clipboardFiles))
                        {
                            Status = TaskStatus.Failed;
                            Error = new Exception("Clipboard is empty or contains unsupported data.");
                            OnStatusChanged();
                            return;
                        }

                        if (clipboardFiles != null && clipboardFiles.Length > 1)
                        {
                            await UploadClipboardFilesAsync(taskSettings, clipboardFiles, token);
                            return;
                        }

                        break;

                    case WorkflowType.PrintScreen:
                        if (isScreenCaptureDelay && !await ApplyCaptureStartDelayAsync(taskSettings, workflowCategory, captureDelaySeconds, token))
                        {
                            return;
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
                        if (isScreenCaptureDelay && !await ApplyCaptureStartDelayAsync(taskSettings, workflowCategory, captureDelaySeconds, token))
                        {
                            return;
                        }
                        if (PlatformServices.Window != null)
                        {
                            image = await PlatformServices.ScreenCapture.CaptureActiveWindowAsync(PlatformServices.Window, captureOptions);
                        }
                        break;

                    case WorkflowType.FileUpload:
                        // If file path is not already set (e.g. via args), ask user
                        if (string.IsNullOrEmpty(Info.FilePath) && ShowOpenFileDialogCallback != null)
                        {
                            TroubleshootingHelper.Log(taskSettings.Job.ToString(), "UI", "Requesting file from user via dialog...");
                            var selectedFile = await ShowOpenFileDialogCallback();
                            
                            if (!string.IsNullOrEmpty(selectedFile))
                            {
                                TroubleshootingHelper.Log(taskSettings.Job.ToString(), "UI", $"User selected file: {selectedFile}");
                                Info.FilePath = selectedFile;
                                Info.DataType = EDataType.File;
                                
                                // Set filename in metadata for convenience
                                // Info.FileName is set automatically by property setter of FilePath
                            }
                            else
                            {
                                TroubleshootingHelper.Log(taskSettings.Job.ToString(), "UI", "User cancelled file selection");
                                Status = TaskStatus.Stopped;
                                OnStatusChanged();
                                return;
                            }
                        }
                        else if (!string.IsNullOrEmpty(Info.FilePath))
                        {
                             // File path already provided (drag/drop or args)
                             Info.DataType = EDataType.File;
                             // Info.FileName is set automatically by property setter of FilePath
                        }
                        else
                        {
                             // No file and no callback
                            DebugHelper.WriteLine("FileUpload job started but no file provided and no dialog callback available.");
                            Status = TaskStatus.Failed;
                            Error = new Exception("No file selected and dialog unavailable");
                            OnStatusChanged(); // Will trigger failure toast in finally
                            return;
                        }
                        Info.Job = TaskJob.FileUpload;
                        break;

                    case WorkflowType.IndexFolder:
                        if (!TryIndexFolder(taskSettings, out string? indexPath))
                        {
                            Status = TaskStatus.Failed;
                            Error = new Exception("Index folder path is invalid or indexing failed.");
                            OnStatusChanged();
                            return;
                        }

                        Info.FilePath = indexPath ?? "";
                        Info.DataType = EDataType.File;
                        Info.Job = TaskJob.FileUpload;
                        break;

                    case WorkflowType.CustomWindow:
                        if (PlatformServices.Window != null)
                        {
                            TroubleshootingHelper.Log("CustomWindow", "TASK", "Task started for CustomWindow");
                            TroubleshootingHelper.Log("CustomWindow", "TASK", $"TaskSettings provided: {taskSettings != null}");

                            string targetWindow = captureSettings.CaptureCustomWindow;
                            TroubleshootingHelper.Log("CustomWindow", "CONFIG", $"Configured target window: '{targetWindow}'");

                            // Also inspect global settings as sanity check
                            TroubleshootingHelper.Log("CustomWindow", "CONFIG", $"Global default target window: '{SettingsManager.DefaultTaskSettings?.CaptureSettings?.CaptureCustomWindow}'");

                            if (string.IsNullOrEmpty(targetWindow))
                            {
                                // No target window configured - show window selector
                                TroubleshootingHelper.Log("CustomWindow", "UI", "No target window configured. Showing window selector...");

                                if (ShowWindowSelectorCallback != null)
                                {
                                    var selectedWindow = await ShowWindowSelectorCallback();
                                    if (selectedWindow != null)
                                    {
                                        TroubleshootingHelper.Log("CustomWindow", "UI", $"User selected window: '{selectedWindow.Title}' (Handle: {selectedWindow.Handle}, PID: {selectedWindow.ProcessId})");
                                        TroubleshootingHelper.Log("CustomWindow", "UI", $"Window bounds: X={selectedWindow.Bounds.X}, Y={selectedWindow.Bounds.Y}, W={selectedWindow.Bounds.Width}, H={selectedWindow.Bounds.Height}");

                                        // Restore if minimized
                                        if (PlatformServices.Window.IsWindowMinimized(selectedWindow.Handle))
                                        {
                                            TroubleshootingHelper.Log("CustomWindow", "WINDOW", "Window is minimized, restoring...");
                                            PlatformServices.Window.ShowWindow(selectedWindow.Handle, 9); // SW_RESTORE = 9
                                            await Task.Delay(WindowActivationDelayMs, token);
                                        }

                                        // Capture using window handle directly (guarantees correct window)
                                        TroubleshootingHelper.Log("CustomWindow", "CAPTURE", $"Capturing window by handle: {selectedWindow.Handle}");

                                        // Log window info at capture time for verification
                                        var captureTimeTitle = PlatformServices.Window.GetWindowText(selectedWindow.Handle);
                                        var captureTimeBounds = PlatformServices.Window.GetWindowBounds(selectedWindow.Handle);
                                        TroubleshootingHelper.Log("CustomWindow", "VERIFY", $"Selected title: '{selectedWindow.Title}'");
                                        TroubleshootingHelper.Log("CustomWindow", "VERIFY", $"Capture-time title: '{captureTimeTitle}'");
                                        TroubleshootingHelper.Log("CustomWindow", "VERIFY", $"Capture-time bounds: X={captureTimeBounds.X}, Y={captureTimeBounds.Y}, W={captureTimeBounds.Width}, H={captureTimeBounds.Height}");

                                        // Always activate the selected window before capture
                                        TroubleshootingHelper.Log("CustomWindow", "ACTIVATE", "Activating selected window before capture...");
                                        if (!PlatformServices.Window.ActivateWindow(selectedWindow.Handle))
                                        {
                                            TroubleshootingHelper.Log("CustomWindow", "ACTIVATE", "ActivateWindow returned false, but proceeding check...");
                                        }
                                        await Task.Delay(WindowActivationDelayMs, token); // Increased delay for activation to settle

                                        // Verify foreground is now our target
                                        var foregroundHandle = PlatformServices.Window.GetForegroundWindow();
                                        var foregroundTitle = PlatformServices.Window.GetWindowText(foregroundHandle);
                                        TroubleshootingHelper.Log("CustomWindow", "ACTIVATE", $"After activation - Foreground handle: {foregroundHandle}, Title: '{foregroundTitle}'");
                                        TroubleshootingHelper.Log("CustomWindow", "ACTIVATE", $"Foreground matches selected: {foregroundHandle == selectedWindow.Handle}");

                                        if (isScreenCaptureDelay && !await ApplyCaptureStartDelayAsync(taskSettings!, workflowCategory, captureDelaySeconds, token))
                                        {
                                            return;
                                        }

                                        // Capture active window
                                        image = await PlatformServices.ScreenCapture.CaptureActiveWindowAsync(PlatformServices.Window, captureOptions);
                                        TroubleshootingHelper.Log("CustomWindow", "CAPTURE", $"Capture active window result: {image != null}");
                                    }
                                    else
                                    {
                                        TroubleshootingHelper.Log("CustomWindow", "UI", "User cancelled window selection");
                                        DebugHelper.WriteLine("Custom window capture cancelled by user");
                                    }
                                }
                                else
                                {
                                    TroubleshootingHelper.Log("CustomWindow", "ERROR", "Window selector callback not configured");
                                    DebugHelper.WriteLine("Custom window capture failed: Window selector not available");
                                }
                            }
                            else
                            {
                                // Use SearchWindow to find the target window (matches original ShareX behavior)
                                TroubleshootingHelper.Log("CustomWindow", "SEARCH", $"Searching for window using SearchWindow: '{targetWindow}'");
                                IntPtr hWnd = PlatformServices.Window.SearchWindow(targetWindow);

                                if (hWnd != IntPtr.Zero)
                                {
                                    TroubleshootingHelper.Log("CustomWindow", "SEARCH", $"Window found with handle: {hWnd}");

                                    // Get window bounds for logging and potential restore
                                    var bounds = PlatformServices.Window.GetWindowBounds(hWnd);
                                    TroubleshootingHelper.Log("CustomWindow", "WINDOW", $"Window bounds: X={bounds.X}, Y={bounds.Y}, W={bounds.Width}, H={bounds.Height}");

                                    // Restore if minimized (like original ShareX)
                                    if (PlatformServices.Window.IsWindowMinimized(hWnd))
                                    {
                                        TroubleshootingHelper.Log("CustomWindow", "WINDOW", "Window is minimized, restoring...");
                                        PlatformServices.Window.ShowWindow(hWnd, 9); // SW_RESTORE = 9
                                        await Task.Delay(WindowActivationDelayMs, token);
                                    }

                                    // Capture using window handle directly (guarantees correct window)
                                    TroubleshootingHelper.Log("CustomWindow", "CAPTURE", $"Capturing window by handle: {hWnd}");

                                    // Verify window title at capture time matches search term
                                    var captureTimeTitle = PlatformServices.Window.GetWindowText(hWnd);
                                    var captureTimeBounds = PlatformServices.Window.GetWindowBounds(hWnd);
                                    TroubleshootingHelper.Log("CustomWindow", "VERIFY", $"Search term: '{targetWindow}'");
                                    TroubleshootingHelper.Log("CustomWindow", "VERIFY", $"Capture-time title: '{captureTimeTitle}'");
                                    TroubleshootingHelper.Log("CustomWindow", "VERIFY", $"Capture-time bounds: X={captureTimeBounds.X}, Y={captureTimeBounds.Y}, W={captureTimeBounds.Width}, H={captureTimeBounds.Height}");
                                    TroubleshootingHelper.Log("CustomWindow", "VERIFY", $"Title contains search term: {captureTimeTitle?.Contains(targetWindow, StringComparison.OrdinalIgnoreCase) ?? false}");

                                    if (isScreenCaptureDelay && !await ApplyCaptureStartDelayAsync(taskSettings!, workflowCategory, captureDelaySeconds, token))
                                    {
                                        return;
                                    }

                                    image = await PlatformServices.ScreenCapture.CaptureWindowAsync(hWnd, PlatformServices.Window, captureOptions);
                                    TroubleshootingHelper.Log("CustomWindow", "CAPTURE", $"Capture window result: {image != null}");
                                }
                                else
                                {
                                    TroubleshootingHelper.Log("CustomWindow", "ERROR", $"Window with title containing '{targetWindow}' not found via SearchWindow.");
                                    DebugHelper.WriteLine($"Custom window capture failed: Unable to find window with title '{targetWindow}'.");
                                }
                            }
                        }
                        break;

                    // Stage 5: Screen Recording Integration
                    case WorkflowType.ScreenRecorder:
                    case WorkflowType.StartScreenRecorder:
                    case WorkflowType.ScreenRecorderGIF:
                    case WorkflowType.StartScreenRecorderGIF:
                        TroubleshootingHelper.Log(Info.TaskSettings.Job.ToString(), "WORKER_TASK", "ScreenRecorder case matched, showing region selector");

                        // Clear any previous image (recording doesn't produce image, only video)
                        if (Info.Metadata.Image != null)
                        {
                            Info.Metadata.Image.Dispose();
                            Info.Metadata.Image = null;
                        }

                        // Show region selector and get user selection
                        var regionCaptureOptions = new CaptureOptions
                        {
                            UseModernCapture = Info.TaskSettings.CaptureSettings.UseModernCapture,
                            ShowCursor = Info.TaskSettings.CaptureSettings.ShowCursor,
                            WorkflowId = Info.TaskSettings.WorkflowId
                        };

                        SKRectI selection = await PlatformServices.ScreenCapture.SelectRegionAsync(regionCaptureOptions);

                        if (selection.IsEmpty || selection.Width <= 0 || selection.Height <= 0)
                        {
                            TroubleshootingHelper.Log(Info.TaskSettings.Job.ToString(), "WORKER_TASK", "Region selection cancelled, aborting recording");
                            Status = TaskStatus.Stopped;
                            OnStatusChanged();
                            return;
                        }

                        // Convert SKRectI to System.Drawing.Rectangle for recording options
                        // Video encoders require dimensions divisible by VideoDimensionAlignment
                        int adjustedWidth = selection.Width - (selection.Width % VideoDimensionAlignment);
                        int adjustedHeight = selection.Height - (selection.Height % VideoDimensionAlignment);

                        // Ensure minimum dimensions for video encoding
                        if (adjustedWidth < MinVideoWidth || adjustedHeight < MinVideoHeight)
                        {
                            TroubleshootingHelper.Log(Info.TaskSettings.Job.ToString(), "WORKER_TASK", $"Region too small after adjustment: {adjustedWidth}x{adjustedHeight}, aborting recording");
                            Status = TaskStatus.Stopped;
                            OnStatusChanged();
                            return;
                        }

                        var recordingRegion = new Rectangle(selection.Left, selection.Top, adjustedWidth, adjustedHeight);

                        if (adjustedWidth != selection.Width || adjustedHeight != selection.Height)
                        {
                            TroubleshootingHelper.Log(Info.TaskSettings.Job.ToString(), "WORKER_TASK", $"Region adjusted for encoder: {selection.Width}x{selection.Height} → {adjustedWidth}x{adjustedHeight}");
                        }

                        TroubleshootingHelper.Log(Info.TaskSettings.Job.ToString(), "WORKER_TASK", $"Region selected: {recordingRegion}, calling HandleStartRecordingAsync");

                        if (isScreenRecordDelay && !await ApplyCaptureStartDelayAsync(taskSettings, workflowCategory, captureDelaySeconds, token))
                        {
                            return;
                        }

                        await HandleStartRecordingAsync(CaptureMode.Region, region: recordingRegion);
                        TroubleshootingHelper.Log(Info.TaskSettings.Job.ToString(), "WORKER_TASK", "HandleStartRecordingAsync completed");
                        return; // Recording tasks don't proceed to image processing

                    case WorkflowType.ScreenRecorderActiveWindow:
                    case WorkflowType.ScreenRecorderGIFActiveWindow:
                        // Clear any previous image (recording doesn't produce image, only video)
                        if (Info.Metadata.Image != null)
                        {
                            Info.Metadata.Image.Dispose();
                            Info.Metadata.Image = null;
                        }

                        if (PlatformServices.Window != null)
                        {
                            var foregroundWindow = PlatformServices.Window.GetForegroundWindow();
                            if (isScreenRecordDelay && !await ApplyCaptureStartDelayAsync(taskSettings, workflowCategory, captureDelaySeconds, token))
                            {
                                return;
                            }
                            await HandleStartRecordingAsync(CaptureMode.Window, foregroundWindow);
                        }
                        return;

                    case WorkflowType.ScreenRecorderCustomRegion:
                    case WorkflowType.ScreenRecorderGIFCustomRegion:
                        // Clear any previous image (recording doesn't produce image, only video)
                        if (Info.Metadata.Image != null)
                        {
                            Info.Metadata.Image.Dispose();
                            Info.Metadata.Image = null;
                        }

                        // TODO: Show region selector UI and get selected region
                        // For now, just start full screen recording
                        DebugHelper.WriteLine("ScreenRecorderCustomRegion: Region selector not yet implemented, falling back to full screen");
                        if (isScreenRecordDelay && !await ApplyCaptureStartDelayAsync(taskSettings, workflowCategory, captureDelaySeconds, token))
                        {
                            return;
                        }
                        await HandleStartRecordingAsync(CaptureMode.Screen);
                        return;

                    case WorkflowType.StopScreenRecording:
                        await HandleStopRecordingAsync();
                        return;

                    case WorkflowType.PauseScreenRecording:
                        await HandlePauseRecordingAsync();
                        return;

                    case WorkflowType.AbortScreenRecording:
                        await HandleAbortRecordingAsync();
                        return;

                    // Tool Workflows
                    case WorkflowType.ColorPicker:
                    case WorkflowType.ScreenColorPicker:
                    case WorkflowType.QRCode:
                    case WorkflowType.QRCodeDecodeFromScreen:
                    case WorkflowType.QRCodeScanRegion:
                    case WorkflowType.ScrollingCapture:
                        await HandleToolWorkflowAsync(token);
                        return;
                }

                captureStopwatch.Stop();

                bool hasClipboardPayload = taskSettings?.Job is WorkflowType.ClipboardUpload or WorkflowType.ClipboardUploadWithContentViewer
                    && (metadata.Image != null || !string.IsNullOrEmpty(Info.TextContent) || !string.IsNullOrEmpty(Info.FilePath));

                if (image != null)
                {
                    metadata.Image = image;
                    DebugHelper.WriteLine($"Captured image: {image.Width}x{image.Height} in {captureStopwatch.ElapsedMilliseconds}ms");
                }
                else if (hasClipboardPayload)
                {
                    DebugHelper.WriteLine($"Clipboard content loaded: dataType={Info.DataType}, filePath=\"{Info.FilePath}\", textLength={(Info.TextContent?.Length ?? 0)}");
                }
                else if ((taskSettings?.Job == WorkflowType.FileUpload || taskSettings?.Job == WorkflowType.IndexFolder) &&
                         !string.IsNullOrEmpty(Info.FilePath))
                {
                    DebugHelper.WriteLine($"FileUpload selected file: {Info.FilePath}");
                }
                else
                {
                    DebugHelper.WriteLine($"Capture returned null for job type: {taskSettings?.Job} (elapsed {captureStopwatch.ElapsedMilliseconds}ms)");
                    
                    // IF capture returned null (e.g. user cancelled region selection), stop the task here.
                    // This prevents empty tasks from being marked as 'Completed' successfully.
                    Status = TaskStatus.Stopped;
                    OnStatusChanged();
                    return;
                }
            }
            else if (Info.Metadata.Image == null)
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

                Status = TaskStatus.Failed;
                Error = new InvalidOperationException("Platform services not initialized");
                OnStatusChanged();
                return;
            }

            // Execute Capture Job (File Save, Clipboard, etc)
            var captureProcessor = new CaptureJobProcessor();
            await captureProcessor.ProcessAsync(Info, token);

            // Execute Upload Job
            var uploadProcessor = new UploadJobProcessor();
            await uploadProcessor.ProcessAsync(Info, token);
        }

        public void Stop()
        {
            if (IsWorking)
            {
                Status = TaskStatus.Stopping;
                OnStatusChanged();
                _cancellationTokenSource.Cancel();
            }
        }

        protected virtual void OnStatusChanged()
        {
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnTaskCompleted()
        {
            TaskCompleted?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationTokenSource?.Dispose();
            }
        }
    }
}
