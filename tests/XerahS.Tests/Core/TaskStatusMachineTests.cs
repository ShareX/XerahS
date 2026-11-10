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

using NUnit.Framework;
using XerahS.Core;
using XerahS.Core.Tasks;
using TaskStatus = XerahS.Core.TaskStatus;

namespace XerahS.Tests.Core;

[TestFixture]
public sealed class TaskStatusMachineTests
{
    [TestCase(TaskStatus.InQueue, TaskStatus.Preparing)]
    [TestCase(TaskStatus.Preparing, TaskStatus.Working)]
    [TestCase(TaskStatus.Preparing, TaskStatus.Failed)]
    [TestCase(TaskStatus.Working, TaskStatus.Stopping)]
    [TestCase(TaskStatus.Working, TaskStatus.Completed)]
    [TestCase(TaskStatus.Working, TaskStatus.Canceled)]
    [TestCase(TaskStatus.Stopping, TaskStatus.Stopped)]
    [TestCase(TaskStatus.Stopping, TaskStatus.Completed)]
    [TestCase(TaskStatus.Completed, TaskStatus.History)]
    [TestCase(TaskStatus.Working, TaskStatus.Working)]
    public void ValidTransitions_AreAccepted(TaskStatus from, TaskStatus to)
    {
        Assert.That(TaskStatusMachine.IsValidTransition(from, to), Is.True);
    }

    [TestCase(TaskStatus.InQueue, TaskStatus.Working)]
    [TestCase(TaskStatus.InQueue, TaskStatus.Completed)]
    [TestCase(TaskStatus.Stopping, TaskStatus.Working)]
    [TestCase(TaskStatus.Completed, TaskStatus.Stopping)]
    [TestCase(TaskStatus.Stopped, TaskStatus.Working)]
    [TestCase(TaskStatus.Failed, TaskStatus.Completed)]
    [TestCase(TaskStatus.History, TaskStatus.InQueue)]
    public void InvalidTransitions_AreRejected(TaskStatus from, TaskStatus to)
    {
        Assert.That(TaskStatusMachine.IsValidTransition(from, to), Is.False);
    }

    [Test]
    public void EveryStatus_IsExactlyOneOfQueuedWorkingOrFinished()
    {
        foreach (TaskStatus status in Enum.GetValues<TaskStatus>())
        {
            int groups = (status == TaskStatus.InQueue ? 1 : 0) +
                (TaskStatusMachine.IsWorking(status) ? 1 : 0) +
                (TaskStatusMachine.IsFinished(status) ? 1 : 0);

            Assert.That(groups, Is.EqualTo(1), status.ToString());
        }
    }

    [Test]
    public void TryTransition_AppliesAnUnexpectedTransitionBetweenUnfinishedStatuses()
    {
        var machine = new TaskStatusMachine();

        Assert.That(machine.TryTransition(TaskStatus.Working), Is.False);
        Assert.That(machine.Current, Is.EqualTo(TaskStatus.Working));
    }

    [TestCase(TaskStatus.Completed)]
    [TestCase(TaskStatus.Stopped)]
    [TestCase(TaskStatus.Failed)]
    [TestCase(TaskStatus.Canceled)]
    public void TryTransition_NeverTakesAFinishedTaskBackToAnUnfinishedStatus(TaskStatus finished)
    {
        var machine = new TaskStatusMachine();
        machine.TryTransition(TaskStatus.Preparing);
        machine.TryTransition(TaskStatus.Working);
        machine.TryTransition(finished);

        // A Stop() that loses the race with completion must not leave the task on "Stopping".
        Assert.That(machine.TryTransition(TaskStatus.Stopping), Is.False);
        Assert.That(machine.Current, Is.EqualTo(finished));
        Assert.That(machine.TryTransition(TaskStatus.Working), Is.False);
        Assert.That(machine.Current, Is.EqualTo(finished));
    }

    [Test]
    public void TryTransition_FollowsTheNormalLifecycle()
    {
        var machine = new TaskStatusMachine();

        Assert.That(machine.Current, Is.EqualTo(TaskStatus.InQueue));
        Assert.That(machine.TryTransition(TaskStatus.Preparing), Is.True);
        Assert.That(machine.TryTransition(TaskStatus.Working), Is.True);
        Assert.That(machine.TryTransition(TaskStatus.Stopping), Is.True);
        Assert.That(machine.TryTransition(TaskStatus.Stopped), Is.True);
        Assert.That(machine.Current, Is.EqualTo(TaskStatus.Stopped));
    }

    // The pipeline's status copy is seeded with Working. A stage outcome wins; an untouched copy must not
    // overwrite what the task did meanwhile.
    [TestCase(TaskStatus.Working, TaskStatus.Working, TaskStatus.Working)]
    [TestCase(TaskStatus.Working, TaskStatus.Failed, TaskStatus.Failed)]
    [TestCase(TaskStatus.Working, TaskStatus.Canceled, TaskStatus.Canceled)]
    [TestCase(TaskStatus.Stopping, TaskStatus.Stopped, TaskStatus.Stopped)]
    [TestCase(TaskStatus.Stopping, TaskStatus.Working, TaskStatus.Stopping)]
    // Regression: a capture cancelled during its start delay was reset to Working and ended as Completed.
    [TestCase(TaskStatus.Stopped, TaskStatus.Working, TaskStatus.Stopped)]
    public void ResolveAfterPipeline_KeepsTheTaskStatusUnlessAStageDecidedAnOutcome(
        TaskStatus taskStatus, TaskStatus pipelineStatus, TaskStatus expected)
    {
        Assert.That(TaskStatusMachine.ResolveAfterPipeline(taskStatus, pipelineStatus), Is.EqualTo(expected));
    }
}
