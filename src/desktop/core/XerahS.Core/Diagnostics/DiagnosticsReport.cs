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
using System.Text.Json.Serialization;

namespace XerahS.Core.Diagnostics;

// Wire contract for POST /v1/reports (web/functions/diagnostics/schema.ts).

public enum DiagnosticsWindow
{
    Past24Hours,
    PastWeek,
}

public static class DiagnosticsWindowExtensions
{
    public static string ToWireKind(this DiagnosticsWindow window) => window == DiagnosticsWindow.Past24Hours ? "24h" : "7d";

    public static TimeSpan ToTimeSpan(this DiagnosticsWindow window) =>
        window == DiagnosticsWindow.Past24Hours ? TimeSpan.FromHours(24) : TimeSpan.FromDays(7);

    public static string ToDisplayName(this DiagnosticsWindow window) =>
        window == DiagnosticsWindow.Past24Hours ? "Past 24 hours" : "Past week";
}

public sealed record DiagnosticsReportPayload
{
    public int SchemaVersion { get; init; } = DiagnosticsLogAnalyzer.SchemaVersion;
    public int RedactionVersion { get; init; } = DiagnosticsLogAnalyzer.RedactionVersion;
    public string Trigger { get; init; } = "manual";
    public required Guid InstallId { get; init; }
    public required Guid ClientReportId { get; init; }
    public string? Comment { get; init; }
    public required AppInfo App { get; init; }
    public required OsInfo Os { get; init; }
    public required CaptureInfo Capture { get; init; }
    public required HardwareInfo Hardware { get; init; }
    public required IReadOnlyList<DisplayInfo> Displays { get; init; }
    public required WindowInfo Window { get; init; }
    public required IReadOnlyList<SessionInfo> Sessions { get; init; }
    public required IReadOnlyList<EventInfo> Events { get; init; }
    public required IReadOnlyList<LogFile> Logs { get; init; }
    public Dictionary<string, object?> Extra { get; init; } = new();
}

public sealed record AppInfo(string Version, string BuildFlavor, string? DotnetVersion);

public sealed record OsInfo
{
    public required string Family { get; init; }
    public string? Description { get; init; }
    public string? Version { get; init; }
    public string? Architecture { get; init; }
    public string? ProcessArchitecture { get; init; }
    public bool? Elevated { get; init; }
    public string? Sandbox { get; init; }
    public string? SessionType { get; init; }
    public string? DesktopEnvironment { get; init; }
    public string? UiLanguage { get; init; }
}

public sealed record CaptureInfo(string? CaptureBackend, string? RecordingBackend, FfmpegInfo Ffmpeg);

public sealed record FfmpegInfo(string? Version, string? Source, IReadOnlyList<string> HwEncoders);

public sealed record HardwareInfo(string? CpuModel, int? LogicalCores, int? RamGbBucket, IReadOnlyList<GpuInfo> Gpus);

public sealed record GpuInfo
{
    public string? Name { get; init; }
    public string? VendorId { get; init; }
    public string? DeviceId { get; init; }
    public string? DriverVersion { get; init; }
    public int? VramMbBucket { get; init; }
    public bool? IsSoftwareRenderer { get; init; }
}

public sealed record DisplayInfo
{
    public int Ordinal { get; init; }
    public string? DeviceName { get; init; }
    public bool? IsPrimary { get; init; }
    public int? X { get; init; }
    public int? Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public double? Scale { get; init; }
    public int? Rotation { get; init; }
    public double? RefreshHz { get; init; }
    public int? BitsPerPixel { get; init; }
    public bool? IsHdr { get; init; }
}

public sealed record WindowInfo(string Kind, DateTimeOffset Start, DateTimeOffset End);

public sealed record SessionInfo(
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    string? AppVersion,
    string ExitKind,
    string? LastComponent,
    string? LastMessageTemplate);

public sealed record EventInfo(
    string Kind,
    string? Component,
    string? ExceptionType,
    string MessageTemplate,
    string? SampleMessage,
    IReadOnlyList<string> TopFrames,
    DateTimeOffset? FirstAt,
    DateTimeOffset? LastAt,
    int Occurrences);

public sealed record DiagnosticsSubmitResult
{
    public Guid ReportId { get; init; }
    public DateTimeOffset? ReceivedAt { get; init; }
    public bool Duplicate { get; init; }
    /// <summary>null, "same_report" or "no_new_entries".</summary>
    public string? Reason { get; init; }
    public long? PrimarySignatureId { get; init; }
    public DateTimeOffset? DeduplicatedBefore { get; init; }
    public DateTimeOffset? LastLogAt { get; init; }
    public string? DeleteToken { get; init; }
}

public sealed record DiagnosticsInstallStatus
{
    public DateTimeOffset? LastLogAt { get; init; }
    public Guid? LastReportId { get; init; }
    public DateTimeOffset? LastReceivedAt { get; init; }
    public int ReportCount { get; init; }
}

public static class DiagnosticsJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static JsonSerializerOptions Indented { get; } = new(Options) { WriteIndented = true };
}
