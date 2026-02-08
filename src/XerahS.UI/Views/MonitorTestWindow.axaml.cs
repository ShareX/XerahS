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
using Avalonia.Rendering.Composition;
using System.ComponentModel;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class MonitorTestWindow : Window
{
    private MonitorTestViewModel? ViewModel => DataContext as MonitorTestViewModel;

    public MonitorTestWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (ViewModel != null)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            DrawMonitorLayout();
        }
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
    }

    private void DrawMonitorLayout()
    {
        if (ViewModel?.Snapshot == null || LayoutCanvas == null)
            return;

        LayoutCanvas.Children.Clear();

        var snapshot = ViewModel.Snapshot;
        if (snapshot.MonitorCount == 0)
            return;

        var virtualBounds = snapshot.VirtualDesktopBounds;
        var canvasWidth = LayoutCanvas.Bounds.Width;
        var canvasHeight = LayoutCanvas.Bounds.Height;

        if (canvasWidth == 0 || canvasHeight == 0)
            return;

        // Calculate scale to fit all monitors in canvas
        var scaleX = (canvasWidth - 40) / virtualBounds.Width;
        var scaleY = (canvasHeight - 40) / virtualBounds.Height;
        var scale = Math.Min(scaleX, scaleY);

        // Draw each monitor
        for (int i = 0; i < snapshot.Monitors.Count; i++)
        {
            var monitor = snapshot.Monitors[i];
            var bounds = monitor.PhysicalBounds;

            // Calculate scaled position and size
            var x = (bounds.X - virtualBounds.X) * scale + 20;
            var y = (bounds.Y - virtualBounds.Y) * scale + 20;
            var width = bounds.Width * scale;
            var height = bounds.Height * scale;

            // Draw monitor rectangle
            var rect = new Border
            {
                Width = width,
                Height = height,
                BorderBrush = monitor.IsPrimary ? Brushes.Green : Brushes.Gray,
                BorderThickness = new Thickness(2),
                Background = new SolidColorBrush(Colors.LightGray, 0.3),
                CornerRadius = new CornerRadius(4)
            };

            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            LayoutCanvas.Children.Add(rect);

            // Draw label
            var label = new TextBlock
            {
                Text = $"Monitor {i + 1}\n{bounds.Width}x{bounds.Height}\n({bounds.X}, {bounds.Y})\n{monitor.ScaleFactor:F2}x",
                FontSize = 11,
                Foreground = Brushes.Black,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
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
