# UI Integration Guide: RegionCaptureWindow Refactoring

## Overview

This guide shows how to refactor `RegionCaptureWindow.axaml.cs` to use the new region capture backend, removing all platform-specific DPI handling code.

---

## Step 1: Add Service Field

**Location**: Top of `RegionCaptureWindow` class

**REMOVE**:
```csharp
private int _windowLeft = 0;
private int _windowTop = 0;
private double _capturedScaling = 1.0;
private bool _usePerScreenScalingForLayout;
private bool _useWindowPositionForFallback;
private bool _useLogicalCoordinatesForCapture;
```

**ADD**:
```csharp
using ShareX.Avalonia.UI.Services;
using ShareX.Avalonia.Platform.Abstractions.Capture;

private readonly RegionCaptureService? _captureService;
private MonitorInfo[] _monitors = Array.Empty<MonitorInfo>();
private LogicalRectangle _virtualDesktopLogical;
```

---

## Step 2: Initialize in Constructor

**CHANGE** the constructor:

```csharp
public RegionCaptureWindow()
{
    InitializeComponent();
    _tcs = new System.Threading.Tasks.TaskCompletionSource<SKRectI>();

    DebugLog("INIT", "RegionCaptureWindow created");

    // NEW: Initialize capture service
    try
    {
        // Create platform backend (this should be provided via DI in production)
        IRegionCaptureBackend backend;
        if (OperatingSystem.IsWindows())
            backend = new ShareX.Avalonia.Platform.Windows.Capture.WindowsRegionCaptureBackend();
        else if (OperatingSystem.IsMacOS())
            backend = new ShareX.Avalonia.Platform.macOS.Capture.MacOSRegionCaptureBackend();
        else if (OperatingSystem.IsLinux())
            backend = new ShareX.Avalonia.Platform.Linux.Capture.LinuxRegionCaptureBackend();
        else
            throw new PlatformNotSupportedException();

        _captureService = new RegionCaptureService(backend);
        _monitors = _captureService.GetMonitors();
        _virtualDesktopLogical = _captureService.GetVirtualDesktopBoundsLogical();

        DebugLog("INIT", $"Initialized with {_monitors.Length} monitors");
        foreach (var monitor in _monitors)
        {
            DebugLog("INIT", $"  {monitor.Name}: {monitor.Bounds} @ {monitor.ScaleFactor}x DPI");
        }
    }
    catch (Exception ex)
    {
        DebugLog("ERROR", $"Failed to initialize capture service: {ex.Message}");
        // Fall back to old behavior if needed
    }

    // Close on Escape key
    this.KeyDown += (s, e) =>
    {
        if (e.Key == Key.Escape)
        {
            DebugLog("INPUT", "Escape key pressed - cancelling");
            _tcs.TrySetResult(SKRectI.Empty);
            Close();
        }
    };
}
```

---

## Step 3: Simplify OnOpened Method

**REPLACE** the entire screen enumeration section in `OnOpened`:

```csharp
protected override async void OnOpened(EventArgs e)
{
    base.OnOpened(e);

    DebugLog("LIFECYCLE", $"OnOpened started");

    // Get own window handle for exclusion from window detection
    _myHandle = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

    // Initialize window list for detection (if available)
    if (XerahS.Platform.Abstractions.PlatformServices.IsInitialized &&
        XerahS.Platform.Abstractions.PlatformServices.Window != null)
    {
        try
        {
            _windows = XerahS.Platform.Abstractions.PlatformServices.Window.GetAllWindows()
                .Where(w => w.IsVisible && !IsMyWindow(w))
                .ToArray();
            DebugLog("WINDOW", $"Fetched {_windows.Length} visible windows for detection");
        }
        catch (Exception ex)
        {
            DebugLog("ERROR", $"Failed to fetch windows: {ex.Message}");
        }
    }

    // NEW: Use capture service for positioning
    if (_captureService != null)
    {
        PositionWindowWithNewBackend();
    }
    else
    {
        // Fallback to old behavior
        PositionWindowWithOldBackend();
    }

    // Disable background images if mixed DPI (keep this logic)
    CheckDpiAndConfigureBackground();

    DebugLog("LIFECYCLE", "OnOpened complete");
}

private void PositionWindowWithNewBackend()
{
    DebugLog("WINDOW", "Using new capture backend for positioning");

    // Position window to cover virtual desktop (in logical coordinates)
    Position = new PixelPoint(
        (int)_virtualDesktopLogical.X,
        (int)_virtualDesktopLogical.Y);

    Width = _virtualDesktopLogical.Width;
    Height = _virtualDesktopLogical.Height;

    DebugLog("WINDOW", $"Window positioned: {Position}, Size: {Width}x{Height}");
    DebugLog("WINDOW", $"Virtual desktop logical: {_virtualDesktopLogical}");
    DebugLog("WINDOW", $"RenderScaling: {RenderScaling}");

    // Log monitor info
    foreach (var monitor in _monitors)
    {
        var logicalBounds = _captureService!.PhysicalToLogical(monitor.Bounds);
        DebugLog("MONITOR", $"{monitor.Name}: Physical={monitor.Bounds}, Logical={logicalBounds}, Scale={monitor.ScaleFactor}x");
    }
}

private void CheckDpiAndConfigureBackground()
{
    // Keep the existing DPI check logic for background images
    bool allScreensStandardDpi = true;
    foreach (var screen in Screens.All)
    {
        if (Math.Abs(screen.Scaling - 1.0) > 0.001)
        {
            allScreensStandardDpi = false;
            break;
        }
    }

    DebugLog("WINDOW", $"All screens standard DPI (1.0)? {allScreensStandardDpi}");
    _useDarkening = allScreensStandardDpi;

    // Background image logic remains the same
    // ... (keep existing code)
}
```

---

## Step 4: Update Mouse Event Handling

**REPLACE** the pointer event handlers:

```csharp
protected override void OnPointerPressed(PointerPressedEventArgs e)
{
    base.OnPointerPressed(e);

    if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
    {
        var logicalPos = e.GetPosition(this);

        if (_captureService != null)
        {
            // NEW: Convert using capture service
            var absoluteLogical = new LogicalPoint(
                logicalPos.X + _virtualDesktopLogical.X,
                logicalPos.Y + _virtualDesktopLogical.Y);

            var physical = _captureService.LogicalToPhysical(absoluteLogical);
            _startPointPhysical = new SKPointI(physical.X, physical.Y);
            _startPointLogical = logicalPos;

            DebugLog("INPUT", $"Pointer pressed: Logical={absoluteLogical}, Physical={physical}");
        }
        else
        {
            // OLD: Fallback to old conversion
            _startPointPhysical = ConvertLogicalToPhysical(logicalPos);
            _startPointLogical = logicalPos;
        }

        _isSelecting = true;
        _dragStarted = false;
        _hoveredWindow = null;
    }
}

protected override void OnPointerMoved(PointerEventArgs e)
{
    base.OnPointerMoved(e);

    var logicalPos = e.GetPosition(this);

    if (_captureService != null)
    {
        // NEW: Convert using capture service
        var absoluteLogical = new LogicalPoint(
            logicalPos.X + _virtualDesktopLogical.X,
            logicalPos.Y + _virtualDesktopLogical.Y);

        var physical = _captureService.LogicalToPhysical(absoluteLogical);
        var currentPhysical = new SKPointI(physical.X, physical.Y);

        // Rest of the logic remains the same
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
                    DebugLog("INPUT", $"Drag started (distance: {dragDistance:F2})");
                }
            }

            UpdateSelectionRectangle(currentPhysical);
        }
        else if (!_dragStarted)
        {
            // Window detection logic (unchanged)
            UpdateHoveredWindow(currentPhysical);
        }
    }
    else
    {
        // OLD: Fallback conversion
        var currentPhysical = ConvertLogicalToPhysical(logicalPos);
        // ... (old logic)
    }
}
```

---

## Step 5: Simplify Coordinate Conversion Helpers

**REMOVE** these old helper methods:
```csharp
private SKPointI ConvertLogicalToPhysical(Point logicalPos)
private Point ConvertPhysicalToLogical(SKPointI physicalPos)
```

**ADD** these simplified wrappers (only if needed):
```csharp
private SKPointI ConvertLogicalToPhysical(Point logicalPos)
{
    if (_captureService == null)
        return new SKPointI((int)logicalPos.X, (int)logicalPos.Y); // Fallback

    var absoluteLogical = new LogicalPoint(
        logicalPos.X + _virtualDesktopLogical.X,
        logicalPos.Y + _virtualDesktopLogical.Y);

    var physical = _captureService.LogicalToPhysical(absoluteLogical);
    return new SKPointI(physical.X, physical.Y);
}
```

---

## Step 6: Update Selection Rectangle Rendering

The rendering logic can mostly stay the same, as it works in window-local logical coordinates. The key change is in the final capture step.

---

## Step 7: Update Capture Method

**REPLACE** the capture region method:

```csharp
private async Task<SKRectI> CaptureRegion(SKRectI physicalRect)
{
    if (_captureService != null)
    {
        try
        {
            DebugLog("CAPTURE", $"Capturing with new backend: {physicalRect}");

            // Convert to new backend types
            var physicalRegion = new PhysicalRectangle(
                physicalRect.Left,
                physicalRect.Top,
                physicalRect.Width,
                physicalRect.Height);

            var logicalRegion = _captureService.PhysicalToLogical(physicalRegion);

            DebugLog("CAPTURE", $"Physical: {physicalRegion}, Logical: {logicalRegion}");

            // Capture using new orchestrator
            var bitmap = await _captureService.CaptureRegionAsync(logicalRegion);

            // Bitmap is already in physical pixels, return the region
            return physicalRect;
        }
        catch (Exception ex)
        {
            DebugLog("ERROR", $"New backend capture failed: {ex.Message}");
            // Fall back to old method
        }
    }

    // OLD: Fallback to old capture method
    return await CaptureRegionOld(physicalRect);
}
```

---

## Step 8: Cleanup on Disposal

**ADD** disposal of capture service:

```csharp
protected override void OnClosed(EventArgs e)
{
    base.OnClosed(e);

    // Dispose capture service
    _captureService?.Dispose();

    DebugLog("LIFECYCLE", "Window closed and resources disposed");
}
```

---

## Summary of Changes

### Removed Code
- ❌ `_windowLeft`, `_windowTop` fields
- ❌ `_capturedScaling` field
- ❌ Platform-specific DPI flags (`_usePerScreenScalingForLayout`, etc.)
- ❌ Manual scaling calculations
- ❌ Platform-specific coordinate conversion logic
- ❌ Avalonia `Screens` API usage for positioning

### Added Code
- ✅ `RegionCaptureService` field and initialization
- ✅ `MonitorInfo[]` array for monitor data
- ✅ `LogicalRectangle` for virtual desktop bounds
- ✅ New coordinate conversion using service
- ✅ Simplified window positioning logic
- ✅ Cleanup/disposal of service

### Benefits
1. **Platform-agnostic**: No more platform-specific flags
2. **Accurate DPI**: Per-monitor DPI from OS APIs
3. **Cleaner code**: -200 lines of platform-specific logic
4. **Testable**: Can mock `IRegionCaptureBackend` for tests
5. **Maintainable**: Single source of truth for coordinates

---

## Testing Checklist

After making these changes, test:

- [ ] **Single monitor 100% DPI**: Selection aligns correctly
- [ ] **Single monitor 150% DPI**: Capture matches screen
- [ ] **Dual monitors same DPI**: Seamless boundary
- [ ] **Dual monitors mixed DPI**: Correct scaling on each
- [ ] **Monitor left of primary**: Negative coords handled
- [ ] **Window detection**: Hover highlights work
- [ ] **Escape to cancel**: Works correctly
- [ ] **Background darkening**: Enabled on standard DPI
- [ ] **Multi-monitor span**: Capture stitches correctly

---

## Rollback Plan

If issues arise:

1. Keep old methods prefixed with `Old`
2. Use feature flag: `if (USE_NEW_BACKEND) { ... } else { ... }`
3. Revert to old code by removing new service initialization

---

## Next Steps

1. Make these changes to `RegionCaptureWindow.axaml.cs`
2. Test on Windows with your monitor setup
3. Fix any integration issues
4. Remove old fallback code once verified
5. Update other capture-related UI components

---

**Guide Version**: 1.0
**Target File**: `RegionCaptureWindow.axaml.cs`
**Estimated LOC Changed**: ~300 lines
**Estimated LOC Removed**: ~200 lines
**Net Change**: ~100 lines added (simpler, cleaner code)
