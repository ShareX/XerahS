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
    internal sealed class ScreenRecordingCoordinatorAdapter(ScreenRecordingManager manager) : IScreenRecordingCoordinator
    {
        public event EventHandler<RecordingStatusEventArgs>? StatusChanged
        {
            add => manager.StatusChanged += value;
            remove => manager.StatusChanged -= value;
        }

        public event EventHandler<RecordingErrorEventArgs>? ErrorOccurred
        {
            add => manager.ErrorOccurred += value;
            remove => manager.ErrorOccurred -= value;
        }

        public event EventHandler<RecordingStartedEventArgs>? RecordingStarted
        {
            add => manager.RecordingStarted += value;
            remove => manager.RecordingStarted -= value;
        }

        public bool IsRecording => manager.IsRecording;
        public bool IsPaused => manager.IsPaused;
        public bool IsUsingFallback => manager.IsUsingFallback;
        public RecordingRuntimeCapabilities CurrentCapabilities => manager.CurrentCapabilities;
        public RecordingOptions? CurrentOptions => manager.CurrentOptions;

        public Task? PlatformInitializationTask
        {
            get => ScreenRecordingManager.PlatformInitializationTask;
            set => ScreenRecordingManager.PlatformInitializationTask = value;
        }

        public Task StartRecordingAsync(RecordingOptions options) => manager.StartRecordingAsync(options);
        public Task<string?> StopRecordingAsync() => manager.StopRecordingAsync();
        public Task AbortRecordingAsync() => manager.AbortRecordingAsync();
        public Task TogglePauseResumeAsync() => manager.TogglePauseResumeAsync();
        public Task ResumeRecordingAtAsync(System.Drawing.Rectangle region) => manager.ResumeRecordingAtAsync(region);
        public void SignalStop() => manager.SignalStop();
        public void RequestRestart() => manager.RequestRestart();
    }
}
