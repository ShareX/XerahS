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

namespace XerahS.RegionCapture.ScreenRecording;

/// <summary>
/// Defines how a recording backend can support pause and resume.
/// </summary>
public enum RecordingPauseBehavior
{
    /// <summary>
    /// Pause and resume are not supported safely for this backend.
    /// </summary>
    Unsupported,

    /// <summary>
    /// Pause is implemented by stopping the current segment and resuming with a new segment.
    /// </summary>
    SegmentedRestart,

    /// <summary>
    /// Pause and resume can be handled natively without tearing down the recording session.
    /// </summary>
    NativePauseResume
}

/// <summary>
/// Runtime capabilities exposed by a concrete recording backend for a specific start request.
/// </summary>
public readonly record struct RecordingRuntimeCapabilities(
    RecordingPauseBehavior PauseBehavior,
    bool RequiresPersistentSession = false,
    bool SupportsRegionMove = false)
{
    public static RecordingRuntimeCapabilities None { get; } =
        new(RecordingPauseBehavior.Unsupported);

    public static RecordingRuntimeCapabilities SegmentedRestart { get; } =
        new(RecordingPauseBehavior.SegmentedRestart);

    public bool SupportsPauseResume => PauseBehavior != RecordingPauseBehavior.Unsupported;
}
