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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using XerahS.Common;

namespace XerahS.Platform.Linux.Capture;

internal sealed class PortalBusMonitor : IDisposable
{
    private const string MonitorEnv = "XERAHS_PORTAL_MONITOR";
    private readonly Process? _process;
    private readonly CancellationTokenSource _cts = new();
    private readonly string _logPrefix;

    private PortalBusMonitor(Process process, string logPrefix)
    {
        _process = process;
        _logPrefix = logPrefix;
        StartReader(process.StandardOutput, "stdout");
        StartReader(process.StandardError, "stderr");
    }

    public static PortalBusMonitor? TryStart(string logPrefix)
    {
        var enabled = Environment.GetEnvironmentVariable(MonitorEnv);
        if (!string.Equals(enabled, "1", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "busctl",
                Arguments = "--user monitor org.freedesktop.portal.Desktop",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = Process.Start(startInfo);
            if (process == null)
            {
                DebugHelper.WriteLine($"{logPrefix}: Failed to start portal bus monitor (busctl).");
                return null;
            }

            DebugHelper.WriteLine($"{logPrefix}: Portal bus monitor started (busctl).");
            return new PortalBusMonitor(process, logPrefix);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"{logPrefix}: Unable to start portal bus monitor (busctl).");
            return null;
        }
    }

    private void StartReader(System.IO.StreamReader reader, string streamName)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null)
                    {
                        break;
                    }
                    DebugHelper.WriteLine($"{_logPrefix}: busctl {streamName}: {line}");
                }
            }
            catch
            {
                // Best-effort logging; ignore failures.
            }
        });
    }

    public void Dispose()
    {
        try
        {
            _cts.Cancel();
        }
        catch
        {
        }

        try
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }

        try
        {
            _process?.Dispose();
        }
        catch
        {
        }
    }
}
