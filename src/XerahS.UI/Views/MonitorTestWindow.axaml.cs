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
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System.ComponentModel;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class MonitorTestWindow : Window
{
    private MonitorTestViewModel? _boundViewModel;
    private MonitorTestViewModel? ViewModel => DataContext as MonitorTestViewModel;

    public MonitorTestWindow()
    {
        InitializeComponent();

        // Redraw only when canvas size changes to avoid layout feedback loops.
        LayoutCanvas.SizeChanged += (_, e) =>
        {
            if (e.NewSize.Width > 0 &&
                e.NewSize.Height > 0 &&
                ViewModel?.Snapshot != null &&
                ViewModel.SelectedTestMode == TestMode.MonitorDiagnostics)
            {
                DrawMonitorLayout();
            }
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_boundViewModel != null)
        {
            _boundViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _boundViewModel = ViewModel;

        if (_boundViewModel != null)
        {
            _boundViewModel.PropertyChanged += ViewModel_PropertyChanged;
            UpdateViewVisibility();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_boundViewModel != null)
        {
            _boundViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _boundViewModel = null;
        }

        base.OnClosed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (ViewModel != null)
        {
            var position = e.GetPosition(this);
            ViewModel.UpdateCursorPosition(position);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MonitorTestViewModel.Snapshot))
        {
            DrawMonitorLayout();
        }
        else if (e.PropertyName == nameof(MonitorTestViewModel.SelectedTestMode))
        {
            UpdateViewVisibility();
        }
    }

    private void UpdateViewVisibility()
    {
        if (ViewModel == null) return;

        var isDiagnostics = ViewModel.SelectedTestMode == TestMode.MonitorDiagnostics;
        DiagnosticsView.IsVisible = isDiagnostics;
        TestPatternView.IsVisible = !isDiagnostics;

        if (isDiagnostics)
        {
            DrawMonitorLayout();
        }
    }

    private void DrawMonitorLayout()
    {
        if (ViewModel?.Snapshot == null ||
            LayoutCanvas == null ||
            ViewModel.SelectedTestMode != TestMode.MonitorDiagnostics)
            return;

        LayoutCanvas.Children.Clear();

        var snapshot = ViewModel.Snapshot;
        if (snapshot.MonitorCount == 0)
            return;

        var virtualBounds = snapshot.VirtualDesktopBounds;
        var canvasWidth = LayoutCanvas.Bounds.Width;
        var canvasHeight = LayoutCanvas.Bounds.Height;

        if (canvasWidth <= 0 ||
            canvasHeight <= 0 ||
            virtualBounds.Width <= 0 ||
            virtualBounds.Height <= 0)
            return;

        // Calculate scale to fit all monitors in canvas with padding
        var padding = 40.0;
        var availableWidth = canvasWidth - (padding * 2);
        var availableHeight = canvasHeight - (padding * 2);

        var scaleX = availableWidth / virtualBounds.Width;
        var scaleY = availableHeight / virtualBounds.Height;
        var scale = Math.Min(scaleX, scaleY);

        // Center the layout
        var totalScaledWidth = virtualBounds.Width * scale;
        var totalScaledHeight = virtualBounds.Height * scale;
        var offsetX = (canvasWidth - totalScaledWidth) / 2;
        var offsetY = (canvasHeight - totalScaledHeight) / 2;

        // Draw each monitor
        for (int i = 0; i < snapshot.Monitors.Count; i++)
        {
            var monitor = snapshot.Monitors[i];
            var bounds = monitor.PhysicalBounds;

            // Calculate scaled position and size
            var x = (bounds.X - virtualBounds.X) * scale + offsetX;
            var y = (bounds.Y - virtualBounds.Y) * scale + offsetY;
            var width = bounds.Width * scale;
            var height = bounds.Height * scale;

            // Draw monitor rectangle
            var rect = new Border
            {
                Width = width,
                Height = height,
                BorderBrush = monitor.IsPrimary ? Brushes.Green : Brushes.Gray,
                BorderThickness = new Thickness(monitor.IsPrimary ? 3 : 2),
                Background = new SolidColorBrush(Color.FromArgb(40, 100, 149, 237)), // Light blue tint
                CornerRadius = new CornerRadius(6)
            };

            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            LayoutCanvas.Children.Add(rect);

            // Draw label with monitor info
            var labelText = $"{monitor.DeviceName}\n{bounds.Width:F0} × {bounds.Height:F0}\n({bounds.X:F0}, {bounds.Y:F0})\n{monitor.ScaleFactor:F2}× scale";

            if (monitor.IsPrimary)
            {
                labelText += "\n★ PRIMARY";
            }

            var label = new TextBlock
            {
                Text = labelText,
                FontSize = Math.Max(10, Math.Min(14, width / 15)), // Scale font with monitor size
                Foreground = Brushes.Black,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                FontWeight = FontWeight.SemiBold
            };

            var labelContainer = new Border
            {
                Width = width,
                Height = height,
                Child = label
            };

            Canvas.SetLeft(labelContainer, x);
            Canvas.SetTop(labelContainer, y);
            LayoutCanvas.Children.Add(labelContainer);
        }
    }
}
