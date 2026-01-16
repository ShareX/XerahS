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

namespace XerahS.Core;

/// <summary>
/// Manages the list of recent tasks (MVVM-compliant, no UI dependencies)
/// </summary>
public class RecentTaskManager
{
    private readonly object _itemsLock = new();
    private int _maxCount = 10;

    public int MaxCount
    {
        get => _maxCount;
        set
        {
            _maxCount = value.Clamp(1, 100);
            lock (_itemsLock)
            {
                while (Tasks.Count > _maxCount)
                {
                    Tasks.Dequeue();
                }
                OnTasksChanged();
            }
        }
    }

    public Queue<RecentTask> Tasks { get; private set; } = new();

    /// <summary>
    /// Event raised when the task list changes
    /// </summary>
    public event EventHandler? TasksChanged;

    public RecentTaskManager()
    {
    }

    /// <summary>
    /// Initialize from saved settings
    /// </summary>
    public void Initialize(RecentTask[]? savedTasks, int maxCount)
    {
        lock (_itemsLock)
        {
            _maxCount = maxCount.Clamp(1, 100);

            if (savedTasks != null)
            {
                Tasks = new Queue<RecentTask>(savedTasks.Take(_maxCount));
            }
            else
            {
                Tasks = new Queue<RecentTask>();
            }

            OnTasksChanged();
        }
    }

    /// <summary>
    /// Add a completed task to recent list
    /// </summary>
    public void Add(TaskInfo taskInfo)
    {
        string info = taskInfo.ToString();

        if (!string.IsNullOrEmpty(info))
        {
            var recentItem = new RecentTask
            {
                FilePath = taskInfo.FilePath,
                URL = taskInfo.Result?.URL ?? "",
                ThumbnailURL = taskInfo.Result?.ThumbnailURL ?? "",
                DeletionURL = taskInfo.Result?.DeletionURL ?? "",
                ShortenedURL = taskInfo.Result?.ShortenedURL ?? ""
            };

            Add(recentItem);
        }
    }

    /// <summary>
    /// Add a RecentTask directly
    /// </summary>
    public void Add(RecentTask task)
    {
        lock (_itemsLock)
        {
            while (Tasks.Count >= MaxCount)
            {
                Tasks.Dequeue();
            }

            Tasks.Enqueue(task);
            OnTasksChanged();
        }
    }

    /// <summary>
    /// Clear all recent tasks
    /// </summary>
    public void Clear()
    {
        lock (_itemsLock)
        {
            Tasks.Clear();
            OnTasksChanged();
        }
    }

    /// <summary>
    /// Get tasks as array for saving
    /// </summary>
    public RecentTask[] ToArray()
    {
        lock (_itemsLock)
        {
            return Tasks.ToArray();
        }
    }

    /// <summary>
    /// Get all tasks (enumerable)
    /// </summary>
    public IEnumerable<RecentTask> GetTasks(bool mostRecentFirst = false)
    {
        lock (_itemsLock)
        {
            var tasks = Tasks.ToList();
            if (mostRecentFirst)
            {
                tasks.Reverse();
            }
            return tasks;
        }
    }

    protected virtual void OnTasksChanged()
    {
        TasksChanged?.Invoke(this, EventArgs.Empty);
    }
}
