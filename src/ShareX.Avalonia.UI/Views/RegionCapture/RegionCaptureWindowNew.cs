// This file contains the NEW backend integration code for RegionCaptureWindow
// It will be merged into RegionCaptureWindow.axaml.cs once tested

using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ShareX.Avalonia.UI.Services;
using ShareX.Avalonia.Platform.Abstractions.Capture;
using SkiaSharp;
using XerahS.Core.Helpers;

namespace XerahS.UI.Views.RegionCapture
{
    /// <summary>
    /// New backend integration methods for RegionCaptureWindow.
    /// These methods will replace the old DPI handling code.
    /// </summary>
    public partial class RegionCaptureWindow
    {
        // NEW: Fields for new backend
        private RegionCaptureService? _newCaptureService;
        private MonitorInfo[] _newMonitors = Array.Empty<MonitorInfo>();
        private LogicalRectangle _newVirtualDesktopLogical;
        private RegionCaptureCoordinateMapper? _newCoordinateMapper;
        private SKBitmap? _capturedBitmap;

        // Feature flag to enable new backend
        private const bool USE_NEW_BACKEND = true; // New backend enabled - platform backends now compile successfully

        // Public property to check if new backend is initialized
        public bool IsNewBackendInitialized => _newCaptureService != null;

        // Public property to get the captured bitmap (will be set after selection)
        public SKBitmap? GetCapturedBitmap() => _capturedBitmap;

        /// <summary>
        /// NEW: Initialize the new capture service backend.
        /// Call this in the constructor.
        /// </summary>
        private bool TryInitializeNewBackend()
        {
            if (!USE_NEW_BACKEND)
                return false;

            try
            {
                TroubleshootingHelper.Log("RegionCapture","INIT", "Initializing NEW capture backend");

                // Create platform-specific backend
                IRegionCaptureBackend backend;

#if WINDOWS
                if (OperatingSystem.IsWindows())
                {
                    backend = new ShareX.Avalonia.Platform.Windows.Capture.WindowsRegionCaptureBackend();
                }
                else
#elif MACCATALYST || MACOS
                if (OperatingSystem.IsMacOS())
                {
                    backend = new ShareX.Avalonia.Platform.macOS.Capture.MacOSRegionCaptureBackend();
                }
                else
#elif LINUX
                if (OperatingSystem.IsLinux())
                {
                    backend = new ShareX.Avalonia.Platform.Linux.Capture.LinuxRegionCaptureBackend();
                }
                else
#endif
                {
                    TroubleshootingHelper.Log("RegionCapture","ERROR", "Unsupported platform for new backend");
                    return false;
                }

                _newCaptureService = new RegionCaptureService(backend);
                _newMonitors = _newCaptureService.GetMonitors();
                _newVirtualDesktopLogical = _newCaptureService.GetVirtualDesktopBoundsLogical();

                TroubleshootingHelper.Log("RegionCapture","INIT", $"NEW backend initialized successfully with {_newMonitors.Length} monitors");

                var capabilities = _newCaptureService.GetCapabilities();
                TroubleshootingHelper.Log("RegionCapture","INIT", $"Backend: {capabilities.BackendName} {capabilities.Version}");
                TroubleshootingHelper.Log("RegionCapture","INIT", $"  HW Accel: {capabilities.SupportsHardwareAcceleration}");
                TroubleshootingHelper.Log("RegionCapture","INIT", $"  Per-Mon DPI: {capabilities.SupportsPerMonitorDpi}");

                foreach (var monitor in _newMonitors)
                {
                    TroubleshootingHelper.Log("RegionCapture","MONITOR", $"{monitor.Name}:");
                    TroubleshootingHelper.Log("RegionCapture","MONITOR", $"  Physical: {monitor.Bounds}");
                    TroubleshootingHelper.Log("RegionCapture","MONITOR", $"  Scale: {monitor.ScaleFactor}x ({monitor.PhysicalDpi:F0} DPI)");
                    TroubleshootingHelper.Log("RegionCapture","MONITOR", $"  Primary: {monitor.IsPrimary}");
                }

                return true;
            }
            catch (Exception ex)
            {
                TroubleshootingHelper.Log("RegionCapture","ERROR", $"Failed to initialize new backend: {ex.Message}");
                TroubleshootingHelper.Log("RegionCapture","ERROR", $"Stack: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// NEW: Position window using the new backend.
        /// Replaces the old screen enumeration logic in OnOpened.
        /// </summary>
        private void PositionWindowWithNewBackend()
        {
            if (_newCaptureService == null)
            {
                TroubleshootingHelper.Log("RegionCapture","ERROR", "PositionWindowWithNewBackend called but service is null");
                return;
            }

            TroubleshootingHelper.Log("RegionCapture","WINDOW", "=== Using NEW backend for positioning ===");

            // Get virtual desktop bounds in logical coordinates
            var logicalBounds = _newVirtualDesktopLogical;
            TroubleshootingHelper.Log("RegionCapture","WINDOW", $"Virtual desktop logical: {logicalBounds}");

            // Position window (Avalonia uses logical coordinates for Position)
            Position = new PixelPoint(
                (int)Math.Round(logicalBounds.X),
                (int)Math.Round(logicalBounds.Y));

            // Size window (Avalonia uses logical coordinates for Width/Height)
            Width = logicalBounds.Width;
            Height = logicalBounds.Height;

            TroubleshootingHelper.Log("RegionCapture","WINDOW", $"Window positioned at: {Position}");
            TroubleshootingHelper.Log("RegionCapture","WINDOW", $"Window size: {Width}x{Height}");
            TroubleshootingHelper.Log("RegionCapture","WINDOW", $"RenderScaling: {RenderScaling}");

            // Log physical bounds for comparison
            var physicalBounds = _newCaptureService.GetVirtualDesktopBoundsPhysical();
            TroubleshootingHelper.Log("RegionCapture","WINDOW", $"Virtual desktop physical: {physicalBounds}");

            _newCoordinateMapper = new RegionCaptureCoordinateMapper(
                _newCaptureService,
                new LogicalPoint(logicalBounds.X, logicalBounds.Y));

            TroubleshootingHelper.Log("RegionCapture","WINDOW", "=== NEW backend positioning complete ===");
        }

        /// <summary>
        /// NEW: Convert logical point to physical using new backend.
        /// Replaces ConvertLogicalToScreen.
        /// </summary>
        private SKPointI ConvertLogicalToPhysicalNew(Point logicalWindowPos)
        {
            if (_newCoordinateMapper == null)
                throw new InvalidOperationException("New capture service not initialized");

            var physical = _newCoordinateMapper.WindowLogicalToPhysical(logicalWindowPos);
            return new SKPointI(physical.X, physical.Y);
        }

        /// <summary>
        /// NEW: Convert physical point to logical using new backend.
        /// For window-local coordinates, subtract window position.
        /// </summary>
        private Point ConvertPhysicalToLogicalNew(SKPointI physicalScreen)
        {
            if (_newCoordinateMapper == null)
                throw new InvalidOperationException("New capture service not initialized");

            var logical = _newCoordinateMapper.PhysicalToWindowLogical(
                new PhysicalPoint(physicalScreen.X, physicalScreen.Y));
            return logical;
        }

        /// <summary>
        /// NEW: Handle pointer pressed with new backend.
        /// </summary>
        private void OnPointerPressedNew(PointerPressedEventArgs e)
        {
            if (_newCaptureService == null) return;

            var logicalPos = e.GetPosition(this);
            var physical = ConvertLogicalToPhysicalNew(logicalPos);

            _startPointPhysical = physical;
            _startPointLogical = logicalPos;

            TroubleshootingHelper.Log("RegionCapture","INPUT", $"[NEW] Pressed: Window-local={logicalPos}, Physical={physical}");

            _isSelecting = true;
            _dragStarted = false;

            // Detect window under cursor at press time (for single-click window selection)
            UpdateWindowSelection(physical);

            if (_hoveredWindow != null)
            {
                TroubleshootingHelper.Log("RegionCapture", "INPUT", $"[NEW] Window detected at press: {_hoveredWindow.Title}");
            }
        }

        /// <summary>
        /// NEW: Handle pointer moved with new backend.
        /// </summary>
        private void OnPointerMovedNew(PointerEventArgs e)
        {
            if (_newCaptureService == null) return;

            var logicalPos = e.GetPosition(this);
            var currentPhysical = ConvertLogicalToPhysicalNew(logicalPos);

            if (_isSelecting)
            {
                // Check drag threshold
                if (!_dragStarted)
                {
                    var dragDistance = Math.Sqrt(
                        Math.Pow(currentPhysical.X - _startPointPhysical.X, 2) +
                        Math.Pow(currentPhysical.Y - _startPointPhysical.Y, 2));

                    if (dragDistance >= DragThreshold)
                    {
                        _dragStarted = true;
                        TroubleshootingHelper.Log("RegionCapture","INPUT", $"[NEW] Drag started (distance: {dragDistance:F2}px)");
                    }
                }

                // Update selection rectangle - pass both logical and physical coords
                UpdateSelectionRectangleNew(logicalPos, currentPhysical);
            }
            else if (!_dragStarted)
            {
                // Window detection (keep existing logic)
                UpdateWindowSelection(currentPhysical);
            }
        }

        /// <summary>
        /// NEW: Handle pointer released with new backend.
        /// </summary>
        private void OnPointerReleasedNew(PointerReleasedEventArgs e)
        {
            TroubleshootingHelper.Log("RegionCapture","INPUT", $"[NEW] OnPointerReleasedNew called: _isSelecting={_isSelecting}, _newCaptureService={(_newCaptureService != null ? "initialized" : "null")}");

            if (_newCaptureService == null)
            {
                TroubleshootingHelper.Log("RegionCapture","ERROR", "[NEW] Release event called but service is null");
                return;
            }

            if (!_isSelecting)
            {
                TroubleshootingHelper.Log("RegionCapture","WARNING", "[NEW] Release event called but not selecting - ignoring");
                return;
            }

            _isSelecting = false;

            // If we didn't drag and have a hovered window, use that
            if (!_dragStarted && _hoveredWindow != null)
            {
                var rect = _hoveredWindow.Bounds;
                TroubleshootingHelper.Log("RegionCapture","RESULT", $"[NEW] Selected window: {rect}");

                // Don't capture here - let ScreenCaptureService use the old platform capture which works reliably
                // The new backend is only used for coordinate conversion and window detection
                TroubleshootingHelper.Log("RegionCapture","WINDOW", "[NEW] Window selection complete, closing overlay");
                _tcs.TrySetResult(new SKRectI(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height));
                Close();
                return;
            }

            // Get final position
            var logicalPos = e.GetPosition(this);
            var currentPhysical = ConvertLogicalToPhysicalNew(logicalPos);

            TroubleshootingHelper.Log("RegionCapture","MOUSE", $"[NEW] Released: Logical={logicalPos}, Physical={currentPhysical}");

            // Calculate selection rectangle
            var x = Math.Min(_startPointPhysical.X, currentPhysical.X);
            var y = Math.Min(_startPointPhysical.Y, currentPhysical.Y);
            var width = Math.Abs(_startPointPhysical.X - currentPhysical.X);
            var height = Math.Abs(_startPointPhysical.Y - currentPhysical.Y);

            TroubleshootingHelper.Log("RegionCapture","RESULT", $"[NEW] Final selection: X={x}, Y={y}, W={width}, H={height}");

            // Ensure non-zero size
            if (width <= 0) width = 1;
            if (height <= 0) height = 1;

            LogSelectionDiagnostics(
                new LogicalPoint(_startPointLogical.X, _startPointLogical.Y),
                new LogicalPoint(logicalPos.X, logicalPos.Y),
                new PhysicalRectangle(x, y, width, height));

            // Don't capture here - let ScreenCaptureService use the old platform capture which works reliably
            // The new backend is only used for coordinate conversion and window detection
            var resultRect = new SKRectI(x, y, x + width, y + height);
            TroubleshootingHelper.Log("RegionCapture","WINDOW", $"[NEW] Region selection complete, closing overlay: {resultRect}");
            _tcs.TrySetResult(resultRect);
            Close();
        }

        private void LogSelectionDiagnostics(
            LogicalPoint startLogical,
            LogicalPoint endLogical,
            PhysicalRectangle physicalRect)
        {
            if (_newCaptureService == null)
                return;

            var virtualPhysical = _newCaptureService.GetVirtualDesktopBoundsPhysical();
            var virtualLogical = _newCaptureService.GetVirtualDesktopBoundsLogical();

            TroubleshootingHelper.Log("RegionCapture", "DIAGNOSTIC", "[NEW] === Selection Diagnostics ===");
            TroubleshootingHelper.Log("RegionCapture", "DIAGNOSTIC", $"Virtual desktop physical: {virtualPhysical}");
            TroubleshootingHelper.Log("RegionCapture", "DIAGNOSTIC", $"Virtual desktop logical: {virtualLogical}");
            TroubleshootingHelper.Log("RegionCapture", "DIAGNOSTIC", $"Selection logical: ({startLogical}) -> ({endLogical})");
            TroubleshootingHelper.Log("RegionCapture", "DIAGNOSTIC", $"Selection physical: {physicalRect}");

            var originPhysical = new PhysicalPoint(physicalRect.X, physicalRect.Y);
            var originMonitor = _newMonitors.FirstOrDefault(m => m.Bounds.Contains(originPhysical));
            if (originMonitor != null)
            {
                TroubleshootingHelper.Log("RegionCapture", "DIAGNOSTIC",
                    $"Origin monitor: {originMonitor.Name} {originMonitor.Bounds} @ {originMonitor.ScaleFactor:F2}x");
            }

            var intersectingMonitors = _newMonitors
                .Where(m => m.Bounds.Intersect(physicalRect) != null)
                .ToArray();

            if (intersectingMonitors.Length > 0)
            {
                TroubleshootingHelper.Log("RegionCapture", "DIAGNOSTIC",
                    $"Intersecting monitors: {intersectingMonitors.Length}");

                foreach (var monitor in intersectingMonitors)
                {
                    TroubleshootingHelper.Log("RegionCapture", "DIAGNOSTIC",
                        $"  - {monitor.Name}: {monitor.Bounds} @ {monitor.ScaleFactor:F2}x");
                }
            }
        }

        /// <summary>
        /// NEW: Update selection rectangle rendering.
        /// Uses logical coordinates for visual rendering (aligned with Avalonia's coordinate space)
        /// and physical coordinates for capture selection.
        /// </summary>
        private void UpdateSelectionRectangleNew(Point currentLogical, SKPointI currentPhysical)
        {
            if (_newCaptureService == null) return;

            // Calculate physical rectangle for capture and monitor detection
            var physX = Math.Min(_startPointPhysical.X, currentPhysical.X);
            var physY = Math.Min(_startPointPhysical.Y, currentPhysical.Y);
            var physWidth = Math.Abs(_startPointPhysical.X - currentPhysical.X);
            var physHeight = Math.Abs(_startPointPhysical.Y - currentPhysical.Y);
            var physicalRect = new PhysicalRectangle(physX, physY, physWidth, physHeight);

            // Calculate logical rectangle for visual rendering
            // Use Avalonia's logical coordinates directly - this ensures perfect alignment with mouse cursor
            var logicalX = Math.Min(_startPointLogical.X, currentLogical.X);
            var logicalY = Math.Min(_startPointLogical.Y, currentLogical.Y);
            var logicalWidth = Math.Abs(_startPointLogical.X - currentLogical.X);
            var logicalHeight = Math.Abs(_startPointLogical.Y - currentLogical.Y);

            // Update UI elements with logical coordinates
            UpdateSelectionVisuals(logicalX, logicalY, logicalWidth, logicalHeight);

            // Log for debugging
            var intersectingMonitors = _newMonitors
                .Where(m => m.Bounds.Intersect(physicalRect) != null)
                .ToArray();

            if (intersectingMonitors.Length > 1)
            {
                TroubleshootingHelper.Log("RegionCapture","SELECTION", $"[NEW] Spanning {intersectingMonitors.Length} monitors:");
                foreach (var mon in intersectingMonitors)
                {
                    TroubleshootingHelper.Log("RegionCapture","SELECTION", $"  - {mon.Name} @ {mon.ScaleFactor}x");
                }
            }
        }

        /// <summary>
        /// Helper to update visual elements (existing code can stay the same).
        /// </summary>
        private void UpdateSelectionVisuals(double x, double y, double width, double height)
        {
            var selectionBorder = this.FindControl<Avalonia.Controls.Shapes.Rectangle>("SelectionBorder");
            var selectionBorderInner = this.FindControl<Avalonia.Controls.Shapes.Rectangle>("SelectionBorderInner");
            var darkeningOverlay = this.FindControl<Avalonia.Controls.Shapes.Path>("DarkeningOverlay");
            var infoText = this.FindControl<Avalonia.Controls.TextBlock>("InfoText");

            if (selectionBorder != null)
            {
                selectionBorder.Width = width;
                selectionBorder.Height = height;
                Avalonia.Controls.Canvas.SetLeft(selectionBorder, x);
                Avalonia.Controls.Canvas.SetTop(selectionBorder, y);
                selectionBorder.IsVisible = true;
            }

            if (selectionBorderInner != null)
            {
                selectionBorderInner.Width = width;
                selectionBorderInner.Height = height;
                Avalonia.Controls.Canvas.SetLeft(selectionBorderInner, x);
                Avalonia.Controls.Canvas.SetTop(selectionBorderInner, y);
                selectionBorderInner.IsVisible = true;
            }

            // Update darkening overlay (keep existing logic)
            // Update info text (keep existing logic)
        }

        /// <summary>
        /// NEW: Cleanup on disposal.
        /// </summary>
        private void DisposeNewBackend()
        {
            if (_newCaptureService != null)
            {
                TroubleshootingHelper.Log("RegionCapture","LIFECYCLE", "[NEW] Disposing capture service");
                _newCaptureService.Dispose();
                _newCaptureService = null;
            }
        }
    }
}
