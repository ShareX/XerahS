#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
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
using XerahS.Core.Helpers;
using XerahS.Core.Managers;
using XerahS.Core.Tasks.Processors;
using XerahS.Platform.Abstractions;
using XerahS.ScreenCapture.ScreenRecording;
using SkiaSharp;
using System.Diagnostics;

namespace XerahS.Core.Tasks
{
    public class WorkerTask
    {
        public TaskInfo Info { get; private set; }
        public TaskStatus Status { get; private set; }
        public Exception? Error { get; private set; }
        public bool IsBusy => Status == TaskStatus.InQueue || IsWorking;
        public bool IsWorking => Status == TaskStatus.Preparing || Status == TaskStatus.Working || Status == TaskStatus.Stopping;

        private CancellationTokenSource _cancellationTokenSource;

        public event EventHandler StatusChanged;
        public event EventHandler TaskCompleted;

        /// <summary>
        /// Delegate to show window selector when CustomWindow capture has no target configured.
        /// Returns selected window or null if cancelled.
        /// </summary>
        public static Func<Task<XerahS.Platform.Abstractions.WindowInfo?>>? ShowWindowSelectorCallback { get; set; }

        private WorkerTask(TaskSettings taskSettings, SKBitmap? inputImage = null)
        {
            Status = TaskStatus.InQueue;
            Info = new TaskInfo(taskSettings);
            if (inputImage != null)
            {
                Info.Metadata.Image = inputImage;
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
                await Task.Run(async () => await DoWorkAsync(_cancellationTokenSource.Token));
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
                        errorMessage = errorMessage.Substring(0, 147) + "...";
                    }

                    PlatformServices.Toast?.ShowToast(new Platform.Abstractions.ToastConfig
                    {
                        Title = $"{Info.TaskSettings.Job} Failed",
                        Text = errorMessage,
                        Duration = 5f,
                        Size = new System.Drawing.Size(400, 120),
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
            TroubleshootingHelper.Log(Info.TaskSettings?.Job.ToString() ?? "Unknown", "WORKER_TASK", "DoWorkAsync Entry");
            
            Status = TaskStatus.Working;
            OnStatusChanged();

            // Perform Capture Phase based on Job Type
            // Only capture if we don't already have an image (e.g. passed from UI)
            if (Info.Metadata.Image == null && PlatformServices.IsInitialized)
            {
                TroubleshootingHelper.Log(Info.TaskSettings.Job.ToString(), "WORKER_TASK", "Entering capture phase");
                
                SKBitmap? image = null;
                var captureStopwatch = Stopwatch.StartNew();
                DebugHelper.WriteLine($"Capture start: Job={Info.TaskSettings.Job}");

                // Create capture options from task settings
                var captureOptions = new CaptureOptions
                {
                    UseModernCapture = Info.TaskSettings.CaptureSettings.UseModernCapture,
                    ShowCursor = Info.TaskSettings.CaptureSettings.ShowCursor,
                    CaptureTransparent = Info.TaskSettings.CaptureSettings.CaptureTransparent,
                    CaptureShadow = Info.TaskSettings.CaptureSettings.CaptureShadow,
                    CaptureClientArea = Info.TaskSettings.CaptureSettings.CaptureClientArea
                };

                switch (Info.TaskSettings.Job)
                {
                    case HotkeyType.PrintScreen:
                        image = await PlatformServices.ScreenCapture.CaptureFullScreenAsync(captureOptions);
                        break;

                    case HotkeyType.RectangleRegion:
                        image = await PlatformServices.ScreenCapture.CaptureRegionAsync(captureOptions);
                        break;

                    case HotkeyType.ActiveWindow:
                        if (PlatformServices.Window != null)
                        {
                            image = await PlatformServices.ScreenCapture.CaptureActiveWindowAsync(PlatformServices.Window, captureOptions);
                        }
                        break;

                    case HotkeyType.CustomWindow:
                        if (PlatformServices.Window != null)
                        {
                            TroubleshootingHelper.Log("CustomWindow", "TASK", "Task started for CustomWindow");
                            TroubleshootingHelper.Log("CustomWindow", "TASK", $"TaskSettings provided: {Info.TaskSettings != null}");

                            string targetWindow = Info.TaskSettings?.CaptureSettings?.CaptureCustomWindow;
                            TroubleshootingHelper.Log("CustomWindow", "CONFIG", $"Configured target window: '{targetWindow}'");

                            // Also inspect global settings as sanity check
                            TroubleshootingHelper.Log("CustomWindow", "CONFIG", $"Global default target window: '{SettingManager.DefaultTaskSettings?.CaptureSettings?.CaptureCustomWindow}'");

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
                                            await Task.Delay(250, token);
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
                                        await Task.Delay(250, token); // Increased delay for activation to settle

                                        // Verify foreground is now our target
                                        var foregroundHandle = PlatformServices.Window.GetForegroundWindow();
                                        var foregroundTitle = PlatformServices.Window.GetWindowText(foregroundHandle);
                                        TroubleshootingHelper.Log("CustomWindow", "ACTIVATE", $"After activation - Foreground handle: {foregroundHandle}, Title: '{foregroundTitle}'");
                                        TroubleshootingHelper.Log("CustomWindow", "ACTIVATE", $"Foreground matches selected: {foregroundHandle == selectedWindow.Handle}");

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
                                        await Task.Delay(250, token);
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
                    case HotkeyType.ScreenRecorder:
                    case HotkeyType.StartScreenRecorder:
                        TroubleshootingHelper.Log(Info.TaskSettings.Job.ToString(), "WORKER_TASK", "ScreenRecorder case matched, calling HandleStartRecordingAsync");
                        await HandleStartRecordingAsync(CaptureMode.Screen);
                        TroubleshootingHelper.Log(Info.TaskSettings.Job.ToString(), "WORKER_TASK", "HandleStartRecordingAsync completed");
                        return; // Recording tasks don't proceed to image processing

                    case HotkeyType.ScreenRecorderActiveWindow:
                        if (PlatformServices.Window != null)
                        {
                            var foregroundWindow = PlatformServices.Window.GetForegroundWindow();
                            await HandleStartRecordingAsync(CaptureMode.Window, foregroundWindow);
                        }
                        return;

                    case HotkeyType.ScreenRecorderCustomRegion:
                        // TODO: Show region selector UI and get selected region
                        // For now, just start full screen recording
                        DebugHelper.WriteLine("ScreenRecorderCustomRegion: Region selector not yet implemented, falling back to full screen");
                        await HandleStartRecordingAsync(CaptureMode.Screen);
                        return;

                    case HotkeyType.StopScreenRecording:
                        await HandleStopRecordingAsync();
                        return;

                    case HotkeyType.AbortScreenRecording:
                        await HandleAbortRecordingAsync();
                        return;
                }

                captureStopwatch.Stop();

                if (image != null)
                {
                    Info.Metadata.Image = image;
                    DebugHelper.WriteLine($"Captured image: {image.Width}x{image.Height} in {captureStopwatch.ElapsedMilliseconds}ms");
                }
                else
                {
                    DebugHelper.WriteLine($"Capture returned null for job type: {Info.TaskSettings.Job} (elapsed {captureStopwatch.ElapsedMilliseconds}ms)");
                }
            }
            else if (Info.Metadata.Image == null)
            {
                DebugHelper.WriteLine("PlatformServices not initialized - cannot capture");
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

        #region Recording Handlers (Stage 5)

        private async Task HandleStartRecordingAsync(CaptureMode mode, IntPtr windowHandle = default)
        {
            TroubleshootingHelper.Log(Info.TaskSettings?.Job.ToString() ?? "Unknown", "WORKER_TASK", $"HandleStartRecordingAsync Entry: mode={mode}");
            
            try
            {
                // Check if already recording
                if (ScreenRecordingManager.Instance.IsRecording)
                {
                    TroubleshootingHelper.Log(Info.TaskSettings?.Job.ToString() ?? "Unknown", "WORKER_TASK", "Already recording, stopping first");
                    DebugHelper.WriteLine("Recording already in progress, stopping existing recording first...");
                    await ScreenRecordingManager.Instance.StopRecordingAsync();
                }

                // Build recording options from task settings
                var recordingOptions = new RecordingOptions
                {
                    Mode = mode,
                    Settings = Info.TaskSettings.CaptureSettings.ScreenRecordingSettings,
                    TargetWindowHandle = windowHandle
                };

                // Generate output path
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string recordingsPath = Path.Combine(documentsPath, "ShareX", "Recordings", DateTime.Now.ToString("yyyy-MM"));
                Directory.CreateDirectory(recordingsPath);
                recordingOptions.OutputPath = Path.Combine(recordingsPath, $"Recording_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4");

                if (recordingOptions.Settings != null &&
                    (recordingOptions.Settings.CaptureSystemAudio || recordingOptions.Settings.CaptureMicrophone))
                {
                    // Force FFmpeg path until native audio capture is implemented
                    recordingOptions.Settings.ForceFFmpeg = true;
                }

                TroubleshootingHelper.Log(Info.TaskSettings?.Job.ToString() ?? "Unknown", "WORKER_TASK", "Calling ScreenRecordingManager.StartRecordingAsync");
                DebugHelper.WriteLine($"Starting recording: Mode={mode}, Codec={recordingOptions.Settings?.Codec}, FPS={recordingOptions.Settings?.FPS}");
                DebugHelper.WriteLine($"Output path: {recordingOptions.OutputPath}");

                // Start recording via manager
                await ScreenRecordingManager.Instance.StartRecordingAsync(recordingOptions);
                TroubleshootingHelper.Log(Info.TaskSettings?.Job.ToString() ?? "Unknown", "WORKER_TASK", "ScreenRecordingManager.StartRecordingAsync completed");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to start recording");
                throw;
            }
        }

        private async Task HandleStopRecordingAsync()
        {
            try
            {
                if (!ScreenRecordingManager.Instance.IsRecording)
                {
                    DebugHelper.WriteLine("No recording in progress to stop");
                    return;
                }

                DebugHelper.WriteLine("Stopping recording...");
                string? outputPath = await ScreenRecordingManager.Instance.StopRecordingAsync();

                if (!string.IsNullOrEmpty(outputPath))
                {
                    DebugHelper.WriteLine($"Recording saved to: {outputPath}");
                    // TODO: Process recording file (upload, after-capture tasks, etc.)
                    // For now, just log the completion
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to stop recording");
                throw;
            }
        }

        private async Task HandleAbortRecordingAsync()
        {
            try
            {
                if (!ScreenRecordingManager.Instance.IsRecording)
                {
                    DebugHelper.WriteLine("No recording in progress to abort");
                    return;
                }

                DebugHelper.WriteLine("Aborting recording...");
                await ScreenRecordingManager.Instance.AbortRecordingAsync();
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to abort recording");
                throw;
            }
        }

        #endregion

        protected virtual void OnStatusChanged()
        {
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnTaskCompleted()
        {
            TaskCompleted?.Invoke(this, EventArgs.Empty);
        }
    }
}
