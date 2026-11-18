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

namespace XerahS.Common.NetworkMonitor;

/// <summary>
/// Probes every selected host at once and keeps the fastest successful reply.
/// A down transition waits for <see cref="NetworkMonitorOptions.FailThreshold"/>
/// failed rounds, and those confirmation rounds run a second apart. The
/// disconnect is stamped at the first failed round, not the confirming one.
/// </summary>
public sealed class InternetConnectionMonitor : IDisposable
{
    public const int DisconnectConfirmationDelayMs = 1000;

    private readonly INetworkProbe _probe;
    private readonly Func<DateTime> _clock;
    private readonly Lock _sync = new();
    private NetworkMonitorOptions _options;
    private CancellationTokenSource? _loopCts;
    private int _failCount;
    private bool _isFirstEvent = true;
    private DateTime _firstFailDate;
    private bool _disposed;

    public event Action? ProbeStarted;
    public event Action<NetworkStatusEvent>? StatusChanged;
    public event Action<NetworkLatencySample>? SampleReceived;

    public InternetConnectionMonitor(
        INetworkProbe? probe = null,
        NetworkMonitorOptions? options = null,
        Func<DateTime>? clock = null)
    {
        _probe = probe ?? new IcmpNetworkProbe();
        _options = CloneOptions(options ?? new NetworkMonitorOptions());
        _clock = clock ?? (() => DateTime.Now);
    }

    public bool IsMonitoring { get; private set; }
    public bool IsConnected { get; private set; }
    public bool HasConnectionState
    {
        get
        {
            lock (_sync)
            {
                return !_isFirstEvent;
            }
        }
    }

    public int DisconnectCount { get; private set; }
    public string LastAddress { get; private set; } = string.Empty;
    public NetworkLatencySample? LastSample { get; private set; }

    public NetworkMonitorOptions Options
    {
        get
        {
            lock (_sync)
            {
                return CloneOptions(_options);
            }
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            lock (_sync)
            {
                _options = CloneOptions(value);
                _options.PingAddresses ??= [];
            }
        }
    }

    public static int GetDelayAfterProbe(bool suspectingDisconnect, int intervalMs)
    {
        int interval = Math.Max(200, intervalMs);
        return suspectingDisconnect ? Math.Min(interval, DisconnectConfirmationDelayMs) : interval;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_sync)
        {
            if (IsMonitoring)
            {
                return;
            }

            _loopCts = new CancellationTokenSource();
            IsMonitoring = true;
            CancellationToken token = _loopCts.Token;
            _ = Task.Run(() => RunLoopAsync(token), token);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        lock (_sync)
        {
            if (!IsMonitoring)
            {
                return;
            }

            cts = _loopCts;
            _loopCts = null;
            IsMonitoring = false;
        }

        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        cts?.Dispose();
    }

    public async Task CheckOnceAsync(CancellationToken cancellationToken = default)
    {
        string[] addresses;
        int timeoutMs;
        int failThreshold;
        lock (_sync)
        {
            addresses = _options.PingAddresses ?? [];
            timeoutMs = Math.Max(200, _options.PingTimeoutMs);
            failThreshold = Math.Max(1, _options.FailThreshold);
        }

        if (addresses.Length == 0)
        {
            return;
        }

        ProbeStarted?.Invoke();
        (string Address, NetworkProbeResult Result)[] probed = await Task.WhenAll(
            addresses.Select(address => ProbeAddressAsync(address, timeoutMs, cancellationToken))).ConfigureAwait(false);
        (string Address, NetworkProbeResult Result) best = SelectBest(probed);
        NetworkProbeResult result = best.Result;
        string address = best.Address;
        DateTime now = _clock();

        NetworkLatencySample sample = new()
        {
            Timestamp = now,
            Success = result.Success,
            RoundtripMs = result.RoundtripMs,
            Address = address,
            Method = result.Method,
            Error = result.Error
        };

        NetworkStatusEvent? statusEvent = null;

        lock (_sync)
        {
            LastAddress = address;
            LastSample = sample;

            if (result.Success)
            {
                _failCount = 0;
                if (!IsConnected)
                {
                    IsConnected = true;
                    statusEvent = new NetworkStatusEvent
                    {
                        Timestamp = now,
                        IsConnected = true,
                        RoundtripMs = result.RoundtripMs,
                        IsBaseline = _isFirstEvent
                    };
                    _isFirstEvent = false;
                }
                else if (_isFirstEvent)
                {
                    _isFirstEvent = false;
                }
            }
            else
            {
                _failCount++;
                if (_failCount == 1)
                {
                    _firstFailDate = now;
                }

                if (IsConnected)
                {
                    if (_failCount >= failThreshold)
                    {
                        IsConnected = false;
                        DisconnectCount++;
                        _isFirstEvent = false;
                        statusEvent = new NetworkStatusEvent
                        {
                            Timestamp = _firstFailDate,
                            IsConnected = false
                        };
                    }
                }
                else if (_isFirstEvent && _failCount >= failThreshold)
                {
                    _isFirstEvent = false;
                    statusEvent = new NetworkStatusEvent
                    {
                        Timestamp = _firstFailDate,
                        IsConnected = false,
                        IsBaseline = true
                    };
                }
            }
        }

        SampleReceived?.Invoke(sample);
        if (statusEvent != null)
        {
            StatusChanged?.Invoke(statusEvent);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await CheckOnceAsync(cancellationToken).ConfigureAwait(false);
                int delayMs;
                lock (_sync)
                {
                    bool hasConnectionState = !_isFirstEvent;
                    bool suspecting = LastSample is { Success: false } && (IsConnected || !hasConnectionState);
                    delayMs = GetDelayAfterProbe(suspecting, _options.PingIntervalMs);
                }

                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Network monitor loop");
        }
    }

    private async Task<(string Address, NetworkProbeResult Result)> ProbeAddressAsync(
        string address,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        try
        {
            NetworkProbeResult result = await _probe.ProbeAsync(address, timeoutMs, cancellationToken).ConfigureAwait(false);
            return (address, result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (address, new NetworkProbeResult(false, null, $"{address} failed ({ex.Message})."));
        }
    }

    private static (string Address, NetworkProbeResult Result) SelectBest(
        IReadOnlyList<(string Address, NetworkProbeResult Result)> results)
    {
        (string Address, NetworkProbeResult Result)? fastest = null;
        foreach ((string Address, NetworkProbeResult Result) candidate in results)
        {
            if (!candidate.Result.Success)
            {
                continue;
            }

            if (fastest == null ||
                (candidate.Result.RoundtripMs ?? long.MaxValue) < (fastest.Value.Result.RoundtripMs ?? long.MaxValue))
            {
                fastest = candidate;
            }
        }

        if (fastest != null)
        {
            return fastest.Value;
        }

        string error = string.Join("; ", results
            .Select(item => item.Result.Error)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal));
        string address = results.Count == 1
            ? results[0].Address
            : string.Join(", ", results.Select(item => item.Address));
        return (address, new NetworkProbeResult(false, null, string.IsNullOrWhiteSpace(error) ? "No response." : error));
    }

    private static NetworkMonitorOptions CloneOptions(NetworkMonitorOptions source)
    {
        return new NetworkMonitorOptions
        {
            FailThreshold = source.FailThreshold,
            PingIntervalMs = source.PingIntervalMs,
            PingTimeoutMs = source.PingTimeoutMs,
            PingAddresses = source.PingAddresses == null ? [] : [.. source.PingAddresses]
        };
    }
}
