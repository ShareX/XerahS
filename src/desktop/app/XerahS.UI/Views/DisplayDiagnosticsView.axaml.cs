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
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.UI.Diagnostics;

namespace XerahS.UI.Views;

public partial class DisplayDiagnosticsView : UserControl
{
    private static readonly TimeSpan StatusDuration = TimeSpan.FromSeconds(4);

    private readonly DispatcherTimer _statusTimer;
    private string _report = string.Empty;

    public DisplayDiagnosticsView()
    {
        InitializeComponent();

        _statusTimer = new DispatcherTimer { Interval = StatusDuration };
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            StatusPanel.Opacity = 0;
        };

        Loaded += (_, _) => RefreshReport();
        Unloaded += (_, _) => _statusTimer.Stop();
    }

    public void RefreshReport()
    {
        DisplayDiagnosticsResult result;

        try
        {
            Window? owner = TopLevel.GetTopLevel(this) as Window;
            result = DisplayDiagnosticsCollector.Collect(owner);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Display diagnostics failed.");
            result = new DisplayDiagnosticsResult
            {
                Text = $"Display diagnostics failed:{Environment.NewLine}{ex}"
            };
        }

        _report = result.Text;
        ReportTextBox.Text = _report;
        ReportTextBox.CaretIndex = 0;
        LayoutView.SetMonitors(result.Monitors, result.CursorPosition);
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        RefreshReport();
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_report))
        {
            return;
        }

        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(_report);
            }
            else if (PlatformServices.IsInitialized)
            {
                await PlatformServices.Clipboard.SetTextAsync(_report);
            }
            else
            {
                return;
            }

            ShowStatus("Display diagnostics copied to clipboard.");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to copy display diagnostics to clipboard.");
            ShowStatus($"Copy failed: {ex.Message}");
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_report))
        {
            return;
        }

        try
        {
            string? filePath = await SaveReportAsync();
            if (!string.IsNullOrEmpty(filePath))
            {
                ShowStatus($"Saved to {filePath}");
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to save the display diagnostics report.");
            ShowStatus($"Save failed: {ex.Message}");
        }
    }

    private async Task<string?> SaveReportAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null)
        {
            return null;
        }

        IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Display Diagnostics Report",
            SuggestedFileName = $"XerahS-display-diagnostics-{DateTime.Now.ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture)}.txt",
            DefaultExtension = "txt",
            FileTypeChoices =
            [
                new FilePickerFileType("Text Files") { Patterns = ["*.txt"] }
            ]
        });

        string? filePath = file?.TryGetLocalPath();
        if (string.IsNullOrEmpty(filePath))
        {
            return null;
        }

        await File.WriteAllTextAsync(filePath, _report);
        return filePath;
    }

    private void ShowStatus(string message)
    {
        StatusText.Text = message;
        StatusPanel.Opacity = 1;
        _statusTimer.Stop();
        _statusTimer.Start();
    }
}
