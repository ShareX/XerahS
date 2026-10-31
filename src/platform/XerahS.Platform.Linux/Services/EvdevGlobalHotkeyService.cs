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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Avalonia.Threading;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Input;
using XerahS.Platform.Linux.Input.Evdev;
using HotkeyStatus = XerahS.Platform.Abstractions.HotkeyStatus;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// Global hotkey provider that listens directly to raw keyboard input via evdev,
/// rather than relying on the XDG GlobalShortcuts portal or X11 key grabs. This works
/// uniformly across Wayland compositors (GNOME, KDE, Hyprland) and X11.
///
/// XerahS receives every key event on the system and matches it against its own list
/// of configured hotkeys, so hotkey ownership is decided by XerahS, not the compositor.
/// </summary>
public sealed class EvdevGlobalHotkeyService : IHotkeyService
{
    private readonly Dictionary<ushort, HotkeyInfo> _registered = new();
    private readonly object _registrationLock = new();
    private readonly ModifierStateTracker _modifierTracker = new();
    private readonly EvdevHotkeyMatcher _matcher = new();
    private readonly List<EvdevReader> _readers = new();
    private readonly object _readerLock = new();
    private ushort _nextId = 1;
    private bool _listening;
    private bool _disposed;
    private int _restartInProgress;

    public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;
    public event EventHandler? HotkeysChanged
    {
        add { }
        remove { }
    }

    public bool IsSuspended { get; set; }

    /// <summary>
    /// Returns true when at least one keyboard device can be opened for reading,
    /// i.e. the evdev hotkey path is viable for this user/session.
    /// </summary>
    public static bool IsAvailable()
    {
        if (!OperatingSystem.IsLinux() || !InputDeviceEnumerator.InputDirectoryExists())
        {
            return false;
        }

        return InputDeviceEnumerator.GetReadableKeyboards().Count > 0;
    }

    public EvdevGlobalHotkeyService()
    {
        StartListening();
    }

    private void StartListening()
    {
        if (_listening || _disposed)
        {
            return;
        }

        var keyboards = InputDeviceEnumerator.GetReadableKeyboards();
        if (keyboards.Count == 0)
        {
            DebugHelper.WriteLine("EvdevGlobalHotkeyService: No readable keyboard devices found. Hotkeys via evdev are unavailable.");
            return;
        }

        lock (_readerLock)
        {
            foreach (var device in keyboards)
            {
                try
                {
                    var reader = new EvdevReader(device.Path, device.Name);
                    reader.EventReceived += OnEvdevEvent;
                    reader.ErrorOccurred += OnReaderError;
                    reader.Start();
                    _readers.Add(reader);
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, $"EvdevGlobalHotkeyService: Failed to open {device.Name} ({device.Path})");
                }
            }

            _listening = _readers.Count > 0;
        }

        DebugHelper.WriteLine($"EvdevGlobalHotkeyService: Listening on {_readers.Count} keyboard device(s).");
    }

    private void StopListening()
    {
        lock (_readerLock)
        {
            foreach (var reader in _readers)
            {
                try
                {
                    reader.EventReceived -= OnEvdevEvent;
                    reader.ErrorOccurred -= OnReaderError;
                    reader.Dispose();
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "EvdevGlobalHotkeyService: Error stopping reader");
                }
            }

            _readers.Clear();
            _listening = false;
        }

        _modifierTracker.Clear();
    }

    private void OnEvdevEvent(EvdevReader reader, EvdevNative.InputEvent ev)
    {
        if (_disposed || ev.Type != InputEventCodes.EV_KEY)
        {
            return;
        }

        ushort code = ev.Code;

        // value: 0 = up, 1 = down, 2 = auto-repeat.
        if (InputEventCodes.IsModifierKey(code))
        {
            if (ev.Value == 1)
            {
                _modifierTracker.OnKeyDown(code);
            }
            else if (ev.Value == 0)
            {
                _modifierTracker.OnKeyUp(code);
            }

            return;
        }

        if (ev.Value != 1)
        {
            return; // Only act on the initial key-down for non-modifier keys.
        }

        if (IsSuspended)
        {
            return;
        }

        var currentModifiers = _modifierTracker.CurrentModifiers;
        long now = Environment.TickCount64;

        HotkeyInfo? matched = null;
        lock (_registrationLock)
        {
            foreach (var hotkey in _registered.Values)
            {
                if (_matcher.TryMatch(hotkey, code, currentModifiers, now))
                {
                    matched = hotkey;
                    break;
                }
            }
        }

        if (matched == null)
        {
            return;
        }

        var args = new HotkeyTriggeredEventArgs(matched);
        try
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    HotkeyTriggered?.Invoke(this, args);
                }
                catch (ObjectDisposedException)
                {
                    DebugHelper.WriteLine("EvdevGlobalHotkeyService: handler disposed during invoke, skipping.");
                }
            });
        }
        catch (ObjectDisposedException)
        {
            DebugHelper.WriteLine("EvdevGlobalHotkeyService: dispatcher disposed, skipping hotkey event.");
        }
    }

    private void OnReaderError(Exception ex)
    {
        if (_disposed)
        {
            return;
        }

        DebugHelper.WriteException(ex, "EvdevGlobalHotkeyService: Reader error; attempting to restart listening.");
        _ = TryRestartAsync();
    }

    private async System.Threading.Tasks.Task TryRestartAsync()
    {
        if (Interlocked.CompareExchange(ref _restartInProgress, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await System.Threading.Tasks.Task.Delay(500).ConfigureAwait(false);
            if (_disposed)
            {
                return;
            }

            StopListening();
            StartListening();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "EvdevGlobalHotkeyService: Failed to restart listening");
        }
        finally
        {
            Interlocked.Exchange(ref _restartInProgress, 0);
        }
    }

    public bool RegisterHotkey(HotkeyInfo hotkeyInfo)
    {
        if (!hotkeyInfo.IsValid)
        {
            hotkeyInfo.Status = HotkeyStatus.NotConfigured;
            return false;
        }

        if (!EvdevKeyMap.TryGetEvdevCode(hotkeyInfo.Key, out _))
        {
            hotkeyInfo.Status = HotkeyStatus.Failed;
            DebugHelper.WriteLine($"EvdevGlobalHotkeyService: No evdev mapping for key {hotkeyInfo.Key}.");
            return false;
        }

        lock (_registrationLock)
        {
            if (hotkeyInfo.Id == 0)
            {
                hotkeyInfo.Id = _nextId++;
            }

            _registered[hotkeyInfo.Id] = hotkeyInfo;
        }

        if (!_listening)
        {
            hotkeyInfo.Status = HotkeyStatus.Failed;
            DebugHelper.WriteLine("EvdevGlobalHotkeyService: Registered hotkey but no input devices are being listened to.");
            return false;
        }

        hotkeyInfo.Status = HotkeyStatus.Registered;
        return true;
    }

    public bool UnregisterHotkey(HotkeyInfo hotkeyInfo)
    {
        if (hotkeyInfo.Id == 0)
        {
            hotkeyInfo.Status = HotkeyStatus.NotConfigured;
            return false;
        }

        bool removed;
        lock (_registrationLock)
        {
            removed = _registered.Remove(hotkeyInfo.Id);
        }

        _matcher.Forget(hotkeyInfo.Id);
        hotkeyInfo.Status = HotkeyStatus.NotConfigured;
        return removed;
    }

    public void UnregisterAll()
    {
        lock (_registrationLock)
        {
            _registered.Clear();
        }

        _matcher.ResetDebounce();
    }

    public bool IsRegistered(HotkeyInfo hotkeyInfo)
    {
        lock (_registrationLock)
        {
            return hotkeyInfo.Id != 0 && _registered.ContainsKey(hotkeyInfo.Id);
        }
    }

    public HotkeyDiagnostics GetDiagnostics()
    {
        bool listening;
        lock (_readerLock)
        {
            listening = _listening;
        }

        return listening
            ? new HotkeyDiagnostics(HotkeyBackendState.Native, "Direct keyboard access (evdev)", null)
            : new HotkeyDiagnostics(
                HotkeyBackendState.Unavailable,
                "Direct keyboard access (evdev)",
                "XerahS cannot read any keyboard, so global hotkeys do not work. Run 'xerahs doctor --linux-input' for details.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopListening();
        GC.SuppressFinalize(this);
    }
}
