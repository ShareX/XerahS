using System;
using System.Threading;
using System.Threading.Tasks;
using ShareX.Avalonia.Core;

using ShareX.Avalonia.Common;

using ShareX.Avalonia.Core.Tasks.Processors;
using ShareX.Avalonia.Platform.Abstractions;

namespace ShareX.Avalonia.Core.Tasks
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

            // Perform Capture Phase
            if (Info.TaskSettings.Job == HotkeyType.RectangleRegion && PlatformServices.IsInitialized)
            {
                var image = await PlatformServices.ScreenCapture.CaptureRegionAsync();
                
                if (image is System.Drawing.Bitmap bitmap)
                {
                    Info.Metadata.Image = bitmap;
                }
                else if (image != null)
                {
                    // Handle other image types if needed or error
                    DebugHelper.WriteLine("Captured image is not a valid Bitmap.");
                }
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
