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

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using ShareX.Ava.Core;
using ShareX.Ava.Common;
using ShareX.Ava.Core.Tasks.Processors;
using ShareX.Ava.Platform.Abstractions;
using SkiaSharp;

namespace ShareX.Ava.Core.Tasks
{
    public class WorkerTask
    {
        public TaskInfo Info { get; private set; }
        public TaskStatus Status { get; private set; }
        public bool IsBusy => Status == TaskStatus.InQueue || IsWorking;
        public bool IsWorking => Status == TaskStatus.Preparing || Status == TaskStatus.Working || Status == TaskStatus.Stopping;
        
        private CancellationTokenSource _cancellationTokenSource;

        public event EventHandler StatusChanged;
        public event EventHandler TaskCompleted;

        /// <summary>
        /// Delegate to show window selector when CustomWindow capture has no target configured.
        /// Returns selected window or null if cancelled.
        /// </summary>
        public static Func<Task<ShareX.Ava.Platform.Abstractions.WindowInfo?>>? ShowWindowSelectorCallback { get; set; }

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
                DebugHelper.WriteLine($"Task failed: {ex.Message}");
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
            Status = TaskStatus.Working;
            OnStatusChanged();

            // Perform Capture Phase based on Job Type
            // Only capture if we don't already have an image (e.g. passed from UI)
            if (Info.Metadata.Image == null && PlatformServices.IsInitialized)
            {
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
                        var debugFolder = System.IO.Path.Combine(SettingManager.PersonalFolder, "Troubleshooting", "CustomWindow");
                        try { System.IO.Directory.CreateDirectory(debugFolder); } catch {}
                        var logFile = System.IO.Path.Combine(debugFolder, $"custom-window-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");

                        void Log(string message) 
                        {
                            try 
                            { 
                                System.IO.File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}"); 
                            } 
                            catch {} 
                        }

                        Log($"Task started for CustomWindow");
                        Log($"TaskSettings provided: {Info.TaskSettings != null}");

                        string targetWindow = Info.TaskSettings?.CaptureSettings?.CaptureCustomWindow;
                        Log($"Configured target window: '{targetWindow}'");
                        
                        // Also inspect global settings as sanity check
                        Log($"Global default target window: '{SettingManager.DefaultTaskSettings?.CaptureSettings?.CaptureCustomWindow}'");

                        if (string.IsNullOrEmpty(targetWindow))
                        {
                            // No target window configured - show window selector
                            Log("No target window configured. Showing window selector...");
                            
                            if (ShowWindowSelectorCallback != null)
                            {
                                var selectedWindow = await ShowWindowSelectorCallback();
                                if (selectedWindow != null)
                                {
                                    Log($"User selected window: '{selectedWindow.Title}' (Handle: {selectedWindow.Handle})");
                                    targetWindow = selectedWindow.Title;
                                    PlatformServices.Window.SetForegroundWindow(selectedWindow.Handle);
                                    await Task.Delay(250, token);
                                    image = await PlatformServices.ScreenCapture.CaptureActiveWindowAsync(PlatformServices.Window, captureOptions);
                                    Log($"Capture active window result: {image != null}");
                                }
                                else
                                {
                                    Log("User cancelled window selection");
                                    DebugHelper.WriteLine("Custom window capture cancelled by user");
                                }
                            }
                            else
                            {
                                Log("Window selector callback not configured");
                                DebugHelper.WriteLine("Custom window capture failed: Window selector not available");
                            }
                        }
                        else if (!string.IsNullOrEmpty(targetWindow))
                        {
                            var windows = PlatformServices.Window.GetAllWindows();
                            Log($"Total open windows found: {windows.Length}");

                            foreach (var w in windows) 
                            {
                                if (w.Title.Contains(targetWindow, StringComparison.OrdinalIgnoreCase))
                                {
                                     Log($"[MATCH] Window found: '{w.Title}' (Handle: {w.Handle})");
                                }
                                else 
                                {
                                     // Log all windows to see what's available
                                     // Log($"[NO MATCH] '{w.Title}'"); 
                                }
                            }

                            var winInfo = windows.FirstOrDefault(w => w.Title.Contains(targetWindow, StringComparison.OrdinalIgnoreCase));

                            if (winInfo != null && winInfo.Handle != IntPtr.Zero)
                            {
                                Log($"Activating window handle {winInfo.Handle}");
                                PlatformServices.Window.SetForegroundWindow(winInfo.Handle);

                                // Give it a moment to come to foreground
                                await Task.Delay(250, token);

                                image = await PlatformServices.ScreenCapture.CaptureActiveWindowAsync(PlatformServices.Window, captureOptions);
                                Log($"Capture active window result: {image != null}");
                            }
                            else
                            {
                                Log($"Window with title containing '{targetWindow}' not found.");
                                DebugHelper.WriteLine($"Custom window capture failed: Window with title containing '{targetWindow}' not found.");
                            }
                        }
                    }
                    break;
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
