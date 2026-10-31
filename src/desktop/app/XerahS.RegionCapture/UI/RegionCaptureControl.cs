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
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;
using XerahS.RegionCapture.Models;
using XerahS.RegionCapture.Services;
using AvPixelRect = Avalonia.PixelRect;
using AvPixelPoint = Avalonia.PixelPoint;
using PixelRect = XerahS.RegionCapture.Models.PixelRect;
using PixelPoint = XerahS.RegionCapture.Models.PixelPoint;

namespace XerahS.RegionCapture.UI;

/// <summary>
/// Custom control that handles region capture rendering and interaction.
/// Uses composited rendering with XOR-style selection cutout.
/// </summary>
public sealed class RegionCaptureControl : UserControl
{
    private readonly MonitorInfo _monitor;
    private readonly CoordinateTranslationService _coordinateService;
    private readonly WindowDetectionService _windowService;
    private readonly SelectionStateMachine _stateMachine;
    private readonly MagnifierControl _magnifier;
    private readonly Canvas _hudCanvas;
    private readonly bool _enableKeyboardNudge;
    private readonly RegionCaptureMode _mode;
    private PixelRect _physicalViewportBounds;
    private bool _enableMagnifier;
    private readonly bool _useSquareMagnifier;
    private readonly bool _showInfo;
    private int _magnifierPixelCount;

    // Rendering configuration
    private readonly double _dimOpacity;
    private readonly bool _dimAllExceptSelection;
    private readonly uint _crosshairColor;
    private readonly uint _crosshairLineColor;
    private readonly bool _showScreenCrosshair;
    private readonly bool _enableWindowSnapping;
    private readonly bool _useTransparentOverlay;
    private readonly bool _quickCrop;
    private readonly bool _useLightResizeNodes;
    private readonly DateTime? _sessionStartUtc;
    private readonly XerahS.Platform.Abstractions.CursorInfo? _ghostCursor;
    private readonly Bitmap? _ghostCursorBitmap;
    private readonly SkiaSharp.SKBitmap? _backgroundBitmap;
    private readonly Bitmap? _backgroundAvBitmap;
    private readonly WindowPreselectionCapability _windowPreselectionCapability;

    // Keyboard state tracking
    private SelectionModifier _activeModifiers = SelectionModifier.None;

    // Throttle crosshair redraws to ~60 FPS to reduce compositor load (Linux/mixed DPI)
    private static readonly long CrosshairInvalidateIntervalTicks = Math.Max(1, Stopwatch.Frequency / 60);
    private long _lastCrosshairInvalidateTicks;

    // Milestone: log first pointer move once (to diagnose delay until crosshair is responsive)
    private bool _firstPointerMovedLogged;

    // Visual brushes and pens (lazy initialization for performance)
    private IBrush? _dimBrush;
    private IBrush DimBrush => _dimBrush ??= new SolidColorBrush(Color.FromArgb((byte)(_dimOpacity * 255), 0, 0, 0));
    private IPen? _crosshairLinePen;
    private IPen? _crosshairPen;

    private static readonly IPen SelectionPen = new Pen(Brushes.White, 2);
    private static readonly IPen SelectionShadowPen = new Pen(new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)), 4);
    private static readonly IPen WindowSnapPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 0, 174, 255)), 3);
    private static readonly IPen WindowSnapShadowPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 0, 174, 255)), 6);
    private static readonly IBrush InfoBackgroundBrush = new SolidColorBrush(Color.FromArgb(220, 30, 30, 30));

    public event Action<RegionSelectionResult>? RegionSelected;
    public event Action<PixelRect>? SelectionChanged;
    public event Action? Cancelled;

    /// <summary>
    /// Indicates whether annotation mode is active (vs region selection mode).
    /// When true, drawing tools are active. When false (CTRL held), region selection is active.
    /// </summary>
    public bool IsAnnotationMode { get; set; }

    /// <summary>
    /// Indicates whether annotations have been drawn during this capture session.
    /// </summary>
    public bool HasAnnotations { get; set; }

    /// <summary>
    /// Indicates whether a region has been selected but not yet finalized (waiting for Enter).
    /// </summary>
    public bool HasPendingSelection { get; set; }

    // State machine accessors for rendering
    private CaptureState _state => _stateMachine.CurrentState;
    private PixelPoint _currentPoint => _stateMachine.CurrentPoint;
    private PixelRect _selectionRect => _stateMachine.SelectionRect;
    private WindowInfo? _hoveredWindow => _stateMachine.HoveredWindow;
    internal PixelPoint CurrentPointForTests => _currentPoint;

    internal void NudgeFromKeyboardForTests(int dx, int dy, bool shift)
    {
        int step = shift ? 10 : 1;
        if (_state == CaptureState.Hovering)
        {
            NudgePhysicalCursor(dx * step, dy * step);
            return;
        }

        _stateMachine.NudgeSelection(dx * step, dy * step);
        InvalidateVisual();
    }
    internal int MagnifierPixelCountForTests => _magnifierPixelCount;
    internal bool MagnifierUsesSquareForTests => _useSquareMagnifier;
    internal MagnifierControl MagnifierForTests => _magnifier;

    public RegionCaptureControl(MonitorInfo monitor, RegionCaptureOptions? options = null, XerahS.Platform.Abstractions.CursorInfo? ghostCursor = null)
    {
        options ??= new RegionCaptureOptions();

        _monitor = monitor;
        _physicalViewportBounds = monitor.PhysicalBounds;
        _ghostCursor = ghostCursor;
        _coordinateService = new CoordinateTranslationService();
        _windowService = new WindowDetectionService(options.DetectControls);

        // Initialize state machine
        _stateMachine = new SelectionStateMachine(
            options.QuickCrop,
            options.SnapSizes,
            options.SnapDistance);
        _stateMachine.SelectionConfirmed += OnSelectionConfirmed;
        _stateMachine.SelectionCancelled += OnSelectionCancelled;
        _stateMachine.StateChanged += _ => InvalidateVisual();
        _stateMachine.SelectionChanged += OnSelectionChanged;

        _dimOpacity = options.DimOpacity;
        _dimAllExceptSelection = options.DimAllExceptSelection;
        _mode = options.Mode;
        bool requestedWindowSnapping = options.EnableWindowSnapping && _mode != RegionCaptureMode.ScreenColorPicker;
        _windowPreselectionCapability = WindowDetectionService.GetWindowPreselectionCapability();
        _enableWindowSnapping = requestedWindowSnapping && _windowPreselectionCapability.IsEnabled;
        _enableMagnifier = options.EnableMagnifier;
        _useSquareMagnifier = options.UseSquareMagnifier;
        _showInfo = options.ShowInfo;
        _enableKeyboardNudge = options.EnableKeyboardNudge;
        _backgroundBitmap = options.BackgroundImage;
        _useTransparentOverlay = options.UseTransparentOverlay;
        _crosshairColor = options.CrosshairColor;
        _crosshairLineColor = options.CrosshairLineColor;
        _showScreenCrosshair = options.ShowScreenCrosshair;
        _quickCrop = options.QuickCrop;
        _useLightResizeNodes = options.UseLightResizeNodes;
        _sessionStartUtc = options.SessionStartUtc;

        // Convert background bitmap to Avalonia Bitmap for rendering when not transparent
        // PERFORMANCE: Use direct pixel copy instead of slow PNG encoding (~1-2s saved for 4K screens)
        if (!_useTransparentOverlay && _backgroundBitmap != null)
        {
            try
            {
                _backgroundAvBitmap = ConvertSkBitmapToAvalonia(_backgroundBitmap);
            }
            catch
            {
                _backgroundAvBitmap = null;
            }
        }

        _magnifierPixelCount = MagnifierLayout.NormalizePixelCount(
            options.MagnifierPixelCount,
            Math.Max(1, monitor.ScaleFactor));
        _magnifier = new MagnifierControl();
        _magnifier.ApplyShape(_useSquareMagnifier);
        _magnifier.SetAccentBrush(new SolidColorBrush(Color.FromUInt32(options.WindowSnapColor)));
        _magnifier.SetPixelCount(_magnifierPixelCount);
        _magnifier.SetInfoFormat(options.CustomInfoFormat);
        _magnifier.SetHudVisibility(_enableMagnifier, _showInfo);

        _hudCanvas = new Canvas { IsHitTestVisible = false };
        _hudCanvas.Children.Add(_magnifier);
        Content = _hudCanvas;

        Focusable = true;
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.None);

        // Fix for hit testing: Ensure the control has a background to capture mouse events
        // Use a near-transparent color (Alpha=1) instead of fully transparent to ensure
        // it works correctly with layered windows on Windows.
        Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));

        // Cache the ghost cursor Avalonia Bitmap once
        // PERFORMANCE: Use direct pixel copy instead of slow PNG encoding
        if (_ghostCursor?.Image != null)
        {
            try
            {
                _ghostCursorBitmap = ConvertSkBitmapToAvalonia(_ghostCursor.Image);
            }
            catch
            {
                _ghostCursorBitmap = null;
            }
        }

        LogWindowPreselectionCapability(requestedWindowSnapping);
    }

    /// <summary>
    /// Converts an SKBitmap to an Avalonia Bitmap using direct pixel copy.
    /// PERFORMANCE: This is ~50-100x faster than PNG encoding/decoding for large images.
    /// </summary>
    private static Bitmap ConvertSkBitmapToAvalonia(SKBitmap skBitmap)
    {
        // Ensure the SKBitmap is in BGRA8888 format for direct copy
        SKBitmap? convertedBitmap = null;
        SKBitmap sourceBitmap = skBitmap;

        if (skBitmap.ColorType != SKColorType.Bgra8888)
        {
            convertedBitmap = new SKBitmap(skBitmap.Width, skBitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(convertedBitmap);
            canvas.DrawBitmap(skBitmap, 0, 0, SKSamplingOptions.Default);
            sourceBitmap = convertedBitmap;
        }

        try
        {
            // Create WriteableBitmap with matching dimensions
            var writeableBitmap = new WriteableBitmap(
                new Avalonia.PixelSize(sourceBitmap.Width, sourceBitmap.Height),
                new Avalonia.Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                Avalonia.Platform.AlphaFormat.Premul);

            using (var frameBuffer = writeableBitmap.Lock())
            {
                var srcPtr = sourceBitmap.GetPixels();
                var dstPtr = frameBuffer.Address;
                var srcRowBytes = sourceBitmap.RowBytes;
                var dstRowBytes = frameBuffer.RowBytes;
                var height = sourceBitmap.Height;

                // Copy row by row to handle potential stride differences
                unsafe
                {
                    for (int y = 0; y < height; y++)
                    {
                        var srcRow = IntPtr.Add(srcPtr, y * srcRowBytes);
                        var dstRow = IntPtr.Add(dstPtr, y * dstRowBytes);
                        Buffer.MemoryCopy((void*)srcRow, (void*)dstRow, dstRowBytes, Math.Min(srcRowBytes, dstRowBytes));
                    }
                }
            }

            return writeableBitmap;
        }
        finally
        {
            convertedBitmap?.Dispose();
        }
    }

    public RegionCaptureControl(MonitorInfo monitor) : this(monitor, null, null)
    {
    }

    public void SetPhysicalViewport(PixelRect physicalViewportBounds, string source)
    {
        if (physicalViewportBounds.IsEmpty)
            return;

        _physicalViewportBounds = physicalViewportBounds;
        XerahS.Common.DebugHelper.WriteLine($"[RegionCaptureControl.{source}] {_monitor.DeviceName}: PhysicalViewport=({_physicalViewportBounds.X:F0},{_physicalViewportBounds.Y:F0},{_physicalViewportBounds.Width:F0}x{_physicalViewportBounds.Height:F0}) MonitorPhysical=({_monitor.PhysicalBounds.X:F0},{_monitor.PhysicalBounds.Y:F0},{_monitor.PhysicalBounds.Width:F0}x{_monitor.PhysicalBounds.Height:F0})");
        InvalidateVisual();
    }

    public bool TryConfirmCurrentSelection() => _stateMachine.TryConfirm();

    private double GetPhysicalHandleSize()
    {
        double logical = _useLightResizeNodes ? 6.0 : 8.0;
        return Math.Max(8, logical * Math.Max(1, _monitor.ScaleFactor));
    }

    private void OnSelectionConfirmed(RegionSelectionResult result)
    {
        if (_sessionStartUtc is { } start)
        {
            double elapsedMs = (DateTime.UtcNow - start).TotalMilliseconds;
            XerahS.Common.DebugHelper.WriteLine($"[RegionCapture] Milestone: selection confirmed (+{elapsedMs:F0} ms)");
        }
        RegionSelected?.Invoke(result);
    }
    private void OnSelectionChanged(PixelRect rect) => SelectionChanged?.Invoke(rect);
    private void OnSelectionCancelled() => Cancelled?.Invoke();

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        UpdateModifiers(e.KeyModifiers);

        var point = e.GetPosition(this);
        var physicalPoint = LocalToPhysical(point);

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if (_sessionStartUtc is { } start)
            {
                double elapsedMs = (DateTime.UtcNow - start).TotalMilliseconds;
                XerahS.Common.DebugHelper.WriteLine($"[RegionCapture] Milestone: first mouse down (+{elapsedMs:F0} ms)");
            }
            _lastCrosshairInvalidateTicks = 0; // Allow immediate redraw on drag start
            if (_mode == RegionCaptureMode.ScreenColorPicker)
            {
                _stateMachine.ConfirmPoint(physicalPoint);
                e.Handled = true;
                return;
            }

            if (_state == CaptureState.Selected && !_selectionRect.IsEmpty)
            {
                var handle = SelectionSnapHelper.HitTest(_selectionRect, physicalPoint, GetPhysicalHandleSize());
                if (handle is not SelectionHandle.None and not SelectionHandle.Body)
                {
                    _stateMachine.BeginResize(handle, physicalPoint);
                    e.Pointer.Capture(this);
                    InvalidateVisual();
                    return;
                }

                if (handle == SelectionHandle.Body)
                {
                    _stateMachine.BeginMove(physicalPoint);
                    e.Pointer.Capture(this);
                    InvalidateVisual();
                    return;
                }
            }

            // Always start dragging/interaction
            // If the user releases immediately (click), EndDrag will handle snapping to the hovered window.
            _stateMachine.BeginDrag(physicalPoint);
            e.Pointer.Capture(this);

            InvalidateVisual();
        }
        else if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            _stateMachine.Cancel();
        }
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_sessionStartUtc is { } start)
        {
            double elapsedMs = (DateTime.UtcNow - start).TotalMilliseconds;
            XerahS.Common.DebugHelper.WriteLine($"[RegionCapture] Milestone: overlay control attached to visual tree (+{elapsedMs:F0} ms)");
        }
        UpdateMagnifierHud();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        UpdateAimFromOverlayPointer(e.GetPosition(this), e.KeyModifiers);
    }

    public void UpdateAimFromOverlayPointer(Point localPoint, KeyModifiers keyModifiers)
    {
        UpdateModifiers(keyModifiers);

        if (!_firstPointerMovedLogged && _sessionStartUtc is { } start)
        {
            _firstPointerMovedLogged = true;
            double elapsedMs = (DateTime.UtcNow - start).TotalMilliseconds;
            XerahS.Common.DebugHelper.WriteLine($"[RegionCapture] Milestone: first pointer moved (+{elapsedMs:F0} ms)");
        }

        var physicalPoint = LocalToPhysical(localPoint);

        _stateMachine.UpdateCursorPosition(physicalPoint);

        if (_state == CaptureState.Hovering && _enableWindowSnapping)
        {
            var window = _windowService.GetWindowAtPoint(physicalPoint);
            _stateMachine.UpdateHoveredWindow(window);
        }

        UpdateMagnifierHud();

        // Throttle redraws to ~60 FPS to avoid sluggish crosshair on Linux (Avalonia #19363, compositor load)
        long now = Stopwatch.GetTimestamp();
        if (_lastCrosshairInvalidateTicks == 0 || (now - _lastCrosshairInvalidateTicks) >= CrosshairInvalidateIntervalTicks)
        {
            _lastCrosshairInvalidateTicks = now;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        UpdateModifiers(e.KeyModifiers);

        if (_state == CaptureState.Dragging && _mode != RegionCaptureMode.ScreenColorPicker)
        {
            if (_sessionStartUtc is { } start)
            {
                double elapsedMs = (DateTime.UtcNow - start).TotalMilliseconds;
                XerahS.Common.DebugHelper.WriteLine($"[RegionCapture] Milestone: mouse up (+{elapsedMs:F0} ms)");
            }
            var point = e.GetPosition(this);
            var physicalPoint = LocalToPhysical(point);
            _stateMachine.UpdateCursorPosition(physicalPoint);
            e.Pointer.Capture(null);
            _stateMachine.EndDrag();
            InvalidateVisual();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Update modifiers
        UpdateModifiers(e.KeyModifiers);

        switch (e.Key)
        {
            case Key.Escape:
                _stateMachine.Cancel();
                e.Handled = true;
                break;

            case Key.Enter:
                if (TryConfirmCurrentSelection())
                {
                    e.Handled = true;
                }
                break;

            // Arrow key nudging
            case Key.Left when _enableKeyboardNudge:
                HandleArrowKey(-1, 0, e);
                break;

            case Key.Right when _enableKeyboardNudge:
                HandleArrowKey(1, 0, e);
                break;

            case Key.Up when _enableKeyboardNudge:
                HandleArrowKey(0, -1, e);
                break;

            case Key.Down when _enableKeyboardNudge:
                HandleArrowKey(0, 1, e);
                break;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        UpdateModifiers(e.KeyModifiers);
    }

    private void UpdateModifiers(KeyModifiers keyModifiers)
    {
        var modifiers = SelectionModifier.None;

        if (keyModifiers.HasFlag(KeyModifiers.Shift))
            modifiers |= SelectionModifier.LockAspectRatio;

        if (keyModifiers.HasFlag(KeyModifiers.Control))
            modifiers |= SelectionModifier.PixelNudge;

        if (keyModifiers.HasFlag(KeyModifiers.Alt))
            modifiers |= SelectionModifier.FromCenter;

        if (_activeModifiers != modifiers)
        {
            _activeModifiers = modifiers;
            _stateMachine.SetModifiers(modifiers);
            InvalidateVisual();
        }
    }

    private void HandleArrowKey(int dx, int dy, KeyEventArgs e)
    {
        int step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;

        if (_state == CaptureState.Hovering)
        {
            NudgePhysicalCursor(dx * step, dy * step);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _stateMachine.ResizeSelection(dx * step, dy * step);
        }
        else
        {
            _stateMachine.NudgeSelection(dx * step, dy * step);
        }

        e.Handled = true;
        InvalidateVisual();
    }

    private void NudgePhysicalCursor(int dx, int dy)
    {
        var next = new PixelPoint(_currentPoint.X + dx, _currentPoint.Y + dy);
        _coordinateService.SetPhysicalCursorPosition(next);
        _stateMachine.UpdateCursorPosition(next);

        if (_state == CaptureState.Hovering && _enableWindowSnapping)
        {
            var window = _windowService.GetWindowAtPoint(next);
            _stateMachine.UpdateHoveredWindow(window);
        }

        UpdateMagnifierHud();
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (TryAdjustMagnifierFromWheel(e.Delta.Y))
        {
            e.Handled = true;
        }
    }

    public bool TryAdjustMagnifierFromWheel(double deltaY)
    {
        double scale = GetMagnifierRenderScale();
        int next = MagnifierLayout.PixelCountFromWheel(_magnifierPixelCount, deltaY, scale);
        _enableMagnifier = true;
        _magnifierPixelCount = next;
        _magnifier.SetPixelCount(next);
        _magnifier.SetHudVisibility(true, _showInfo);
        UpdateMagnifierHud();
        return true;
    }

    private void UpdateMagnifierHud()
    {
        if (!_enableMagnifier && !_showInfo)
        {
            _magnifier.IsVisible = false;
            return;
        }

        var cursorLocal = PhysicalToLocal(_currentPoint);
        var virtualBounds = _coordinateService.GetVirtualScreenBounds();
        _magnifier.SetHudVisibility(_enableMagnifier, _showInfo);
        _magnifier.UpdateFromBackground(_currentPoint, _backgroundBitmap, virtualBounds);
        _magnifier.PositionNearPointer(cursorLocal, Bounds.Size, GetMagnifierRenderScale());
    }

    private double GetMagnifierRenderScale()
    {
        double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? _monitor.ScaleFactor;
        return double.IsFinite(scale) && scale > 0 ? scale : 1;
    }

    private PixelPoint LocalToPhysical(Point local)
    {
        // Convert from control-local logical coordinates to physical screen coordinates.
        // macOS can force a borderless overlay to start at the menu-bar working-area origin
        // even when the monitor starts at y=0, so use the actual window viewport origin here.
        return new PixelPoint(
            local.X * _monitor.ScaleFactor + _physicalViewportBounds.X,
            local.Y * _monitor.ScaleFactor + _physicalViewportBounds.Y);
    }

    private Point PhysicalToLocal(PixelPoint physical)
    {
        // Convert from physical screen coordinates to control-local logical coordinates.
        // See LocalToPhysical: this must use the same actual viewport origin.
        return new Point(
            (physical.X - _physicalViewportBounds.X) / _monitor.ScaleFactor,
            (physical.Y - _physicalViewportBounds.Y) / _monitor.ScaleFactor);
    }

    private Rect PhysicalRectToLocal(PixelRect rect)
    {
        var topLeft = PhysicalToLocal(rect.TopLeft);
        var bottomRight = PhysicalToLocal(rect.BottomRight);
        return new Rect(topLeft, bottomRight);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);

        // Draw frozen background when not in transparent mode
        if (!_useTransparentOverlay && _backgroundAvBitmap != null)
        {
            DrawFrozenBackground(context, bounds);
        }

        Rect? clearRect = null;

        // Determine the clear rect (selection or window snap area)
        if (_state == CaptureState.Dragging || _state == CaptureState.Selected)
        {
            if (!_selectionRect.IsEmpty)
            {
                clearRect = PhysicalRectToLocal(_selectionRect);
            }
        }
        else if (_state == CaptureState.Hovering && _hoveredWindow is not null)
        {
            clearRect = PhysicalRectToLocal(_hoveredWindow.SnapBounds);
        }

        // Hover outlines remain visible even when their interior stays dimmed.
        bool hasHighlight = clearRect is { Width: > 0, Height: > 0 };
        bool undimHighlight = hasHighlight && (!_dimAllExceptSelection || _state != CaptureState.Hovering);
        if (undimHighlight && clearRect is { } cutout)
        {
            DrawDimmedOutside(context, bounds, cutout);
        }
        else
        {
            context.DrawRectangle(DimBrush, null, bounds);
        }

        if (hasHighlight && clearRect is { } rect)
        {
            // Draw the selection/snap border with shadow effect
            if (_state == CaptureState.Dragging || _state == CaptureState.Selected)
            {
                // Shadow first, then border
                context.DrawRectangle(null, SelectionShadowPen, rect);
                context.DrawRectangle(null, SelectionPen, rect);

                // Draw resize handles at corners
                DrawResizeHandles(context, rect);
                DrawSelectionCenterCrosshair(context, rect);

                // Draw mode-specific overlays
                if (_mode == RegionCaptureMode.Ruler)
                {
                    // Fill selection with semi-transparent white
                    var rulerFillBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                    context.DrawRectangle(rulerFillBrush, null, rect);

                    // Draw ruler ticks and measurements
                    DrawRulerTicks(context, rect);
                    DrawRulerMeasurements(context, rect);
                }
                else
                {
                    // Standard dimensions text for other modes
                    DrawDimensionsText(context, rect);
                }
            }
            else if (_hoveredWindow is not null)
            {
                // Window snap highlight
                context.DrawRectangle(null, WindowSnapShadowPen, rect);
                context.DrawRectangle(null, WindowSnapPen, rect);

                // Draw window title
                DrawWindowTitle(context, rect, _hoveredWindow.DisplayTitle);
            }
        }
        // Draw crosshair at cursor position
        DrawCrosshair(context, bounds);

        // Draw modifier hints (bottom-right)
        DrawModifierHints(context);

        // Draw instructions (top-center, only in hover state)
        DrawInstructions(context);

        // Draw ghost cursor if available and configured
        DrawGhostCursor(context);
    }

    private void DrawDimmedOutside(DrawingContext context, Rect bounds, Rect rect)
    {
        // Draw dimmed background using 4 rectangles to avoid expensive geometry operations
        // This is significantly faster than CombinedGeometry

        // Top
        if (rect.Top > 0)
            context.DrawRectangle(DimBrush, null, new Rect(0, 0, bounds.Width, rect.Top));

        // Bottom
        if (rect.Bottom < bounds.Height)
            context.DrawRectangle(DimBrush, null, new Rect(0, rect.Bottom, bounds.Width, bounds.Height - rect.Bottom));

        // Left (clamped between Top and Bottom)
        if (rect.Left > 0)
            context.DrawRectangle(DimBrush, null, new Rect(0, rect.Top, rect.Left, rect.Height));

        // Right (clamped between Top and Bottom)
        if (rect.Right < bounds.Width)
            context.DrawRectangle(DimBrush, null, new Rect(rect.Right, rect.Top, bounds.Width - rect.Right, rect.Height));

    }

    private void DrawGhostCursor(DrawingContext context)
    {
        if (_ghostCursorBitmap == null || _ghostCursor == null) return;

        // Convert physical position to local logical coordinates
        var cursorPhysicalPos = new PixelPoint(_ghostCursor.Position.X, _ghostCursor.Position.Y);
        var cursorLogicalPos = PhysicalToLocal(cursorPhysicalPos);

        // Calculate draw position (offset by hotspot)
        double scale = 1.0 / _monitor.ScaleFactor;
        var drawPos = new Point(
            cursorLogicalPos.X - (_ghostCursor.Hotspot.X * scale),
            cursorLogicalPos.Y - (_ghostCursor.Hotspot.Y * scale));

        try
        {
            // Draw the cached cursor bitmap
            var size = new Size(_ghostCursorBitmap.Size.Width * scale, _ghostCursorBitmap.Size.Height * scale);
            context.DrawImage(_ghostCursorBitmap, new Rect(drawPos, size));
        }
        catch
        {
            // Ignore drawing errors for ghost cursor
        }
    }

    private void DrawFrozenBackground(DrawingContext context, Rect bounds)
    {
        if (_backgroundAvBitmap == null) return;

        // Calculate the portion of the background bitmap that corresponds to the actual
        // overlay viewport. On macOS this can differ from the monitor origin after NSWindow
        // creation, and drawing from monitor origin would visibly shift the frozen desktop.
        var virtualBounds = _coordinateService.GetVirtualScreenBounds();
        var visibleViewport = _physicalViewportBounds.Intersect(_monitor.PhysicalBounds);
        if (visibleViewport.IsEmpty)
        {
            visibleViewport = _physicalViewportBounds;
        }

        var srcX = visibleViewport.X - virtualBounds.X;
        var srcY = visibleViewport.Y - virtualBounds.Y;

        // Source rect in the full screenshot (physical pixels)
        var sourceRect = new Rect(
            srcX,
            srcY,
            visibleViewport.Width,
            visibleViewport.Height);

        var destinationRect = new Rect(
            (visibleViewport.X - _physicalViewportBounds.X) / _monitor.ScaleFactor,
            (visibleViewport.Y - _physicalViewportBounds.Y) / _monitor.ScaleFactor,
            visibleViewport.Width / _monitor.ScaleFactor,
            visibleViewport.Height / _monitor.ScaleFactor);

        context.DrawImage(_backgroundAvBitmap, sourceRect, destinationRect);
    }

    private void DrawResizeHandles(DrawingContext context, Rect rect)
    {
        // Use lighter/smaller handles in ruler mode to reduce visual clutter
        var handleSize = _useLightResizeNodes ? 6.0 : 8.0;
        IBrush handleBrush = _useLightResizeNodes
            ? new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))  // Semi-transparent white
            : Brushes.White;
        IPen handlePen = _useLightResizeNodes
            ? new Pen(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), 1)  // Lighter border
            : new Pen(Brushes.Black, 1);

        var handles = new[]
        {
            new Point(rect.Left, rect.Top),
            new Point(rect.Right, rect.Top),
            new Point(rect.Left, rect.Bottom),
            new Point(rect.Right, rect.Bottom),
            new Point(rect.Left + rect.Width / 2, rect.Top),
            new Point(rect.Left + rect.Width / 2, rect.Bottom),
            new Point(rect.Left, rect.Top + rect.Height / 2),
            new Point(rect.Right, rect.Top + rect.Height / 2)
        };

        foreach (var handle in handles)
        {
            var handleRect = new Rect(
                handle.X - handleSize / 2,
                handle.Y - handleSize / 2,
                handleSize,
                handleSize);

            context.DrawRectangle(handleBrush, handlePen, handleRect);
        }
    }

    private void DrawCrosshair(DrawingContext context, Rect bounds)
    {
        var cursorLocal = PhysicalToLocal(_currentPoint);

        // Only draw if cursor is within bounds
        if (!bounds.Contains(cursorLocal))
            return;

        // Cache pens to avoid per-frame allocations (Avalonia high-frequency rendering; issue #19363)
        _crosshairLinePen ??= new Pen(new SolidColorBrush(Color.FromUInt32(_crosshairLineColor)), 1);
        _crosshairPen ??= new Pen(new SolidColorBrush(Color.FromUInt32(_crosshairColor)), 1);

        int centerX = (int)Math.Floor(cursorLocal.X);
        int centerY = (int)Math.Floor(cursorLocal.Y);
        const double crosshairLength = 32;

        context.DrawLine(_crosshairPen,
            new Point(centerX, Math.Max(0, centerY - crosshairLength)),
            new Point(centerX, Math.Min(bounds.Height, centerY + crosshairLength)));
        context.DrawLine(_crosshairPen,
            new Point(Math.Max(0, centerX - crosshairLength), centerY),
            new Point(Math.Min(bounds.Width, centerX + crosshairLength), centerY));

        if (!_showScreenCrosshair)
        {
            return;
        }

        context.DrawLine(_crosshairLinePen,
            new Point(centerX, 0),
            new Point(centerX, Math.Max(0, centerY - crosshairLength)));
        context.DrawLine(_crosshairLinePen,
            new Point(centerX, Math.Min(bounds.Height, centerY + crosshairLength)),
            new Point(centerX, bounds.Height));
        context.DrawLine(_crosshairLinePen,
            new Point(0, centerY),
            new Point(Math.Max(0, centerX - crosshairLength), centerY));
        context.DrawLine(_crosshairLinePen,
            new Point(Math.Min(bounds.Width, centerX + crosshairLength), centerY),
            new Point(bounds.Width, centerY));
    }

    private static void DrawSelectionCenterCrosshair(DrawingContext context, Rect rectangle)
    {
        if (rectangle.Width < 2 || rectangle.Height < 2)
        {
            return;
        }

        int centerX = (int)Math.Floor(rectangle.Center.X);
        int centerY = (int)Math.Floor(rectangle.Center.Y);
        DrawPixelCross(context, Brushes.Black, centerX - 1, centerY - 1);
        DrawPixelCross(context, Brushes.White, centerX, centerY);
    }

    private static void DrawPixelCross(DrawingContext context, IBrush brush, int centerX, int centerY)
    {
        const int radius = 10;
        const int diameter = radius * 2 + 1;
        context.DrawRectangle(brush, null, new Rect(centerX - radius, centerY, diameter, 1));
        context.DrawRectangle(brush, null, new Rect(centerX, centerY - radius, 1, diameter));
    }

    private void DrawDimensionsText(DrawingContext context, Rect rect)
    {
        var text = $"{_selectionRect.Width:F0} x {_selectionRect.Height:F0}";

        var formattedText = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            14,
            Brushes.White);

        var textX = rect.X + (rect.Width - formattedText.Width) / 2;
        var textY = rect.Bottom + 8;

        // Ensure text stays on screen
        if (textY + formattedText.Height > Bounds.Height - 10)
            textY = rect.Top - formattedText.Height - 8;

        // Clamp to horizontal bounds
        textX = Math.Max(8, Math.Min(Bounds.Width - formattedText.Width - 8, textX));

        // Draw text background with rounded corners
        var textBounds = new Rect(textX - 8, textY - 4,
            formattedText.Width + 16, formattedText.Height + 8);
        context.DrawRectangle(InfoBackgroundBrush, null, textBounds, 4, 4);

        // Draw text
        context.DrawText(formattedText, new Point(textX, textY));
    }

    private void DrawRulerTicks(DrawingContext context, Rect rect)
    {
        // Draw ruler ticks on all four edges
        var rulerPen = new Pen(Brushes.White, 1);
        var smallTickSize = 5;
        var largeTickSize = 15;
        var smallTickInterval = 10;
        var largeTickInterval = 100;

        // Horizontal ticks (top and bottom edges)
        for (double x = 0; x <= rect.Width; x += smallTickInterval)
        {
            var isLargeTick = (x % largeTickInterval) == 0;
            var tickSize = isLargeTick ? largeTickSize : smallTickSize;

            // Top edge ticks
            context.DrawLine(rulerPen,
                new Point(rect.Left + x, rect.Top),
                new Point(rect.Left + x, rect.Top + tickSize));

            // Bottom edge ticks
            context.DrawLine(rulerPen,
                new Point(rect.Left + x, rect.Bottom),
                new Point(rect.Left + x, rect.Bottom - tickSize));
        }

        // Vertical ticks (left and right edges)
        for (double y = 0; y <= rect.Height; y += smallTickInterval)
        {
            var isLargeTick = (y % largeTickInterval) == 0;
            var tickSize = isLargeTick ? largeTickSize : smallTickSize;

            // Left edge ticks
            context.DrawLine(rulerPen,
                new Point(rect.Left, rect.Top + y),
                new Point(rect.Left + tickSize, rect.Top + y));

            // Right edge ticks
            context.DrawLine(rulerPen,
                new Point(rect.Right, rect.Top + y),
                new Point(rect.Right - tickSize, rect.Top + y));
        }

        // Draw crosshair at center
        var centerX = rect.Left + rect.Width / 2;
        var centerY = rect.Top + rect.Height / 2;
        var crosshairSize = 10;

        var centerPen = new Pen(Brushes.White, 2);
        var centerShadowPen = new Pen(Brushes.Black, 3);

        // Shadow
        context.DrawLine(centerShadowPen,
            new Point(centerX - crosshairSize, centerY),
            new Point(centerX + crosshairSize, centerY));
        context.DrawLine(centerShadowPen,
            new Point(centerX, centerY - crosshairSize),
            new Point(centerX, centerY + crosshairSize));

        // Crosshair
        context.DrawLine(centerPen,
            new Point(centerX - crosshairSize, centerY),
            new Point(centerX + crosshairSize, centerY));
        context.DrawLine(centerPen,
            new Point(centerX, centerY - crosshairSize),
            new Point(centerX, centerY + crosshairSize));
    }

    private void DrawRulerMeasurements(DrawingContext context, Rect rect)
    {
        var width = _selectionRect.Width;
        var height = _selectionRect.Height;
        var topLeftX = _selectionRect.X;
        var topLeftY = _selectionRect.Y;
        var bottomRightX = topLeftX + width;
        var bottomRightY = topLeftY + height;

        // Calculate diagonal distance and angle
        var distance = Math.Sqrt(width * width + height * height);
        var angle = Math.Atan2(height, width) * (180.0 / Math.PI);
        var area = width * height;
        var perimeter = 2 * (width + height);

        // Build measurement text
        var measurements = $"X: {topLeftX:F0}, Y: {topLeftY:F0} | Right: {bottomRightX:F0}, Bottom: {bottomRightY:F0}\n" +
                          $"Width: {width:F0} px | Height: {height:F0} px\n" +
                          $"Area: {area:F0} px² | Perimeter: {perimeter:F0} px\n" +
                          $"Distance: {distance:F2} px | Angle: {angle:F2}°";

        var formattedText = new FormattedText(
            measurements,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal),
            12,
            Brushes.White);

        // Position text below the selection (or above if not enough space)
        var textX = rect.X + (rect.Width - formattedText.Width) / 2;
        var textY = rect.Bottom + 12;

        if (textY + formattedText.Height > Bounds.Height - 10)
            textY = rect.Top - formattedText.Height - 12;

        textX = Math.Max(8, Math.Min(Bounds.Width - formattedText.Width - 8, textX));

        // Draw text background
        var textBounds = new Rect(textX - 12, textY - 6,
            formattedText.Width + 24, formattedText.Height + 12);
        context.DrawRectangle(InfoBackgroundBrush, null, textBounds, 6, 6);

        // Draw text
        context.DrawText(formattedText, new Point(textX, textY));
    }

    private void DrawWindowTitle(DrawingContext context, Rect rect, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        // Truncate long titles
        if (title.Length > 50)
            title = string.Concat(title.AsSpan(0, 47), "...");

        var formattedText = new FormattedText(
            title,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal),
            12,
            Brushes.White);

        var textX = rect.X + (rect.Width - formattedText.Width) / 2;
        var textY = rect.Top - formattedText.Height - 8;

        // Ensure text stays on screen
        if (textY < 10)
            textY = rect.Bottom + 8;

        // Clamp to horizontal bounds
        textX = Math.Max(8, Math.Min(Bounds.Width - formattedText.Width - 8, textX));

        // Draw text background
        var textBounds = new Rect(textX - 8, textY - 4,
            formattedText.Width + 16, formattedText.Height + 8);
        context.DrawRectangle(InfoBackgroundBrush, null, textBounds, 4, 4);

        // Draw text
        context.DrawText(formattedText, new Point(textX, textY));
    }

    private void DrawModifierHints(DrawingContext context)
    {
        var hints = new List<string>();

        if (_activeModifiers.HasFlag(SelectionModifier.LockAspectRatio))
            hints.Add("Shift: Lock aspect ratio");

        if (_activeModifiers.HasFlag(SelectionModifier.FromCenter))
            hints.Add("Alt: Expand from center");

        if (_activeModifiers.HasFlag(SelectionModifier.PixelNudge))
            hints.Add(_state == CaptureState.Dragging ? "Ctrl: Move selection" : "Ctrl: Resize mode");

        if (hints.Count == 0)
            return;

        var hintText = string.Join(" | ", hints);
        var formattedHint = new FormattedText(
            hintText,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal),
            11,
            new SolidColorBrush(Color.FromRgb(200, 200, 200)));

        var x = Bounds.Width - formattedHint.Width - 16;
        var y = Bounds.Height - formattedHint.Height - 12;

        var bgRect = new Rect(x - 8, y - 4, formattedHint.Width + 16, formattedHint.Height + 8);
        context.DrawRectangle(InfoBackgroundBrush, null, bgRect, 4, 4);
        context.DrawText(formattedHint, new Point(x, y));
    }

    private static readonly IBrush AnnotateModeBrush = new SolidColorBrush(Color.FromArgb(220, 255, 140, 0)); // Orange
    private static readonly IBrush RegionModeBrush = new SolidColorBrush(Color.FromArgb(220, 0, 174, 255));   // Blue
    private static readonly IBrush ReminderBrush = new SolidColorBrush(Color.FromArgb(220, 50, 205, 50));     // Green
    private static readonly IBrush CapabilityBrush = new SolidColorBrush(Color.FromArgb(220, 216, 184, 116));

    private void LogWindowPreselectionCapability(bool requestedWindowSnapping)
    {
        if (!requestedWindowSnapping || !OperatingSystem.IsLinux())
            return;

        switch (_windowPreselectionCapability.Level)
        {
            case WindowPreselectionSupportLevel.Partial:
                XerahS.Common.DebugHelper.WriteLine(
                    $"[RegionCapture] Window preselection is limited on this Linux session. {_windowPreselectionCapability.UserMessage}");
                break;
            case WindowPreselectionSupportLevel.Unsupported:
                XerahS.Common.DebugHelper.WriteLine(
                    $"[RegionCapture] Window preselection is unavailable on this Linux session. {_windowPreselectionCapability.UserMessage}");
                break;
        }
    }

    private string GetInstructionText()
    {
        if (_mode == RegionCaptureMode.ScreenColorPicker)
        {
            return "Click to pick a color | Esc to cancel";
        }

        if (_mode == RegionCaptureMode.Ruler)
        {
            return "Drag to measure distance and area | Arrow keys: adjust | Enter: finish | Esc: cancel";
        }

        string confirmHint = _quickCrop
            ? "Enter: finish | Esc: cancel"
            : "resize handles | Enter: confirm | Esc: cancel";

        if (_enableWindowSnapping)
        {
            return _windowPreselectionCapability.Level == WindowPreselectionSupportLevel.Partial
                ? $"Drag to select region | Click to snap supported windows | Ctrl: move while dragging | {confirmHint}"
                : $"Drag to select region | Click to snap window | Ctrl: move while dragging | {confirmHint}";
        }

        return $"Drag to select region | Ctrl: move while dragging | {confirmHint}";
    }

    private string? GetCapabilityMessage()
    {
        if (_mode == RegionCaptureMode.ScreenColorPicker || _mode == RegionCaptureMode.Ruler)
            return null;

        return _windowPreselectionCapability.Level is WindowPreselectionSupportLevel.Partial or WindowPreselectionSupportLevel.Unsupported
            ? _windowPreselectionCapability.UserMessage
            : null;
    }

    private void DrawInstructions(DrawingContext context)
    {
        var yOffset = 12.0;

        // Only show mode indicator and instructions when hovering (not during selection)
        if (_state == CaptureState.Hovering)
        {
            // Draw mode indicator pill
            if (_mode != RegionCaptureMode.ScreenColorPicker)
            {
                var modeText = IsAnnotationMode ? "Annotate Mode" : "Region Capture Mode";
                var modeBrush = IsAnnotationMode ? AnnotateModeBrush : RegionModeBrush;

                var modeFormatted = new FormattedText(
                    modeText,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
                    13,
                    Brushes.White);

                var modeX = (Bounds.Width - modeFormatted.Width) / 2;
                var modeBgRect = new Rect(modeX - 14, yOffset - 4, modeFormatted.Width + 28, modeFormatted.Height + 8);
                context.DrawRectangle(modeBrush, null, modeBgRect, 12, 12);
                context.DrawText(modeFormatted, new Point(modeX, yOffset));

                yOffset += modeFormatted.Height + 16;
            }

            // Draw instructions
            var instructions = GetInstructionText();
            var formatted = new FormattedText(
                instructions,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal),
                12,
                new SolidColorBrush(Color.FromRgb(180, 180, 180)));

            var x = (Bounds.Width - formatted.Width) / 2;

            var bgRect = new Rect(x - 12, yOffset - 4, formatted.Width + 24, formatted.Height + 8);
            context.DrawRectangle(InfoBackgroundBrush, null, bgRect, 4, 4);
            context.DrawText(formatted, new Point(x, yOffset));

            yOffset += formatted.Height + 16;

            if (GetCapabilityMessage() is { } capabilityMessage)
            {
                var noteFormatted = new FormattedText(
                    capabilityMessage,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal),
                    11,
                    CapabilityBrush);

                var noteX = (Bounds.Width - noteFormatted.Width) / 2;
                var noteRect = new Rect(noteX - 12, yOffset - 4, noteFormatted.Width + 24, noteFormatted.Height + 8);
                context.DrawRectangle(InfoBackgroundBrush, null, noteRect, 4, 4);
                context.DrawText(noteFormatted, new Point(noteX, yOffset));

                yOffset += noteFormatted.Height + 16;
            }
        }

        // Draw "Press Enter to finish" reminder when annotations exist and region is selected
        // This is shown regardless of state so user always sees it after selecting a region
        if (HasAnnotations && HasPendingSelection)
        {
            var reminderFormatted = new FormattedText(
                "Press Enter to finish capture",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
                14,
                Brushes.White);

            var reminderX = (Bounds.Width - reminderFormatted.Width) / 2;
            var reminderBgRect = new Rect(reminderX - 14, yOffset - 4, reminderFormatted.Width + 28, reminderFormatted.Height + 8);
            context.DrawRectangle(ReminderBrush, null, reminderBgRect, 12, 12);
            context.DrawText(reminderFormatted, new Point(reminderX, yOffset));
        }
    }
}
