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

using XerahS.Core.Managers;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Bootstrap
{
    /// <summary>
    /// Host-facing abstraction over the shared screen recording coordinator.
    /// </summary>
    public interface IScreenRecordingCoordinator
    {
        event EventHandler<RecordingStatusEventArgs>? StatusChanged;
        event EventHandler<RecordingErrorEventArgs>? ErrorOccurred;
        event EventHandler<RecordingStartedEventArgs>? RecordingStarted;

        bool IsRecording { get; }
        bool IsPaused { get; }
        bool IsUsingFallback { get; }
        RecordingRuntimeCapabilities CurrentCapabilities { get; }
        RecordingOptions? CurrentOptions => null;
        Task? PlatformInitializationTask { get; set; }

        Task StartRecordingAsync(RecordingOptions options);
        Task<string?> StopRecordingAsync();
        Task AbortRecordingAsync();
        Task TogglePauseResumeAsync();
        Task ResumeRecordingAtAsync(System.Drawing.Rectangle region) =>
            Task.FromException(new NotSupportedException("This recorder cannot move a paused region."));
        void SignalStop();

        /// <summary>Discard the current take and start recording again with the same settings.</summary>
        void RequestRestart() { }
    }
}
