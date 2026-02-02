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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Threading;
using XerahS.Platform.Linux.Capture;
using Tmds.DBus;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using PlatformHotkeyStatus = XerahS.Platform.Abstractions.HotkeyStatus;

namespace XerahS.Platform.Linux.Services;

public sealed class WaylandPortalHotkeyService : IHotkeyService
{
    private const string PortalBusName = "org.freedesktop.portal.Desktop";
    private static readonly ObjectPath PortalObjectPath = new("/org/freedesktop/portal/desktop");

    private readonly Connection? _connection;
    private readonly IGlobalShortcuts? _portal;
    private readonly SemaphoreSlim _bindSemaphore = new(1, 1);
    private readonly object _hotkeyLock = new();
    private readonly Dictionary<ushort, HotkeyInfo> _registered = new();
    private Dictionary<string, HotkeyInfo> _shortcutMap = new();
    private ushort _nextId = 1;
    private ObjectPath? _sessionHandle;
    private IPortalSession? _sessionProxy;
    private IDisposable? _activatedSubscription;
    private IDisposable? _deactivatedSubscription;
    private bool _disposed;

    public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;
    public bool IsSuspended { get; set; }

    public WaylandPortalHotkeyService()
    {
        try
        {
            _connection = new Connection(Address.Session);
            _connection.ConnectAsync().GetAwaiter().GetResult();
            _portal = _connection.CreateProxy<IGlobalShortcuts>(PortalBusName, PortalObjectPath);
            _activatedSubscription = _portal.WatchActivatedAsync(OnActivated).GetAwaiter().GetResult();
            _deactivatedSubscription = _portal.WatchDeactivatedAsync(OnDeactivated).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "WaylandPortalHotkeyService: Unable to initialize portal");
            _portal = null;
            _connection?.Dispose();
        }
    }

    public bool RegisterHotkey(HotkeyInfo hotkeyInfo)
    {
        if (_portal == null || !hotkeyInfo.IsValid)
        {
            hotkeyInfo.Status = _portal == null ? PlatformHotkeyStatus.UnsupportedPlatform : PlatformHotkeyStatus.NotConfigured;
            return false;
        }

        lock (_hotkeyLock)
        {
            if (hotkeyInfo.Id == 0)
            {
                hotkeyInfo.Id = _nextId++;
            }

            _registered[hotkeyInfo.Id] = hotkeyInfo;
        }

        try
        {
            RebindShortcutsAsync().GetAwaiter().GetResult();
            hotkeyInfo.Status = PlatformHotkeyStatus.Registered;
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "WaylandPortalHotkeyService: Failed to register hotkey");
            hotkeyInfo.Status = PlatformHotkeyStatus.Failed;
            return false;
        }
    }

    public bool UnregisterHotkey(HotkeyInfo hotkeyInfo)
    {
        if (_portal == null || hotkeyInfo.Id == 0)
        {
            hotkeyInfo.Status = PlatformHotkeyStatus.NotConfigured;
            return false;
        }

        bool removed;
        lock (_hotkeyLock)
        {
            removed = _registered.Remove(hotkeyInfo.Id);
        }

        if (!removed)
        {
            hotkeyInfo.Status = PlatformHotkeyStatus.NotConfigured;
            return false;
        }

        try
        {
            RebindShortcutsAsync().GetAwaiter().GetResult();
            hotkeyInfo.Status = PlatformHotkeyStatus.NotConfigured;
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "WaylandPortalHotkeyService: Failed to unregister hotkey");
            hotkeyInfo.Status = PlatformHotkeyStatus.Failed;
            return false;
        }
    }

    public void UnregisterAll()
    {
        if (_portal == null)
        {
            return;
        }

        lock (_hotkeyLock)
        {
            _registered.Clear();
        }

        try
        {
            RebindShortcutsAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "WaylandPortalHotkeyService: Failed to unregister all hotkeys");
        }
    }

    public bool IsRegistered(HotkeyInfo hotkeyInfo)
    {
        lock (_hotkeyLock)
        {
            return hotkeyInfo.Id != 0 && _registered.ContainsKey(hotkeyInfo.Id);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _activatedSubscription?.Dispose();
        _deactivatedSubscription?.Dispose();
        CloseSessionAsync().GetAwaiter().GetResult();
        _connection?.Dispose();
        _bindSemaphore.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private async Task RebindShortcutsAsync()
    {
        if (_portal == null)
        {
            throw new InvalidOperationException("Global shortcuts portal is not available.");
        }

        await _bindSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var (bindings, map) = BuildShortcutBindings();
            if (bindings.Length == 0)
            {
                await CloseSessionAsync().ConfigureAwait(false);
                _shortcutMap.Clear();
                return;
            }

            await CloseSessionAsync().ConfigureAwait(false);
            _sessionHandle = await CreateSessionAsync().ConfigureAwait(false);
            ObjectPath sessionHandle = (ObjectPath)_sessionHandle!;
            _sessionProxy = _connection!.CreateProxy<IPortalSession>(PortalBusName, sessionHandle);
            await BindShortcutsAsync(bindings).ConfigureAwait(false);
            _shortcutMap = map;
        }
        finally
        {
            _bindSemaphore.Release();
        }
    }

    private async Task CloseSessionAsync()
    {
        if (_sessionProxy == null)
        {
            _sessionHandle = null;
            return;
        }

        try
        {
            await _sessionProxy.CloseAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "WaylandPortalHotkeyService: Failed to close session");
        }
        finally
        {
            _sessionProxy = null;
            _sessionHandle = null;
            _shortcutMap.Clear();
        }
    }

    private async Task<ObjectPath> CreateSessionAsync()
    {
        var options = new Dictionary<string, object>
        {
            ["session_handle_token"] = $"sharex_hk_{Guid.NewGuid():N}"
        };

        var requestPath = await _portal!.CreateSessionAsync(options).ConfigureAwait(false);
        var request = _connection!.CreateProxy<IPortalRequest>(PortalBusName, requestPath);
        var (response, results) = await request.WaitForResponseAsync().ConfigureAwait(false);

        if (response != 0)
        {
            throw new InvalidOperationException($"WaylandPortalHotkeyService: CreateSession failed ({response})");
        }

        if (!results.TryGetValue("session_handle", out var handleObj) || handleObj is not string handlePath)
        {
            throw new InvalidOperationException("WaylandPortalHotkeyService: Session handle missing in portal response");
        }

        return new ObjectPath(handlePath);
    }

    private async Task BindShortcutsAsync(ValueTuple<string, IDictionary<string, object>>[] bindings)
    {
        if (_sessionHandle == null)
        {
            throw new InvalidOperationException("WaylandPortalHotkeyService: Session handle is not initialized");
        }

        ObjectPath sessionHandle = (ObjectPath)_sessionHandle!;
        var requestPath = await _portal!.BindShortcutsAsync(sessionHandle, bindings, string.Empty, new Dictionary<string, object>()).ConfigureAwait(false);
        var request = _connection!.CreateProxy<IPortalRequest>(PortalBusName, requestPath);
        var (response, _) = await request.WaitForResponseAsync().ConfigureAwait(false);

        if (response != 0)
        {
            throw new InvalidOperationException($"WaylandPortalHotkeyService: BindShortcuts failed ({response})");
        }
    }

    private (ValueTuple<string, IDictionary<string, object>>[] bindings, Dictionary<string, HotkeyInfo> map) BuildShortcutBindings()
    {
        var shortcuts = new List<ValueTuple<string, IDictionary<string, object>>>();
        var map = new Dictionary<string, HotkeyInfo>();

        lock (_hotkeyLock)
        {
            foreach (var hotkey in _registered.Values)
            {
                var description = hotkey.ToString();
                var trigger = BuildPreferredTrigger(hotkey);
                var entry = new Dictionary<string, object>
                {
                    ["description"] = description,
                    ["preferred_trigger"] = trigger
                };

                var shortcutId = hotkey.Id.ToString();
                shortcuts.Add(ValueTuple.Create(shortcutId, (IDictionary<string, object>)entry));
                map[shortcutId] = hotkey;
            }
        }

        return (shortcuts.ToArray(), map);
    }

    private void OnActivated((ObjectPath sessionHandle, string shortcutId, ulong timestamp, IDictionary<string, object> options) data)
    {
        if (_sessionHandle == null || !_sessionHandle.Equals(data.sessionHandle) || IsSuspended)
        {
            return;
        }

        HotkeyInfo? info;
        lock (_hotkeyLock)
        {
            _shortcutMap.TryGetValue(data.shortcutId, out info);
        }

        if (info == null)
        {
            return;
        }

        var args = new HotkeyTriggeredEventArgs(info);
        Dispatcher.UIThread.Post(() => HotkeyTriggered?.Invoke(this, args));
    }

    private void OnDeactivated((ObjectPath sessionHandle, string shortcutId, ulong timestamp, IDictionary<string, object> options) data)
    {
        // Portal currently only triggers once per activation; no action needed.
    }

    private static string BuildPreferredTrigger(HotkeyInfo hotkeyInfo)
    {
        var parts = new List<string>(4);
        if (hotkeyInfo.HasControl)
        {
            parts.Add("Ctrl");
        }

        if (hotkeyInfo.HasAlt)
        {
            parts.Add("Alt");
        }

        if (hotkeyInfo.HasShift)
        {
            parts.Add("Shift");
        }

        if (hotkeyInfo.HasMeta)
        {
            parts.Add("Meta");
        }

        var keyName = MapKeyName(hotkeyInfo.Key);
        if (!string.IsNullOrEmpty(keyName))
        {
            parts.Add(keyName);
        }

        return string.Join("+", parts);
    }

    private static string MapKeyName(Key key)
    {
        if (ShortcutKeyNames.TryGetValue(key, out var name))
        {
            return name;
        }

        if (key >= Key.A && key <= Key.Z)
        {
            return key.ToString();
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            return ((int)(key - Key.D0)).ToString();
        }

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return "Numpad " + (int)(key - Key.NumPad0);
        }

        if (key >= Key.F1 && key <= Key.F24)
        {
            return key.ToString();
        }

        return key.ToString();
    }

    private static readonly Dictionary<Key, string> ShortcutKeyNames = new()
    {
        { Key.PrintScreen, "Print" },
        { Key.Scroll, "Scroll_Lock" },
        { Key.Pause, "Pause" },
        { Key.CapsLock, "Caps_Lock" },
        { Key.Space, "Space" },
        { Key.Tab, "Tab" },
        { Key.Enter, "Return" },
        { Key.Back, "BackSpace" },
        { Key.Escape, "Escape" },
        { Key.Delete, "Delete" },
        { Key.Insert, "Insert" },
        { Key.Home, "Home" },
        { Key.End, "End" },
        { Key.PageUp, "Page_Up" },
        { Key.PageDown, "Page_Down" },
        { Key.Left, "Left" },
        { Key.Right, "Right" },
        { Key.Up, "Up" },
        { Key.Down, "Down" },
        { Key.NumLock, "Num_Lock" },
        { Key.OemPlus, "plus" },
        { Key.OemMinus, "minus" },
        { Key.OemComma, "comma" },
        { Key.OemPeriod, "period" },
        { Key.Oem1, "semicolon" },
        { Key.Oem2, "slash" },
        { Key.Oem3, "grave" },
        { Key.Oem4, "bracketleft" },
        { Key.Oem5, "backslash" },
        { Key.Oem6, "bracketright" },
        { Key.Oem7, "apostrophe" },
        { Key.Apps, "Menu" },
        { Key.Divide, "KP_Divide" },
        { Key.Multiply, "KP_Multiply" },
        { Key.Add, "KP_Add" },
        { Key.Subtract, "KP_Subtract" },
        { Key.Decimal, "KP_Decimal" }
    };

    [DBusInterface("org.freedesktop.portal.GlobalShortcuts")]
    public interface IGlobalShortcuts : IDBusObject
    {
        Task<ObjectPath> CreateSessionAsync(IDictionary<string, object> options);

        Task<ObjectPath> BindShortcutsAsync(ObjectPath sessionHandle, ValueTuple<string, IDictionary<string, object>>[] shortcuts, string parentWindow, IDictionary<string, object> options);

        Task<ObjectPath> ListShortcutsAsync(ObjectPath sessionHandle, IDictionary<string, object> options);

        Task ConfigureShortcutsAsync(ObjectPath sessionHandle, string parentWindow, IDictionary<string, object> options);

        Task<IDisposable> WatchActivatedAsync(Action<(ObjectPath sessionHandle, string shortcutId, ulong timestamp, IDictionary<string, object> options)> handler, Action<Exception>? error = null);

        Task<IDisposable> WatchDeactivatedAsync(Action<(ObjectPath sessionHandle, string shortcutId, ulong timestamp, IDictionary<string, object> options)> handler, Action<Exception>? error = null);
    }

    // Session interface is defined in PortalSession.cs to avoid duplicate proxy names.
}
