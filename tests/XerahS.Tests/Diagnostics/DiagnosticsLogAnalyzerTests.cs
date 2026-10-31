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

using System.IO.Compression;
using System.Text.Json;
using NUnit.Framework;
using XerahS.Core.Diagnostics;

namespace XerahS.Tests.Diagnostics;

/// <summary>
/// Parity with web/functions/diagnostics/analysis.test.ts: same fixture, same expectations.
/// </summary>
public class DiagnosticsLogAnalyzerTests
{
    private static readonly string WindowsLog = string.Join("\r\n",
        "\uFEFF2026-09-29 11:06:33.290 - Executing job: RectangleRegion",
        "2026-09-29 11:06:33.402 - DxgiOutputDuplicationHelper: DuplicateOutput1 failed, using DuplicateOutput. HRESULT: [0x80070057], ApiCode: [E_INVALIDARG/Invalid arguments], Message: [The parameter is incorrect.",
        "]",
        "2026-09-29 11:49:00.978 - [PathTrace 4a33bdcf3b664d9db8632e862e4b7ec7] SaveImageToFile: dir=\"C:\\Users\\Dream\\OneDrive - Contoso Ltd\\Pictures\\Screenshots\"",
        "2026-09-29 12:09:03.898 - CaptureFullScreenDxgi: EnumDisplaySettings orientation for \\\\.\\DISPLAY4 => dmDisplayOrientation=3, mappedRotation=Rotate270",
        "2026-09-29 12:11:48.698 - XerahS starting.",
        "2026-09-29 12:11:48.699 - Version: 0.31.3",
        "2026-09-29 12:11:48.699 - Build: Release",
        "2026-09-29 12:11:48.701 - Operating system: Microsoft Windows 10.0.26200 (X64)",
        "2026-09-29 12:11:49.203 - [SettingsManager] UploadersConfig load started: C:\\Users\\Dream\\Documents\\XerahS\\Settings\\UploadersConfig-DREAM.json",
        "2026-09-29 12:11:49.409 - System cursors were left hidden by a previous session; restoring them.",
        "2026-09-29 12:11:51.595 - Uploaded to https://i.example-host.com/a/B1gSecret.png for dream@example.com with token=abcd1234efgh",
        "2026-09-29 12:11:52.100 - Screen capture: DXGI Output Duplication succeeded (9120x3984)",
        "2026-09-29 12:11:53.000 - Unhandled exception in worker:",
        "System.NullReferenceException: Object reference not set to an instance of an object.",
        "   at XerahS.Core.Tasks.WorkerTask.Run() in /home/runner/work/XerahS/src/WorkerTask.cs:line 42",
        "   at XerahS.Core.Tasks.TaskManager.Start(WorkerTask task)",
        "2026-09-29 12:11:54.000 - [Plugins] Complete: 5 succeeded, 0 failed",
        "2026-09-29 13:50:38.889 - Tray: Exit",
        "2026-09-29 13:51:05.464 - XerahS starting.",
        "2026-09-29 13:51:05.466 - Version: 0.25.5",
        "2026-09-29 14:33:42.253 - [DestinationSettings] Initialize skipped (already initialized).");

    [Test]
    public void CollectIdentifiers_FindsUserMachineAndOrgButNotDistroHosts()
    {
        Assert.That(DiagnosticsLogAnalyzer.CollectIdentifiers(WindowsLog), Is.SupersetOf(new[] { "Dream", "DREAM", "Contoso Ltd" }));
        Assert.That(DiagnosticsLogAnalyzer.CollectIdentifiers("UploadersConfig-fedora.json /home/runner/x"), Is.Empty);
    }

    [Test]
    public void Scrub_RemovesPersonalData()
    {
        string text = DiagnosticsLogAnalyzer.Scrub(WindowsLog, DiagnosticsLogAnalyzer.CollectIdentifiers(WindowsLog));
        Assert.That(text, Does.Not.Match("(?i)dream|contoso|example\\.com\\b|B1gSecret|abcd1234efgh"));
        Assert.That(text, Does.Contain("C:\\Users\\<user>\\OneDrive - <org>\\Pictures"));
        Assert.That(text, Does.Contain("UploadersConfig-<id>.json"));
        Assert.That(text, Does.Contain("https://i.example-host.com/<path>"));
        Assert.That(text, Does.Contain("<email>"));
        Assert.That(text, Does.Contain("token=<redacted>"));
    }

    [Test]
    public void Scrub_KeepsPublicUrlsVersionsAndLoopback()
    {
        Assert.That(
            DiagnosticsLogAnalyzer.Scrub("GET https://api.github.com/repos/KovaForge/XerahS/releases?x=1 at 127.0.0.1 on 10.0.26200 from 203.0.113.9"),
            Is.EqualTo("GET https://api.github.com/repos/KovaForge/XerahS/releases at 127.0.0.1 on 10.0.26200 from <ip>"));
    }

    [Test]
    public void Scrub_DoesNotMistakeVersionsForAddresses()
    {
        Assert.That(
            DiagnosticsLogAnalyzer.Scrub("Version: 0.31.3.12 Dev, plugin v1.2.3.4, peer 203.0.113.9, VERSION=2.0.1.7"),
            Is.EqualTo("Version: 0.31.3.12 Dev, plugin v1.2.3.4, peer <ip>, VERSION=2.0.1.7"));
    }

    [Test]
    public void ParseHyprlandMonitors_ReadsLayoutWithoutSerials()
    {
        const string json = """
            [{"id":0,"name":"eDP-1","description":"Apple Computer Inc iMac 9ACC64B374424","width":3840,"height":2160,
              "refreshRate":59.997,"x":0,"y":0,"scale":1.25,"transform":1,"disabled":false},
             {"id":1,"name":"DP-2","description":"x","width":0,"height":0,"x":0,"y":0,"scale":1,"transform":0}]
            """;
        var displays = DiagnosticsSystemInfo.ParseHyprlandMonitors(json);
        Assert.That(displays, Has.Count.EqualTo(1));
        Assert.That(displays[0], Is.EqualTo(new DisplayInfo
        {
            Ordinal = 0, DeviceName = "eDP-1", IsPrimary = true, X = 0, Y = 0, Width = 3840, Height = 2160,
            Scale = 1.25, Rotation = 90, RefreshHz = 59.997,
        }));
        Assert.That(DiagnosticsSystemInfo.ParseHyprlandMonitors("not json"), Is.Empty);
    }

    [Test]
    public void NormalizeTemplate_KeepsIdentifiersAndHresults()
    {
        Assert.That(
            DiagnosticsLogAnalyzer.NormalizeTemplate("DuplicateOutput1 failed HRESULT [0x80070057] hwnd=0x17103C took 25 ms on \\\\.\\DISPLAY4 Rotate270"),
            Is.EqualTo("DuplicateOutput1 failed HRESULT [0x80070057] hwnd=<hex> took # ms on <display> Rotate270"));
        Assert.That(
            DiagnosticsLogAnalyzer.NormalizeTemplate("saved C:\\Users\\<user>\\a.png and /home/<user>/b.png id 4a33bdcf3b664d9db8632e862e4b7ec7"),
            Is.EqualTo("saved <path> and <path> id <guid>"));
    }

    [Test]
    public void Parsing_JoinsContinuationLinesAndDropsRepeatedTimestamp()
    {
        var entries = DiagnosticsLogAnalyzer.ParseLog("2026-03-01 03:52:22.373 - 2026-03-01 03:52:22.373 - XerahS starting.\n  detail", "a.log");
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].Message, Is.EqualTo("XerahS starting."));
        Assert.That(entries[0].Details, Is.EqualTo(new[] { "  detail" }));

        Assert.That(DiagnosticsLogAnalyzer.SplitComponent("[FFmpeg] Found it"), Is.EqualTo(("FFmpeg", "Found it")));
        Assert.That(DiagnosticsLogAnalyzer.SplitComponent("Hotkey triggered: Print Screen"), Is.EqualTo(((string?)null, "Hotkey triggered: Print Screen")));
        Assert.That(DiagnosticsLogAnalyzer.IsErrorLine("[Plugins] Complete: 5 succeeded, 0 failed"), Is.False);
        Assert.That(DiagnosticsLogAnalyzer.IsErrorLine("DuplicateOutput1 failed, using DuplicateOutput"), Is.True);
    }

    [Test]
    public void Analyze_ClassifiesSessionsAndRanksEvents()
    {
        AnalyzedLogs result = DiagnosticsLogAnalyzer.Analyze(new[] { ("XerahS-20260929.log", WindowsLog) });

        Assert.That(result.Sessions.Select(s => (s.AppVersion, s.ExitKind)), Is.EqualTo(new (string?, string)[]
        {
            (null, "abnormal"),
            ("0.31.3", "clean"),
            ("0.25.5", "unknown"),
        }));
        Assert.That(result.Sessions[0].LastComponent, Is.EqualTo("CaptureFullScreenDxgi"));
        Assert.That(result.Sessions[0].LastMessageTemplate,
            Is.EqualTo("EnumDisplaySettings orientation for <display> => dmDisplayOrientation=#, mappedRotation=Rotate270"));

        Assert.That(result.Events.Select(e => e.Kind), Is.EqualTo(new[] { "abnormal_exit", "exception", "error_line" }));
        Assert.That(result.Events[1].ExceptionType, Is.EqualTo("System.NullReferenceException"));
        Assert.That(result.Events[1].TopFrames, Is.EqualTo(new[]
        {
            "XerahS.Core.Tasks.WorkerTask.Run()",
            "XerahS.Core.Tasks.TaskManager.Start(WorkerTask task)",
        }));
        Assert.That(result.Events[2].MessageTemplate, Does.StartWith("DuplicateOutput1 failed, using DuplicateOutput. HRESULT: [0x80070057]"));
        Assert.That(result.CaptureBackend, Is.EqualTo("dxgi"));
        Assert.That(result.Logs[0].Content, Does.Not.Match("(?i)dream|contoso"));

        var live = DiagnosticsLogAnalyzer.Analyze(new[] { ("x.log", WindowsLog) }, lastSessionIsCurrentProcess: true);
        Assert.That(live.Sessions[^1].ExitKind, Is.EqualTo("running"));
    }

    [Test]
    public void Analyze_TrimsToWindowAndToWhatWasAlreadySent()
    {
        var since = DiagnosticsLogAnalyzer.Analyze(new[] { ("x.log", WindowsLog) }, sinceInclusive: "2026-09-29 13:00:00.000");
        Assert.That(since.Entries, Has.Count.EqualTo(4));

        // Already sent up to 13:50:38.889: that entry is excluded, newer ones kept.
        var after = DiagnosticsLogAnalyzer.Analyze(new[] { ("x.log", WindowsLog) }, afterExclusive: "2026-09-29 13:50:38.889");
        Assert.That(after.Entries.Select(e => e.Timestamp).First(), Is.EqualTo("2026-09-29 13:51:05.464"));

        var nothing = DiagnosticsLogAnalyzer.Analyze(new[] { ("x.log", WindowsLog) }, afterExclusive: "2026-09-29 14:33:42.253");
        Assert.That(nothing.Logs, Is.Empty);
    }

    [Test]
    public void Build_ProducesTheWireShapeAndStableContentIds()
    {
        var state = new DiagnosticsState { InstallId = Guid.Parse("11111111-1111-4111-8111-111111111111") };
        var os = new OsInfo { Family = "windows", Architecture = "X64", ProcessArchitecture = "X64" };
        var hardware = new HardwareInfo("CPU", 8, 16, Array.Empty<GpuInfo>());
        var ffmpeg = new FfmpegInfo("7.1", "bundled", new[] { "h264_nvenc" });
        var now = new DateTime(2026, 9, 29, 15, 0, 0);

        PreparedDiagnosticsReport Build(DiagnosticsState s, string comment) => DiagnosticsReportBuilder.Build(
            DiagnosticsWindow.Past24Hours, s, comment, now,
            logsOverride: new[] { ("XerahS-20260929.log", WindowsLog) },
            os: os, hardware: hardware, displays: Array.Empty<DisplayInfo>(), ffmpeg: ffmpeg);

        PreparedDiagnosticsReport first = Build(state, "It crashed at 12:09, mail dream@example.com");
        PreparedDiagnosticsReport again = Build(state, "different comment");
        Assert.That(again.Payload.ClientReportId, Is.EqualTo(first.Payload.ClientReportId), "same logs give the same report id");
        Assert.That(first.Payload.Comment, Does.Contain("<email>").And.Not.Contain("dream@"));
        Assert.That(first.HasNewEntries, Is.True);
        Assert.That(first.AbnormalExitCount, Is.EqualTo(1));

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(first.Payload, DiagnosticsJson.Options));
        JsonElement root = json.RootElement;
        Assert.That(root.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
        Assert.That(root.GetProperty("installId").GetString(), Is.EqualTo("11111111-1111-4111-8111-111111111111"));
        Assert.That(root.GetProperty("window").GetProperty("kind").GetString(), Is.EqualTo("24h"));
        Assert.That(root.GetProperty("capture").GetProperty("ffmpeg").GetProperty("hwEncoders")[0].GetString(), Is.EqualTo("h264_nvenc"));
        Assert.That(root.GetProperty("events")[0].GetProperty("kind").GetString(), Is.EqualTo("abnormal_exit"));
        Assert.That(root.GetProperty("logs")[0].GetProperty("fileName").GetString(), Is.EqualTo("XerahS-20260929.log"));
        Assert.That(root.GetProperty("extra").GetProperty("source").GetString(), Is.EqualTo("app"));

        // After a send that reached the last entry, nothing new remains.
        var sent = state with { LastLogAt = DiagnosticsReportBuilder.ToOffset(first.LastEntry!.Value) };
        Assert.That(Build(sent, "").HasNewEntries, Is.False);

        byte[] gzip = DiagnosticsClient.Compress(first.Payload);
        using var inflated = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress);
        Assert.That(JsonDocument.Parse(inflated).RootElement.GetProperty("clientReportId").GetString(),
            Is.EqualTo(first.Payload.ClientReportId.ToString()));

        string preview = DiagnosticsReportBuilder.BuildPreview(first);
        Assert.That(preview, Does.Contain("=== XerahS-20260929.log"));
        Assert.That(preview, Does.Not.Match("(?i)dream"));
    }

    [Test]
    public void RamBucket_RoundsUpToPowersOfTwo()
    {
        Assert.That(DiagnosticsSystemInfo.RamGbBucket(15L * 1024 * 1024 * 1024 + 800L * 1024 * 1024), Is.EqualTo(16));
        Assert.That(DiagnosticsSystemInfo.RamGbBucket(31L * 1024 * 1024 * 1024), Is.EqualTo(32));
        Assert.That(DiagnosticsSystemInfo.VramMbBucket(24L * 1024 * 1024 * 1024), Is.EqualTo(32768));
    }

    [Test]
    public void FindLogFiles_SelectsDaysInTheWindow()
    {
        string root = Path.Combine(Path.GetTempPath(), "xerahs-diag-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "2026-09"));
            Directory.CreateDirectory(Path.Combine(root, "2026-10"));
            string app = XerahS.Common.AppResources.AppName;
            string errors = XerahS.Common.PathsManager.ErrorLogFileNamePrefix;
            foreach (string name in new[] { $"2026-09/{app}-20260920.log", $"2026-09/{app}-20260929.log", $"2026-09/{errors}-20260930.log",
                         $"2026-10/{app}-20261001.log", "2026-09/NetworkMonitor-20260929.log" })
            {
                File.WriteAllText(Path.Combine(root, name), "");
            }

            var files = DiagnosticsReportBuilder.FindLogFiles(root, new DateTime(2026, 9, 24, 12, 0, 0), new DateTime(2026, 10, 1, 12, 0, 0))
                .Select(Path.GetFileName);
            Assert.That(files, Is.EquivalentTo(new[] { $"{app}-20260929.log", $"{errors}-20260930.log", $"{app}-20261001.log" }));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
