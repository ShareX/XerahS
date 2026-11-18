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
using System.Collections.Concurrent;
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
    private readonly ManualResetEventSlim _rebindIdle = new(initialState: true);
    private readonly Func<Task>? _testRebindAction;
    private readonly Lock _hotkeyLock = new();
    private readonly ConcurrentDictionary<string, long> _hotkeyDebounceTimes = new();
    private readonly Dictionary<ushort, HotkeyInfo> _registered = new();
    private Dictionary<string, HotkeyInfo> _shortcutMap = new();
    private ushort _nextId = 1;
    private ObjectPath? _sessionHandle;
    private IPortalSession? _sessionProxy;
    private IDisposable? _activatedSubscription;
    private IDisposable? _deactivatedSubscription;
    private IDisposable? _shortcutsChangedSubscription;
    private IHotkeyService? _fallbackHotkeyService;
    private bool _portalUnavailableForSession;
    private bool _fallbackActivationLogged;
    private bool _isSuspended;
    private bool _disposed;
    private CancellationTokenSource? _rebindDebounceCts;
    private string[] _lastBoundIds = Array.Empty<string>();
    private int _activeRebindOperations;

    public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;
    public event EventHandler? HotkeysChanged;
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

    public WaylandPortalHotkeyService() : this(testRebindAction: null, skipPortalInitialization: false)
    {
    }

    internal WaylandPortalHotkeyService(Func<Task>? testRebindAction, bool skipPortalInitialization)
    {
        _testRebindAction = testRebindAction;
        if (skipPortalInitialization)
        {
            return;
        }

        try
        {
            var previousContext = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                _connection = new Connection(Address.Session);
                var connectionInfo = _connection.ConnectAsync().GetAwaiter().GetResult();
                global::XerahS.Platform.Linux.Capture.PortalRequestExtensions.CacheLocalConnectionName(_connection, connectionInfo);
                _portal = _connection.CreateProxy<IGlobalShortcuts>(PortalBusName, PortalObjectPath);
                _activatedSubscription = _portal.WatchActivatedAsync(OnActivated, OnPortalWatchError).GetAwaiter().GetResult();
                _deactivatedSubscription = _portal.WatchDeactivatedAsync(OnDeactivated, OnPortalWatchError).GetAwaiter().GetResult();
                _shortcutsChangedSubscription = _portal.WatchShortcutsChangedAsync(OnShortcutsChanged, OnPortalWatchError).GetAwaiter().GetResult();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "WaylandPortalHotkeyService: Unable to initialize portal");
            _portal = null;
            _connection?.Dispose();
        }
    }

    public void NotifyWindowReady()
    {
        if (_disposed || _portal == null)
            return;

        // The portal BindShortcuts call at startup may have received parentWindow="" because
        // NativeWindowHandleProvider was not yet set (window hadn't opened yet).
        // Now that the window is open and the handle is available, reset any response=2
        // failure and retry the portal path.
        if (_portalUnavailableForSession)
        {
            DebugHelper.WriteLine("WaylandPortalHotkeyService: NotifyWindowReady — window handle now available; resetting portal-unavailable flag and retrying bind.");
            _portalUnavailableForSession = false;
            _fallbackActivationLogged = false;
            ScheduleRebind();
        }
        else if (_sessionHandle == null && _registered.Count > 0)
        {
            // No session established yet (startup race where first bind hasn't completed) — kick off bind now.
            DebugHelper.WriteLine("WaylandPortalHotkeyService: NotifyWindowReady — no portal session yet; scheduling bind now that window handle is available.");
            ScheduleRebind();
        }
    }

    public async Task<bool> ShowInteractiveConfigurationAsync()
    {
        if (_portal == null || _sessionHandle == null || ShouldUseFallbackHotkeys())
            return false;

        // ConfigureShortcuts requires portal interface version >= 2 (XIP0079 P1 / XIP0044).
        uint? version = PortalInterfaceChecker.TryGetInterfaceVersion("org.freedesktop.portal.GlobalShortcuts");
        if (version is < 2)
        {
            DebugHelper.WriteLine($"WaylandPortalHotkeyService: ConfigureShortcuts requires GlobalShortcuts portal v2+ (found v{version?.ToString() ?? "unknown"}); use the in-app hotkey recorder.");
            return false;
        }

        try
        {
            var parentWindow = PlatformServices.NativeWindowHandleProvider?.Invoke() ?? string.Empty;
            var options = new Dictionary<string, object>();
            var (response, _) = await _connection!
                .SendPortalRequestAsync(
                    PortalBusName,
                    options,
                    () => _portal.ConfigureShortcutsAsync((ObjectPath)_sessionHandle, parentWindow, options))
                .ConfigureAwait(false);
            if (response == 0)
            {
                await RefreshShortcutsFromPortalAsync().ConfigureAwait(false);
            }

            return response == 0; // Success
        }
        catch (Exception ex) when (ex is DBusException dbusEx && dbusEx.ErrorName == "org.freedesktop.DBus.Error.UnknownMethod")
        {
            DebugHelper.WriteLine("WaylandPortalHotkeyService: ConfigureShortcuts not available on KDE Plasma — use XerahS workflow editor to set hotkeys");
            return false;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "WaylandPortalHotkeyService: ConfigureShortcuts failed, fallback to native app UI.");
            return false;
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

        hotkeyInfo.Status = PlatformHotkeyStatus.Registered;
        ScheduleRebind();
        return true;
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

        hotkeyInfo.Status = PlatformHotkeyStatus.NotConfigured;
        ScheduleRebind();
        return true;
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

        ScheduleRebind();
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

    /// <inheritdoc />
    public HotkeyDiagnostics GetDiagnostics()
    {
        if (_portal == null)
        {
            return new HotkeyDiagnostics(
                HotkeyBackendState.Unavailable,
                "GlobalShortcuts portal",
                "The XDG GlobalShortcuts portal is not available in this session. Global hotkeys cannot be registered.");
        }

        if (ShouldUseFallbackHotkeys() || _fallbackHotkeyService != null)
        {
            return new HotkeyDiagnostics(
                HotkeyBackendState.X11FallbackFocusOnly,
                "XGrabKey (X11 fallback)",
                "Global shortcuts portal is unavailable — hotkeys only fire while XerahS is focused. " +
                "Use the in-app hotkey recorder below, or see developers/linux/INSTALL.md (Hotkey troubleshooting).");
        }

        if (_sessionHandle != null && _registered.Count > 0)
        {
            return new HotkeyDiagnostics(
                HotkeyBackendState.PortalBound,
                "GlobalShortcuts portal",
                null);
        }

        if (_registered.Count > 0)
        {
            return new HotkeyDiagnostics(
                HotkeyBackendState.PortalPending,
                "GlobalShortcuts portal",
                "Portal hotkey session is being established. Shortcuts may not fire until binding completes.");
        }

        return new HotkeyDiagnostics(HotkeyBackendState.PortalBound, "GlobalShortcuts portal", null);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var debounceCts = Interlocked.Exchange(ref _rebindDebounceCts, null);
        debounceCts?.Cancel();
        WaitForRebindOperationsToDrain();
        debounceCts?.Dispose();

        _activatedSubscription?.Dispose();
        _deactivatedSubscription?.Dispose();
        _shortcutsChangedSubscription?.Dispose();
        CloseSessionAsync().GetAwaiter().GetResult();
        _connection?.Dispose();
        if (_fallbackHotkeyService != null)
        {
            _fallbackHotkeyService.HotkeyTriggered -= OnFallbackHotkeyTriggered;
            _fallbackHotkeyService.Dispose();
            _fallbackHotkeyService = null;
        }
        _bindSemaphore.Dispose();
        _rebindIdle.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ScheduleRebind()
    {
        if (_disposed)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        var old = Interlocked.Exchange(ref _rebindDebounceCts, cts);
        // Cancel only — do not dispose here. The old task's lambda may not have started yet
        // and still holds a reference to old's CTS; disposing it synchronously would cause
        // ObjectDisposedException when the lambda accesses cts.Token inside Task.Delay.
        // The old CTS is either disposed by its own lambda's finally block (if it ran first
        // and its CompareExchange still matched) or collected by GC otherwise.
        old?.Cancel();
        MarkRebindOperationStarted();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(100, cts.Token).ConfigureAwait(false);
                if (_disposed) return;
                await RebindShortcutsAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) when (_disposed) { }
            catch (PortalBindFailedException ex) when (ex.ResponseCode == 1)
            {
                DebugHelper.WriteException(ex, "WaylandPortalHotkeyService: Portal bind cancelled by user (response=1); enabling X11 fallback");
                ActivateFallbackHotkeys("portal BindShortcuts cancelled by user (response=1)");
            }
            catch (PortalBindFailedException ex) when (ex.ResponseCode == 2)
            {
                DebugHelper.WriteException(ex, "WaylandPortalHotkeyService: Portal bind failed with non-recoverable response (2); enabling X11 fallback");
                ActivateFallbackHotkeys("portal BindShortcuts failed with response=2");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "WaylandPortalHotkeyService: Failed to rebind shortcuts");
            }
            finally
            {
                var current = Interlocked.CompareExchange(ref _rebindDebounceCts, null, cts);
                if (ReferenceEquals(current, cts))
                {
                    cts.Dispose();
                }

                MarkRebindOperationCompleted();
            }
        });
    }

    private async Task RebindShortcutsAsync()
    {
        if (_testRebindAction != null)
        {
            await _testRebindAction().ConfigureAwait(false);
            return;
        }

        if (_disposed)
        {
            return;
        }

        if (_portal == null)
        {
            throw new InvalidOperationException("Global shortcuts portal is not available.");
        }

        await _bindSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            var (bindings, map) = BuildShortcutBindings();
            
            // Fix 4: Session Persistence. Do not recreate session if the set of shortcut IDs hasn't changed.
            // (Recreating forces a new permission dialog and loses user UI config state).
            var currentIds = map.Keys.OrderBy(x => x).ToArray();
            if (_sessionHandle != null && _lastBoundIds.SequenceEqual(currentIds))
            {
                DebugHelper.WriteLine("WaylandPortalHotkeyService: Shortcut set unchanged. Preserving session.");
                _shortcutMap = map;
                return;
            }

            if (bindings.Length == 0)
            {
                await CloseSessionAsync().ConfigureAwait(false);
                _shortcutMap.Clear();
                _lastBoundIds = Array.Empty<string>();
                return;
            }

            await CloseSessionAsync().ConfigureAwait(false);
            _sessionHandle = await CreateSessionAsync().ConfigureAwait(false);
            ObjectPath sessionHandle = (ObjectPath)_sessionHandle!;
            _sessionProxy = _connection!.CreateProxy<IPortalSession>(PortalBusName, sessionHandle);
            DebugHelper.WriteLine($"WaylandPortalHotkeyService: Binding {bindings.Length} shortcut(s) to portal session {sessionHandle}");
            await BindShortcutsAsync(bindings).ConfigureAwait(false);
            _shortcutMap = map;
            _lastBoundIds = currentIds;

            // Portal bind succeeded. If we previously activated the X11 fallback (e.g. because
            // the initial bind ran before the window handle was available and got response=2),
            // release it now so the portal is the sole delivery path.
            if (_fallbackHotkeyService != null)
            {
                DebugHelper.WriteLine("WaylandPortalHotkeyService: Portal bind succeeded; releasing X11 fallback hotkeys.");
                var fallback = _fallbackHotkeyService;
                _fallbackHotkeyService = null;
                fallback.UnregisterAll();
                fallback.HotkeyTriggered -= OnFallbackHotkeyTriggered;
                fallback.Dispose();
            }
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

        var (response, results) = await _connection!
            .SendPortalRequestAsync(
                PortalBusName,
                options,
                () => _portal!.CreateSessionAsync(options))
            .ConfigureAwait(false);
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
        var parentWindow = PlatformServices.NativeWindowHandleProvider?.Invoke() ?? string.Empty;
        var appName = global::Avalonia.Application.Current?.Name ?? "<null>";
        DebugHelper.WriteLine($"WaylandPortalHotkeyService: BindShortcuts payload: [{payload}], parentWindow={(string.IsNullOrEmpty(parentWindow) ? "<empty>" : parentWindow)}, app_id={appName}");
        var options = new Dictionary<string, object>();
        var (response, results) = await _connection!
            .SendPortalRequestAsync(
                PortalBusName,
                options,
                () => _portal!.BindShortcutsAsync(sessionHandle, bindings, parentWindow, options))
            .ConfigureAwait(false);
        DebugHelper.WriteLine($"WaylandPortalHotkeyService: BindShortcuts response={response} ({DescribePortalResponse(response)})");

        if (response != 0)
        {
            throw new PortalBindFailedException(response, $"WaylandPortalHotkeyService: BindShortcuts failed ({response}, {DescribePortalResponse(response)})");
        }

        if (TryGetShortcutResults(results, out var shortcuts))
        {
            ApplyPortalShortcutSnapshot(shortcuts);
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
        if (_disposed || _sessionHandle == null || !_sessionHandle.Equals(data.sessionHandle) || IsSuspended)
        {
            return;
        }

        const long debounceWindowTicks = 1500 * 10_000; // 1500ms in 100ns ticks
        var nowTicks = DateTime.UtcNow.Ticks;
        var shouldProceed = _hotkeyDebounceTimes.AddOrUpdate(
            data.shortcutId,
            nowTicks, // Key didn't exist — add and proceed
            (key, lastTicks) =>
            {
                if (nowTicks - lastTicks < debounceWindowTicks)
                    return lastTicks; // Still in window — keep old value, caller skips
                return nowTicks; // Expired — update and proceed
            });
        if (shouldProceed != nowTicks)
            return; // Debounce active, skip

        HotkeyInfo? info;
        lock (_hotkeyLock)
        {
            _shortcutMap.TryGetValue(data.shortcutId, out info);
        }

        if (info == null)
        {
            return;
        }

        try
        {
            var args = new HotkeyTriggeredEventArgs(info);
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    HotkeyTriggered?.Invoke(this, args);
                }
                catch (ObjectDisposedException)
                {
                    DebugHelper.WriteLine("WaylandPortalHotkeyService: handler disposed during invoke, skipping.");
                }
            });
        }
        catch (ObjectDisposedException)
        {
            // Service was disposed while the portal callback was in flight. Silently skip.
            DebugHelper.WriteLine("WaylandPortalHotkeyService: OnActivated — dispatcher disposed, skipping hotkey event.");
        }
    }

    private void OnDeactivated((ObjectPath sessionHandle, string shortcutId, ulong timestamp, IDictionary<string, object> options) data)
    {
        // Portal currently only triggers once per activation; no action needed.
    }

    private void OnShortcutsChanged((ObjectPath sessionHandle, ValueTuple<string, IDictionary<string, object>>[] shortcuts) data)
    {
        if (_sessionHandle == null || !_sessionHandle.Equals(data.sessionHandle) || IsSuspended)
            return;

        DebugHelper.WriteLine("WaylandPortalHotkeyService: ShortcutsChanged signal received. DE updated bindings.");
        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshShortcutsFromPortalAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "WaylandPortalHotkeyService: Failed to refresh shortcuts after ShortcutsChanged");
            }
        });
    }

    private static void OnPortalWatchError(Exception ex)
    {
        DebugHelper.WriteException(ex, "WaylandPortalHotkeyService: Portal watch error (e.g. service gone); hotkeys may use X11 fallback.");
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

    internal static string BuildPreferredTrigger(HotkeyInfo hotkeyInfo)
    {
        // XDG GlobalShortcuts portal uses GLib/GTK accelerator format:
        // modifiers are <Primary>, <Alt>, <Shift>, <Super> with no separator,
        // followed by the lowercase key name. E.g. "<Primary><Shift>f", not "Ctrl+Shift+F".
        var parts = new List<string>(5);
        if (hotkeyInfo.HasControl)
        {
            parts.Add("<Primary>");
        }

        if (hotkeyInfo.HasAlt)
        {
            parts.Add("<Alt>");
        }

        if (hotkeyInfo.HasShift)
        {
            parts.Add("<Shift>");
        }

        if (hotkeyInfo.HasMeta)
        {
            parts.Add("<Super>");
        }

        var keyName = MapKeyName(hotkeyInfo.Key);
        if (!string.IsNullOrEmpty(keyName))
        {
            parts.Add(keyName);
        }

        return string.Concat(parts);
    }

    private void WaitForRebindOperationsToDrain()
    {
        if (Volatile.Read(ref _activeRebindOperations) == 0)
        {
            return;
        }

        if (!_rebindIdle.Wait(TimeSpan.FromSeconds(5)))
        {
            DebugHelper.WriteLine("WaylandPortalHotkeyService: Timed out waiting for rebind tasks to drain during dispose.");
        }
    }

    private void MarkRebindOperationStarted()
    {
        _rebindIdle.Reset();
        Interlocked.Increment(ref _activeRebindOperations);
    }

    private void MarkRebindOperationCompleted()
    {
        if (Interlocked.Decrement(ref _activeRebindOperations) == 0)
        {
            _rebindIdle.Set();
        }
    }

    internal void ScheduleRebindForTesting()
    {
        ScheduleRebind();
    }

    private async Task RefreshShortcutsFromPortalAsync()
    {
        if (_disposed || _portal == null || _connection == null || _sessionHandle == null)
        {
            return;
        }

        await _bindSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || _portal == null || _connection == null || _sessionHandle == null)
            {
                return;
            }

            var options = new Dictionary<string, object>();
            var (response, results) = await _connection
                .SendPortalRequestAsync(
                    PortalBusName,
                    options,
                    () => _portal.ListShortcutsAsync((ObjectPath)_sessionHandle, options))
                .ConfigureAwait(false);
            DebugHelper.WriteLine($"WaylandPortalHotkeyService: ListShortcuts response={response} ({DescribePortalResponse(response)})");

            if (response != 0)
            {
                return;
            }

            if (TryGetShortcutResults(results, out var shortcuts))
            {
                ApplyPortalShortcutSnapshot(shortcuts);
            }
        }
        finally
        {
            _bindSemaphore.Release();
        }
    }

    private bool ApplyPortalShortcutSnapshot(ValueTuple<string, IDictionary<string, object>>[] shortcuts)
    {
        bool changed = false;
        var shortcutsById = BuildShortcutSnapshotMap(shortcuts);

        lock (_hotkeyLock)
        {
            foreach (var entry in _registered)
            {
                string shortcutId = entry.Key.ToString();
                var hotkeyInfo = entry.Value;

                if (shortcutsById.TryGetValue(shortcutId, out var metadata))
                {
                    string? triggerDescription = GetTriggerDescription(metadata);
                    if (!string.Equals(hotkeyInfo.NativeTriggerDescription, triggerDescription, StringComparison.Ordinal))
                    {
                        hotkeyInfo.NativeTriggerDescription = triggerDescription;
                        changed = true;
                    }

                    if (hotkeyInfo.Status != PlatformHotkeyStatus.Registered)
                    {
                        hotkeyInfo.Status = PlatformHotkeyStatus.Registered;
                        changed = true;
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(hotkeyInfo.NativeTriggerDescription))
                    {
                        hotkeyInfo.NativeTriggerDescription = null;
                        changed = true;
                    }

                    if (hotkeyInfo.Status != PlatformHotkeyStatus.Failed)
                    {
                        hotkeyInfo.Status = PlatformHotkeyStatus.Failed;
                        changed = true;
                    }
                }
            }
        }

        if (changed)
        {
            HotkeysChanged?.Invoke(this, EventArgs.Empty);
        }

        return changed;
    }

    internal static Dictionary<string, IDictionary<string, object>> BuildShortcutSnapshotMap(
        ValueTuple<string, IDictionary<string, object>>[] shortcuts)
    {
        var shortcutsById = new Dictionary<string, IDictionary<string, object>>(StringComparer.Ordinal);

        foreach (var shortcut in shortcuts)
        {
            if (string.IsNullOrWhiteSpace(shortcut.Item1))
            {
                continue;
            }

            shortcutsById[shortcut.Item1] = shortcut.Item2;
        }

        return shortcutsById;
    }

    private static bool TryGetShortcutResults(
        IDictionary<string, object> results,
        out ValueTuple<string, IDictionary<string, object>>[] shortcuts)
    {
        if (results.TryGetResult("shortcuts", out ValueTuple<string, IDictionary<string, object>>[]? value) &&
            value != null)
        {
            shortcuts = value;
            return true;
        }

        shortcuts = Array.Empty<ValueTuple<string, IDictionary<string, object>>>();
        return false;
    }

    private static string? GetTriggerDescription(IDictionary<string, object> metadata)
    {
        if (!metadata.TryGetValue("trigger_description", out var raw) || raw == null)
        {
            return null;
        }

        var value = raw.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    internal static string MapKeyName(Key key)
    {
        if (ShortcutKeyNames.TryGetValue(key, out var name))
        {
            return name;
        }

        if (key >= Key.A && key <= Key.Z)
        {
            return key.ToString().ToLowerInvariant();
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            return ((int)(key - Key.D0)).ToString();
        }

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return "KP_" + (int)(key - Key.NumPad0);
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
        { Key.Space, "space" },
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
        { Key.Oem102, "backslash" },
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

    Task<ObjectPath> ConfigureShortcutsAsync(ObjectPath sessionHandle, string parentWindow, IDictionary<string, object> options);

    Task<IDisposable> WatchActivatedAsync(Action<(ObjectPath sessionHandle, string shortcutId, ulong timestamp, IDictionary<string, object> options)> handler, Action<Exception>? error = null);

    Task<IDisposable> WatchDeactivatedAsync(Action<(ObjectPath sessionHandle, string shortcutId, ulong timestamp, IDictionary<string, object> options)> handler, Action<Exception>? error = null);

    Task<IDisposable> WatchShortcutsChangedAsync(Action<(ObjectPath sessionHandle, ValueTuple<string, IDictionary<string, object>>[] shortcuts)> handler, Action<Exception>? error = null);
}
