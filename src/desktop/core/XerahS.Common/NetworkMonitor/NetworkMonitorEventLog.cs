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

using System.Globalization;
using System.Text;

namespace XerahS.Common.NetworkMonitor;

public sealed class NetworkMonitorEventLog
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    private readonly Lock _sync = new();
    private readonly Func<string?> _filePathProvider;

    public NetworkMonitorEventLog(string? filePath)
    {
        _filePathProvider = () => filePath;
    }

    private NetworkMonitorEventLog(Func<string?> filePathProvider)
    {
        _filePathProvider = filePathProvider;
    }

    /// <summary>Today's log file; <see cref="CreateDaily"/> logs roll over at midnight.</summary>
    public string? FilePath => _filePathProvider();

    /// <summary>Filename prefix for the outage log (full name: NetworkMonitor-yyyyMMdd.log).</summary>
    public const string FileNamePrefix = "NetworkMonitor";

    /// <summary>Full path to the outage log for the given date: Logs/yyyy-MM/NetworkMonitor-yyyyMMdd.log.</summary>
    public static string GetDefaultPath(DateTime? date = null)
    {
        DateTime day = date ?? DateTime.Now;
        return Path.Combine(PathsManager.GetLogsFolderForMonth(day), $"{FileNamePrefix}-{day:yyyyMMdd}.log");
    }

    /// <summary>Creates a log that writes to a new date-stamped file each day, like the main and error logs.</summary>
    public static NetworkMonitorEventLog CreateDaily(Func<DateTime>? clock = null)
    {
        Func<DateTime> now = clock ?? (() => DateTime.Now);
        return new NetworkMonitorEventLog(() => GetDefaultPath(now()));
    }

    public void AppendRaw(string line)
    {
        string? filePath = FilePath;
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        lock (_sync)
        {
            try
            {
                EnsureFileExistsUnlocked(filePath);
                File.AppendAllText(filePath, line.TrimEnd() + Environment.NewLine, Utf8NoBom);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to write the network monitor event log");
            }
        }
    }

    public void Clear()
    {
        string? filePath = FilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        lock (_sync)
        {
            try
            {
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(filePath, string.Empty, Utf8NoBom);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to clear the network monitor event log");
            }
        }
    }

    public bool EnsureFileExists()
    {
        lock (_sync)
        {
            return EnsureFileExistsUnlocked(FilePath);
        }
    }

    private static bool EnsureFileExistsUnlocked(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        try
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            {
                File.WriteAllText(filePath, NetworkMonitorLogLines.Header, Utf8NoBom);
            }

            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to create the network monitor event log");
            return false;
        }
    }

}

public static class NetworkMonitorLogLines
{
    public static readonly TimeSpan StillDownInterval = TimeSpan.FromMinutes(5);

    public static string Header { get; } =
        "# XerahS network outage log" + Environment.NewLine +
        "# Times are local. Each line is one event." + Environment.NewLine +
        "# DOWN means the internet check failed. UP means it recovered." + Environment.NewLine +
        "# While the link stays down, another DOWN line is written every 5 minutes." + Environment.NewLine +
        "# WATCH, PAUSED, and STOPPED explain gaps where nothing was recorded." + Environment.NewLine;

    public static string Down(DateTime timestamp, NetworkLatencySample? sample, bool alreadyDown)
    {
        string reason = Reason(sample);
        string message = alreadyDown
            ? $"Internet is down. It was already down when monitoring confirmed the link. {reason}"
            : $"Internet down. {reason}";
        return Line(timestamp, "DOWN", message);
    }

    public static string StillDown(DateTime timestamp, NetworkLatencySample? sample, TimeSpan downFor)
    {
        return Line(timestamp, "DOWN", $"Still down for {FormatDuration(downFor)}. Last check: {Reason(sample)}");
    }

    public static string Up(DateTime timestamp, NetworkStatusEvent statusEvent, NetworkLatencySample? sample)
    {
        string reply = Reply(statusEvent, sample);
        if (statusEvent.IsBaseline)
        {
            return Line(timestamp, "UP", $"Internet is up. {reply}");
        }

        string lasted = statusEvent.Duration.HasValue
            ? $" Outage lasted {FormatDuration(statusEvent.Duration.Value)}."
            : string.Empty;
        return Line(timestamp, "UP", $"Internet back. {reply}{lasted}");
    }

    public static string Watch(DateTime timestamp, IReadOnlyList<string>? addresses, TimeSpan interval)
    {
        string target = addresses == null || addresses.Count == 0
            ? "Waiting for a host to check."
            : $"Checking {string.Join(", ", addresses)} every {FormatInterval(interval)}.";
        return Line(timestamp, "WATCH", $"Monitoring started. {target}");
    }

    public static string Paused(DateTime timestamp)
    {
        return Line(timestamp, "PAUSED", "Monitoring paused. Outages will not be recorded until monitoring starts again.");
    }

    public static string Stopped(DateTime timestamp)
    {
        return Line(timestamp, "STOPPED", "Monitoring stopped because XerahS is closing.");
    }

    public static string HistoryCleared(DateTime timestamp)
    {
        return Line(timestamp, "NOTE", "The on-screen event list was cleared. This outage log was kept.");
    }

    public static string FormatDuration(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        if (value.TotalSeconds < 1)
        {
            return $"{value.TotalMilliseconds:0} ms";
        }

        if (value.TotalMinutes < 1)
        {
            return $"{value.Seconds}s";
        }

        if (value.TotalHours < 1)
        {
            return $"{value.Minutes}m {value.Seconds}s";
        }

        if (value.TotalDays < 1)
        {
            return $"{(int)value.TotalHours}h {value.Minutes}m";
        }

        return $"{(int)value.TotalDays}d {value.Hours}h {value.Minutes}m";
    }

    private static string Line(DateTime timestamp, string kind, string message)
    {
        string time = timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return $"{time}  {kind,-7} {message}";
    }

    private static string Reason(NetworkLatencySample? sample)
    {
        if (sample == null)
        {
            return "No reply.";
        }

        if (!string.IsNullOrWhiteSpace(sample.Error))
        {
            return sample.Error.Trim();
        }

        if (!string.IsNullOrWhiteSpace(sample.Address))
        {
            return $"No reply from {sample.Address}.";
        }

        return "No reply.";
    }

    private static string Reply(NetworkStatusEvent statusEvent, NetworkLatencySample? sample)
    {
        if (sample?.Success == true && sample.RoundtripMs.HasValue && !string.IsNullOrWhiteSpace(sample.Address))
        {
            string via = string.IsNullOrWhiteSpace(sample.Method) ? string.Empty : $" via {sample.Method}";
            return $"Reply from {sample.Address}{via} in {sample.RoundtripMs.Value} ms.";
        }

        if (statusEvent.RoundtripMs.HasValue)
        {
            return $"Reply in {statusEvent.RoundtripMs.Value} ms.";
        }

        return "Link is up.";
    }

    private static string FormatInterval(TimeSpan interval)
    {
        if (interval.TotalSeconds >= 1 && interval.TotalMilliseconds % 1000 == 0)
        {
            return $"{(int)interval.TotalSeconds} s";
        }

        return $"{interval.TotalMilliseconds:0} ms";
    }
}
