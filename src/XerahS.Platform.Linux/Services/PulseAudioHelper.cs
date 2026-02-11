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

using System.Diagnostics;
using XerahS.Common;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// Helper for detecting PulseAudio/PipeWire audio sources used for capture.
/// </summary>
internal static class PulseAudioHelper
{
    /// <summary>
    /// Returns the monitor source for the default audio output sink.
    /// This captures what the user is actually hearing.
    /// Falls back to "default.monitor" if pactl is unavailable.
    /// </summary>
    public static string GetDefaultMonitorSource()
    {
        try
        {
            // First, get the default sink name
            string? defaultSink = RunPactl("get-default-sink");
            if (!string.IsNullOrWhiteSpace(defaultSink))
            {
                string monitorName = defaultSink.Trim() + ".monitor";

                // Verify this monitor source actually exists
                if (IsSourceAvailable(monitorName))
                {
                    DebugHelper.WriteLine($"[PulseAudioHelper] Using default sink monitor: {monitorName}");
                    return monitorName;
                }

                DebugHelper.WriteLine($"[PulseAudioHelper] Default sink monitor '{monitorName}' not found in sources, searching alternatives");
            }

            // Fallback: find the first RUNNING or IDLE .monitor source
            string? sourcesOutput = RunPactl("list sources short");
            if (!string.IsNullOrEmpty(sourcesOutput))
            {
                string? runningMonitor = null;
                string? anyMonitor = null;

                foreach (string line in sourcesOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length >= 2)
                    {
                        string sourceName = parts[1].Trim();
                        if (!sourceName.EndsWith(".monitor", StringComparison.OrdinalIgnoreCase))
                            continue;

                        anyMonitor ??= sourceName;

                        // Prefer RUNNING sources (actively playing audio)
                        if (parts.Length >= 5 && parts[4].Trim().Equals("RUNNING", StringComparison.OrdinalIgnoreCase))
                        {
                            runningMonitor ??= sourceName;
                        }
                    }
                }

                if (runningMonitor != null)
                {
                    DebugHelper.WriteLine($"[PulseAudioHelper] Using running monitor source: {runningMonitor}");
                    return runningMonitor;
                }

                if (anyMonitor != null)
                {
                    DebugHelper.WriteLine($"[PulseAudioHelper] Using first available monitor source: {anyMonitor}");
                    return anyMonitor;
                }
            }

            DebugHelper.WriteLine("[PulseAudioHelper] No monitor source found, falling back to default.monitor");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[PulseAudioHelper] Error querying pactl: {ex.Message}, falling back to default.monitor");
        }

        return "default.monitor";
    }

    /// <summary>
    /// Returns all available audio sources with their display names.
    /// Useful for populating a source selector UI.
    /// </summary>
    public static List<AudioSourceInfo> GetAvailableSources()
    {
        var sources = new List<AudioSourceInfo>();

        try
        {
            string? output = RunPactl("list sources short");
            if (string.IsNullOrEmpty(output)) return sources;

            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Split('\t');
                if (parts.Length >= 2)
                {
                    string name = parts[1].Trim();
                    bool isMonitor = name.EndsWith(".monitor", StringComparison.OrdinalIgnoreCase);
                    string state = parts.Length >= 5 ? parts[4].Trim() : "UNKNOWN";

                    sources.Add(new AudioSourceInfo
                    {
                        DeviceName = name,
                        IsMonitor = isMonitor,
                        State = state
                    });
                }
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[PulseAudioHelper] Error listing sources: {ex.Message}");
        }

        return sources;
    }

    /// <summary>
    /// Checks whether a given PulseAudio source name exists and is available.
    /// </summary>
    public static bool IsSourceAvailable(string sourceName)
    {
        try
        {
            string? output = RunPactl("list sources short");
            if (string.IsNullOrEmpty(output)) return false;

            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Split('\t');
                if (parts.Length >= 2 && parts[1].Trim().Equals(sourceName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch
        {
            // pactl not available
        }

        return false;
    }

    private static string? RunPactl(string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "pactl",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);

            return output;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Represents an available PulseAudio/PipeWire audio source.
/// </summary>
internal class AudioSourceInfo
{
    public string DeviceName { get; set; } = string.Empty;
    public bool IsMonitor { get; set; }
    public string State { get; set; } = string.Empty;
}
