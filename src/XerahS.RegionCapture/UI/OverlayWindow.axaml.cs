using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using XerahS.RegionCapture.Models;
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
    private readonly TaskCompletionSource<PixelRect?> _completionSource;
    private readonly RegionCaptureControl _captureControl;

    public OverlayWindow()
    {
        // Design-time constructor
        _monitor = new MonitorInfo("Design", new PixelRect(0, 0, 1920, 1080),
            new PixelRect(0, 0, 1920, 1040), 1.0, true);
        _completionSource = new TaskCompletionSource<PixelRect?>();
        _captureControl = new RegionCaptureControl(_monitor);
        InitializeComponent();
    }

    public OverlayWindow(MonitorInfo monitor, TaskCompletionSource<PixelRect?> completionSource, Action<PixelRect>? selectionChanged = null)
    {
        _monitor = monitor;
        _completionSource = completionSource;

        InitializeComponent();

        // Position window to cover the entire monitor
        Position = new AvPixelPoint((int)monitor.PhysicalBounds.X, (int)monitor.PhysicalBounds.Y);
        Width = monitor.PhysicalBounds.Width / monitor.ScaleFactor;
        Height = monitor.PhysicalBounds.Height / monitor.ScaleFactor;

        // Create and add the capture control
        _captureControl = new RegionCaptureControl(_monitor);
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

    private void OnRegionSelected(PixelRect region)
    {
        _completionSource.TrySetResult(region);
    }

    private void OnCancelled()
    {
        _completionSource.TrySetResult(null);
    }
}
