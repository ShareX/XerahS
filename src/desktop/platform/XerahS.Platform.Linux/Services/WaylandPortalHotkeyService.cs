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
using System.Linq;
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
    private IHotkeyService? _fallbackHotkeyService;
    private bool _portalUnavailableForSession;
    private bool _fallbackActivationLogged;
    private bool _isSuspended;
    private bool _disposed;

    public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;
    public bool IsSuspended
    {
        get => _isSuspended;
        set
        {
            _isSuspended = value;
            if (_fallbackHotkeyService != null)
            {
                _fallbackHotkeyService.IsSuspended = value;
            }
        }
    }

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
        if (!hotkeyInfo.IsValid)
        {
            hotkeyInfo.Status = PlatformHotkeyStatus.NotConfigured;
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

        if (ShouldUseFallbackHotkeys())
        {
            bool fallbackReady = ActivateFallbackHotkeys("portal unavailable during hotkey registration");
            bool isRegistered = fallbackReady && _fallbackHotkeyService != null && _fallbackHotkeyService.IsRegistered(hotkeyInfo);
            hotkeyInfo.Status = isRegistered ? PlatformHotkeyStatus.Registered : PlatformHotkeyStatus.UnsupportedPlatform;
            return isRegistered;
        }

        try
        {
            RebindShortcutsAsync().GetAwaiter().GetResult();
            hotkeyInfo.Status = PlatformHotkeyStatus.Registered;
            return true;
        }
        catch (PortalBindFailedException ex) when (ex.ResponseCode == 2)
        {
            DebugHelper.WriteException(ex, "WaylandPortalHotkeyService: Portal bind failed with non-recoverable response (2); enabling X11 fallback");
            bool fallbackReady = ActivateFallbackHotkeys("portal BindShortcuts failed with response=2");
            bool isRegistered = fallbackReady && _fallbackHotkeyService != null && _fallbackHotkeyService.IsRegistered(hotkeyInfo);
            hotkeyInfo.Status = isRegistered ? PlatformHotkeyStatus.Registered : PlatformHotkeyStatus.Failed;
            return isRegistered;
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
        if (hotkeyInfo.Id == 0)
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

        if (ShouldUseFallbackHotkeys())
        {
            if (_fallbackHotkeyService != null)
            {
                _fallbackHotkeyService.UnregisterHotkey(hotkeyInfo);
            }

            hotkeyInfo.Status = PlatformHotkeyStatus.NotConfigured;
            return true;
        }

        try
        {
            RebindShortcutsAsync().GetAwaiter().GetResult();
            hotkeyInfo.Status = PlatformHotkeyStatus.NotConfigured;
            return true;
        }
        catch (PortalBindFailedException ex) when (ex.ResponseCode == 2)
        {
            DebugHelper.WriteException(ex, "WaylandPortalHotkeyService: Portal bind failed during unregister; enabling X11 fallback");
            bool fallbackReady = ActivateFallbackHotkeys("portal BindShortcuts failed during unregister with response=2");
            if (fallbackReady && _fallbackHotkeyService != null)
            {
                _fallbackHotkeyService.UnregisterHotkey(hotkeyInfo);
            }

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
        lock (_hotkeyLock)
        {
            _registered.Clear();
        }

        if (ShouldUseFallbackHotkeys())
        {
            _fallbackHotkeyService?.UnregisterAll();
            return;
        }

        try
        {
            RebindShortcutsAsync().GetAwaiter().GetResult();
        }
        catch (PortalBindFailedException ex) when (ex.ResponseCode == 2)
        {
            DebugHelper.WriteException(ex, "WaylandPortalHotkeyService: Portal bind failed during unregister-all; enabling X11 fallback");
            if (ActivateFallbackHotkeys("portal BindShortcuts failed during unregister-all with response=2"))
            {
                _fallbackHotkeyService?.UnregisterAll();
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "WaylandPortalHotkeyService: Failed to unregister all hotkeys");
        }
    }

    public bool IsRegistered(HotkeyInfo hotkeyInfo)
    {
        if (ShouldUseFallbackHotkeys())
        {
            return _fallbackHotkeyService?.IsRegistered(hotkeyInfo) == true;
        }

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
        if (_fallbackHotkeyService != null)
        {
            _fallbackHotkeyService.HotkeyTriggered -= OnFallbackHotkeyTriggered;
            _fallbackHotkeyService.Dispose();
            _fallbackHotkeyService = null;
        }
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
            DebugHelper.WriteLine($"WaylandPortalHotkeyService: Binding {bindings.Length} shortcut(s) to portal session {sessionHandle}");
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
        DebugHelper.WriteLine($"WaylandPortalHotkeyService: CreateSession response={response} ({DescribePortalResponse(response)})");

        if (response != 0)
        {
            throw new InvalidOperationException($"WaylandPortalHotkeyService: CreateSession failed ({response}, {DescribePortalResponse(response)})");
        }

        if (!results.TryGetResult("session_handle", out string? handlePath) || string.IsNullOrWhiteSpace(handlePath))
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
        string payload = string.Join(", ",
            bindings.Select(binding =>
            {
                string trigger = binding.Item2.TryGetValue("preferred_trigger", out var value) ? value?.ToString() ?? "<null>" : "<missing>";
                return $"{binding.Item1}:{trigger}";
            }));
        DebugHelper.WriteLine($"WaylandPortalHotkeyService: BindShortcuts payload: [{payload}]");
        var requestPath = await _portal!.BindShortcutsAsync(sessionHandle, bindings, string.Empty, new Dictionary<string, object>()).ConfigureAwait(false);
        var request = _connection!.CreateProxy<IPortalRequest>(PortalBusName, requestPath);
        var (response, _) = await request.WaitForResponseAsync().ConfigureAwait(false);
        DebugHelper.WriteLine($"WaylandPortalHotkeyService: BindShortcuts response={response} ({DescribePortalResponse(response)})");

        if (response != 0)
        {
            throw new PortalBindFailedException(response, $"WaylandPortalHotkeyService: BindShortcuts failed ({response}, {DescribePortalResponse(response)})");
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
                DebugHelper.WriteLine($"WaylandPortalHotkeyService: Prepared binding id={shortcutId}, trigger={trigger}, description={description}");
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

    private void OnFallbackHotkeyTriggered(object? sender, HotkeyTriggeredEventArgs e)
    {
        if (IsSuspended)
        {
            return;
        }

        HotkeyTriggered?.Invoke(this, e);
    }

    private bool ShouldUseFallbackHotkeys()
    {
        return _portalUnavailableForSession || _portal == null;
    }

    private bool ActivateFallbackHotkeys(string reason)
    {
        _portalUnavailableForSession = true;

        if (!EnsureFallbackHotkeyService(reason))
        {
            return false;
        }

        if (_fallbackHotkeyService == null)
        {
            return false;
        }

        _fallbackHotkeyService.UnregisterAll();

        List<HotkeyInfo> snapshot;
        lock (_hotkeyLock)
        {
            snapshot = _registered.Values.ToList();
        }

        foreach (var hotkey in snapshot)
        {
            bool ok = _fallbackHotkeyService.RegisterHotkey(hotkey);
            hotkey.Status = ok ? PlatformHotkeyStatus.Registered : PlatformHotkeyStatus.Failed;
            if (!ok)
            {
                DebugHelper.WriteLine($"WaylandPortalHotkeyService: X11 fallback failed to register {hotkey}");
            }
        }

        return true;
    }

    private bool EnsureFallbackHotkeyService(string reason)
    {
        if (_fallbackHotkeyService != null)
        {
            return true;
        }

        try
        {
            _fallbackHotkeyService = new LinuxHotkeyService();
            _fallbackHotkeyService.IsSuspended = IsSuspended;
            _fallbackHotkeyService.HotkeyTriggered += OnFallbackHotkeyTriggered;

            if (!_fallbackActivationLogged)
            {
                DebugHelper.WriteLine($"WaylandPortalHotkeyService: Activating X11 fallback hotkeys. Reason: {reason}");
                _fallbackActivationLogged = true;
            }

            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "WaylandPortalHotkeyService: Failed to activate X11 fallback hotkeys");
            return false;
        }
    }

    private static string DescribePortalResponse(uint response)
    {
        return response switch
        {
            0 => "Success",
            1 => "Cancelled",
            2 => "Failed",
            _ => "Unknown"
        };
    }

    private sealed class PortalBindFailedException : InvalidOperationException
    {
        public uint ResponseCode { get; }

        public PortalBindFailedException(uint responseCode, string message) : base(message)
        {
            ResponseCode = responseCode;
        }
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

    // Session interface is defined in PortalSession.cs to avoid duplicate proxy names.
}

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
