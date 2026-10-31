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
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// Hotkey service whose backend can be replaced while XerahS runs, for example when Quick Setup grants
/// keyboard access and hotkeys move from the portal or X11 to direct evdev listening.
/// It owns hotkey ids so they stay stable across backends, and it re-registers every requested hotkey
/// on the new backend.
/// </summary>
internal sealed class SwitchableHotkeyService : IHotkeyService
{
    private readonly object _lock = new();
    private readonly Dictionary<ushort, HotkeyInfo> _requested = new();
    private IHotkeyService _backend;
    private ushort _lastId;
    private bool _isSuspended;
    private bool _disposed;

    public SwitchableHotkeyService(IHotkeyService backend)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        Attach(_backend);
    }

    public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;

    public event EventHandler? HotkeysChanged;

    /// <summary>The backend currently delivering hotkeys.</summary>
    public IHotkeyService Backend
    {
        get
        {
            lock (_lock)
            {
                return _backend;
            }
        }
    }

    public bool IsSuspended
    {
        get
        {
            lock (_lock)
            {
                return _isSuspended;
            }
        }
        set
        {
            lock (_lock)
            {
                _isSuspended = value;
                _backend.IsSuspended = value;
            }
        }
    }

    public bool RegisterHotkey(HotkeyInfo hotkeyInfo)
    {
        ArgumentNullException.ThrowIfNull(hotkeyInfo);

        lock (_lock)
        {
            if (hotkeyInfo.Id == 0)
            {
                hotkeyInfo.Id = NextId();
            }

            _requested[hotkeyInfo.Id] = hotkeyInfo;
            return _backend.RegisterHotkey(hotkeyInfo);
        }
    }

    public bool UnregisterHotkey(HotkeyInfo hotkeyInfo)
    {
        ArgumentNullException.ThrowIfNull(hotkeyInfo);

        lock (_lock)
        {
            _requested.Remove(hotkeyInfo.Id);
            return _backend.UnregisterHotkey(hotkeyInfo);
        }
    }

    public void UnregisterAll()
    {
        lock (_lock)
        {
            _requested.Clear();
            _backend.UnregisterAll();
        }
    }

    public bool IsRegistered(HotkeyInfo hotkeyInfo)
    {
        lock (_lock)
        {
            return _backend.IsRegistered(hotkeyInfo);
        }
    }

    public Task<bool> ShowInteractiveConfigurationAsync() => Backend.ShowInteractiveConfigurationAsync();

    public void NotifyWindowReady() => Backend.NotifyWindowReady();

    public HotkeyDiagnostics GetDiagnostics() => Backend.GetDiagnostics();

    /// <summary>
    /// Moves every requested hotkey to <paramref name="replacement"/> and disposes the old backend.
    /// </summary>
    public void SwitchTo(IHotkeyService replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        IHotkeyService previous;
        int registered = 0;
        int requested;

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (ReferenceEquals(replacement, _backend))
            {
                return;
            }

            previous = _backend;
            Detach(previous);
            previous.UnregisterAll();

            _backend = replacement;
            _backend.IsSuspended = _isSuspended;
            Attach(_backend);

            requested = _requested.Count;
            foreach (HotkeyInfo hotkey in _requested.Values)
            {
                if (_backend.RegisterHotkey(hotkey))
                {
                    registered++;
                }
            }
        }

        try
        {
            previous.Dispose();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "SwitchableHotkeyService: failed to dispose the previous backend");
        }

        DebugHelper.WriteLine(
            $"SwitchableHotkeyService: switched to {replacement.GetType().Name}; re-registered {registered}/{requested} hotkeys.");
        HotkeysChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        IHotkeyService backend;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            backend = _backend;
            Detach(backend);
        }

        backend.Dispose();
    }

    private ushort NextId()
    {
        do
        {
            _lastId = _lastId == ushort.MaxValue ? (ushort)1 : (ushort)(_lastId + 1);
        }
        while (_requested.ContainsKey(_lastId));

        return _lastId;
    }

    private void Attach(IHotkeyService backend)
    {
        backend.HotkeyTriggered += OnBackendHotkeyTriggered;
        backend.HotkeysChanged += OnBackendHotkeysChanged;
    }

    private void Detach(IHotkeyService backend)
    {
        backend.HotkeyTriggered -= OnBackendHotkeyTriggered;
        backend.HotkeysChanged -= OnBackendHotkeysChanged;
    }

    private void OnBackendHotkeyTriggered(object? sender, HotkeyTriggeredEventArgs e) => HotkeyTriggered?.Invoke(this, e);

    private void OnBackendHotkeysChanged(object? sender, EventArgs e) => HotkeysChanged?.Invoke(this, e);
}
