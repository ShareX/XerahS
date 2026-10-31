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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using XerahS.Common;

namespace XerahS.Core.Diagnostics;

/// <summary>A scrubbed report ready to review and send.</summary>
public sealed class PreparedDiagnosticsReport
{
    public required DiagnosticsReportPayload Payload { get; init; }
    public required DiagnosticsWindow Window { get; init; }
    public required IReadOnlyList<string> SourceFiles { get; init; }
    public DateTime? FirstEntry { get; init; }
    public DateTime? LastEntry { get; init; }
    /// <summary>Entries up to this local time were already sent from this install.</summary>
    public DateTime? AlreadySentUntil { get; init; }
    public int LineCount { get; init; }
    public long LogBytes { get; init; }

    public bool HasNewEntries => Payload.Logs.Count > 0;
    public int SessionCount => Payload.Sessions.Count;
    public int AbnormalExitCount => Payload.Sessions.Count(s => s.ExitKind == "abnormal");
    public int EventCount => Payload.Events.Count;
}

public static class DiagnosticsReportBuilder
{
    private static readonly Regex DatedLogName = new(@"-(\d{8})\.log$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Log files that may hold entries in the window: the main and error logs
    /// (AppName-yyyyMMdd.log, AppName-errors-yyyyMMdd.log) of every day in it.
    /// </summary>
    public static IReadOnlyList<string> FindLogFiles(string logsFolder, DateTime windowStartLocal, DateTime nowLocal)
    {
        if (!Directory.Exists(logsFolder)) return Array.Empty<string>();
        var files = new List<string>();
        for (var month = new DateTime(windowStartLocal.Year, windowStartLocal.Month, 1);
             month <= nowLocal;
             month = month.AddMonths(1))
        {
            string folder = Path.Combine(logsFolder, month.ToString("yyyy-MM", CultureInfo.InvariantCulture));
            if (!Directory.Exists(folder)) continue;
            foreach (string file in Directory.EnumerateFiles(folder, "*.log"))
            {
                string name = Path.GetFileName(file);
                if (!name.StartsWith(AppResources.AppName + "-", StringComparison.Ordinal) &&
                    !name.StartsWith(PathsManager.ErrorLogFileNamePrefix + "-", StringComparison.Ordinal))
                {
                    continue;
                }
                Match date = DatedLogName.Match(name);
                if (!date.Success ||
                    !DateTime.TryParseExact(date.Groups[1].Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime day))
                {
                    continue;
                }
                if (day >= windowStartLocal.Date && day <= nowLocal.Date) files.Add(file);
            }
        }
        return files.OrderBy(f => f, StringComparer.Ordinal).ToList();
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>Names that identify this user or machine and must never leave it.</summary>
    public static IReadOnlyList<string> LocalIdentifiers()
    {
        var names = new List<string>();
        void Add(Func<string?> read)
        {
            try
            {
                string? value = read();
                if (!string.IsNullOrWhiteSpace(value)) names.Add(value.Trim());
            }
            catch
            {
                // Not available on this platform.
            }
        }
        Add(() => Environment.UserName);
        Add(() => Environment.MachineName);
        Add(() => Environment.UserDomainName);
        return names;
    }

    /// <summary>The user's optional note, scrubbed like the logs and capped at 2000 characters.</summary>
    public static string? ScrubComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment)) return null;
        string scrubbed = DiagnosticsLogAnalyzer.Scrub(comment.Trim(), LocalIdentifiers());
        return scrubbed.Length > 2000 ? scrubbed[..2000] : scrubbed;
    }

    /// <summary>Local log time to an absolute time, using the offset in force at that moment.</summary>
    public static DateTimeOffset ToOffset(DateTime local) => new(local, TimeZoneInfo.Local.GetUtcOffset(local));

    public static PreparedDiagnosticsReport Build(
        DiagnosticsWindow window,
        DiagnosticsState state,
        string? comment,
        DateTime? nowLocal = null,
        string? logsFolder = null,
        IReadOnlyList<(string FileName, string Content)>? logsOverride = null,
        OsInfo? os = null,
        HardwareInfo? hardware = null,
        IReadOnlyList<DisplayInfo>? displays = null,
        FfmpegInfo? ffmpeg = null)
    {
        DateTime now = nowLocal ?? DateTime.Now;
        DateTime windowStart = now - window.ToTimeSpan();
        DateTime? alreadySentUntil = state.LastLogAt?.ToLocalTime().DateTime;

        IReadOnlyList<string> sourceFiles = Array.Empty<string>();
        IReadOnlyList<(string FileName, string Content)> files;
        if (logsOverride != null)
        {
            files = logsOverride;
        }
        else
        {
            DebugHelper.Flush();
            sourceFiles = FindLogFiles(logsFolder ?? PathsManager.LogsFolderBase, windowStart, now);
            var read = new List<(string, string)>();
            foreach (string path in sourceFiles)
            {
                try
                {
                    read.Add((Path.GetFileName(path), ReadShared(path)));
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteLine($"[Diagnostics] Skipped unreadable log {Path.GetFileName(path)}: {ex.Message}");
                }
            }
            files = read;
        }

        AnalyzedLogs analyzed = DiagnosticsLogAnalyzer.Analyze(
            files,
            sinceInclusive: DiagnosticsLogAnalyzer.FormatTimestamp(windowStart),
            afterExclusive: alreadySentUntil is { } sent ? DiagnosticsLogAnalyzer.FormatTimestamp(sent) : null,
            lastSessionIsCurrentProcess: true,
            extraIdentifiers: LocalIdentifiers());

        DateTime? first = analyzed.WindowStart is { } s ? DiagnosticsLogAnalyzer.ParseTimestamp(s) : null;
        DateTime? last = analyzed.WindowEnd is { } e ? DiagnosticsLogAnalyzer.ParseTimestamp(e) : null;

        static DateTimeOffset At(string timestamp) => ToOffset(DiagnosticsLogAnalyzer.ParseTimestamp(timestamp));
        static DateTimeOffset? AtOptional(string? timestamp) => timestamp == null ? null : At(timestamp);

        string logsText = string.Join("\n", analyzed.Logs.Select(l => l.Content));
        string? scrubbedComment = ScrubComment(comment);

        var payload = new DiagnosticsReportPayload
        {
            InstallId = state.InstallId,
            ClientReportId = ContentId(state.InstallId, window, analyzed.WindowStart, analyzed.WindowEnd, logsText),
            Comment = scrubbedComment,
            App = new AppInfo(AppResources.Version,
#if DEBUG
                "Debug",
#else
                "Release",
#endif
                Environment.Version.ToString()),
            Os = os ?? DiagnosticsSystemInfo.CollectOs(),
            Capture = new CaptureInfo(analyzed.CaptureBackend, analyzed.RecordingBackend,
                ffmpeg ?? DiagnosticsSystemInfo.CollectFfmpeg()),
            Hardware = hardware ?? DiagnosticsSystemInfo.CollectHardware(),
            Displays = displays ?? DiagnosticsSystemInfo.CollectDisplays(),
            Window = new WindowInfo(window.ToWireKind(), ToOffset(first ?? windowStart), ToOffset(last ?? now)),
            Sessions = analyzed.Sessions.Select(x => new SessionInfo(
                At(x.StartedAt), AtOptional(x.EndedAt), x.AppVersion, x.ExitKind, x.LastComponent, x.LastMessageTemplate)).ToList(),
            Events = analyzed.Events.Select(x => new EventInfo(
                x.Kind, x.Component, x.ExceptionType, x.MessageTemplate, x.SampleMessage, x.TopFrames,
                AtOptional(x.FirstAt), AtOptional(x.LastAt), x.Occurrences)).ToList(),
            Logs = analyzed.Logs,
            Extra = new Dictionary<string, object?>
            {
                ["utcOffsetMinutes"] = (int)TimeZoneInfo.Local.GetUtcOffset(now).TotalMinutes,
                ["source"] = "app",
            },
        };

        return new PreparedDiagnosticsReport
        {
            Payload = payload,
            Window = window,
            SourceFiles = sourceFiles.Select(Path.GetFileName).OfType<string>().ToList(),
            FirstEntry = first,
            LastEntry = last,
            AlreadySentUntil = alreadySentUntil,
            LineCount = analyzed.Logs.Sum(l => l.Content.Count(c => c == '\n') + 1),
            LogBytes = Encoding.UTF8.GetByteCount(logsText),
        };
    }

    /// <summary>
    /// Same install, window and log content give the same id, so a resend of an
    /// identical report is recognised even after the app restarts.
    /// </summary>
    public static Guid ContentId(Guid installId, DiagnosticsWindow window, string? start, string? end, string logs)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{installId:D}\u001f{window.ToWireKind()}\u001f{start}\u001f{end}\u001f{logs}"));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50); // version 5 layout
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // RFC 4122 variant
        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }

    /// <summary>
    /// Human-readable view of exactly what will be sent: every field, with each
    /// log shown in full up to <paramref name="maxLogLines"/> lines.
    /// </summary>
    public static string BuildPreview(PreparedDiagnosticsReport report, int maxLogLines = 400)
    {
        DiagnosticsReportPayload metadata = report.Payload with { Logs = Array.Empty<LogFile>() };
        var text = new StringBuilder();
        text.AppendLine("=== Report data ===");
        text.AppendLine(JsonSerializer.Serialize(metadata, DiagnosticsJson.Indented));
        foreach (LogFile log in report.Payload.Logs)
        {
            string[] lines = log.Content.Split('\n');
            text.AppendLine();
            text.AppendLine($"=== {log.FileName} ({lines.Length:N0} lines, scrubbed) ===");
            if (lines.Length <= maxLogLines)
            {
                text.AppendLine(log.Content);
            }
            else
            {
                int half = maxLogLines / 2;
                text.AppendLine(string.Join('\n', lines.Take(half)));
                text.AppendLine($"… {lines.Length - maxLogLines:N0} more lines (use Save a copy to see everything) …");
                text.AppendLine(string.Join('\n', lines.Skip(lines.Length - half)));
            }
        }
        return text.ToString();
    }
}
