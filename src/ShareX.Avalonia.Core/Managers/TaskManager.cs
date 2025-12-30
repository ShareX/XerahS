using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShareX.Avalonia.Core;
using ShareX.Avalonia.Core.Tasks;
using ShareX.Avalonia.Common;

namespace ShareX.Avalonia.Core.Managers
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

        public async Task StartTask(TaskSettings taskSettings)
        {
            var task = WorkerTask.Create(taskSettings);
            _tasks.Add(task);

            task.StatusChanged += (s, e) => DebugHelper.WriteLine($"Task Status: {task.Status}");
            
            await task.StartAsync();
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
