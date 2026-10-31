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
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core.Diagnostics;

namespace XerahS.UI.ViewModels;

/// <summary>
/// Debug tab "Share logs" modal (ShareLogsDialog, hosted in the main window's
/// ModalContent overlay): review, then send scrubbed logs and system data.
/// </summary>
public partial class ShareLogsViewModel : ViewModelBase
{
    private readonly DiagnosticsStateStore _stateStore;
    private readonly DiagnosticsClient _client;
    private DiagnosticsState _state;
    private PreparedDiagnosticsReport? _prepared;
    private CancellationTokenSource? _prepareCancellation;
    private int _prepareVersion;

    public ShareLogsViewModel()
        : this(new DiagnosticsStateStore(), new DiagnosticsClient())
    {
    }

    /// <param name="build">Builds a report for a window; defaults to reading this machine's logs.</param>
    public ShareLogsViewModel(
        DiagnosticsStateStore stateStore,
        DiagnosticsClient client,
        Func<DiagnosticsWindow, DiagnosticsState, PreparedDiagnosticsReport>? build = null)
    {
        _stateStore = stateStore;
        _client = client;
        _build = build ?? ((window, state) => DiagnosticsReportBuilder.Build(window, state, comment: null));
        _state = stateStore.Load();
    }

    private readonly Func<DiagnosticsWindow, DiagnosticsState, PreparedDiagnosticsReport> _build;

    /// <summary>Slider position: 0 = past 24 hours, 1 = past week.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowLabel))]
    private int _windowIndex;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isPreparing;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteLastReportCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCopyCommand))]
    private bool _hasNewEntries;

    [ObservableProperty]
    private string _summaryText = "Collecting logs…";

    [ObservableProperty]
    private string _alreadySentText = "";

    [ObservableProperty]
    private string _previewText = "";

    [ObservableProperty]
    private string _comment = "";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _statusIsError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSentReport))]
    [NotifyCanExecuteChangedFor(nameof(CopyReportIdCommand))]
    private string? _sentReportId;

    public bool HasSentReport => SentReportId != null;

    public string WindowLabel => Window.ToDisplayName();

    private DiagnosticsWindow Window => WindowIndex <= 0 ? DiagnosticsWindow.Past24Hours : DiagnosticsWindow.PastWeek;

    /// <summary>Set by the modal host (IViewDialogService.ShowShareLogsAsync).</summary>
    public Action<bool>? CloseRequested { get; set; }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(HasSentReport);

    private bool _initialized;

    /// <summary>Set by the view: asks for a path to save a copy of the report.</summary>
    public Func<string, Task<string?>>? PickSavePath { get; set; }

    /// <summary>Set by the view: copies text to the clipboard.</summary>
    public Func<string, Task>? CopyToClipboard { get; set; }

    public string Endpoint => _client.Endpoint.Host;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        // The server knows what this install already sent, even from another
        // copy of the settings. Fall back to the local record when offline.
        try
        {
            DiagnosticsInstallStatus status = await _client.GetStatusAsync(_state.InstallId, CancellationToken.None);
            if (status.LastLogAt != _state.LastLogAt)
            {
                _state = _state with { LastLogAt = status.LastLogAt };
                _stateStore.Save(_state);
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[Diagnostics] Status check failed, using local state: {ex.Message}");
        }

        SentReportId = _state.SentReports.LastOrDefault()?.ReportId.ToString();
        await PrepareAsync();
    }

    partial void OnWindowIndexChanged(int value) => _ = PrepareAsync();

    private async Task PrepareAsync()
    {
        _prepareCancellation?.Cancel();
        var cancellation = _prepareCancellation = new CancellationTokenSource();
        int version = Interlocked.Increment(ref _prepareVersion);
        DiagnosticsWindow window = Window;
        DiagnosticsState state = _state;

        IsPreparing = true;
        SummaryText = $"Collecting logs for the {window.ToDisplayName().ToLowerInvariant()}…";
        try
        {
            var (prepared, preview) = await Task.Run(() =>
            {
                PreparedDiagnosticsReport report = _build(window, state);
                return (report, DiagnosticsReportBuilder.BuildPreview(report));
            }, cancellation.Token);

            if (version != _prepareVersion) return;
            _prepared = prepared;
            HasNewEntries = prepared.HasNewEntries;
            PreviewText = preview;
            SummaryText = Describe(prepared);
            AlreadySentText = prepared.AlreadySentUntil is { } until
                ? prepared.HasNewEntries
                    ? $"Logs up to {FormatTime(until)} were already shared from this computer. Only newer entries will be sent."
                    : $"Nothing new since your last report (logs up to {FormatTime(until)} were already shared)."
                : "";
        }
        catch (OperationCanceledException)
        {
            // A newer window selection replaced this one.
        }
        catch (Exception ex)
        {
            if (version != _prepareVersion) return;
            _prepared = null;
            HasNewEntries = false;
            SummaryText = "The logs could not be collected.";
            SetStatus($"Could not collect logs: {ex.Message}", isError: true);
            DebugHelper.WriteException(ex, "[Diagnostics] Preparing report failed");
        }
        finally
        {
            if (version == _prepareVersion) IsPreparing = false;
        }
    }

    private static string Describe(PreparedDiagnosticsReport report)
    {
        if (!report.HasNewEntries) return "No log entries to send for this period.";
        string range = report.FirstEntry is { } first && report.LastEntry is { } last
            ? $"{FormatTime(first)} – {FormatTime(last)}"
            : "";
        string crashes = report.AbnormalExitCount switch
        {
            0 => "",
            1 => " · 1 unexpected exit",
            int n => $" · {n} unexpected exits",
        };
        return $"{report.Payload.Logs.Count} log file(s) · {report.LineCount:N0} lines · {FormatSize(report.LogBytes)} · {range}{crashes}";
    }

    private static string FormatTime(DateTime value) => value.ToString("d MMM HH:mm", CultureInfo.CurrentCulture);

    private static string FormatSize(long bytes) => bytes >= 1024 * 1024
        ? $"{bytes / 1024d / 1024d:0.0} MB"
        : $"{Math.Max(1, bytes / 1024d):0} KB";

    private void SetStatus(string text, bool isError = false)
    {
        StatusText = text;
        StatusIsError = isError;
    }

    private bool CanSend() => !IsBusy && !IsPreparing && HasNewEntries && _prepared != null;

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        // ICommand.Execute does not check CanExecute, so guard here too:
        // however Send is triggered, one report is uploaded at a time.
        if (IsBusy || _prepared is not { } prepared) return;
        IsBusy = true;
        SetStatus("Sending…");
        try
        {
            DiagnosticsReportPayload payload = prepared.Payload with
            {
                Comment = DiagnosticsReportBuilder.ScrubComment(Comment),
            };
            byte[] body = await Task.Run(() => DiagnosticsClient.Compress(payload));
            DiagnosticsSubmitResult result = await _client.SubmitAsync(body, CancellationToken.None);

            var sentReports = _state.SentReports.ToList();
            if (!result.Duplicate && result.DeleteToken != null)
            {
                sentReports.Add(new SentDiagnosticsReport(result.ReportId, result.DeleteToken, DateTimeOffset.Now,
                    payload.Window.End));
            }
            _state = _state with
            {
                LastLogAt = result.LastLogAt ?? payload.Window.End,
                SentReports = sentReports,
            };
            _stateStore.Save(_state);
            SentReportId = result.ReportId.ToString();

            SetStatus(result.Reason switch
            {
                "no_new_entries" => $"These logs were already shared in report {result.ReportId}. Nothing new was sent.",
                "same_report" => $"This report was already received as {result.ReportId}.",
                _ when result.DeduplicatedBefore != null =>
                    $"Sent. Report ID {result.ReportId}. Entries you had already shared were skipped.",
                _ => $"Sent. Thank you! Report ID {result.ReportId}.",
            });
            DebugHelper.WriteLine($"[Diagnostics] Report {result.ReportId} sent (duplicate={result.Duplicate}, reason={result.Reason ?? "none"}).");
        }
        catch (DiagnosticsUploadException ex)
        {
            SetStatus(ex.Message, isError: true);
        }
        catch (Exception ex)
        {
            SetStatus($"Sending failed: {ex.Message}", isError: true);
            DebugHelper.WriteException(ex, "[Diagnostics] Sending report failed");
        }
        finally
        {
            IsBusy = false;
        }

        await PrepareAsync();
    }

    private bool CanDelete() => !IsBusy && _state.SentReports.Count > 0;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteLastReportAsync()
    {
        SentDiagnosticsReport? last = _state.SentReports.LastOrDefault();
        if (IsBusy || last == null) return;
        IsBusy = true;
        SetStatus($"Deleting report {last.ReportId}…");
        try
        {
            bool deleted = await _client.DeleteAsync(last.ReportId, last.DeleteToken, CancellationToken.None);
            DiagnosticsInstallStatus status = await _client.GetStatusAsync(_state.InstallId, CancellationToken.None);
            _state = _state with
            {
                LastLogAt = status.LastLogAt,
                SentReports = _state.SentReports.Where(r => r.ReportId != last.ReportId).ToList(),
            };
            _stateStore.Save(_state);
            SentReportId = _state.SentReports.LastOrDefault()?.ReportId.ToString();
            SetStatus(deleted
                ? $"Report {last.ReportId} was deleted. Its logs can be sent again."
                : $"Report {last.ReportId} no longer exists on the server.");
        }
        catch (DiagnosticsUploadException ex)
        {
            SetStatus(ex.Message, isError: true);
        }
        finally
        {
            IsBusy = false;
            DeleteLastReportCommand.NotifyCanExecuteChanged();
        }

        await PrepareAsync();
    }

    private bool CanCopyReportId() => SentReportId != null;

    [RelayCommand(CanExecute = nameof(CanCopyReportId))]
    private async Task CopyReportIdAsync()
    {
        if (SentReportId != null && CopyToClipboard != null)
        {
            await CopyToClipboard(SentReportId);
            SetStatus($"Copied report ID {SentReportId}.");
        }
    }

    private bool CanSaveCopy() => HasNewEntries && _prepared != null;

    [RelayCommand(CanExecute = nameof(CanSaveCopy))]
    private async Task SaveCopyAsync()
    {
        if (_prepared is not { } prepared || PickSavePath == null) return;
        string? path = await PickSavePath($"XerahS-diagnostics-{DateTime.Now:yyyyMMdd-HHmm}.json");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            DiagnosticsReportPayload payload = prepared.Payload with { Comment = DiagnosticsReportBuilder.ScrubComment(Comment) };
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(payload, DiagnosticsJson.Indented));
            SetStatus($"Saved a copy to {Path.GetFileName(path)}.");
        }
        catch (Exception ex)
        {
            SetStatus($"Could not save the copy: {ex.Message}", isError: true);
        }
    }

    partial void OnIsBusyChanged(bool value) => DeleteLastReportCommand.NotifyCanExecuteChanged();
}
