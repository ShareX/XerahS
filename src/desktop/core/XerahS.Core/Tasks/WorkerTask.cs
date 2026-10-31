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
using XerahS.Services.Abstractions;
using XerahS.RegionCapture.ScreenRecording;
using SkiaSharp;
using System.Diagnostics;
using System.IO;
using System.Linq;
using XerahS.History;
using Avalonia.Threading;
using System.Drawing;
using XerahS.Media;
using XerahS.Core.Tasks.Pipeline;
using XerahS.Uploaders;

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
                bool hasImage = _hasImageOutput || Info.Metadata?.Image != null;
                bool hasFile = !string.IsNullOrEmpty(Info.FilePath);
                bool hasUrl = !string.IsNullOrEmpty(Info.Metadata?.UploadURL);

                return hasImage || hasFile || hasUrl;
            }
        }

        private CancellationTokenSource _cancellationTokenSource;
        private bool _hasImageOutput;
        private bool _disposeRequested;
        private bool _disposed;
        private readonly object _lifetimeLock = new();
        private volatile bool _hasFinished;
        internal bool HasFinished => _hasFinished;

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

        /// <summary>Callback to open the history window from the UI layer.</summary>
        public static Action<WorkflowType>? OpenHistoryCallback { get; set; }

        /// <summary>Callback to open and focus the main window from the UI layer.</summary>
        public static Action? OpenMainWindowCallback { get; set; }

        /// <summary>Callback to exit the application from the UI layer.</summary>
        public static Action? ExitApplicationCallback { get; set; }

        /// <summary>Callback to toggle hotkey registration from the UI layer.</summary>
        public static Action? ToggleHotkeysCallback { get; set; }

        /// <summary>
        /// Screen recording abstraction used by recording workflows.
        /// Defaults to the singleton manager for compatibility.
        /// </summary>
        public static IScreenRecordingManager RecordingManagerService { get; set; } = ScreenRecordingManager.Instance;

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

        /// <summary>
        /// Creates a task that takes ownership of <paramref name="inputImage"/>.
        /// Completion handlers must copy the image if they need it after the handler returns.
        /// </summary>
        public static WorkerTask Create(TaskSettings taskSettings, SKBitmap? inputImage = null)
        {
            return new WorkerTask(taskSettings, inputImage);
        }

        public async Task StartAsync()
        {
            lock (_lifetimeLock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (Status != TaskStatus.InQueue) return;
                Info.TaskStartTime = DateTime.Now;
                Status = TaskStatus.Preparing;
            }
            DebugHelper.WriteLine($"Task started: Job={Info.TaskSettings.Job}");

            try
            {
                OnStatusChanged();
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
                DebugHelper.WriteException(ex, "Task failed");

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
                        ErrorDetails = ex.ToString(),
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
                if (Status != TaskStatus.Failed && Status != TaskStatus.Stopped && Status != TaskStatus.Canceled)
                {
                    Status = TaskStatus.Completed;
                }

                _hasImageOutput = Info.Metadata?.Image != null;
                try
                {
                    OnTaskCompleted();
                    OnStatusChanged();
                }
                finally
                {
                    // History retains task metadata, not full-resolution native pixel buffers.
                    // Synchronous completion handlers can copy the image before it is released.
                    ReleaseImage();
                    lock (_lifetimeLock)
                    {
                        _hasFinished = true;
                        if (_disposeRequested)
                        {
                            Dispose();
                        }
                    }
                }
            }
        }

        private async Task DoWorkAsync(CancellationToken token)
        {
            // Ensure critical context is not null for the remainder of this task
            Info.TaskSettings ??= new TaskSettings();
            Info.Metadata ??= new TaskMetadata();

            XerahS.Common.TroubleshootingHelper.Log(Info.TaskSettings.Job.ToString(), "WORKER_TASK", "DoWorkAsync Entry");
            
            Status = TaskStatus.Working;
            OnStatusChanged();

            var pipelineContext = new PipelineContext
            {
                Info = Info,
                Status = Status,
                Error = Error,
                OnStatusChanged = OnStatusChanged
            };

            var pipeline = new WorkerTaskPipeline()
                .AddStage(new CaptureStage(this))
                .AddStage(new FinalizationStage());

            var result = await pipeline.ExecuteAsync(pipelineContext, token);

            // Sync state back from pipeline context
            Status = pipelineContext.Status;
            Error = pipelineContext.Error;

            if (result == PipelineStageResult.Failed && Error != null)
            {
                if (Error is InvalidOperationException)
                {
                    throw Error;
                }
                // Let the finally block in ProcessAsync handle other exceptions and toasts
            }
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

        /// <summary>
        /// Select a region using slurp (Linux Wayland native tool).
        /// Returns the selected region, or empty if cancelled/failed.
        /// </summary>
        private static string GetSlurpExecutablePath()
        {
            if (OperatingSystem.IsLinux())
            {
                var candidates = new[] { "/usr/bin/slurp", "/usr/local/bin/slurp" };
                foreach (var path in candidates)
                {
                    if (File.Exists(path))
                        return path;
                }
            }
            return "slurp";
        }

        internal async Task<(SKRectI Region, bool WasCancelled)> SelectRegionWithSlurpAsync()
        {
            try
            {
                var slurpStartInfo = new ProcessStartInfo
                {
                    FileName = GetSlurpExecutablePath(),
                    Arguments = "-f \"%x %y %w %h\"",  // Output format: x y width height
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var slurpProcess = Process.Start(slurpStartInfo);
                if (slurpProcess == null)
                {
                    DebugHelper.WriteLine("WorkerTask: Failed to start slurp process");
                    return (SKRectI.Empty, false);
                }

                var completed = await Task.Run(() => slurpProcess.WaitForExit(60000));
                if (!completed)
                {
                    try { slurpProcess.Kill(); } catch { }
                    DebugHelper.WriteLine("WorkerTask: slurp timed out");
                    return (SKRectI.Empty, false);
                }

                if (slurpProcess.ExitCode != 0)
                {
                    // Exit code 1 typically means user cancelled (pressed Escape)
                    DebugHelper.WriteLine($"WorkerTask: slurp exited with code {slurpProcess.ExitCode} (likely cancelled)");
                    return (SKRectI.Empty, slurpProcess.ExitCode == 1);
                }

                string output = (await slurpProcess.StandardOutput.ReadToEndAsync()).Trim();
                DebugHelper.WriteLine($"WorkerTask: slurp output: '{output}'");

                // Parse "x y w h" format
                var parts = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 &&
                    int.TryParse(parts[0], out int x) &&
                    int.TryParse(parts[1], out int y) &&
                    int.TryParse(parts[2], out int w) &&
                    int.TryParse(parts[3], out int h))
                {
                    DebugHelper.WriteLine($"WorkerTask: slurp region selected: x={x}, y={y}, w={w}, h={h}");
                    return (new SKRectI(x, y, x + w, y + h), false);
                }

                DebugHelper.WriteLine($"WorkerTask: Failed to parse slurp output: '{output}'");
                return (SKRectI.Empty, false);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"WorkerTask: slurp exception: {ex.Message}");
                return (SKRectI.Empty, false);
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

        private static bool ShouldRequireSuccessfulUpload(TaskInfo info)
        {
            return info.IsUploadJob &&
                   (info.DataType == EDataType.Image ||
                    info.DataType == EDataType.Text ||
                    info.DataType == EDataType.File);
        }

        private static bool IsUploadResultSuccessful(UploadResult? result)
        {
            if (result == null)
            {
                return false;
            }

            return result.IsSuccess || (!result.IsError && !string.IsNullOrWhiteSpace(result.URL));
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
                lock (_lifetimeLock)
                {
                    _disposeRequested = true;
                    if (Status != TaskStatus.InQueue && !HasFinished)
                    {
                        // In-flight pipeline stages and completion callbacks still borrow the image.
                        return;
                    }
                    _disposed = true;
                    ReleaseImage();
                    _cancellationTokenSource?.Dispose();
                }
            }
        }

        private void ReleaseImage()
        {
            var image = Info.Metadata?.Image;
            if (Info.Metadata != null)
            {
                Info.Metadata.Image = null;
            }
            image?.Dispose();
        }
    }
}
