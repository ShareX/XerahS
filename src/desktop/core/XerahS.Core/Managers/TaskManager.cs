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
using XerahS.Core.Tasks;
using XerahS.Services.Abstractions;
using System.Collections.Concurrent;
using SkiaSharp;

namespace XerahS.Core.Managers
{
    public class TaskManager : ITaskManager
    {
        private static readonly Lazy<TaskManager> _lazy = new(() => new TaskManager());
        public static TaskManager Instance => _lazy.Value;

        private readonly ConcurrentQueue<WorkerTask> _tasks = new();
        private readonly int _maxHistoricalTasks = 100;
        private readonly Lock _tasksLock = new();

        public IEnumerable<WorkerTask> Tasks
        {
            get
            {
                lock (_tasksLock)
                {
                    return _tasks.ToArray();
                }
            }
        }

        private TaskManager()
        {
        }

        // Event fired when a task completes with an image
        public event EventHandler<WorkerTask>? TaskCompleted;
        public event EventHandler<WorkerTask>? TaskStarted;

        public async Task StartTask(TaskSettings? taskSettings, SkiaSharp.SKBitmap? inputImage = null)
        {
            if (taskSettings == null)
            {
                DebugHelper.WriteLine("StartTask called with null TaskSettings, skipping.");
                return;
            }

            XerahS.Common.TroubleshootingHelper.Log(taskSettings.Job.ToString(), "TASK_MANAGER", "StartTask Entry");

            var task = WorkerTask.Create(taskSettings, inputImage);

            AddTask(task);

            XerahS.Common.TroubleshootingHelper.Log(task.Info?.TaskSettings?.Job.ToString() ?? "Unknown", "TASK_MANAGER", "Task created");

            task.StatusChanged += (s, e) => DebugHelper.WriteLine($"Task Status: {task.Status}");
            task.TaskCompleted += (s, e) =>
            {
                // Fire event so listeners (like App.axaml.cs) can update UI
                TaskCompleted?.Invoke(this, task);
            };

            TaskStarted?.Invoke(this, task);

            XerahS.Common.TroubleshootingHelper.Log(task.Info?.TaskSettings?.Job.ToString() ?? "Unknown", "TASK_MANAGER", "Calling task.StartAsync...");
            await task.StartAsync();
            XerahS.Common.TroubleshootingHelper.Log(task.Info?.TaskSettings?.Job.ToString() ?? "Unknown", "TASK_MANAGER", "task.StartAsync completed");
        }

        public async Task StartFileTask(TaskSettings? taskSettings, string filePath)
        {
            if (taskSettings == null)
            {
                DebugHelper.WriteLine("StartFileTask called with null TaskSettings, skipping.");
                return;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                DebugHelper.WriteLine("StartFileTask called with empty file path, skipping.");
                return;
            }

            XerahS.Common.TroubleshootingHelper.Log(taskSettings?.Job.ToString() ?? "Unknown", "TASK_MANAGER", $"StartFileTask Entry: FilePath={filePath}");

            var safeTaskSettings = taskSettings ?? new TaskSettings();
            var task = WorkerTask.Create(safeTaskSettings);
            task.Info.FilePath = filePath;
            task.Info.DataType = EDataType.File;
            task.Info.Job = TaskJob.FileUpload;

            string extension = Path.GetExtension(filePath);
            task.Info.SetFileName(TaskHelpers.GetFileName(safeTaskSettings, extension, task.Info.Metadata));

            AddTask(task);

            task.StatusChanged += (s, e) => DebugHelper.WriteLine($"Task Status: {task.Status}");
            task.TaskCompleted += (s, e) =>
            {
                TaskCompleted?.Invoke(this, task);
            };

            TaskStarted?.Invoke(this, task);

            await task.StartAsync();
        }

        public async Task StartImageUploadTask(TaskSettings? taskSettings, SkiaSharp.SKBitmap image)
        {
            if (taskSettings == null)
            {
                DebugHelper.WriteLine("StartImageUploadTask called with null TaskSettings, skipping.");
                return;
            }

            if (image == null)
            {
                DebugHelper.WriteLine("StartImageUploadTask called with null image, skipping.");
                return;
            }

            XerahS.Common.TroubleshootingHelper.Log(taskSettings.Job.ToString(), "TASK_MANAGER", "StartImageUploadTask Entry");

            var safeTaskSettings = taskSettings;
            var task = WorkerTask.Create(safeTaskSettings, image);
            task.Info.DataType = EDataType.Image;
            task.Info.Job = TaskJob.DataUpload;

            string imageExtension = EnumExtensions.GetDescription(safeTaskSettings.ImageSettings.ImageFormat);
            task.Info.SetFileName(TaskHelpers.GetFileName(safeTaskSettings, imageExtension, task.Info.Metadata));

            AddTask(task);

            task.StatusChanged += (s, e) => DebugHelper.WriteLine($"Task Status: {task.Status}");
            task.TaskCompleted += (s, e) =>
            {
                TaskCompleted?.Invoke(this, task);
            };

            TaskStarted?.Invoke(this, task);

            await task.StartAsync();
        }

        public async Task StartTextTask(TaskSettings? taskSettings, string text)
        {
            if (taskSettings == null)
            {
                DebugHelper.WriteLine("StartTextTask called with null TaskSettings, skipping.");
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                DebugHelper.WriteLine("StartTextTask called with empty text, skipping.");
                return;
            }

            TaskSettings safeTaskSettings = taskSettings;
            XerahS.Common.TroubleshootingHelper.Log(safeTaskSettings.Job.ToString(), "TASK_MANAGER", $"StartTextTask Entry: textLength={text.Length}");
            DebugHelper.WriteLine(
                $"[UploadContentDebug] StartTextTask: job={safeTaskSettings.Job}, workflowId=\"{safeTaskSettings.WorkflowId ?? string.Empty}\", " +
                $"destinationInstanceId=\"{safeTaskSettings.DestinationInstanceId ?? string.Empty}\", " +
                $"urlShortenerInstanceId=\"{safeTaskSettings.UrlShortenerDestinationInstanceId ?? string.Empty}\", " +
                $"afterCapture={safeTaskSettings.AfterCaptureJob}, afterUpload={safeTaskSettings.AfterUploadJob}");

            var task = WorkerTask.Create(safeTaskSettings);
            task.Info.TextContent = text;
            task.Info.DataType = EDataType.Text;
            task.Info.Job = TaskJob.TextUpload;

            string extension = safeTaskSettings.AdvancedSettings?.TextFileExtension ?? "txt";
            task.Info.SetFileName(TaskHelpers.GetFileName(safeTaskSettings, extension, task.Info.Metadata));
            DebugHelper.WriteLine(
                $"[UploadContentDebug] StartTextTask created WorkerTask: fileName=\"{task.Info.FileName}\", " +
                $"dataType={task.Info.DataType}, taskJob={task.Info.Job}, textLength={(task.Info.TextContent?.Length ?? 0)}");

            AddTask(task);

            task.StatusChanged += (s, e) => DebugHelper.WriteLine($"Task Status: {task.Status}");
            task.TaskCompleted += (s, e) =>
            {
                TaskCompleted?.Invoke(this, task);
            };

            TaskStarted?.Invoke(this, task);

            await task.StartAsync();
        }

        private void AddTask(WorkerTask task)
        {
            lock (_tasksLock)
            {
                _tasks.Enqueue(task);
                int candidates = _tasks.Count;
                while (_tasks.Count > _maxHistoricalTasks && candidates-- > 0 &&
                       _tasks.TryDequeue(out var oldTask))
                {
                    // A long-running upload or recording still owns its buffers and cancellation source.
                    if (!oldTask.HasFinished)
                    {
                        _tasks.Enqueue(oldTask);
                        continue;
                    }

                    try
                    {
                        oldTask.Dispose();
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteLine($"Error disposing old task: {ex.Message}");
                    }
                }
            }
        }

        public void StopAllTasks()
        {
            foreach (var task in _tasks.Where(t => t.IsWorking))
            {
                task.Stop();
            }
        }

        async Task ITaskManager.StartTask(object? taskSettings, SKBitmap? inputImage)
        {
            await StartTask(taskSettings as TaskSettings, inputImage);
        }

        async Task ITaskManager.StartFileTask(object? taskSettings, string filePath)
        {
            await StartFileTask(taskSettings as TaskSettings, filePath);
        }

        async Task ITaskManager.StartImageUploadTask(object? taskSettings, SKBitmap image)
        {
            await StartImageUploadTask(taskSettings as TaskSettings, image);
        }

        async Task ITaskManager.StartTextTask(object? taskSettings, string text)
        {
            await StartTextTask(taskSettings as TaskSettings, text);
        }
    }
}
