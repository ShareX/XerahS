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

using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using XerahS.RegionCapture.Models;
using XerahS.RegionCapture.Services;

namespace XerahS.UI.ViewModels;

public partial class MonitorTestViewModel : ViewModelBase
{
    [ObservableProperty]
    private MonitorSnapshot? _snapshot;

    [ObservableProperty]
    private ObservableCollection<MonitorInfo> _monitors = [];

    [ObservableProperty]
    private MonitorInfo? _selectedMonitor;

    [ObservableProperty]
    private Point _cursorPosition;

    [ObservableProperty]
    private string _cursorInfo = string.Empty;

    [ObservableProperty]
    private TestMode _selectedTestMode = TestMode.MonitorDiagnostics;

    // Visual pattern test properties
    [ObservableProperty]
    private Color _testColor = Colors.White;

    [ObservableProperty]
    private int _grayscaleValue = 128;

    [ObservableProperty]
    private int _redValue = 255;

    [ObservableProperty]
    private int _greenValue = 255;

    [ObservableProperty]
    private int _blueValue = 255;

    [ObservableProperty]
    private GradientMode _selectedGradientMode = GradientMode.Horizontal;

    [ObservableProperty]
    private Color _gradientColor1 = Colors.Black;

    [ObservableProperty]
    private Color _gradientColor2 = Colors.White;

    [ObservableProperty]
    private PatternType _selectedPatternType = PatternType.HorizontalLines;

    [ObservableProperty]
    private int _patternSize = 10;

    [ObservableProperty]
    private bool _showSettings = true;

    public TestMode[] TestModes { get; } = Enum.GetValues<TestMode>();
    public GradientMode[] GradientModes { get; } = Enum.GetValues<GradientMode>();
    public PatternType[] PatternTypes { get; } = Enum.GetValues<PatternType>();

    /// <summary>
    /// Callback to request clipboard copy. Set by the tool service.
    /// </summary>
    public Action<string>? CopyToClipboardRequested { get; set; }

    /// <summary>
    /// Callback to save file. Set by the tool service.
    /// </summary>
    public Func<string, string, Task<string?>>? SaveFileRequested { get; set; }

    public MonitorTestViewModel()
    {
        RefreshMonitors();
    }

    [RelayCommand]
    private void RefreshMonitors()
    {
        Snapshot = MonitorSnapshotService.GetSnapshot();
        Monitors = new ObservableCollection<MonitorInfo>(Snapshot.Monitors);

        if (Monitors.Count > 0 && SelectedMonitor == null)
        {
            SelectedMonitor = Snapshot.PrimaryMonitor ?? Monitors[0];
        }
    }

    [RelayCommand]
    private void CopyDiagnostics()
    {
        if (Snapshot == null) return;

        var report = Snapshot.GenerateReport();
        CopyToClipboardRequested?.Invoke(report);
    }

    [RelayCommand]
    private async Task ExportDiagnosticsAsync()
    {
        if (Snapshot == null || SaveFileRequested == null) return;

        var report = Snapshot.GenerateReport();
        var fileName = $"MonitorDiagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

        var path = await SaveFileRequested(fileName, "Text files (*.txt)|*.txt");

        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                await File.WriteAllTextAsync(path, report);
            }
            catch (Exception ex)
            {
                // Log error silently
                System.Diagnostics.Debug.WriteLine($"Failed to export diagnostics: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        ShowSettings = !ShowSettings;
    }

    [RelayCommand]
    private void UpdateGrayscale()
    {
        var gray = (byte)GrayscaleValue;
        TestColor = Color.FromRgb(gray, gray, gray);
    }

    [RelayCommand]
    private void UpdateRgb()
    {
        TestColor = Color.FromRgb(
            (byte)RedValue,
            (byte)GreenValue,
            (byte)BlueValue);
    }

    partial void OnSelectedTestModeChanged(TestMode value)
    {
        // Update test color based on selected mode
        switch (value)
        {
            case TestMode.Grayscale:
                UpdateGrayscale();
                break;
            case TestMode.RgbColor:
                UpdateRgb();
                break;
            case TestMode.SolidColor:
                // Keep current test color
                break;
        }
    }

    partial void OnGrayscaleValueChanged(int value)
    {
        if (SelectedTestMode == TestMode.Grayscale)
        {
            UpdateGrayscale();
        }
    }

    partial void OnRedValueChanged(int value)
    {
        if (SelectedTestMode == TestMode.RgbColor)
        {
            UpdateRgb();
        }
    }

    partial void OnGreenValueChanged(int value)
    {
        if (SelectedTestMode == TestMode.RgbColor)
        {
            UpdateRgb();
        }
    }

    partial void OnBlueValueChanged(int value)
    {
        if (SelectedTestMode == TestMode.RgbColor)
        {
            UpdateRgb();
        }
    }

    public void UpdateCursorPosition(Point position)
    {
        CursorPosition = position;

        if (Snapshot != null)
        {
            var monitorIndex = FindMonitorAtPosition((int)position.X, (int)position.Y);
            CursorInfo = monitorIndex >= 0
                ? $"X: {position.X:F0}, Y: {position.Y:F0} (Monitor {monitorIndex + 1})"
                : $"X: {position.X:F0}, Y: {position.Y:F0}";
        }
        else
        {
            CursorInfo = $"X: {position.X:F0}, Y: {position.Y:F0}";
        }
    }

    private int FindMonitorAtPosition(int x, int y)
    {
        if (Snapshot == null) return -1;

        for (int i = 0; i < Snapshot.Monitors.Count; i++)
        {
            var bounds = Snapshot.Monitors[i].PhysicalBounds;
            if (x >= bounds.X && x < bounds.X + bounds.Width &&
                y >= bounds.Y && y < bounds.Y + bounds.Height)
            {
                return i;
            }
        }

        return -1;
    }
}

public enum TestMode
{
    MonitorDiagnostics,
    SolidColor,
    Grayscale,
    RgbColor,
    Gradient,
    Pattern
}

public enum GradientMode
{
    Horizontal,
    Vertical,
    DiagonalForward,
    DiagonalBackward
}

public enum PatternType
{
    HorizontalLines,
    VerticalLines,
    Checkerboard
}
