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

using System.Text.Json;
using XerahS.Common;

namespace XerahS.Core.Diagnostics;

/// <summary>A report this install sent, with the token that deletes it.</summary>
public sealed record SentDiagnosticsReport(Guid ReportId, string DeleteToken, DateTimeOffset SentAt, DateTimeOffset LogsUntil);

public sealed record DiagnosticsState
{
    /// <summary>Random id used only to group this machine's reports. Not derived from hardware or accounts.</summary>
    public Guid InstallId { get; init; } = Guid.NewGuid();
    /// <summary>Newest log timestamp the server has from this install (UTC).</summary>
    public DateTimeOffset? LastLogAt { get; init; }
    public List<SentDiagnosticsReport> SentReports { get; init; } = new();
}

/// <summary>Persists <see cref="DiagnosticsState"/> as Diagnostics.json next to the settings files.</summary>
public sealed class DiagnosticsStateStore
{
    private readonly string _path;
    private readonly object _lock = new();

    public DiagnosticsStateStore(string? path = null)
    {
        _path = path ?? Path.Combine(SettingsManager.SettingsFolder, "Diagnostics.json");
    }

    public DiagnosticsState Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_path))
                {
                    var state = JsonSerializer.Deserialize<DiagnosticsState>(File.ReadAllText(_path), DiagnosticsJson.Options);
                    if (state != null && state.InstallId != Guid.Empty) return state;
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"[Diagnostics] Could not read {Path.GetFileName(_path)}; starting fresh: {ex.Message}");
            }

            var fresh = new DiagnosticsState();
            SaveLocked(fresh);
            return fresh;
        }
    }

    public void Save(DiagnosticsState state)
    {
        lock (_lock)
        {
            SaveLocked(state);
        }
    }

    private void SaveLocked(DiagnosticsState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            string temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(state, DiagnosticsJson.Indented));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[Diagnostics] Could not save {Path.GetFileName(_path)}: {ex.Message}");
        }
    }
}
