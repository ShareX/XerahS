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
    private readonly bool _enableKeyboardNudge;
    private readonly RegionCaptureMode _mode;

    // Rendering configuration
    private readonly double _dimOpacity;
    private readonly uint _crosshairColor;
    private readonly uint _crosshairLineColor;
    private readonly bool _enableWindowSnapping;
    private readonly bool _enableMagnifier;
    private readonly bool _useTransparentOverlay;
    private readonly XerahS.Platform.Abstractions.CursorInfo? _ghostCursor;
    private readonly Bitmap? _ghostCursorBitmap;
    private readonly SkiaSharp.SKBitmap? _backgroundBitmap;
    private readonly Bitmap? _backgroundAvBitmap;

    // Keyboard state tracking
    private SelectionModifier _activeModifiers = SelectionModifier.None;

    // Visual brushes and pens (lazy initialization for performance)
    private IBrush? _dimBrush;
    private IBrush DimBrush => _dimBrush ??= new SolidColorBrush(Color.FromArgb((byte)(_dimOpacity * 255), 0, 0, 0));

    private static readonly IPen SelectionPen = new Pen(Brushes.White, 2);
    private static readonly IPen SelectionShadowPen = new Pen(new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)), 4);
    private static readonly IPen WindowSnapPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 0, 174, 255)), 3);
    private static readonly IPen WindowSnapShadowPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 0, 174, 255)), 6);
    private static readonly IBrush InfoBackgroundBrush = new SolidColorBrush(Color.FromArgb(220, 30, 30, 30));

    public event Action<RegionSelectionResult>? RegionSelected;
    public event Action<PixelRect>? SelectionChanged;
    public event Action? Cancelled;

    // State machine accessors for rendering
    private CaptureState _state => _stateMachine.CurrentState;
    private PixelPoint _currentPoint => _stateMachine.CurrentPoint;
    private PixelRect _selectionRect => _stateMachine.SelectionRect;
    private WindowInfo? _hoveredWindow => _stateMachine.HoveredWindow;

    public RegionCaptureControl(MonitorInfo monitor, RegionCaptureOptions? options = null, XerahS.Platform.Abstractions.CursorInfo? ghostCursor = null)
    {
        options ??= new RegionCaptureOptions();

        _monitor = monitor;
        _ghostCursor = ghostCursor;
        _coordinateService = new CoordinateTranslationService();
        _windowService = new WindowDetectionService();

        // Initialize state machine
        _stateMachine = new SelectionStateMachine();
        _stateMachine.SelectionConfirmed += OnSelectionConfirmed;
        _stateMachine.SelectionCancelled += OnSelectionCancelled;
        _stateMachine.StateChanged += _ => InvalidateVisual();
        _stateMachine.SelectionChanged += OnSelectionChanged;

        _dimOpacity = options.DimOpacity;
        _mode = options.Mode;
        _enableWindowSnapping = options.EnableWindowSnapping && _mode != RegionCaptureMode.ScreenColorPicker;
        _enableMagnifier = options.EnableMagnifier;
        _enableKeyboardNudge = options.EnableKeyboardNudge;
        _backgroundBitmap = options.BackgroundImage;
        _useTransparentOverlay = options.UseTransparentOverlay;
        _crosshairColor = options.CrosshairColor;
        _crosshairLineColor = options.CrosshairLineColor;

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

        // Create magnifier if enabled
        _magnifier = new MagnifierControl(options.MagnifierZoom);
        _magnifier.IsVisible = _enableMagnifier;

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
            canvas.DrawBitmap(skBitmap, 0, 0);
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

    private void OnSelectionConfirmed(RegionSelectionResult result) => RegionSelected?.Invoke(result);
    private void OnSelectionChanged(PixelRect rect) => SelectionChanged?.Invoke(rect);
    private void OnSelectionCancelled() => Cancelled?.Invoke();

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetPosition(this);
        var physicalPoint = LocalToPhysical(point);

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if (_mode == RegionCaptureMode.ScreenColorPicker)
            {
                _stateMachine.ConfirmPoint(physicalPoint);
                e.Handled = true;
                return;
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

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var point = e.GetPosition(this);
        var physicalPoint = LocalToPhysical(point);

        _stateMachine.UpdateCursorPosition(physicalPoint);

        if (_state == CaptureState.Hovering && _enableWindowSnapping)
        {
            var window = _windowService.GetWindowAtPoint(physicalPoint);
            _stateMachine.UpdateHoveredWindow(window);
        }

        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_state == CaptureState.Dragging && _mode != RegionCaptureMode.ScreenColorPicker)
        {
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
        UpdateModifiers(e);

        switch (e.Key)
        {
            case Key.Escape:
                _stateMachine.Cancel();
                e.Handled = true;
                break;

            case Key.Enter:
                if (_state == CaptureState.Selected || (_state == CaptureState.Hovering && _hoveredWindow is not null))
                {
                    _stateMachine.SnapToWindow();
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
        UpdateModifiers(e);
    }

    private void UpdateModifiers(KeyEventArgs e)
    {
        var modifiers = SelectionModifier.None;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            modifiers |= SelectionModifier.LockAspectRatio;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            modifiers |= SelectionModifier.PixelNudge;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
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
        // Ctrl+Arrow resizes, plain Arrow moves
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;

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

    private PixelPoint LocalToPhysical(Point local)
    {
        // Convert from control-local logical coordinates to physical screen coordinates
        return new PixelPoint(
            local.X * _monitor.ScaleFactor + _monitor.PhysicalBounds.X,
            local.Y * _monitor.ScaleFactor + _monitor.PhysicalBounds.Y);
    }

    private Point PhysicalToLocal(PixelPoint physical)
    {
        // Convert from physical screen coordinates to control-local logical coordinates
        return new Point(
            (physical.X - _monitor.PhysicalBounds.X) / _monitor.ScaleFactor,
            (physical.Y - _monitor.PhysicalBounds.Y) / _monitor.ScaleFactor);
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

        // Draw dimmed background with cutout using geometry clipping
        if (clearRect is { } rect && rect.Width > 0 && rect.Height > 0)
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

            // Draw the selection/snap border with shadow effect
            if (_state == CaptureState.Dragging || _state == CaptureState.Selected)
            {
                // Shadow first, then border
                context.DrawRectangle(null, SelectionShadowPen, rect);
                context.DrawRectangle(null, SelectionPen, rect);

                // Draw resize handles at corners
                DrawResizeHandles(context, rect);

                // Draw dimensions text
                DrawDimensionsText(context, rect);
            }
            else if (_hoveredWindow is not null)
            {
                // Window snap highlight
                context.DrawRectangle(null, WindowSnapShadowPen, rect);
                context.DrawRectangle(null, WindowSnapPen, rect);

                // Draw window title
                DrawWindowTitle(context, rect, _hoveredWindow.Title);
            }
        }
        else
        {
            // No selection, just draw full dim overlay
            context.DrawRectangle(DimBrush, null, bounds);
        }

        // Draw crosshair at cursor position
        DrawCrosshair(context, bounds);

        // Draw magnifier near cursor
        if (_enableMagnifier)
        {
            DrawMagnifierPosition(context);
        }

        // Draw modifier hints (bottom-right)
        DrawModifierHints(context);

        // Draw instructions (top-center, only in hover state)
        DrawInstructions(context);

        // Draw ghost cursor if available and configured
        DrawGhostCursor(context);
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

        // Calculate the portion of the background bitmap that corresponds to this monitor
        var virtualBounds = _coordinateService.GetVirtualScreenBounds();

        // Monitor's position relative to virtual screen origin
        var srcX = _monitor.PhysicalBounds.X - virtualBounds.X;
        var srcY = _monitor.PhysicalBounds.Y - virtualBounds.Y;

        // Source rect in the full screenshot (physical pixels)
        var sourceRect = new Rect(
            srcX,
            srcY,
            _monitor.PhysicalBounds.Width,
            _monitor.PhysicalBounds.Height);

        // Destination rect is the full control bounds (logical coordinates)
        context.DrawImage(_backgroundAvBitmap, sourceRect, bounds);
    }

    private void DrawResizeHandles(DrawingContext context, Rect rect)
    {
        const double handleSize = 8;
        var handleBrush = Brushes.White;
        var handlePen = new Pen(Brushes.Black, 1);

        var corners = new[]
        {
            new Point(rect.Left, rect.Top),
            new Point(rect.Right, rect.Top),
            new Point(rect.Left, rect.Bottom),
            new Point(rect.Right, rect.Bottom)
        };

        foreach (var corner in corners)
        {
            var handleRect = new Rect(
                corner.X - handleSize / 2,
                corner.Y - handleSize / 2,
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

        const double crosshairLength = 32; // Length of the colored crosshair portion
        
        // Regular line pen (configurable color for full-screen lines)
        var lineColor = Color.FromUInt32(_crosshairLineColor);
        var linePen = new Pen(new SolidColorBrush(lineColor), 1);
        
        // Crosshair colored pen (configurable color, more visible near cursor)
        var crosshairColor = Color.FromUInt32(_crosshairColor);
        var crosshairPen = new Pen(new SolidColorBrush(crosshairColor), 1.5);

        // Vertical line - top portion (regular)
        context.DrawLine(linePen,
            new Point(cursorLocal.X, 0),
            new Point(cursorLocal.X, Math.Max(0, cursorLocal.Y - crosshairLength)));

        // Vertical line - crosshair portion (colored, 32px centered on cursor)
        context.DrawLine(crosshairPen,
            new Point(cursorLocal.X, Math.Max(0, cursorLocal.Y - crosshairLength)),
            new Point(cursorLocal.X, Math.Min(bounds.Height, cursorLocal.Y + crosshairLength)));

        // Vertical line - bottom portion (regular)
        context.DrawLine(linePen,
            new Point(cursorLocal.X, Math.Min(bounds.Height, cursorLocal.Y + crosshairLength)),
            new Point(cursorLocal.X, bounds.Height));

        // Horizontal line - left portion (regular)
        context.DrawLine(linePen,
            new Point(0, cursorLocal.Y),
            new Point(Math.Max(0, cursorLocal.X - crosshairLength), cursorLocal.Y));

        // Horizontal line - crosshair portion (colored, 32px centered on cursor)
        context.DrawLine(crosshairPen,
            new Point(Math.Max(0, cursorLocal.X - crosshairLength), cursorLocal.Y),
            new Point(Math.Min(bounds.Width, cursorLocal.X + crosshairLength), cursorLocal.Y));

        // Horizontal line - right portion (regular)
        context.DrawLine(linePen,
            new Point(Math.Min(bounds.Width, cursorLocal.X + crosshairLength), cursorLocal.Y),
            new Point(bounds.Width, cursorLocal.Y));
    }

    private void DrawMagnifierPosition(DrawingContext context)
    {
        // Position magnifier near cursor but offset to not obstruct view
        var cursorLocal = PhysicalToLocal(_currentPoint);
        const double magnifierOffset = 20;
        const double magnifierSize = 120;

        var x = cursorLocal.X + magnifierOffset;
        var y = cursorLocal.Y + magnifierOffset;

        // Keep magnifier on screen
        if (x + magnifierSize > Bounds.Width)
            x = cursorLocal.X - magnifierOffset - magnifierSize;
        if (y + magnifierSize + 25 > Bounds.Height)
            y = cursorLocal.Y - magnifierOffset - magnifierSize - 25;

        // Update magnifier position
        _magnifier.UpdatePosition(_currentPoint);

        // For now, draw a placeholder - actual pixel capture would require screen capture
        DrawMagnifierPlaceholder(context, new Rect(x, y, magnifierSize, magnifierSize + 25));
    }

    private void DrawMagnifierPlaceholder(DrawingContext context, Rect rect)
    {
        // Draw magnifier background
        context.DrawRectangle(InfoBackgroundBrush, new Pen(Brushes.White, 2), rect, 4, 4);

        var centerX = rect.X + rect.Width / 2;
        var centerY = rect.Y + (rect.Height - 25) / 2;
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 0.5);
        var highlightPen = new Pen(Brushes.Red, 1.5);
        
        const int pixelGridCount = 15; // Number of pixels to show in grid
        const int halfGrid = pixelGridCount / 2;
        double pixelSize = (rect.Width - 8) / pixelGridCount; // Size of each magnified pixel
        
        // Draw actual pixels from background bitmap if available
        if (_backgroundBitmap != null)
        {
            // Calculate the virtual screen bounds to map cursor to bitmap coordinates
            var virtualBounds = _coordinateService.GetVirtualScreenBounds();
            
            // Get pixel coordinates in the background bitmap
            int bitmapX = (int)(_currentPoint.X - virtualBounds.X);
            int bitmapY = (int)(_currentPoint.Y - virtualBounds.Y);
            
            // Draw each pixel in the grid
            for (int dy = -halfGrid; dy <= halfGrid; dy++)
            {
                for (int dx = -halfGrid; dx <= halfGrid; dx++)
                {
                    int srcX = bitmapX + dx;
                    int srcY = bitmapY + dy;
                    
                    // Check bounds
                    if (srcX >= 0 && srcX < _backgroundBitmap.Width && 
                        srcY >= 0 && srcY < _backgroundBitmap.Height)
                    {
                        var skColor = _backgroundBitmap.GetPixel(srcX, srcY);
                        var avColor = Color.FromArgb(skColor.Alpha, skColor.Red, skColor.Green, skColor.Blue);
                        var brush = new SolidColorBrush(avColor);
                        
                        var pixelRect = new Rect(
                            rect.X + 4 + (dx + halfGrid) * pixelSize,
                            rect.Y + 4 + (dy + halfGrid) * pixelSize,
                            pixelSize,
                            pixelSize);
                        
                        context.DrawRectangle(brush, null, pixelRect);
                    }
                }
            }
            
            // Draw grid lines on top of pixels
            for (int i = 0; i <= pixelGridCount; i++)
            {
                var gridX = rect.X + 4 + i * pixelSize;
                var gridY = rect.Y + 4 + i * pixelSize;
                context.DrawLine(gridPen, new Point(gridX, rect.Y + 4), new Point(gridX, rect.Y + rect.Height - 29));
                context.DrawLine(gridPen, new Point(rect.X + 4, gridY), new Point(rect.X + rect.Width - 4, gridY));
            }
        }
        else
        {
            // Fallback: draw placeholder grid when no background image
            for (int i = 0; i < pixelGridCount; i++)
            {
                var offset = (i - halfGrid) * 7;
                context.DrawLine(gridPen,
                    new Point(rect.X + 4, centerY + offset),
                    new Point(rect.X + rect.Width - 4, centerY + offset));
                context.DrawLine(gridPen,
                    new Point(centerX + offset, rect.Y + 4),
                    new Point(centerX + offset, rect.Y + rect.Height - 29));
            }
        }

        // Center highlight (crosshair on center pixel)
        var centerPixelRect = new Rect(
            rect.X + 4 + halfGrid * pixelSize,
            rect.Y + 4 + halfGrid * pixelSize,
            pixelSize,
            pixelSize);
        context.DrawRectangle(null, highlightPen, centerPixelRect);

        // Draw coordinates text
        var coordText = $"({_currentPoint.X:F0}, {_currentPoint.Y:F0})";
        var formattedCoord = new FormattedText(
            coordText,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas", FontStyle.Normal, FontWeight.Normal),
            10,
            Brushes.White);

        context.DrawText(formattedCoord, new Point(rect.X + 4, rect.Bottom - 20));
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
            hints.Add("Ctrl: Resize mode");

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

    private void DrawInstructions(DrawingContext context)
    {
        if (_state != CaptureState.Hovering)
            return;

        var instructions = "Drag to select region | Click to snap window | Ctrl: toggle annotation/selection | Enter: finish | Esc: cancel";
        if (_mode == RegionCaptureMode.ScreenColorPicker)
        {
            instructions = "Click to pick a color | Esc to cancel";
        }
        var formatted = new FormattedText(
            instructions,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal),
            12,
            new SolidColorBrush(Color.FromRgb(180, 180, 180)));

        var x = (Bounds.Width - formatted.Width) / 2;
        var y = 12;

        var bgRect = new Rect(x - 12, y - 4, formatted.Width + 24, formatted.Height + 8);
        context.DrawRectangle(InfoBackgroundBrush, null, bgRect, 4, 4);
        context.DrawText(formatted, new Point(x, y));
    }
}
