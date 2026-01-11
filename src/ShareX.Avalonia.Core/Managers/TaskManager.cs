using XerahS.Common;
using XerahS.Core.Helpers;
using XerahS.Core.Tasks;
using System.Collections.Concurrent;

namespace XerahS.Core.Managers
{
    public class TaskManager
    {
        private static readonly Lazy<TaskManager> _lazy = new(() => new TaskManager());
        public static TaskManager Instance => _lazy.Value;

        private readonly ConcurrentBag<WorkerTask> _tasks = new();
        public IEnumerable<WorkerTask> Tasks => _tasks;

        private TaskManager()
        {
        }

        // Event fired when a task completes with an image
        public event EventHandler<WorkerTask>? TaskCompleted;

        public async Task StartTask(TaskSettings taskSettings, SkiaSharp.SKBitmap? inputImage = null)
        {
            TroubleshootingHelper.Log(taskSettings?.Job.ToString() ?? "Unknown", "TASK_MANAGER", $"StartTask Entry: TaskSettings={taskSettings != null}");
            
            var task = WorkerTask.Create(taskSettings, inputImage);
            _tasks.Add(task);
            
            TroubleshootingHelper.Log(task.Info?.TaskSettings?.Job.ToString() ?? "Unknown", "TASK_MANAGER", "Task created");

            task.StatusChanged += (s, e) => DebugHelper.WriteLine($"Task Status: {task.Status}");
            task.TaskCompleted += (s, e) =>
            {
                // Fire event so listeners (like App.axaml.cs) can update UI
                TaskCompleted?.Invoke(this, task);
            };

            TroubleshootingHelper.Log(task.Info?.TaskSettings?.Job.ToString() ?? "Unknown", "TASK_MANAGER", "Calling task.StartAsync...");
            await task.StartAsync();
            TroubleshootingHelper.Log(task.Info?.TaskSettings?.Job.ToString() ?? "Unknown", "TASK_MANAGER", "task.StartAsync completed");
        }

        public void StopAllTasks()
        {
            foreach (var task in _tasks.Where(t => t.IsWorking))
            {
                task.Stop();
            }
        }
    }
}
