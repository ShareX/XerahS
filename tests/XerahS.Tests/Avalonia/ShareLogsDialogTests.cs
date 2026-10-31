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

using System.Net;
using System.Text;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using NUnit.Framework;
using XerahS.Core.Diagnostics;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.Tests.Avalonia;

[TestFixture]
public class ShareLogsDialogTests
{
    private const string Log =
        "2026-09-29 12:11:48.698 - XerahS starting.\n" +
        "2026-09-29 12:11:48.699 - Version: 0.31.3\n" +
        "2026-09-29 12:12:00.000 - CaptureFullScreenDxgi: EnumDisplaySettings orientation for \\\\.\\DISPLAY4 => mappedRotation=Rotate270\n" +
        "2026-09-29 12:20:00.000 - XerahS starting.\n" +
        "2026-09-29 12:20:00.001 - System cursors were left hidden by a previous session; restoring them.\n" +
        "2026-09-29 14:33:42.253 - Hotkey registered: Print Screen";

    /// <summary>Stands in for the ingest Function and counts uploads.</summary>
    private sealed class FakeServer : HttpMessageHandler
    {
        public int Posts;
        public DateTimeOffset? LastLogAt;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                return Json(HttpStatusCode.OK, LastLogAt == null ? "{\"reportCount\":0}" : $"{{\"lastLogAt\":\"{LastLogAt:O}\",\"reportCount\":1}}");
            }

            Interlocked.Increment(ref Posts);
            await Task.Delay(150, cancellationToken); // a slow network makes double clicks likely
            // Like the real Function: the mark is the window end the client sent.
            using var gzip = new System.IO.Compression.GZipStream(await request.Content!.ReadAsStreamAsync(cancellationToken),
                System.IO.Compression.CompressionMode.Decompress);
            using var json = await System.Text.Json.JsonDocument.ParseAsync(gzip, cancellationToken: cancellationToken);
            LastLogAt = json.RootElement.GetProperty("window").GetProperty("end").GetDateTimeOffset();
            return Json(HttpStatusCode.Created,
                $"{{\"reportId\":\"33333333-3333-4333-8333-333333333333\",\"duplicate\":false,\"reason\":null,\"lastLogAt\":\"{LastLogAt:O}\",\"deleteToken\":\"tok\"}}");
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private static PreparedDiagnosticsReport Build(DiagnosticsWindow window, DiagnosticsState state) =>
        DiagnosticsReportBuilder.Build(window, state, null, new DateTime(2026, 9, 29, 15, 0, 0),
            logsOverride: new[] { ("XerahS-20260929.log", Log) },
            os: new OsInfo { Family = "windows", Architecture = "X64", ProcessArchitecture = "X64" },
            hardware: new HardwareInfo("Test CPU", 8, 16, Array.Empty<GpuInfo>()),
            displays: new[] { new DisplayInfo { Ordinal = 0, Width = 3840, Height = 2160, Scale = 1.5, IsPrimary = true } },
            ffmpeg: new FfmpegInfo("7.1", "bundled", Array.Empty<string>()));

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaTest]
    public async Task DoubleClickingSend_UploadsOnce_ThenReportsNothingNew()
    {
        string statePath = Path.Combine(Path.GetTempPath(), $"xerahs-diag-{Guid.NewGuid():N}.json");
        var server = new FakeServer();
        try
        {
            var client = new DiagnosticsClient(new HttpClient(server), new Uri("https://diagnostics.test/"));
            var vm = new ShareLogsViewModel(new DiagnosticsStateStore(statePath), client, Build);
            // The app shows the dialog in the main window's modal overlay; a plain
            // window stands in for it here.
            var window = new global::Avalonia.Controls.Window
            {
                Width = 720,
                Height = 1400,
                Content = new ShareLogsDialog { DataContext = vm },
            };
            window.Show();

            await WaitUntil(() => vm.HasNewEntries && !vm.IsPreparing);
            Assert.That(vm.SummaryText, Does.Contain("1 unexpected exit"));
            Assert.That(vm.SendCommand.CanExecute(null), Is.True);

            string? screenshot = Environment.GetEnvironmentVariable("XERAHS_SHARELOGS_SCREENSHOT");
            if (!string.IsNullOrEmpty(screenshot))
            {
                window.CaptureRenderedFrame()?.Save(screenshot, global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }

            // Two clicks before the first upload returns.
            vm.SendCommand.Execute(null);
            vm.SendCommand.Execute(null);
            await WaitUntil(() => vm.HasSentReport && !vm.IsBusy && !vm.IsPreparing);

            Assert.That(server.Posts, Is.EqualTo(1), "a double click must upload once");
            Assert.That(vm.SentReportId, Is.EqualTo("33333333-3333-4333-8333-333333333333"));
            Assert.That(vm.StatusText, Does.StartWith("Sent."));
            Assert.That(vm.HasNewEntries, Is.False);
            Assert.That(vm.SendCommand.CanExecute(null), Is.False, "nothing new to send");
            Assert.That(vm.AlreadySentText, Does.StartWith("Nothing new since your last report"));
            Assert.That(vm.AlreadySentText, Does.Contain("14:33"), "the mark is shown in the log's local time");

            // Switching to the past week does not bring the sent entries back.
            vm.WindowIndex = 1;
            await WaitUntil(() => !vm.IsPreparing);
            Assert.That(vm.WindowLabel, Is.EqualTo("Past week"));
            Assert.That(vm.HasNewEntries, Is.False);

            // The token and mark survive a restart.
            DiagnosticsState saved = new DiagnosticsStateStore(statePath).Load();
            Assert.That(saved.SentReports.Single().DeleteToken, Is.EqualTo("tok"));
            Assert.That(saved.LastLogAt, Is.Not.Null);

            string? after = Environment.GetEnvironmentVariable("XERAHS_SHARELOGS_SCREENSHOT_AFTER");
            if (!string.IsNullOrEmpty(after))
            {
                window.CaptureRenderedFrame()?.Save(after, global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }
            bool? closedWith = null;
            vm.CloseRequested = result => closedWith = result;
            vm.CloseCommand.Execute(null);
            Assert.That(closedWith, Is.True, "Close reports that a report was sent");
            window.Close();
        }
        finally
        {
            File.Delete(statePath);
        }
    }
}
