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

namespace XerahS.Core.Tasks;

/// <summary>
/// Owns the lifecycle of one <see cref="WorkerTask"/> (<see cref="TaskStatus"/>) and documents which
/// transitions are legal. Legal transitions:
/// InQueue -> Preparing | Canceled;
/// Preparing -> Working | Stopping | Stopped | Failed;
/// Working -> Stopping | Stopped | Failed | Canceled | Completed;
/// Stopping -> Stopped | Failed | Canceled | Completed (Completed: the stop request came too late);
/// Stopped | Failed | Canceled | Completed -> History.
/// Repeating the current status is always allowed.
/// One rule is enforced: a finished task (Stopped, Failed, Canceled, Completed, History) never goes back
/// to an unfinished status, so a late Stop() cannot leave a finished task stuck on "Stopping". Every other
/// unexpected transition is logged and still applied, so behaviour does not change.
/// </summary>
internal sealed class TaskStatusMachine
{
    private readonly object _lock = new();
    private TaskStatus _current = TaskStatus.InQueue;

    public TaskStatus Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    /// <summary>The task has ended and its status is final (History is only a later relabel).</summary>
    public static bool IsFinished(TaskStatus status) =>
        status is TaskStatus.Stopped or TaskStatus.Failed or TaskStatus.Canceled or TaskStatus.Completed or TaskStatus.History;

    /// <summary>The task has started and has not ended yet.</summary>
    public static bool IsWorking(TaskStatus status) =>
        status is TaskStatus.Preparing or TaskStatus.Working or TaskStatus.Stopping;

    /// <summary>The task ended without the outcome the user asked for.</summary>
    public static bool IsUnsuccessfulEnd(TaskStatus status) =>
        status is TaskStatus.Failed or TaskStatus.Canceled or TaskStatus.Stopped;

    public static bool IsValidTransition(TaskStatus from, TaskStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return from switch
        {
            TaskStatus.InQueue => to is TaskStatus.Preparing or TaskStatus.Canceled,
            TaskStatus.Preparing => to is TaskStatus.Working or TaskStatus.Stopping or TaskStatus.Stopped or TaskStatus.Failed,
            TaskStatus.Working => to is TaskStatus.Stopping or TaskStatus.Stopped or TaskStatus.Failed or TaskStatus.Canceled or TaskStatus.Completed,
            TaskStatus.Stopping => to is TaskStatus.Stopped or TaskStatus.Failed or TaskStatus.Canceled or TaskStatus.Completed,
            TaskStatus.Stopped or TaskStatus.Failed or TaskStatus.Canceled or TaskStatus.Completed => to is TaskStatus.History,
            _ => false
        };
    }

    /// <summary>
    /// Moves to <paramref name="next"/>. Returns false (after logging) when the transition is not in the
    /// table. The status is still updated unless that would take a finished task back to an unfinished
    /// status; that one case is ignored.
    /// </summary>
    public bool TryTransition(TaskStatus next)
    {
        TaskStatus previous;
        bool applied = true;
        lock (_lock)
        {
            previous = _current;
            if (IsFinished(previous) && !IsFinished(next))
            {
                applied = false;
            }
            else
            {
                _current = next;
            }
        }

        if (IsValidTransition(previous, next))
        {
            return true;
        }

        DebugHelper.WriteLine(applied
            ? $"WorkerTask: unexpected task transition {previous} -> {next}"
            : $"WorkerTask: ignored task transition {previous} -> {next}; the task has already finished");
        return false;
    }

    /// <summary>
    /// The status a task takes after its pipeline returns. The pipeline works on its own copy of the
    /// status, seeded with Working. If a stage decided an outcome, that outcome wins. If the copy is
    /// still Working, the task keeps its own status, which may have moved on meanwhile (Stopping after
    /// Stop(), or Stopped after a cancelled capture delay) and must not be reset to Working.
    /// </summary>
    public static TaskStatus ResolveAfterPipeline(TaskStatus taskStatus, TaskStatus pipelineStatus) =>
        pipelineStatus == TaskStatus.Working ? taskStatus : pipelineStatus;
}
