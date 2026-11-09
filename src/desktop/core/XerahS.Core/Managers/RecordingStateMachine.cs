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
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Core.Managers;

/// <summary>
/// Tracks the recording lifecycle (<see cref="RecordingStatus"/>) and documents which transitions are legal.
/// Observe-only: an unexpected transition is logged and the new status is still recorded, so behaviour
/// never changes. Legal transitions:
/// Idle -> Initializing | Recording | Error;
/// Initializing -> Recording | Idle | Error;
/// Recording -> Paused | Finalizing | Idle | Error;
/// Paused -> Recording | Finalizing | Idle | Error;
/// Finalizing -> Idle | Error;
/// Error -> Idle | Initializing | Recording.
/// Repeating the current status is always allowed.
/// </summary>
internal sealed class RecordingStateMachine
{
    private readonly object _lock = new();
    private RecordingStatus _current = RecordingStatus.Idle;

    public RecordingStatus Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    public static bool IsValidTransition(RecordingStatus from, RecordingStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return from switch
        {
            RecordingStatus.Idle => to is RecordingStatus.Initializing or RecordingStatus.Recording or RecordingStatus.Error,
            RecordingStatus.Initializing => to is RecordingStatus.Recording or RecordingStatus.Idle or RecordingStatus.Error,
            RecordingStatus.Recording => to is RecordingStatus.Paused or RecordingStatus.Finalizing or RecordingStatus.Idle or RecordingStatus.Error,
            RecordingStatus.Paused => to is RecordingStatus.Recording or RecordingStatus.Finalizing or RecordingStatus.Idle or RecordingStatus.Error,
            RecordingStatus.Finalizing => to is RecordingStatus.Idle or RecordingStatus.Error,
            RecordingStatus.Error => to is RecordingStatus.Idle or RecordingStatus.Initializing or RecordingStatus.Recording,
            _ => false
        };
    }

    /// <summary>
    /// Records <paramref name="next"/> as the current status. Returns false (after logging) when the
    /// transition is not in the table; the status is updated either way.
    /// </summary>
    public bool Observe(RecordingStatus next)
    {
        RecordingStatus previous;
        lock (_lock)
        {
            previous = _current;
            _current = next;
        }

        if (IsValidTransition(previous, next))
        {
            return true;
        }

        DebugHelper.WriteLine($"ScreenRecordingManager: unexpected recording transition {previous} -> {next}");
        return false;
    }
}

/// <summary>
/// What the UI should show for a given <see cref="RecordingStatus"/>. Single source of truth for the
/// status text and button availability.
/// </summary>
public readonly record struct RecordingUiState(
    string StatusText,
    bool IsRecording,
    bool IsPaused,
    bool CanStart,
    bool CanStop,
    bool CanPauseResume,
    bool CanAbort,
    bool TimerRunning)
{
    public static RecordingUiState From(RecordingStatus status, bool supportsPauseResume) => status switch
    {
        RecordingStatus.Initializing => new("Initializing...", false, false, false, false, false, false, false),
        RecordingStatus.Recording => new("Recording", true, false, false, true, supportsPauseResume, true, true),
        RecordingStatus.Paused => new("Paused", false, true, false, true, supportsPauseResume, true, false),
        RecordingStatus.Finalizing => new("Finalizing...", false, false, false, false, false, false, false),
        RecordingStatus.Error => new("Error", false, false, true, false, false, false, false),
        _ => new("Ready", false, false, true, false, false, false, false)
    };
}
