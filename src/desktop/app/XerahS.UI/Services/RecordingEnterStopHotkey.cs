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

using Avalonia.Input;
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.UI.Services;

/// <summary>
/// While a screen recording is running or paused, Enter stops it. The binding is removed
/// as soon as the recording leaves that state, so Enter is not captured the rest of the time.
/// </summary>
public sealed class RecordingEnterStopHotkey : IDisposable
{
    private readonly IScreenRecordingCoordinator _coordinator;
    private readonly IHotkeyService _hotkeys;
    private readonly HotkeyInfo _enter = new(Key.Return);
    private bool _listening;
    private bool _registerFailed;
    private bool _disposed;

    public RecordingEnterStopHotkey(IScreenRecordingCoordinator coordinator, IHotkeyService hotkeys)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _hotkeys = hotkeys ?? throw new ArgumentNullException(nameof(hotkeys));
        _coordinator.StatusChanged += OnStatusChanged;
        _hotkeys.HotkeyTriggered += OnHotkeyTriggered;
        SetListening(_coordinator.IsRecording || _coordinator.IsPaused);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _coordinator.StatusChanged -= OnStatusChanged;
        _hotkeys.HotkeyTriggered -= OnHotkeyTriggered;
        SetListening(false);
    }

    private void OnStatusChanged(object? sender, RecordingStatusEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        SetListening(e.Status is RecordingStatus.Initializing or RecordingStatus.Recording or RecordingStatus.Paused);
    }

    private void OnHotkeyTriggered(object? sender, HotkeyTriggeredEventArgs e)
    {
        if (_disposed || !_listening || e.HotkeyInfo.Id != _enter.Id)
        {
            return;
        }

        DebugHelper.WriteLine("Enter pressed while recording - stopping.");
        _coordinator.SignalStop();
    }

    private void SetListening(bool listen)
    {
        if (_disposed && listen)
        {
            return;
        }

        if (listen)
        {
            if (_listening || _registerFailed)
            {
                return;
            }

            try
            {
                if (_hotkeys.RegisterHotkey(_enter))
                {
                    _listening = true;
                    DebugHelper.WriteLine("Recording stop hotkey registered: Enter");
                }
                else
                {
                    _registerFailed = true;
                    DebugHelper.WriteLine("Recording stop hotkey failed to register: Enter");
                }
            }
            catch (Exception ex)
            {
                _registerFailed = true;
                DebugHelper.WriteException(ex, "Recording stop hotkey failed to register: Enter");
            }

            return;
        }

        _registerFailed = false;
        if (!_listening)
        {
            return;
        }

        try
        {
            _hotkeys.UnregisterHotkey(_enter);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Recording stop hotkey failed to unregister: Enter");
        }
        finally
        {
            _listening = false;
            DebugHelper.WriteLine("Recording stop hotkey unregistered: Enter");
        }
    }
}
