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
using Avalonia.Markup.Xaml;
using XerahS.RegionCapture.Models;
using XerahS.RegionCapture;
using AvPixelRect = Avalonia.PixelRect;
using AvPixelPoint = Avalonia.PixelPoint;
using PixelRect = XerahS.RegionCapture.Models.PixelRect;
using PixelPoint = XerahS.RegionCapture.Models.PixelPoint;

namespace XerahS.RegionCapture.UI;

/// <summary>
/// A transparent overlay window for a single monitor.
/// Each monitor gets its own overlay to avoid mixed-DPI scaling issues.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly MonitorInfo _monitor;
    private readonly TaskCompletionSource<RegionSelectionResult?> _completionSource;
    private readonly RegionCaptureControl _captureControl;

    public OverlayWindow()
    {
        // Design-time constructor
        _monitor = new MonitorInfo("Design", new PixelRect(0, 0, 1920, 1080),
            new PixelRect(0, 0, 1920, 1040), 1.0, true);
        _completionSource = new TaskCompletionSource<RegionSelectionResult?>();
        _captureControl = new RegionCaptureControl(_monitor);
        InitializeComponent();
    }

    public OverlayWindow(
        MonitorInfo monitor,
        TaskCompletionSource<RegionSelectionResult?> completionSource,
        Action<PixelRect>? selectionChanged = null,
        XerahS.Platform.Abstractions.CursorInfo? initialCursor = null,
        RegionCaptureOptions? options = null)
    {
        _monitor = monitor;
        _completionSource = completionSource;

        InitializeComponent();

        // Position window to cover the entire monitor
        Position = new AvPixelPoint((int)monitor.PhysicalBounds.X, (int)monitor.PhysicalBounds.Y);
        Width = monitor.PhysicalBounds.Width / monitor.ScaleFactor;
        Height = monitor.PhysicalBounds.Height / monitor.ScaleFactor;

        // Create and add the capture control
        _captureControl = new RegionCaptureControl(_monitor, options, initialCursor);
        if (selectionChanged is not null)
            _captureControl.SelectionChanged += selectionChanged;
        _captureControl.RegionSelected += OnRegionSelected;
        _captureControl.Cancelled += OnCancelled;

        var panel = this.FindControl<Panel>("RootPanel")!;
        panel.Children.Add(_captureControl);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            OnCancelled();
            e.Handled = true;
        }
    }

    private void OnRegionSelected(RegionSelectionResult result)
    {
        _completionSource.TrySetResult(result);
    }

    private void OnCancelled()
    {
        _completionSource.TrySetResult(null);
    }
}
