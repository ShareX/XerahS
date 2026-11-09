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
using XerahS.Core.Managers;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Tests.Core;

[TestFixture]
public sealed class RecordingStateMachineTests
{
    [TestCase(RecordingStatus.Idle, RecordingStatus.Initializing)]
    [TestCase(RecordingStatus.Initializing, RecordingStatus.Recording)]
    [TestCase(RecordingStatus.Recording, RecordingStatus.Paused)]
    [TestCase(RecordingStatus.Paused, RecordingStatus.Recording)]
    [TestCase(RecordingStatus.Recording, RecordingStatus.Finalizing)]
    [TestCase(RecordingStatus.Paused, RecordingStatus.Finalizing)]
    [TestCase(RecordingStatus.Finalizing, RecordingStatus.Idle)]
    [TestCase(RecordingStatus.Error, RecordingStatus.Idle)]
    [TestCase(RecordingStatus.Recording, RecordingStatus.Recording)]
    public void ValidTransitions_AreAccepted(RecordingStatus from, RecordingStatus to)
    {
        Assert.That(RecordingStateMachine.IsValidTransition(from, to), Is.True);
    }

    [TestCase(RecordingStatus.Idle, RecordingStatus.Paused)]
    [TestCase(RecordingStatus.Idle, RecordingStatus.Finalizing)]
    [TestCase(RecordingStatus.Finalizing, RecordingStatus.Recording)]
    [TestCase(RecordingStatus.Finalizing, RecordingStatus.Paused)]
    [TestCase(RecordingStatus.Initializing, RecordingStatus.Paused)]
    public void InvalidTransitions_AreRejected(RecordingStatus from, RecordingStatus to)
    {
        Assert.That(RecordingStateMachine.IsValidTransition(from, to), Is.False);
    }

    [Test]
    public void Observe_UpdatesCurrentEvenWhenTransitionIsUnexpected()
    {
        var machine = new RecordingStateMachine();

        Assert.That(machine.Observe(RecordingStatus.Recording), Is.True);
        Assert.That(machine.Observe(RecordingStatus.Initializing), Is.False);
        Assert.That(machine.Current, Is.EqualTo(RecordingStatus.Initializing));
    }

    [Test]
    public void UiState_RecordingAllowsStopAndPauseOnlyWhenSupported()
    {
        RecordingUiState withPause = RecordingUiState.From(RecordingStatus.Recording, supportsPauseResume: true);
        RecordingUiState withoutPause = RecordingUiState.From(RecordingStatus.Recording, supportsPauseResume: false);

        Assert.That(withPause.IsRecording && withPause.CanStop && withPause.CanAbort && withPause.TimerRunning, Is.True);
        Assert.That(withPause.CanStart, Is.False);
        Assert.That(withPause.CanPauseResume, Is.True);
        Assert.That(withoutPause.CanPauseResume, Is.False);
    }

    [TestCase(RecordingStatus.Idle, true)]
    [TestCase(RecordingStatus.Error, true)]
    [TestCase(RecordingStatus.Initializing, false)]
    [TestCase(RecordingStatus.Recording, false)]
    [TestCase(RecordingStatus.Paused, false)]
    [TestCase(RecordingStatus.Finalizing, false)]
    public void UiState_StartOnlyFromIdleOrError(RecordingStatus status, bool canStart)
    {
        Assert.That(RecordingUiState.From(status, true).CanStart, Is.EqualTo(canStart));
    }

    [Test]
    public void UiState_PausedIsNotRecording()
    {
        RecordingUiState paused = RecordingUiState.From(RecordingStatus.Paused, true);

        Assert.That(paused.IsPaused, Is.True);
        Assert.That(paused.IsRecording, Is.False);
        Assert.That(paused.TimerRunning, Is.False);
    }
}
