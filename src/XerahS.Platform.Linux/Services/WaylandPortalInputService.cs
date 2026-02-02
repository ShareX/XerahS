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
using Tmds.DBus;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Capture;

namespace XerahS.Platform.Linux.Services;

public sealed class WaylandPortalInputService : IInputService
{
    private const string PortalBusName = "org.freedesktop.portal.Desktop";
    private static readonly ObjectPath PortalObjectPath = new("/org/freedesktop/portal/desktop");

    private readonly Connection? _connection;
    private readonly IInputCapture? _portal;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly object _cursorLock = new();
    private IDisposable? _activatedSubscription;
    private IDisposable? _deactivatedSubscription;
    private IDisposable? _zonesChangedSubscription;
    private IDisposable? _disabledSubscription;
    private System.Drawing.Point _lastCursor;
    private uint _zoneSet;
    private ObjectPath? _sessionHandle;
    private ISession? _sessionProxy;
    private bool _disposed;

    public WaylandPortalInputService()
    {
        if (!WaylandPortalStrategy.IsSupported())
        {
            return;
        }

        try
        {
            _connection = new Connection(Address.Session);
            _connection.ConnectAsync().GetAwaiter().GetResult();
            _portal = _connection.CreateProxy<IInputCapture>(PortalBusName, PortalObjectPath);
            InitializeAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "WaylandPortalInputService: Failed to initialize portal input capture");
            Dispose();
        }
    }

    public System.Drawing.Point GetCursorPosition()
    {
        lock (_cursorLock)
        {
            return _lastCursor;
        }
    }

    private async Task InitializeAsync()
    {
        if (_portal == null)
        {
            return;
        }

        _sessionHandle = await CreateSessionAsync().ConfigureAwait(false);
        if (_sessionHandle == null)
        {
            return;
        }

        ObjectPath sessionHandle = (ObjectPath)_sessionHandle!;
        _sessionProxy = _connection?.CreateProxy<ISession>(PortalBusName, sessionHandle);

        _activatedSubscription = await _portal.WatchActivatedAsync(OnActivated).ConfigureAwait(false);
        _deactivatedSubscription = await _portal.WatchDeactivatedAsync(OnDeactivated).ConfigureAwait(false);
        _zonesChangedSubscription = await _portal.WatchZonesChangedAsync(OnZonesChanged).ConfigureAwait(false);
        _disabledSubscription = await _portal.WatchDisabledAsync(OnDisabled).ConfigureAwait(false);

        await RefreshZonesAsync().ConfigureAwait(false);
        await EnableAsync().ConfigureAwait(false);
    }

    private static System.Drawing.Point? ExtractCursorPosition(IDictionary<string, object> options)
    {
        if (!options.TryGetValue("cursor_position", out var value))
        {
            return null;
        }

        if (value is ValueTuple<double, double> pair)
        {
            return new System.Drawing.Point((int)Math.Round(pair.Item1), (int)Math.Round(pair.Item2));
        }

        if (value is ValueTuple<float, float> floatPair)
        {
            return new System.Drawing.Point((int)Math.Round(floatPair.Item1), (int)Math.Round(floatPair.Item2));
        }

        if (value is IEnumerable<object> coords)
        {
            var numbers = coords.Take(2).OfType<double>().ToArray();
            if (numbers.Length == 2)
            {
                    return new System.Drawing.Point((int)Math.Round(numbers[0]), (int)Math.Round(numbers[1]));
            }

            var floats = coords.Take(2).OfType<float>().ToArray();
            if (floats.Length == 2)
            {
                    return new System.Drawing.Point((int)Math.Round(floats[0]), (int)Math.Round(floats[1]));
            }
        }

        return null;
    }

    private async Task<ObjectPath?> CreateSessionAsync()
    {
        if (_portal == null)
        {
            return null;
        }

        var options = new Dictionary<string, object>
        {
            ["session_handle_token"] = $"sharex_input_{Guid.NewGuid():N}"
        };

        var requestPath = await _portal.CreateSessionAsync(string.Empty, options).ConfigureAwait(false);
        var request = _connection!.CreateProxy<IPortalRequest>(PortalBusName, requestPath);
        var (response, results) = await request.WaitForResponseAsync().ConfigureAwait(false);

        if (response != 0)
        {
            DebugHelper.WriteLine($"WaylandPortalInputService: CreateSession failed ({response})");
            return null;
        }

        if (!results.TryGetValue("session_handle", out var handleObj) || handleObj is not string handlePath)
        {
            DebugHelper.WriteLine("WaylandPortalInputService: Session handle missing in portal response");
            return null;
        }

        return new ObjectPath(handlePath);
    }

    private async Task RefreshZonesAsync()
    {
        if (_portal == null || _sessionHandle == null)
        {
            return;
        }

        ObjectPath sessionHandle = (ObjectPath)_sessionHandle!;

        await _refreshLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var requestPath = await _portal.GetZonesAsync(sessionHandle, new Dictionary<string, object>()).ConfigureAwait(false);
            var request = _connection!.CreateProxy<IPortalRequest>(PortalBusName, requestPath);
            var (response, results) = await request.WaitForResponseAsync().ConfigureAwait(false);

            if (response != 0)
            {
                DebugHelper.WriteLine($"WaylandPortalInputService: GetZones failed ({response})");
                return;
            }

            if (!results.TryGetValue("zones", out var zonesObj))
            {
                return;
            }

            var parsedZones = ParseZones(zonesObj);
            if (parsedZones.Count == 0)
            {
                return;
            }

            if (results.TryGetValue("zone_set", out var zoneSetObj) && TryConvertToUInt(zoneSetObj, out var zoneSet))
            {
                _zoneSet = zoneSet;
            }

            await SetPointerBarriersAsync(parsedZones, _zoneSet).ConfigureAwait(false);
            await EnableAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "WaylandPortalInputService: Failed to refresh zones");
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static bool TryConvertToUInt(object? value, out uint result)
    {
        switch (value)
        {
            case uint u:
                result = u;
                return true;
            case int i when i >= 0:
                result = (uint)i;
                return true;
            case long l when l >= 0:
                result = (uint)l;
                return true;
        }

        result = 0;
        return false;
    }

    private static List<ZoneDescriptor> ParseZones(object zonesObj)
    {
        var list = new List<ZoneDescriptor>();

        if (zonesObj is IEnumerable<object> zonesEnumerable)
        {
            foreach (var raw in zonesEnumerable)
            {
                switch (raw)
                {
                    case ValueTuple<uint, uint, int, int> tuple:
                        list.Add(new ZoneDescriptor(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4));
                        break;
                    case object[] array when array.Length == 4:
                        if (TryConvertToUInt(array[0], out var width) &&
                            TryConvertToUInt(array[1], out var height) &&
                            TryConvertToInt(array[2], out var offsetX) &&
                            TryConvertToInt(array[3], out var offsetY))
                        {
                            list.Add(new ZoneDescriptor(width, height, offsetX, offsetY));
                        }

                        break;
                }
            }
        }

        return list;
    }

    private static bool TryConvertToInt(object? value, out int result)
    {
        switch (value)
        {
            case int i:
                result = i;
                return true;
            case uint u when u <= int.MaxValue:
                result = (int)u;
                return true;
            case long l when l >= int.MinValue && l <= int.MaxValue:
                result = (int)l;
                return true;
        }

        result = 0;
        return false;
    }

    private async Task SetPointerBarriersAsync(IReadOnlyList<ZoneDescriptor> zones, uint zoneSet)
    {
        if (_portal == null || _sessionHandle == null)
        {
            return;
        }

        ObjectPath sessionHandle = (ObjectPath)_sessionHandle!;
        var barriers = BuildBarriers(zones);
        var requestPath = await _portal.SetPointerBarriersAsync(
            sessionHandle,
            new Dictionary<string, object>(),
            barriers.ToArray(),
            zoneSet).ConfigureAwait(false);

        var request = _connection!.CreateProxy<IPortalRequest>(PortalBusName, requestPath);
        var (response, results) = await request.WaitForResponseAsync().ConfigureAwait(false);

        if (response != 0)
        {
            DebugHelper.WriteLine($"WaylandPortalInputService: SetPointerBarriers failed ({response})");
            return;
        }

        if (results.TryGetValue("failed_barriers", out var failed) && failed is IEnumerable<object> failedList)
        {
            var failedIds = failedList.OfType<uint>().Select(id => id.ToString()).ToArray();
            if (failedIds.Length > 0)
            {
                DebugHelper.WriteLine($"WaylandPortalInputService: Failed barriers {string.Join(",", failedIds)}");
            }
        }
    }

    private static IDictionary<string, object>[] BuildBarriers(IReadOnlyList<ZoneDescriptor> zones)
    {
        var barriers = new List<IDictionary<string, object>>();
        uint nextId = 1;

        foreach (var zone in zones)
        {
            if (zone.Width == 0 || zone.Height == 0)
            {
                continue;
            }

            var left = zone.OffsetX;
            var top = zone.OffsetY;
            var right = zone.OffsetX + (int)zone.Width;
            var bottom = zone.OffsetY + (int)zone.Height;

            barriers.Add(CreateBarrier(nextId++, left, top, right - 1, top));
            barriers.Add(CreateBarrier(nextId++, left, bottom, right - 1, bottom));
            barriers.Add(CreateBarrier(nextId++, left, top, left, bottom - 1));
            barriers.Add(CreateBarrier(nextId++, right, top, right, bottom - 1));
        }

        return barriers.ToArray();
    }

    private static IDictionary<string, object> CreateBarrier(uint barrierId, int x1, int y1, int x2, int y2)
    {
        return new Dictionary<string, object>
        {
            ["barrier_id"] = barrierId,
            ["position"] = new ValueTuple<int, int, int, int>(x1, y1, x2, y2)
        };
    }

    private async Task EnableAsync()
    {
        if (_portal == null || _sessionHandle == null)
        {
            return;
        }

        ObjectPath sessionHandle = (ObjectPath)_sessionHandle!;
        try
        {
            await _portal.EnableAsync(sessionHandle, new Dictionary<string, object>()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "WaylandPortalInputService: Enable failed");
        }
    }

    private bool SessionMatches(ObjectPath sessionHandle)
    {
        return _sessionHandle != null && _sessionHandle.Equals(sessionHandle);
    }

    private void OnActivated(ObjectPath sessionHandle, IDictionary<string, object> options)
    {
        if (!SessionMatches(sessionHandle))
        {
            return;
        }

        var point = ExtractCursorPosition(options);
        if (point.HasValue)
        {
            lock (_cursorLock)
            {
                _lastCursor = point.Value;
            }
        }
    }

    private void OnDeactivated(ObjectPath sessionHandle, IDictionary<string, object> options)
    {
        if (!SessionMatches(sessionHandle))
        {
            return;
        }

        _ = EnableAsync();
    }

    private void OnDisabled(ObjectPath sessionHandle, IDictionary<string, object> options)
    {
        if (!SessionMatches(sessionHandle))
        {
            return;
        }

        _ = EnableAsync();
    }

    private void OnZonesChanged(ObjectPath sessionHandle, IDictionary<string, object> options)
    {
        if (!SessionMatches(sessionHandle))
        {
            return;
        }

        _ = RefreshZonesAsync();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _activatedSubscription?.Dispose();
        _deactivatedSubscription?.Dispose();
        _zonesChangedSubscription?.Dispose();
        _disabledSubscription?.Dispose();

        CloseSessionAsync().GetAwaiter().GetResult();
        _connection?.Dispose();
        _refreshLock.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
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
            DebugHelper.WriteException(ex, "WaylandPortalInputService: Failed to close session");
        }
        finally
        {
            _sessionProxy = null;
            _sessionHandle = null;
        }
    }

    private sealed record ZoneDescriptor(uint Width, uint Height, int OffsetX, int OffsetY);

    [DBusInterface("org.freedesktop.portal.InputCapture")]
    internal interface IInputCapture : IDBusObject
    {
        Task<ObjectPath> CreateSessionAsync(string parentWindow, IDictionary<string, object> options);

        Task<ObjectPath> GetZonesAsync(ObjectPath sessionHandle, IDictionary<string, object> options);

        Task<ObjectPath> SetPointerBarriersAsync(ObjectPath sessionHandle, IDictionary<string, object> options, IDictionary<string, object>[] barriers, uint zoneSet);

        Task EnableAsync(ObjectPath sessionHandle, IDictionary<string, object> options);

        Task DisableAsync(ObjectPath sessionHandle, IDictionary<string, object> options);

        Task<IDisposable> WatchActivatedAsync(Action<ObjectPath, IDictionary<string, object>> handler, Action<Exception>? error = null);

        Task<IDisposable> WatchDeactivatedAsync(Action<ObjectPath, IDictionary<string, object>> handler, Action<Exception>? error = null);

        Task<IDisposable> WatchDisabledAsync(Action<ObjectPath, IDictionary<string, object>> handler, Action<Exception>? error = null);

        Task<IDisposable> WatchZonesChangedAsync(Action<ObjectPath, IDictionary<string, object>> handler, Action<Exception>? error = null);
    }

    [DBusInterface("org.freedesktop.portal.Session")]
    internal interface ISession : IDBusObject
    {
        Task CloseAsync();
    }
}
