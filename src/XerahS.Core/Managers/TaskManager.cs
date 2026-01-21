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
        public event EventHandler<WorkerTask>? TaskStarted;

        public async Task StartTask(TaskSettings? taskSettings, SkiaSharp.SKBitmap? inputImage = null)
        {
            if (taskSettings == null)
            {
                DebugHelper.WriteLine("StartTask called with null TaskSettings, skipping.");
                return;
            }

            TroubleshootingHelper.Log(taskSettings?.Job.ToString() ?? "Unknown", "TASK_MANAGER", $"StartTask Entry: TaskSettings={taskSettings != null}");
            
            var safeTaskSettings = taskSettings ?? new TaskSettings();
            var task = WorkerTask.Create(safeTaskSettings, inputImage);
            _tasks.Add(task);
            
            TroubleshootingHelper.Log(task.Info?.TaskSettings?.Job.ToString() ?? "Unknown", "TASK_MANAGER", "Task created");

            task.StatusChanged += (s, e) => DebugHelper.WriteLine($"Task Status: {task.Status}");
            task.TaskCompleted += (s, e) =>
            {
                // Fire event so listeners (like App.axaml.cs) can update UI
                TaskCompleted?.Invoke(this, task);
            };

            TaskStarted?.Invoke(this, task);

            TroubleshootingHelper.Log(task.Info?.TaskSettings?.Job.ToString() ?? "Unknown", "TASK_MANAGER", "Calling task.StartAsync...");
            await task.StartAsync();
            TroubleshootingHelper.Log(task.Info?.TaskSettings?.Job.ToString() ?? "Unknown", "TASK_MANAGER", "task.StartAsync completed");
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

            TroubleshootingHelper.Log(taskSettings?.Job.ToString() ?? "Unknown", "TASK_MANAGER", $"StartFileTask Entry: FilePath={filePath}");

            var safeTaskSettings = taskSettings ?? new TaskSettings();
            var task = WorkerTask.Create(safeTaskSettings);
            task.Info.FilePath = filePath;
            task.Info.DataType = EDataType.File;
            task.Info.Job = TaskJob.FileUpload;
            _tasks.Add(task);

            task.StatusChanged += (s, e) => DebugHelper.WriteLine($"Task Status: {task.Status}");
            task.TaskCompleted += (s, e) =>
            {
                TaskCompleted?.Invoke(this, task);
            };

            TaskStarted?.Invoke(this, task);

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
