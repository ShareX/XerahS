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

public sealed class NetworkMonitorHistory
{
    public const int MaxSamples = 90000;
    public static readonly TimeSpan MaxSampleAge = TimeSpan.FromHours(24);
    private readonly List<NetworkStatusEvent> _events = [];
    private readonly List<NetworkLatencySample> _samples = [];
    private readonly Lock _sync = new();

    public IReadOnlyList<NetworkStatusEvent> Events
    {
        get
        {
            lock (_sync)
            {
                return [.. _events];
            }
        }
    }

    public IReadOnlyList<NetworkLatencySample> Samples
    {
        get
        {
            lock (_sync)
            {
                return [.. _samples];
            }
        }
    }

    public void AddEvent(NetworkStatusEvent statusEvent)
    {
        ArgumentNullException.ThrowIfNull(statusEvent);

        lock (_sync)
        {
            if (!statusEvent.IsConnected)
            {
                _events.Add(statusEvent);
                return;
            }

            for (int i = _events.Count - 1; i >= 0; i--)
            {
                NetworkStatusEvent previous = _events[i];
                if (!previous.IsConnected && previous.Duration == null && previous.Timestamp <= statusEvent.Timestamp)
                {
                    previous.Duration = statusEvent.Timestamp - previous.Timestamp;
                    statusEvent.Duration = previous.Duration;
                    break;
                }
            }

            _events.Add(statusEvent);
        }
    }

    public void AddSample(NetworkLatencySample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        lock (_sync)
        {
            _samples.Add(sample);
            DateTime cutoff = sample.Timestamp - MaxSampleAge;
            _samples.RemoveAll(existing => existing.Timestamp < cutoff);
            if (_samples.Count > MaxSamples)
            {
                _samples.RemoveRange(0, _samples.Count - MaxSamples);
            }
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _events.Clear();
            _samples.Clear();
        }
    }

    public void ReplaceEvents(IEnumerable<NetworkStatusEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        lock (_sync)
        {
            _events.Clear();
            _events.AddRange(events.OrderBy(item => item.Timestamp));
        }
    }

    public IReadOnlyList<NetworkStatusEvent> GetEvents(
        DateTime from,
        DateTime to,
        NetworkEventFilter filter = NetworkEventFilter.All)
    {
        lock (_sync)
        {
            IEnumerable<NetworkStatusEvent> query = _events.Where(item => item.Timestamp >= from && item.Timestamp <= to);
            query = filter switch
            {
                NetworkEventFilter.Disconnects => query.Where(item => !item.IsConnected),
                NetworkEventFilter.Connects => query.Where(item => item.IsConnected),
                _ => query
            };

            return query.OrderByDescending(item => item.Timestamp).ToList();
        }
    }

    public NetworkMonitorStats GetStats(
        DateTime from,
        DateTime to,
        bool isCurrentlyConnected,
        DateTime now)
    {
        DateTime rangeEnd = to < now ? to : now;
        DateTime rangeStart = from;
        if (rangeStart == DateTime.MinValue)
        {
            lock (_sync)
            {
                rangeStart = _events.Count > 0 ? _events.Min(item => item.Timestamp) : now;
            }
        }

        if (rangeEnd <= rangeStart)
        {
            rangeEnd = rangeStart.AddSeconds(1);
        }

        List<NetworkStatusEvent> events;
        List<NetworkLatencySample> samples;
        lock (_sync)
        {
            events = [.. _events.Where(item => item.Timestamp <= rangeEnd).OrderBy(item => item.Timestamp)];
            samples = [.. _samples.Where(item => item.Timestamp >= rangeStart && item.Timestamp <= rangeEnd)];
        }

        bool connected = ResolveStartingConnected(events, rangeStart, isCurrentlyConnected);
        DateTime cursor = rangeStart;
        TimeSpan connectedTime = TimeSpan.Zero;
        TimeSpan longestOutage = TimeSpan.Zero;
        TimeSpan currentOutage = TimeSpan.Zero;
        int disconnectCount = 0;

        foreach (NetworkStatusEvent statusEvent in events.Where(item => item.Timestamp >= rangeStart && item.Timestamp <= rangeEnd))
        {
            TimeSpan slice = statusEvent.Timestamp - cursor;
            if (slice < TimeSpan.Zero)
            {
                slice = TimeSpan.Zero;
            }

            if (connected)
            {
                connectedTime += slice;
                currentOutage = TimeSpan.Zero;
            }
            else
            {
                currentOutage += slice;
                if (currentOutage > longestOutage)
                {
                    longestOutage = currentOutage;
                }
            }

            if (connected && !statusEvent.IsConnected)
            {
                disconnectCount++;
            }

            connected = statusEvent.IsConnected;
            cursor = statusEvent.Timestamp;
        }

        TimeSpan tail = rangeEnd - cursor;
        if (tail < TimeSpan.Zero)
        {
            tail = TimeSpan.Zero;
        }

        if (connected)
        {
            connectedTime += tail;
        }
        else
        {
            currentOutage += tail;
            if (currentOutage > longestOutage)
            {
                longestOutage = currentOutage;
            }
        }

        double totalMs = Math.Max(1, (rangeEnd - rangeStart).TotalMilliseconds);
        IEnumerable<long> latencies = samples
            .Where(sample => sample.Success && sample.RoundtripMs.HasValue)
            .Select(sample => sample.RoundtripMs!.Value);

        long[] latencyArray = [.. latencies];

        return new NetworkMonitorStats
        {
            DisconnectCount = disconnectCount,
            EventCount = events.Count(item => item.Timestamp >= rangeStart && item.Timestamp <= rangeEnd),
            UptimePercent = Math.Clamp(connectedTime.TotalMilliseconds / totalMs * 100.0, 0, 100),
            LongestOutage = longestOutage,
            AverageLatencyMs = latencyArray.Length > 0 ? latencyArray.Average() : null,
            LastLatencyMs = samples.LastOrDefault(sample => sample.Success)?.RoundtripMs
        };
    }

    public IReadOnlyList<NetworkChartPoint> BuildChartPoints(
        DateTime from,
        DateTime to,
        bool isCurrentlyConnected,
        DateTime now)
    {
        DateTime rangeEnd = to < now ? to : now;
        DateTime rangeStart = from;

        List<NetworkStatusEvent> events;
        List<NetworkLatencySample> samples;
        lock (_sync)
        {
            if (rangeStart == DateTime.MinValue)
            {
                rangeStart = _events.Count > 0 ? _events.Min(item => item.Timestamp) : now.AddHours(-1);
            }

            if (rangeEnd <= rangeStart)
            {
                rangeEnd = rangeStart.AddSeconds(1);
            }

            events = [.. _events.Where(item => item.Timestamp <= rangeEnd).OrderBy(item => item.Timestamp)];
            samples = [.. _samples.Where(item => item.Timestamp >= rangeStart && item.Timestamp <= rangeEnd)];
        }

        bool connected = ResolveStartingConnected(events, rangeStart, isCurrentlyConnected);
        List<NetworkChartPoint> points =
        [
            new NetworkChartPoint
            {
                Timestamp = rangeStart,
                IsConnected = connected,
                LatencyMs = null
            }
        ];

        foreach (NetworkStatusEvent statusEvent in events.Where(item => item.Timestamp >= rangeStart && item.Timestamp <= rangeEnd))
        {
            points.Add(new NetworkChartPoint
            {
                Timestamp = statusEvent.Timestamp,
                IsConnected = connected,
                LatencyMs = null
            });
            connected = statusEvent.IsConnected;
            points.Add(new NetworkChartPoint
            {
                Timestamp = statusEvent.Timestamp,
                IsConnected = connected,
                LatencyMs = null
            });
        }

        points.Add(new NetworkChartPoint
        {
            Timestamp = rangeEnd,
            IsConnected = connected,
            LatencyMs = null
        });

        foreach (NetworkLatencySample sample in Downsample(samples, 800))
        {
            points.Add(new NetworkChartPoint
            {
                Timestamp = sample.Timestamp,
                IsConnected = sample.Success,
                LatencyMs = sample.RoundtripMs,
                IsSample = true
            });
        }

        return points
            .OrderBy(point => point.Timestamp)
            .ThenBy(point => point.IsSample ? 1 : 0)
            .ToList();
    }

    private static bool ResolveStartingConnected(
        IReadOnlyList<NetworkStatusEvent> events,
        DateTime rangeStart,
        bool isCurrentlyConnected)
    {
        NetworkStatusEvent? prior = events.LastOrDefault(item => item.Timestamp <= rangeStart);
        if (prior != null)
        {
            return prior.IsConnected;
        }

        NetworkStatusEvent? first = events.FirstOrDefault(item => item.Timestamp > rangeStart);
        if (first != null)
        {
            return first.IsBaseline ? first.IsConnected : !first.IsConnected;
        }

        return isCurrentlyConnected;
    }

    private static IEnumerable<NetworkLatencySample> Downsample(List<NetworkLatencySample> samples, int maxPoints)
    {
        if (samples.Count <= maxPoints)
        {
            return samples;
        }

        double step = (double)samples.Count / maxPoints;
        List<NetworkLatencySample> result = new(maxPoints);
        for (int i = 0; i < maxPoints; i++)
        {
            int index = Math.Min(samples.Count - 1, (int)(i * step));
            result.Add(samples[index]);
        }

        return result;
    }
}
