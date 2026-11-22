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
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Core.Editor;
using ShareX.ImageEditor.Presentation.Rendering;
using ShareX.ImageEditor.Presentation.Theming;
using SkiaSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture.Models;
using XerahS.RegionCapture.Services;
using XerahS.RegionCapture.ViewModels;
using AvPixelRect = Avalonia.PixelRect;
using AvPixelPoint = Avalonia.PixelPoint;
using PixelRect = XerahS.RegionCapture.Models.PixelRect;
using PixelPoint = XerahS.RegionCapture.Models.PixelPoint;

namespace XerahS.RegionCapture.UI;

/// <summary>
/// A transparent overlay window for a single monitor.
/// Each monitor gets its own overlay to avoid mixed-DPI scaling issues.
/// XIP-0023: Includes AnnotationToolbar for annotating during capture.
/// </summary>
public partial class OverlayWindow : Window
{
    private static readonly Uri OverlayWindowUri = new("avares://XerahS.RegionCapture/UI/OverlayWindow.axaml");
    private static readonly Uri ImageEditorStylesUri = new("avares://ShareX.ImageEditor/Presentation/Theming/ImageEditorStyles.axaml");
    private static readonly Uri ImageEditorThemeUri = new("avares://ShareX.ImageEditor/Presentation/Theming/ImageEditorTheme.axaml");
    private static readonly long SelectionDragRebuildIntervalTicks = Math.Max(1, Stopwatch.Frequency / 60);

    private readonly Models.MonitorInfo _monitor;
    private readonly TaskCompletionSource<RegionSelectionResult?> _completionSource;
    private readonly RegionCaptureControl _captureControl;
    private readonly RegionCaptureAnnotationViewModel _viewModel;
    private readonly RegionCaptureAnnotationToolCoordinator? _annotationToolCoordinator;
    private readonly SKBitmap? _backgroundBitmap;
    private Canvas? _annotationCanvas;
    private AvPixelPoint _targetPosition;
    private double _targetWidth;
    private double _targetHeight;
    private bool _hasTargetWindowLayout;

    // Annotation drawing state - delegates to EditorCore for lifecycle
    private Control? _currentShape;
    private bool _isDrawing;
    private Annotation? _currentAnnotation;
    private bool _rebuildScheduled;
    private bool _rebuildPending;
    private long _lastRebuildTicks;
    private bool _selectionInteractionActive;
    private bool _suppressInvalidateRequested;
    private readonly List<Control> _persistedAnnotationVisuals = new();

    // CTRL modifier state for toggling between drawing and region selection
    private bool _ctrlPressed;

    // Delayed focus retries to work around Linux/Wayland compositor not granting focus immediately (reduces "first pointer moved" delay)
    private static readonly int[] FocusRetryDelayMs = [50, 200, 500];
    private bool _windowClosed;

    #region Constructors

    public OverlayWindow()
    {
        // Design-time constructor
        _monitor = new Models.MonitorInfo("Design", new PixelRect(0, 0, 1920, 1080),
            new PixelRect(0, 0, 1920, 1040), 1.0, true);
        _completionSource = new TaskCompletionSource<RegionSelectionResult?>();
        _captureControl = new RegionCaptureControl(_monitor);
        _viewModel = new RegionCaptureAnnotationViewModel();
        InitializeComponent();
        Title = PlatformWindowTitles.RegionCaptureOverlay;
        InitializeThemeScope();
        DataContext = _viewModel;
        ApplySelectionCursorPolicy();
    }

    public OverlayWindow(
        Models.MonitorInfo monitor,
        TaskCompletionSource<RegionSelectionResult?> completionSource,
        Action<PixelRect>? selectionChanged = null,
        XerahS.Platform.Abstractions.CursorInfo? initialCursor = null,
        RegionCaptureOptions? options = null,
        RegionCaptureAnnotationToolCoordinator? annotationToolCoordinator = null)
    {
        _monitor = monitor;
        _completionSource = completionSource;
        _backgroundBitmap = options?.BackgroundImage;
        _annotationToolCoordinator = annotationToolCoordinator;

        // XIP-0023: Create ViewModel for annotation toolbar
        _viewModel = new RegionCaptureAnnotationViewModel();
        _viewModel.InvalidateRequested += OnInvalidateRequested;
        _viewModel.AnnotationsRestored += OnAnnotationsRestored;

        // Load saved editor options if available
        if (options?.EditorOptions != null)
        {
            _viewModel.LoadOptions(options.EditorOptions);
        }

        // Load a monitor-scoped background image into EditorCore at logical resolution
        // so annotation coordinates (from Avalonia pointer events) match image coordinates.
        if (_backgroundBitmap != null)
        {
            var editorBitmap = CreateMonitorLogicalBackgroundBitmap(_backgroundBitmap, monitor);
            if (editorBitmap != null)
            {
                _viewModel.LoadBackgroundImage(editorBitmap);
            }
        }

        // Wire up EditorCore events
        _viewModel.EditorCore.EditAnnotationRequested += OnEditAnnotationRequested;

        InitializeComponent();
        Title = PlatformWindowTitles.RegionCaptureOverlay;
        InitializeThemeScope();
        DataContext = _viewModel;

        bool isWindows = OperatingSystem.IsWindows();
        bool isLinux = OperatingSystem.IsLinux();
        bool isAvaloniaWayland = MonitorEnumerationService.IsAvaloniaWaylandBackend();

        if (isWindows)
        {
            Win32Properties.SetIgnoreDpiChangeResize(this, true);
        }

        if (isLinux && WaylandProperties.IsWaylandBackend(this) && WaylandProperties.IsLayerShellSupported(this))
        {
            WaylandProperties.SetLayer(this, WaylandLayer.Overlay);
            WaylandProperties.SetLayerAnchor(this, WaylandLayerAnchor.All);
            WaylandProperties.SetLayerExclusiveZone(this, -1);
            WaylandProperties.SetLayerKeyboardInteractivity(this, WaylandLayerKeyboardInteractivity.Exclusive);
            WaylandProperties.SetLayerNamespace(this, "xerahs-region-capture");
            if (!string.IsNullOrWhiteSpace(monitor.DeviceName) &&
                !monitor.DeviceName.StartsWith("Display ", StringComparison.Ordinal))
            {
                WaylandProperties.SetLayerOutputName(this, monitor.DeviceName);
            }
        }

        var windowLayout = OverlayWindowLayoutCalculator.Calculate(monitor, isWindows, isLinux, isAvaloniaWayland);

        // Window origin and size do not share one coordinate space on every backend:
        // Windows, X11, and macOS use physical origin with logical size; native Wayland uses logical coordinates throughout.
        // Keep the calculated target layout so macOS can reapply it after the native NSWindow is created.
        _targetPosition = new AvPixelPoint((int)windowLayout.Position.X, (int)windowLayout.Position.Y);
        _targetWidth = windowLayout.Width;
        _targetHeight = windowLayout.Height;
        _hasTargetWindowLayout = true;
        ApplyTargetWindowLayout("constructor");
        ClearOwner();

        DebugHelper.WriteLine($"[OverlayWindow] {monitor.DeviceName}: isWindows={isWindows} isAvaloniaWayland={isAvaloniaWayland} Position=({(int)windowLayout.Position.X},{(int)windowLayout.Position.Y}) Width={windowLayout.Width:F1} Height={windowLayout.Height:F1} PhysicalBounds=({monitor.PhysicalBounds.X:F1},{monitor.PhysicalBounds.Y:F1},{monitor.PhysicalBounds.Width:F1},{monitor.PhysicalBounds.Height:F1}) OverlayBounds=({monitor.OverlayBounds.X:F1},{monitor.OverlayBounds.Y:F1},{monitor.OverlayBounds.Width:F1},{monitor.OverlayBounds.Height:F1})");

        // Create and add the capture control
        _captureControl = new RegionCaptureControl(_monitor, options, initialCursor);
        if (selectionChanged is not null)
            _captureControl.SelectionChanged += selectionChanged;
        _captureControl.RegionSelected += OnRegionSelected;
        _captureControl.Cancelled += OnCancelled;

        var panel = this.FindControl<Panel>("RootPanel")!;
        panel.Children.Add(_captureControl);

        // XIP-0023: Wire up annotation canvas events
        _annotationCanvas = this.FindControl<Canvas>("AnnotationCanvas");
        if (_annotationCanvas != null)
        {
            _annotationCanvas.PointerPressed += OnAnnotationCanvasPointerPressed;
            _annotationCanvas.PointerMoved += OnAnnotationCanvasPointerMoved;
            _annotationCanvas.PointerReleased += OnAnnotationCanvasPointerReleased;
            _annotationCanvas.PointerWheelChanged += OnAnnotationCanvasPointerWheelChanged;
        }

        // Subscribe to ActiveTool changes to toggle canvas hit testing
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _annotationToolCoordinator?.Register(_viewModel);

        // Ensure window can receive keyboard input
        Focusable = true;
        ApplySelectionCursorPolicy();

        WireUpToolbarEvents();
    }

    #endregion

    #region Window Lifecycle

    protected override void OnClosed(EventArgs e)
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
        _annotationToolCoordinator?.Unregister(_viewModel);
        _windowClosed = true;
        base.OnClosed(e);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (OperatingSystem.IsMacOS())
        {
            // Regression guard: Avalonia/macOS can clamp a borderless overlay to Screen.WorkingArea after Show(),
            // even when the constructor requested full Screen.Bounds. Reapply the target bounds, then sync
            // coordinate mapping to the actual NSWindow viewport if macOS still refuses the requested origin.
            ApplyTargetWindowLayout("OnOpened");
            SyncCaptureControlViewportToActualWindow("OnOpened");
            Dispatcher.UIThread.Post(() =>
            {
                if (_windowClosed)
                    return;

                ApplyTargetWindowLayout("OnOpened.Post");
                SyncCaptureControlViewportToActualWindow("OnOpened.Post");
                LogActualWindowGeometry("OnOpened.Post");
            }, DispatcherPriority.Send);
        }

        // Focus the capture control so it receives keyboard and pointer events
        this.Focus();
        _captureControl.Focus();
        // On Linux/Wayland the compositor often grants focus with delay; retry focus a few times so pointer events (crosshair) start sooner
        ScheduleDelayedFocusRetries();

        LogActualWindowGeometry("OnOpened");
    }

    /// <summary>
    /// Clears window ownership so Show() does not set X11 transient-for on a non-viewable MainWindow.
    /// Owner's setter is protected on WindowBase; expose clearing for OverlayManager.
    /// </summary>
    internal void ClearOwner() => Owner = null;

    private void ApplyTargetWindowLayout(string source)
    {
        if (!_hasTargetWindowLayout)
            return;

        Position = _targetPosition;
        Width = _targetWidth;
        Height = _targetHeight;

        DebugHelper.WriteLine($"[OverlayWindow.{source}] {_monitor.DeviceName}: Applied target Position={Position} Width={Width:F1} Height={Height:F1}");
    }

    private void SyncCaptureControlViewportToActualWindow(string source)
    {
        try
        {
            var topLeftPhysical = this.PointToScreen(new Avalonia.Point(0, 0));
            var bottomRightPhysical = this.PointToScreen(new Avalonia.Point(Width, Height));
            var viewport = new PixelRect(
                topLeftPhysical.X,
                topLeftPhysical.Y,
                Math.Max(1, bottomRightPhysical.X - topLeftPhysical.X),
                Math.Max(1, bottomRightPhysical.Y - topLeftPhysical.Y));

            _captureControl.SetPhysicalViewport(viewport, source);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[OverlayWindow.{source}] {_monitor.DeviceName}: Viewport sync failed: {ex.Message}");
        }
    }

    private void LogActualWindowGeometry(string source)
    {
        // Diagnostic: log actual window geometry after opening to verify physical pixel sizing
        try
        {
            var topLeftPhysical = this.PointToScreen(new Avalonia.Point(0, 0));
            var bottomRightPhysical = this.PointToScreen(new Avalonia.Point(Width, Height));
            int physicalWindowW = bottomRightPhysical.X - topLeftPhysical.X;
            int physicalWindowH = bottomRightPhysical.Y - topLeftPhysical.Y;

            // Screen info at window position
            var screenAtWindow = Screens?.ScreenFromPoint(Position);
            string screenInfo = screenAtWindow != null
                ? $"ScreenAt={screenAtWindow.Bounds.Width}x{screenAtWindow.Bounds.Height} Scale={screenAtWindow.Scaling:F4} IsPrimary={screenAtWindow.IsPrimary}"
                : "ScreenAt=null";

            DebugHelper.WriteLine($"[OverlayWindow.{source}] {_monitor.DeviceName}: Logical=({Width:F1}x{Height:F1}) Position={Position} PhysicalTopLeft=({topLeftPhysical.X},{topLeftPhysical.Y}) PhysicalSize=({physicalWindowW}x{physicalWindowH}) MonitorPhysical=({_monitor.PhysicalBounds.Width:F0}x{_monitor.PhysicalBounds.Height:F0}) {screenInfo}");
            DebugHelper.WriteLine($"[OverlayWindow.{source}] {_monitor.DeviceName}: FillsMonitor={physicalWindowW >= (int)_monitor.PhysicalBounds.Width && physicalWindowH >= (int)_monitor.PhysicalBounds.Height} (physW={physicalWindowW} >= monW={(int)_monitor.PhysicalBounds.Width}, physH={physicalWindowH} >= monH={(int)_monitor.PhysicalBounds.Height})");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[OverlayWindow.{source}] {_monitor.DeviceName}: Diagnostic failed: {ex.Message}");
        }
    }

    private async void ScheduleDelayedFocusRetries()
    {
        foreach (int delayMs in FocusRetryDelayMs)
        {
            await Task.Delay(delayMs);
            if (_windowClosed)
                return;
            Dispatcher.UIThread.Post(() =>
            {
                if (_windowClosed)
                    return;
                try
                {
                    this.Focus();
                    _captureControl.Focus();
                }
                catch
                {
                    // Window may be closing
                }
            }, DispatcherPriority.Input);
        }
    }

    #endregion

    #region Initialization

    private void InitializeComponent()
    {
        InitializeComponent(loadXaml: true);
        EnsureImageEditorResources();
    }

    private void EnsureImageEditorResources()
    {
        if (!Styles.OfType<StyleInclude>().Any(style => style.Source == ImageEditorStylesUri))
        {
            Styles.Add(new StyleInclude(OverlayWindowUri)
            {
                Source = ImageEditorStylesUri
            });
        }

        if (!Resources.MergedDictionaries.OfType<ResourceInclude>().Any(resource => resource.Source == ImageEditorThemeUri))
        {
            Resources.MergedDictionaries.Add(new ResourceInclude(OverlayWindowUri)
            {
                Source = ImageEditorThemeUri
            });
        }
    }

    private void InitializeThemeScope()
    {
        RequestedThemeVariant = ThemeManager.GetCurrentTheme();
        ThemeManager.ThemeChanged -= OnThemeChanged;
        ThemeManager.ThemeChanged += OnThemeChanged;
    }

    private void ApplySelectionCursorPolicy()
    {
        var hiddenCursor = new Cursor(StandardCursorType.None);
        Cursor = hiddenCursor;
        _captureControl.Cursor = hiddenCursor;

        var root = this.FindControl<Grid>("OverlayRoot");
        if (root != null)
        {
            root.Cursor = hiddenCursor;
        }

        var panel = this.FindControl<Panel>("RootPanel");
        if (panel != null)
        {
            panel.Cursor = hiddenCursor;
        }

        var annotationCanvas = _annotationCanvas ?? this.FindControl<Canvas>("AnnotationCanvas");
        if (annotationCanvas != null)
        {
            annotationCanvas.Cursor = new Cursor(StandardCursorType.Cross);
        }

        var toolbar = this.FindControl<Control>("AnnotationToolbarControl");
        if (toolbar != null)
        {
            toolbar.Cursor = new Cursor(StandardCursorType.Arrow);
        }
    }

    private void OnThemeChanged(object? sender, Avalonia.Styling.ThemeVariant theme)
    {
        Dispatcher.UIThread.Post(() => RequestedThemeVariant = theme);
    }

    #endregion

    #region Keyboard Input

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // If inline text editing is active, let the TextBox handle keys
        if (_inlineTextBox != null)
        {
            if (e.Key == Key.Escape)
            {
                CancelInlineText();
                e.Handled = true;
            }
            return;
        }

        // Track CTRL key for toggling between drawing and region selection
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            _ctrlPressed = true;
            if (UpdateAnnotationCanvasHitTesting())
            {
                _captureControl.InvalidateVisual();
            }
        }

        if (e.Key == Key.Escape)
        {
            OnCancelled();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            // XIP-0023: Toggle annotation toolbar visibility
            ToggleAnnotationToolbar();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (_captureControl.TryConfirmCurrentSelection() || _completionSource.Task.IsCompleted)
            {
                e.Handled = true;
                return;
            }

            // XIP-0023: ENTER confirms capture with annotations
            ConfirmCaptureWithAnnotations();
            e.Handled = true;
        }
        // Tool shortcuts (only when no modifiers)
        else if (e.KeyModifiers == KeyModifiers.None)
        {
            switch (e.Key)
            {
                case Key.V: _viewModel.SelectToolCommand.Execute(EditorTool.Select); e.Handled = true; break;
                case Key.R: _viewModel.SelectToolCommand.Execute(EditorTool.Rectangle); e.Handled = true; break;
                case Key.E: _viewModel.SelectToolCommand.Execute(EditorTool.Ellipse); e.Handled = true; break;
                case Key.L: _viewModel.SelectToolCommand.Execute(EditorTool.Line); e.Handled = true; break;
                case Key.A: _viewModel.SelectToolCommand.Execute(EditorTool.Arrow); e.Handled = true; break;
                case Key.F: _viewModel.SelectToolCommand.Execute(EditorTool.Freehand); e.Handled = true; break;
                case Key.T: _viewModel.SelectToolCommand.Execute(EditorTool.Text); e.Handled = true; break;
                case Key.N: _viewModel.SelectToolCommand.Execute(EditorTool.Step); e.Handled = true; break;
                case Key.H: _viewModel.SelectToolCommand.Execute(EditorTool.Highlight); e.Handled = true; break;
                case Key.W: _viewModel.SelectToolCommand.Execute(EditorTool.SmartEraser); e.Handled = true; break;
                case Key.B: _viewModel.SelectToolCommand.Execute(EditorTool.Blur); e.Handled = true; break;
                case Key.P: _viewModel.SelectToolCommand.Execute(EditorTool.Pixelate); e.Handled = true; break;
                case Key.M: _viewModel.SelectToolCommand.Execute(EditorTool.Magnify); e.Handled = true; break;
                case Key.S: _viewModel.SelectToolCommand.Execute(EditorTool.Spotlight); e.Handled = true; break;
            }
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Key == Key.Z)
            {
                _viewModel.UndoCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Y)
            {
                _viewModel.RedoCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        // Track CTRL key release
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            _ctrlPressed = false;
            if (UpdateAnnotationCanvasHitTesting())
            {
                _captureControl.InvalidateVisual();
            }
        }
    }

    #endregion
}

/// <summary>
/// Extension method to convert SKColor to Avalonia Color.
/// </summary>
internal static class SKColorExtensions
{
    public static Color ToAvalonia(this SKColor color)
    {
        return Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
    }
}
