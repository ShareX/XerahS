using System;
using System.Threading;
using System.Threading.Tasks;
using ShareX.Ava.Core;

using ShareX.Ava.Common;

using ShareX.Ava.Core.Helpers;
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

        private WorkerTask(TaskSettings taskSettings)
        {
            Status = TaskStatus.InQueue;
            Info = new TaskInfo(taskSettings);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public static WorkerTask Create(TaskSettings taskSettings)
        {
            return new WorkerTask(taskSettings);
        }

        public async Task StartAsync()
        {
            if (Status != TaskStatus.InQueue) return;

            Info.TaskStartTime = DateTime.Now;
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
            if (PlatformServices.IsInitialized)
            {
                if (Status == TaskStatus.Canceled) return;

                // Step 2: Capture
                // ---------------------------------------------------------------------
                // UpdateStatus("Capturing...");
                DebugHelper.WriteLine("Capturing...");
                
                // Check capture type from settings
                SKBitmap? capturedBitmap = null;

                if (Info.TaskSettings.Job == HotkeyType.RectangleRegion)
                {
                    capturedBitmap = await PlatformServices.ScreenCapture.CaptureRegionAsync();
                }
                else if (Info.TaskSettings.Job == HotkeyType.PrintScreen)
                {
                    capturedBitmap = await PlatformServices.ScreenCapture.CaptureFullScreenAsync();
                }
                else if (Info.TaskSettings.Job == HotkeyType.ActiveWindow)
                {
                    capturedBitmap = await PlatformServices.ScreenCapture.CaptureActiveWindowAsync(PlatformServices.Window);
                }
                
                // If capture failed or canceled
                if (capturedBitmap == null)
                {
                    // UpdateStatus("Canceled");
                    DebugHelper.WriteLine("Canceled");
                    Status = TaskStatus.Canceled;
                    return;
                }

                Info.Metadata.Image = capturedBitmap;
                DebugHelper.WriteLine($"Captured image: {capturedBitmap.Width}x{capturedBitmap.Height}");

                // Step 3: Process Image Tasks
                // ---------------------------------------------------------------------
                if (Info.TaskSettings.Job != HotkeyType.None)
                {
                    var processor = new Processors.CaptureJobProcessor();
                    await processor.ProcessAsync(Info, token); // Added token parameter
                }
            }
            else
            {
                DebugHelper.WriteLine("PlatformServices not initialized - cannot capture");
            }

            // Execute Capture Job (File Save, Clipboard, etc)
            // The previous CaptureJobProcessor call seems to replace this.
            // Keeping it here as per the diff's end point, but it might be redundant or need adjustment.
            var captureProcessor = new CaptureJobProcessor();
            await captureProcessor.ProcessAsync(Info, token); // Ensure this is intended if the above block also calls it.

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
