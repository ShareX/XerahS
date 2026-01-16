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
        private SKBitmap? _capturedBitmap = null;
        private SKBitmap? _screenSnapshot = null;

        // Feature flag to enable new backend
        private static readonly bool USE_NEW_BACKEND = true; // New backend enabled - platform backends now compile successfully

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

            // [2026-01-15] Log window origin for negative coordinate debugging
            TroubleshootingHelper.Log("RegionCapture","WINDOW", $"Window logical origin: ({logicalBounds.X}, {logicalBounds.Y})");
            if (logicalBounds.X < 0 || logicalBounds.Y < 0)
            {
                TroubleshootingHelper.Log("RegionCapture","WINDOW", "⚠️ NEGATIVE COORDINATES DETECTED - Multi-monitor with secondary left/above primary");
            }

            _newCoordinateMapper = new RegionCaptureCoordinateMapper(
                _newCaptureService,
                new LogicalPoint(logicalBounds.X, logicalBounds.Y));

            TroubleshootingHelper.Log("RegionCapture","WINDOW", "=== NEW backend positioning complete ===");
        }

        public async System.Threading.Tasks.Task InitializeMagnifierBackend()
        {
            if (_newCaptureService == null) return;
            
            try 
            {
                TroubleshootingHelper.Log("RegionCapture", "MAGNIFIER", "Capturing background for magnifier...");
                
                // Temporarily hide window to capture background
                // We use Opacity instead of IsVisible to maintain layout
                var oldOpacity = Opacity;
                Opacity = 0;
                
                // Allow UI to update
                await System.Threading.Tasks.Task.Delay(50);
                
                var virtualDesktop = _newCaptureService.GetVirtualDesktopBoundsPhysical();
                var skRect = new SKRect(virtualDesktop.X, virtualDesktop.Y, virtualDesktop.X + virtualDesktop.Width, virtualDesktop.Y + virtualDesktop.Height);
                
                // Capture entire desktop
                // Note: We use platform capture capable of capturing all screens
                _screenSnapshot = await XerahS.Platform.Abstractions.PlatformServices.ScreenCapture.CaptureRectAsync(skRect);
                
                Opacity = oldOpacity;
                TroubleshootingHelper.Log("RegionCapture", "MAGNIFIER", $"Background captured: {_screenSnapshot?.Width ?? 0}x{_screenSnapshot?.Height ?? 0}");
            }
            catch (Exception ex)
            {
                TroubleshootingHelper.Log("RegionCapture", "ERROR", $"Failed to capture background: {ex.Message}");
                Opacity = 1; // Ensure visible
            }
        }

        private void UpdateMagnifierContent(SKPointI physicalCursorPos)
        {
            if (_screenSnapshot == null || _magnifierImage == null || _infoStack == null || !_infoStack.IsVisible) return;

            try
            {
                // Determine crop size (physical)
                // Magnifier UI size is approx 120x120 logical? 
                // Let's assume we want to show a 2x zoom of a 100x100 area.
                // Or simply show 1:1 area.
                // Existing XAML has a scaling transform usually.
                // Or acts as a loupe.
                // For simplicity: Crop 200x200 physical pixels centered on cursor.
                
                int cropSize = 200;
                int halfSize = cropSize / 2;
                
                var cropRect = new SKRectI(
                    physicalCursorPos.X - halfSize,
                    physicalCursorPos.Y - halfSize,
                    physicalCursorPos.X + halfSize,
                    physicalCursorPos.Y + halfSize);
                
                // Clamp to screenshot bounds
                // Note: _screenSnapshot origin is usually (0,0) relative to its content, 
                // BUT if we captured a virtual desktop starting at negative coordinates, 
                // we need to map physical coordinates to bitmap coordinates.
                // The `CaptureRectAsync` returns a bitmap.
                // If we captured `skRect`, the bitmap (0,0) corresponds to `skRect.Left, skRect.Top`.
                
                var virtualOrigin = _newCaptureService!.GetVirtualDesktopBoundsPhysical();
                
                // Map physical cursor to bitmap relative
                int bitmapX = physicalCursorPos.X - virtualOrigin.X;
                int bitmapY = physicalCursorPos.Y - virtualOrigin.Y;
                
                var srcRect = new SKRectI(
                    bitmapX - halfSize,
                    bitmapY - halfSize,
                    bitmapX + halfSize,
                    bitmapY + halfSize);
                    
                // Create subset
                using (var subset = new SKBitmap(cropSize, cropSize))
                {
                   // _screenSnapshot.ExtractSubset(subset, srcRect) might fail if out of bounds
                   // Safer to draw into subset
                   using (var canvas = new SKCanvas(subset))
                   {
                       canvas.Clear(SKColors.Black);
                       // Draw portion of source
                       canvas.DrawBitmap(_screenSnapshot, 
                           new SKRect(srcRect.Left, srcRect.Top, srcRect.Right, srcRect.Bottom),
                           new SKRect(0, 0, cropSize, cropSize));
                   }
                   
                   _magnifierImage.Source = ConvertToAvaloniaBitmap(subset);
                }
            }
            catch
            {
                // Ignore update errors
            }
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

        private void OnPointerPressedNew(PointerPressedEventArgs e)
        {
            if (_newCaptureService == null) return;

            var logicalPos = e.GetPosition(this);
            var physical = ConvertLogicalToPhysicalNew(logicalPos);

            _startPointPhysical = physical;
            _startPointLogical = logicalPos;

            TroubleshootingHelper.Log("RegionCapture","INPUT", $"[NEW] Pressed: Window-local={logicalPos}, Physical={physical}");

            _state = RegionCaptureState.DragSelecting;
            _dragStarted = false;

            if (_resizeHandlesCanvas != null) _resizeHandlesCanvas.IsVisible = false;

            UpdateWindowSelectionNew(physical);
        }

        private void OnPointerMovedNew(PointerEventArgs e)
        {
            if (_newCaptureService == null) return;

            var logicalPos = e.GetPosition(this);

            var captureCanvas = this.FindControl<RegionCaptureCanvas>("CaptureCanvas");
            if (captureCanvas != null)
            {
                // Correct for Zoom if needed, but here logicalPos is window-relative
                captureCanvas.UpdateCursor(new SKPoint((float)logicalPos.X, (float)logicalPos.Y));
            }

            UpdateMagnifierPosition(logicalPos);
            UpdateMagnifierContent(ConvertLogicalToPhysicalNew(logicalPos));

            var currentPhysical = ConvertLogicalToPhysicalNew(logicalPos);

            if (_state == RegionCaptureState.DragSelecting)
            {
                if (!_dragStarted)
                {
                    var dragDistance = Math.Sqrt(
                        Math.Pow(currentPhysical.X - _startPointPhysical.X, 2) +
                        Math.Pow(currentPhysical.Y - _startPointPhysical.Y, 2));

                    if (dragDistance >= DragThreshold)
                    {
                        _dragStarted = true;
                    }
                }

                // [2026-01-15] FIX: Keep showing window boundary until drag starts
                // This allows clicking on window boundaries to capture the window
                if (!_dragStarted)
                {
                    // Mouse hasn't moved enough - keep showing window boundary
                    UpdateWindowSelectionNew(currentPhysical);
                }
                else
                {
                    // User is dragging - show selection rectangle
                    UpdateSelectionRectangleNew(logicalPos, currentPhysical);
                }
            }
            else if (_state == RegionCaptureState.Idle)
            {
                UpdateWindowSelectionNew(currentPhysical);
            }
        }

        private void OnPointerReleasedNew(PointerReleasedEventArgs e)
        {
            if (_newCaptureService == null) return;

            if (_state != RegionCaptureState.DragSelecting) return;

            if (!_dragStarted && _hoveredWindow != null)
            {
                // Single-click on window: auto-confirm
                var rect = _hoveredWindow.Bounds;
                TroubleshootingHelper.Log("RegionCapture","RESULT", $"[NEW] Selected window: {rect}");
                _currentSelectionPhysical = new SKRectI(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);
                ConfirmSelection();
                return;
            }

            // Drag selection: auto-confirm on mouse release
            _state = RegionCaptureState.Selected;

            var logicalPos = e.GetPosition(this);
            var currentPhysical = ConvertLogicalToPhysicalNew(logicalPos);

            UpdateSelectionRectangleNew(logicalPos, currentPhysical);

            TroubleshootingHelper.Log("RegionCapture","STATE", "Drag selection complete, auto-confirming");

            // Auto-confirm the selection immediately on mouse release
            ConfirmSelection();
        }

        private void LogSelectionDiagnostics(LogicalPoint startLogical, LogicalPoint endLogical, PhysicalRectangle physicalRect)
        {
            if (_newCaptureService == null) return;

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

            // Store current physical selection for Confirmation
            _currentSelectionPhysical = new SKRectI(physX, physY, physX + physWidth, physY + physHeight);
            
            // Calculate logical rectangle for visual rendering
            var logicalX = Math.Min(_startPointLogical.X, currentLogical.X);
            var logicalY = Math.Min(_startPointLogical.Y, currentLogical.Y);
            var logicalWidth = Math.Abs(_startPointLogical.X - currentLogical.X);
            var logicalHeight = Math.Abs(_startPointLogical.Y - currentLogical.Y);

            // Update UI elements 
            UpdateSelectionVisuals(new SKRectI(physX, physY, physX + physWidth, physY + physHeight), 
                                   new Rect(logicalX, logicalY, logicalWidth, logicalHeight));
            
            // Update HUD with physical stats
            if (_infoText != null)
            {
                _infoText.Text = $"X: {physX} Y: {physY} W: {physWidth} H: {physHeight}";
                _infoText.IsVisible = true;
                Canvas.SetLeft(_infoText, logicalX);
                Canvas.SetTop(_infoText, logicalY - 30 > 5 ? logicalY - 30 : logicalY + 5);
            }

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
        /// Helper to update visual elements.
        /// [2026-01-16] Updated to use SkiaSharp-based RegionCaptureCanvas with Physical coordinates
        /// </summary>
        private void UpdateSelectionVisuals(SKRectI physicalRect, Rect logicalRect)
        {
            var captureCanvas = this.FindControl<RegionCaptureCanvas>("CaptureCanvas");
            if (captureCanvas != null)
            {
                // Pass logical coordinates (RegionCaptureCanvas now handles scaling)
                var skRect = new SKRect((float)logicalRect.X, (float)logicalRect.Y, (float)logicalRect.Right, (float)logicalRect.Bottom);
                captureCanvas.UpdateSelection(skRect, _state == RegionCaptureState.DragSelecting || _state == RegionCaptureState.Selected);
                captureCanvas.SetDarkening(_useDarkening);
            }
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
            if (_screenSnapshot != null)
            {
                _screenSnapshot.Dispose();
                _screenSnapshot = null;
            }
        }

        private void ConfirmSelection()
        {
            if (_state != RegionCaptureState.Selected) return;

            TroubleshootingHelper.Log("RegionCapture","RESULT", $"Confirmed selection: {_currentSelectionPhysical}");
            _tcs.TrySetResult(_currentSelectionPhysical);
            Close();
        }

        private void SelectMonitor(int index)
        {
            if (_newCaptureService == null || index < 0 || index >= _newMonitors.Length) return;

            var monitor = _newMonitors[index];
            var bounds = monitor.Bounds;

            // Enter Selected State
            _state = RegionCaptureState.Selected;
            _dragStarted = false;

            _currentSelectionPhysical = new SKRectI(bounds.X, bounds.Y, bounds.X + bounds.Width, bounds.Y + bounds.Height);

            // Convert to logical for visuals
            if (_newCoordinateMapper != null)
            {
                var physicalTL = new PhysicalPoint(bounds.X, bounds.Y);
                var physicalBR = new PhysicalPoint(bounds.X + bounds.Width, bounds.Y + bounds.Height);
                
                var tl = _newCoordinateMapper.PhysicalToWindowLogical(physicalTL);
                var br = _newCoordinateMapper.PhysicalToWindowLogical(physicalBR);

                var logicalX = tl.X;
                var logicalY = tl.Y;
                var logicalWidth = Math.Abs(br.X - tl.X);
                var logicalHeight = Math.Abs(br.Y - tl.Y);

                UpdateSelectionVisuals(new SKRectI(bounds.X, bounds.Y, bounds.X + bounds.Width, bounds.Y + bounds.Height),
                                       new Rect(logicalX, logicalY, logicalWidth, logicalHeight));
                
                // Update text
                if (_infoText != null)
                {
                    _infoText.Text = $"Monitor {index+1}: {bounds.Width}x{bounds.Height}";
                    _infoText.IsVisible = true;
                    Canvas.SetLeft(_infoText, logicalX);
                    Canvas.SetTop(_infoText, logicalY - 30 > 5 ? logicalY - 30 : logicalY + 5);
                }
            }

            // Show handles
            if (_resizeHandlesCanvas != null) _resizeHandlesCanvas.IsVisible = true;

            TroubleshootingHelper.Log("RegionCapture", "INPUT", $"Monitor {index + 1} selected: {bounds}");
        }

        private void SelectActiveMonitor()
        {
            if (_newCaptureService == null || _newMonitors.Length == 0) return;

            var mouse = GetGlobalMousePosition();
            var physical = new PhysicalPoint(mouse.X, mouse.Y);

            for(int i=0; i<_newMonitors.Length; i++)
            {
                if (_newMonitors[i].Bounds.Contains(physical))
                {
                    SelectMonitor(i);
                    return;
                }
            }

            // Fallback to primary or 0
            SelectMonitor(0);
        }

        /// <summary>
        /// NEW: Update window selection using the new coordinate system.
        /// Properly handles per-monitor DPI scaling.
        /// </summary>
        private void UpdateWindowSelectionNew(SKPointI mousePhysical)
        {
            if (_windows == null || _newCaptureService == null || _newCoordinateMapper == null) return;

            // Find window under cursor (windows are in physical coordinates)
            var physicalPoint = new PhysicalPoint(mousePhysical.X, mousePhysical.Y);
            var window = _windows.FirstOrDefault(w => w.Bounds.Contains(mousePhysical.X, mousePhysical.Y));

            if (window != null && window != _hoveredWindow)
            {
                _hoveredWindow = window;

                // Convert window bounds (physical) to window-local logical coordinates
                var physicalTL = new PhysicalPoint(window.Bounds.X, window.Bounds.Y);
                var physicalBR = new PhysicalPoint(
                    window.Bounds.X + window.Bounds.Width,
                    window.Bounds.Y + window.Bounds.Height);

                var logicalTL = _newCoordinateMapper.PhysicalToWindowLogical(physicalTL);
                var logicalBR = _newCoordinateMapper.PhysicalToWindowLogical(physicalBR);

                var logicalX = logicalTL.X;
                var logicalY = logicalTL.Y;
                var logicalW = Math.Abs(logicalBR.X - logicalTL.X);
                var logicalH = Math.Abs(logicalBR.Y - logicalTL.Y);

                // [2026-01-15] Diagnostic logging for negative coordinate debugging
                if (window.Bounds.X < 0 || window.Bounds.Y < 0)
                {
                    TroubleshootingHelper.Log("RegionCapture[NEW]", "COORDS",
                        $"Window with negative coords: Physical=({window.Bounds.X},{window.Bounds.Y}) " +
                        $"→ WindowLocal=({logicalX:F1},{logicalY:F1})");
                }

                // Find which monitor contains this window for logging
                var containingMonitor = _newMonitors.FirstOrDefault(m => m.Bounds.Contains(physicalPoint));
                var monitorIndex = containingMonitor != null ? Array.IndexOf(_newMonitors, containingMonitor) : -1;
                var monitorScale = containingMonitor?.ScaleFactor ?? 1.0;

                // Comprehensive logging
                int processId = 0;
                if (XerahS.Platform.Abstractions.PlatformServices.IsInitialized)
                {
                    try { processId = (int)(XerahS.Platform.Abstractions.PlatformServices.Window?.GetWindowProcessId(window.Handle) ?? 0); }
                    catch { }
                }
                TroubleshootingHelper.LogWindowSelection("RegionCapture[NEW]",
                    window.Title ?? "",
                    processId,
                    new System.Drawing.Rectangle(window.Bounds.X, window.Bounds.Y, window.Bounds.Width, window.Bounds.Height),
                    RenderScaling,
                    logicalX, logicalY, logicalW, logicalH,
                    monitorIndex, monitorScale);

                // Update visuals
                UpdateSelectionVisuals(new SKRectI(window.Bounds.X, window.Bounds.Y, window.Bounds.X + window.Bounds.Width, window.Bounds.Y + window.Bounds.Height),
                                       new Rect(logicalX, logicalY, logicalW, logicalH));

                if (_infoText != null)
                {
                    _infoText.IsVisible = true;
                    var title = !string.IsNullOrEmpty(window.Title) ? window.Title + "\n" : "";
                    _infoText.Text = $"{title}X: {window.Bounds.X} Y: {window.Bounds.Y} W: {window.Bounds.Width} H: {window.Bounds.Height}";

                    Canvas.SetLeft(_infoText, logicalX);

                    var labelHeight = 45;
                    var topPadding = 5;
                    var labelY = logicalY - labelHeight - topPadding;
                    if (labelY < 5) labelY = 5;

                    Canvas.SetTop(_infoText, labelY);
                }
            }
            else if (window == null && _hoveredWindow != null)
            {
                // Lost window hover
                _hoveredWindow = null;
                CancelSelection();
            }
        }
    }
}
