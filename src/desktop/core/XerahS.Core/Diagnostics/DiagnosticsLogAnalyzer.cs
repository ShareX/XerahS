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
using System.Text.RegularExpressions;

namespace XerahS.Core.Diagnostics;

/// <summary>
/// Scrubs and analyses XerahS log files for shared diagnostics reports.
/// This is a port of web/functions/diagnostics/analysis.ts; keep the two in step
/// (DiagnosticsLogAnalyzerTests uses the same fixture as analysis.test.ts).
/// </summary>
public static class DiagnosticsLogAnalyzer
{
    public const int RedactionVersion = 1;
    public const int SchemaVersion = 1;
    public const int MaxEvents = 500;
    public const int MaxSampleLength = 500;
    public const int MaxFrames = 30;

    /// <summary>Written on orderly shutdown from v0.32.0 on.</summary>
    public const string CleanExitMessage = "XerahS exiting.";
    public const string StartMessage = "XerahS starting.";
    public const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
    private static readonly Version ExitMarkerVersion = new(0, 32, 0);

    private static readonly RegexOptions Options = RegexOptions.CultureInvariant;

    // ------------------------------------------------------------------
    // Scrubbing
    // ------------------------------------------------------------------

    private static readonly HashSet<string> PublicUrlHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com", "api.github.com", "raw.githubusercontent.com", "xerahs.com", "cloud.xerahs.com", "getsharex.com"
    };

    private static readonly Regex WindowsUserPath = new(@"([A-Za-z]:\\Users\\)[^\\/\r\n""'<>|]+", Options);
    private static readonly Regex UnixUserPath = new(@"/(home|Users|var/home)/[^/\s""'<>]+", Options);
    private static readonly Regex OneDriveOrg = new(@"OneDrive - [^\\/\r\n""]+", Options);
    private static readonly Regex Email = new(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", Options);
    private static readonly Regex Url = new(@"\b(https?)://([^\s/""'<>?#]+)([^\s""'<>]*)", Options | RegexOptions.IgnoreCase);
    private static readonly Regex Secret = new(
        @"\b(authorization|bearer|api[_-]?key|apikey|access[_-]?token|refresh[_-]?token|client[_-]?secret|secret|password|passwd|token|cookie|sig|signature)(\s*[:=]\s*|\s+)(""?)([^\s""',;&]{4,})",
        Options | RegexOptions.IgnoreCase);
    // IPv4 addresses, but not version numbers ("Version: 0.31.3.1", "v1.2.3.4").
    private static readonly Regex IPv4 = new(
        @"(?<![\d.])(?<!(?:version|ver)[:=]?\s{0,3})(?<!\bv)(?:25[0-5]|2[0-4]\d|1?\d?\d)(?:\.(?:25[0-5]|2[0-4]\d|1?\d?\d)){3}(?![\d.])",
        Options | RegexOptions.IgnoreCase);

    private static readonly Regex IdentifierWindows = new(@"[A-Za-z]:\\Users\\([^\\/\r\n""'<>|]+)", Options);
    private static readonly Regex IdentifierUnix = new(@"/(?:home|Users|var/home)/([^/\s""'<>]+)", Options);
    private static readonly Regex IdentifierConfig = new(@"\b[A-Za-z]+Config-([A-Za-z0-9_.-]+?)\.json\b", Options);
    private static readonly Regex IdentifierOneDrive = new(@"OneDrive - ([^\\/\r\n""]+)", Options);
    private static readonly Regex NotIdentifying = new(
        @"^(public|default|shared|user|users|home|root|admin|<user>|<machine>|fedora|ubuntu|debian|arch|archlinux|manjaro|endeavouros|nixos|pop-os|mint|linuxmint|opensuse|suse|centos|rocky|alma|gentoo|kali|zorin|elementary|garuda|cachyos|bazzite|steamdeck|localhost|runner|vsts|appveyor|builder|desktop|laptop|computer|windows|linux|macbook|imac|omarchy)$",
        Options | RegexOptions.IgnoreCase);

    /// <summary>User, machine and organisation names found in log text.</summary>
    public static IReadOnlyList<string> CollectIdentifiers(string text)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        void Add(string value)
        {
            string name = value.Trim();
            if (name.Length < 3 || NotIdentifying.IsMatch(name)) return;
            names.Add(name);
        }

        foreach (Match m in IdentifierWindows.Matches(text)) Add(m.Groups[1].Value);
        foreach (Match m in IdentifierUnix.Matches(text)) Add(m.Groups[1].Value);
        foreach (Match m in IdentifierConfig.Matches(text)) Add(m.Groups[1].Value);
        foreach (Match m in IdentifierOneDrive.Matches(text)) Add(m.Groups[1].Value);
        return names.OrderByDescending(n => n.Length).ThenBy(n => n, StringComparer.Ordinal).ToList();
    }

    /// <summary>Removes personal data from log text.</summary>
    public static string Scrub(string text, IEnumerable<string>? identifiers = null)
    {
        string result = WindowsUserPath.Replace(text, "$1<user>");
        result = UnixUserPath.Replace(result, "/$1/<user>");
        result = OneDriveOrg.Replace(result, "OneDrive - <org>");
        result = Email.Replace(result, "<email>");
        result = Url.Replace(result, m =>
        {
            string scheme = m.Groups[1].Value;
            string host = Regex.Replace(m.Groups[2].Value, "^[^@]*@", "").ToLowerInvariant();
            string rest = m.Groups[3].Value;
            if (PublicUrlHosts.Contains(host.Split(':')[0]))
            {
                return $"{scheme}://{host}{Regex.Replace(rest, "[?#].*$", "")}";
            }
            return rest.Length > 0 ? $"{scheme}://{host}/<path>" : $"{scheme}://{host}";
        });
        result = Secret.Replace(result, m => $"{m.Groups[1].Value}{m.Groups[2].Value}{m.Groups[3].Value}<redacted>");
        result = IPv4.Replace(result, m => m.Value is "127.0.0.1" or "0.0.0.0" || m.Value.StartsWith("0.", StringComparison.Ordinal) ? m.Value : "<ip>");

        if (identifiers != null)
        {
            foreach (string name in identifiers)
            {
                if (string.IsNullOrEmpty(name) || name.Length < 3) continue;
                result = Regex.Replace(result, $"(?<![A-Za-z0-9]){Regex.Escape(name)}(?![A-Za-z0-9])", "<id>",
                    Options | RegexOptions.IgnoreCase);
            }
        }

        return result;
    }

    // ------------------------------------------------------------------
    // Parsing
    // ------------------------------------------------------------------

    private static readonly Regex Entry = new(@"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}) - (.*)$", Options);
    private static readonly Regex Tag = new(@"^\[([^\]]{1,64})\]\s*(.*)$", Options);
    private static readonly Regex Prefix = new(@"^([A-Za-z][A-Za-z0-9_.]{2,63}):\s+(.*)$", Options);

    public static List<LogEntry> ParseLog(string content, string fileName)
    {
        var entries = new List<LogEntry>();
        if (content.Length > 0 && content[0] == '﻿') content = content[1..];
        foreach (string line in content.Split('\n'))
        {
            string trimmed = line.TrimEnd('\r');
            Match match = Entry.Match(trimmed);
            if (match.Success)
            {
                string message = match.Groups[2].Value;
                Match repeated = Entry.Match(message);
                if (repeated.Success && repeated.Groups[1].Value == match.Groups[1].Value)
                {
                    message = repeated.Groups[2].Value;
                }
                entries.Add(new LogEntry(match.Groups[1].Value, message, new List<string>(), fileName));
            }
            else if (entries.Count > 0 && trimmed.Length > 0)
            {
                entries[^1].Details.Add(trimmed);
            }
        }
        return entries;
    }

    public static (string? Component, string Text) SplitComponent(string message)
    {
        Match tag = Tag.Match(message);
        if (tag.Success) return (tag.Groups[1].Value, tag.Groups[2].Value);
        Match prefix = Prefix.Match(message);
        if (prefix.Success) return (prefix.Groups[1].Value, prefix.Groups[2].Value);
        return (null, message);
    }

    private static readonly Regex DisplayName = new(@"\\\\\.\\DISPLAY\d+", Options);
    private static readonly Regex WindowsPath = new(@"[A-Za-z]:\\[^\s""'|,;)\]]+", Options);
    private static readonly Regex UnixPath = new(
        @"(?<![\w/.<>-])/(?:home|Users|usr|tmp|opt|var|run|etc|mnt|media|srv|proc|dev|nix)/[^\s""',;)\]]*", Options);
    private static readonly Regex Guid = new(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", Options);
    private static readonly Regex CompactGuid = new(@"\b[0-9a-f]{32}\b", Options);
    private static readonly Regex Hex = new(@"\b0x(?![89a-fA-F][0-9a-fA-F]{7}\b)[0-9a-fA-F]+\b", Options);
    private static readonly Regex Number = new(@"\b\d+(?:\.\d+)*\b", Options);
    private static readonly Regex Whitespace = new(@"\s+", Options);

    /// <summary>
    /// Collapses volatile tokens so one failure groups across machines and runs.
    /// Keeps identifiers (DuplicateOutput1, Rotate270) and HRESULT failure codes.
    /// </summary>
    public static string NormalizeTemplate(string text)
    {
        string result = DisplayName.Replace(text, "<display>");
        result = WindowsPath.Replace(result, "<path>");
        result = UnixPath.Replace(result, "<path>");
        result = Guid.Replace(result, "<guid>");
        result = CompactGuid.Replace(result, "<guid>");
        result = Hex.Replace(result, "<hex>");
        result = Number.Replace(result, "#");
        result = Whitespace.Replace(result, " ").Trim();
        return result.Length > 2000 ? result[..2000] : result;
    }

    private static readonly Regex ExceptionHeader = new(
        @"^\s*(?:---> )?((?:[A-Za-z_]\w*\.)*[A-Za-z_]\w*(?:Exception|Error))(?::\s*(.*))?$", Options);
    private static readonly Regex InlineException = new(@"((?:[A-Za-z_]\w*\.)+[A-Za-z_]\w*(?:Exception|Error)):\s*(.*)$", Options);
    private static readonly Regex StackFrame = new(@"^\s+at (.+?)(?: in .+?:line \d+)?\s*$", Options);

    public static (string Type, string Message, List<string> Frames)? FindException(LogEntry entry)
    {
        foreach (string candidate in entry.Details.Prepend(entry.Message))
        {
            Match header = ExceptionHeader.Match(candidate);
            if (!header.Success) continue;
            var frames = new List<string>();
            foreach (string line in entry.Details)
            {
                Match frame = StackFrame.Match(line);
                if (frame.Success) frames.Add(Whitespace.Replace(frame.Groups[1].Value, " "));
                if (frames.Count >= MaxFrames) break;
            }
            return (header.Groups[1].Value, header.Groups[2].Value, frames);
        }

        Match inline = InlineException.Match(entry.Message);
        return inline.Success ? (inline.Groups[1].Value, inline.Groups[2].Value, new List<string>()) : null;
    }

    private static readonly Regex ErrorWords = new(
        @"\b(fail(?:ed|ure|s)?|error|exception|crash(?:ed)?|unable to|could not|cannot|access violation|timed out)\b",
        Options | RegexOptions.IgnoreCase);
    private static readonly Regex NotAnError = new(
        @"\b(0 failed|no errors?|without errors?|0 errors?|errors?=0|failed=0)\b", Options | RegexOptions.IgnoreCase);

    public static bool IsErrorLine(string message) => ErrorWords.IsMatch(message) && !NotAnError.IsMatch(message);

    private static readonly Regex[] PreviousSessionDied =
    {
        new(@"System cursors were left hidden by a previous session", Options | RegexOptions.IgnoreCase),
        new(@"previous session (?:crashed|ended unexpectedly|did not exit cleanly)", Options | RegexOptions.IgnoreCase),
    };
    private static readonly Regex[] ProcessTerminating =
    {
        new(@"Unhandled AppDomain exception terminating=True", Options | RegexOptions.IgnoreCase),
        new(@"Critical application startup failure", Options | RegexOptions.IgnoreCase),
    };
    private static readonly Regex[] OrderlyShutdown =
    {
        new(@"^Tray: Exit$", Options),
        new(@"Installer launched\. Shutting down application", Options | RegexOptions.IgnoreCase),
        new(@"^Exiting \(--exit-on-complete specified\)\.", Options),
        new(@"^Startup wait elapsed\. Exiting after collecting diagnostics\.", Options),
    };
    private static readonly Regex VersionLine = new(@"^Version: (\S+)", Options);

    public static Version? ParseVersion(string? value)
    {
        Match m = Regex.Match(value?.Trim() ?? "", @"^(\d+)\.(\d+)(?:\.(\d+))?", Options);
        return m.Success
            ? new Version(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                m.Groups[3].Success ? int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : 0)
            : null;
    }

    public static List<SessionSummary> ToSessions(IReadOnlyList<LogEntry> entries, bool lastSessionIsCurrentProcess)
    {
        var sessions = new List<SessionSummary>();
        SessionSummary? current = null;
        foreach (LogEntry entry in entries)
        {
            if (entry.Message == StartMessage || current == null)
            {
                current = new SessionSummary { StartedAt = entry.Timestamp };
                sessions.Add(current);
            }
            current.Entries.Add(entry);
            if (current.AppVersion == null)
            {
                Match version = VersionLine.Match(entry.Message);
                if (version.Success) current.AppVersion = version.Groups[1].Value;
            }
        }

        for (int i = 0; i < sessions.Count; i++)
        {
            SessionSummary session = sessions[i];
            LogEntry? last = session.Entries.LastOrDefault();
            session.EndedAt = last?.Timestamp ?? session.StartedAt;
            LogEntry? tail = session.Entries.LastOrDefault(e => e.Message != CleanExitMessage) ?? last;
            if (tail != null)
            {
                (string? component, string text) = SplitComponent(tail.Message);
                session.LastComponent = component;
                session.LastMessageTemplate = NormalizeTemplate(text);
            }

            SessionSummary? next = i + 1 < sessions.Count ? sessions[i + 1] : null;
            bool isLast = next == null;
            Version? version = ParseVersion(session.AppVersion);

            if (session.Entries.Any(e => e.Message == CleanExitMessage || OrderlyShutdown.Any(p => p.IsMatch(e.Message))))
            {
                session.ExitKind = "clean";
            }
            else if (session.Entries.Any(e => ProcessTerminating.Any(p => p.IsMatch(e.Message))))
            {
                session.ExitKind = "abnormal";
            }
            else if (isLast)
            {
                session.ExitKind = lastSessionIsCurrentProcess ? "running" : "unknown";
            }
            else if (next!.Entries.Any(e => PreviousSessionDied.Any(p => p.IsMatch(e.Message))))
            {
                session.ExitKind = "abnormal";
            }
            else if (version != null && version >= ExitMarkerVersion)
            {
                session.ExitKind = "abnormal";
            }
            else
            {
                session.ExitKind = "unknown";
            }
        }

        return sessions;
    }

    public static List<EventSummary> ExtractEvents(IReadOnlyList<SessionSummary> sessions)
    {
        var byKey = new Dictionary<string, EventSummary>(StringComparer.Ordinal);
        void Add(EventSummary candidate, string at)
        {
            string key = string.Join('\u001f', candidate.Kind, candidate.ExceptionType ?? "", candidate.Component ?? "",
                candidate.MessageTemplate, string.Join('|', candidate.TopFrames.Take(5)));
            if (byKey.TryGetValue(key, out EventSummary? existing))
            {
                existing.Occurrences++;
                if (existing.FirstAt == null || string.CompareOrdinal(at, existing.FirstAt) < 0) existing.FirstAt = at;
                if (existing.LastAt == null || string.CompareOrdinal(at, existing.LastAt) > 0) existing.LastAt = at;
            }
            else
            {
                candidate.FirstAt = at;
                candidate.LastAt = at;
                candidate.Occurrences = 1;
                byKey[key] = candidate;
            }
        }

        static string? Sample(string value) => value.Length == 0 ? null : value.Length > MaxSampleLength ? value[..MaxSampleLength] : value;

        foreach (SessionSummary session in sessions)
        {
            foreach (LogEntry entry in session.Entries)
            {
                (string? component, string text) = SplitComponent(entry.Message);
                var exception = FindException(entry);
                if (exception is { } ex)
                {
                    string message = ex.Message.Length > 0 ? ex.Message : text;
                    Add(new EventSummary
                    {
                        Kind = "exception",
                        Component = component,
                        ExceptionType = ex.Type,
                        MessageTemplate = NormalizeTemplate(message),
                        SampleMessage = Sample(message),
                        TopFrames = ex.Frames,
                    }, entry.Timestamp);
                }
                else if (IsErrorLine(entry.Message))
                {
                    Add(new EventSummary
                    {
                        Kind = "error_line",
                        Component = component,
                        MessageTemplate = NormalizeTemplate(text),
                        SampleMessage = Sample(string.Join(' ', entry.Details.Prepend(text))),
                    }, entry.Timestamp);
                }
            }

            if (session.ExitKind == "abnormal")
            {
                Add(new EventSummary
                {
                    Kind = "abnormal_exit",
                    Component = session.LastComponent,
                    MessageTemplate = session.LastMessageTemplate ?? "",
                    SampleMessage = session.Entries.LastOrDefault() is { } last ? Sample(last.Message) : null,
                }, session.EndedAt ?? session.StartedAt);
            }
        }

        static int Rank(EventSummary e) => e.Kind == "abnormal_exit" ? 0 : e.Kind == "exception" ? 1 : 2;
        return byKey.Values
            .OrderBy(Rank)
            .ThenByDescending(e => e.Occurrences)
            .Take(MaxEvents)
            .ToList();
    }

    private static readonly Regex RecordingBackend = new(@"Recording backend: (.+)$", Options);

    /// <summary>Capture and recording backends named in the log (newest wins).</summary>
    public static (string? CaptureBackend, string? RecordingBackend) ExtractBackends(IEnumerable<LogEntry> entries)
    {
        string? capture = null;
        string? recording = null;
        foreach (LogEntry entry in entries)
        {
            string m = entry.Message;
            if (m.Contains("DXGI Output Duplication succeeded", StringComparison.Ordinal)) capture = "dxgi";
            else if (capture == null && Regex.IsMatch(m, @"Windows\.Graphics\.Capture|WGC capture succeeded", Options | RegexOptions.IgnoreCase)) capture = "wgc";
            else if (RecordingBackend.Match(m) is { Success: true } r) recording = r.Groups[1].Value.Trim();
            else if (m.Contains("Native recording (WGC + Media Foundation) is supported", StringComparison.Ordinal)) recording = "WGC + Media Foundation";
        }
        return (capture, recording);
    }

    /// <summary>
    /// Scrubs and analyses log files. Keeps entries at or after <paramref name="sinceInclusive"/>
    /// (the window start) and strictly after <paramref name="afterExclusive"/> (what this
    /// install already sent). Both are local "yyyy-MM-dd HH:mm:ss.fff" timestamps.
    /// </summary>
    public static AnalyzedLogs Analyze(
        IEnumerable<(string FileName, string Content)> files,
        string? sinceInclusive = null,
        string? afterExclusive = null,
        bool lastSessionIsCurrentProcess = false,
        IEnumerable<string>? extraIdentifiers = null)
    {
        var fileList = files.OrderBy(f => f.FileName, StringComparer.Ordinal).ToList();
        var identifiers = (extraIdentifiers ?? Array.Empty<string>())
            .Concat(fileList.SelectMany(f => CollectIdentifiers(f.Content)))
            .Where(n => !string.IsNullOrWhiteSpace(n) && n.Trim().Length >= 3 && !NotIdentifying.IsMatch(n.Trim()))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(n => n.Length)
            .ToList();

        var entries = new List<LogEntry>();
        var logs = new List<LogFile>();
        foreach (var (rawName, content) in fileList)
        {
            string fileName = Scrub(rawName, identifiers).Replace('\\', '_').Replace('/', '_');
            if (fileName.Length > 128) fileName = fileName[..128];
            var parsed = ParseLog(Scrub(content, identifiers), fileName)
                .Where(e => (sinceInclusive == null || string.CompareOrdinal(e.Timestamp, sinceInclusive) >= 0) &&
                            (afterExclusive == null || string.CompareOrdinal(e.Timestamp, afterExclusive) > 0))
                .ToList();
            if (parsed.Count == 0) continue;
            entries.AddRange(parsed);
            var text = new StringBuilder();
            foreach (LogEntry e in parsed)
            {
                if (text.Length > 0) text.Append('\n');
                text.Append(e.Timestamp).Append(" - ").Append(e.Message);
                foreach (string detail in e.Details) text.Append('\n').Append(detail);
            }
            logs.Add(new LogFile(fileName, text.ToString()));
        }

        entries.Sort((a, b) => string.CompareOrdinal(a.Timestamp, b.Timestamp));
        var sessions = ToSessions(entries, lastSessionIsCurrentProcess);
        var (capture, recording) = ExtractBackends(entries);
        return new AnalyzedLogs(
            entries,
            sessions,
            ExtractEvents(sessions),
            logs,
            entries.Count > 0 ? entries[0].Timestamp : null,
            entries.Count > 0 ? entries[^1].Timestamp : null,
            capture,
            recording);
    }

    public static string FormatTimestamp(DateTime local) => local.ToString(TimestampFormat, CultureInfo.InvariantCulture);

    public static DateTime ParseTimestamp(string timestamp) =>
        DateTime.ParseExact(timestamp, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);
}

public sealed record LogEntry(string Timestamp, string Message, List<string> Details, string FileName);

public sealed record LogFile(string FileName, string Content);

public sealed class SessionSummary
{
    public string StartedAt { get; set; } = "";
    public string? EndedAt { get; set; }
    public string? AppVersion { get; set; }
    public string ExitKind { get; set; } = "unknown";
    public string? LastComponent { get; set; }
    public string? LastMessageTemplate { get; set; }
    internal List<LogEntry> Entries { get; } = new();
}

public sealed class EventSummary
{
    public string Kind { get; set; } = "error_line";
    public string? Component { get; set; }
    public string? ExceptionType { get; set; }
    public string MessageTemplate { get; set; } = "";
    public string? SampleMessage { get; set; }
    public List<string> TopFrames { get; set; } = new();
    public string? FirstAt { get; set; }
    public string? LastAt { get; set; }
    public int Occurrences { get; set; } = 1;
}

public sealed record AnalyzedLogs(
    IReadOnlyList<LogEntry> Entries,
    IReadOnlyList<SessionSummary> Sessions,
    IReadOnlyList<EventSummary> Events,
    IReadOnlyList<LogFile> Logs,
    string? WindowStart,
    string? WindowEnd,
    string? CaptureBackend,
    string? RecordingBackend);
