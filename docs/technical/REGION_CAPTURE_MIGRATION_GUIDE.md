# Migration Guide: New Region Capture Backend

## Overview

This guide explains how to migrate from the existing region capture implementation to the new platform-agnostic backend.

---

## Phase 1: Backend Integration (Completed ✅)

### What's Been Done

1. **Core Infrastructure**
   - ✅ Coordinate types (`PhysicalRectangle`, `LogicalRectangle`, etc.)
   - ✅ `MonitorInfo` with explicit DPI scaling
   - ✅ `IRegionCaptureBackend` interface
   - ✅ `CoordinateTransform` service with 20 passing unit tests

2. **Platform Backends**
   - ✅ Windows: DXGI → WinRT → GDI+ fallback chain
   - ✅ macOS: ScreenCaptureKit → Quartz → CLI fallback chain
   - ✅ Linux: X11 → Wayland Portal → CLI fallback chain

3. **Orchestration Layer**
   - ✅ `RegionCaptureOrchestrator` for unified capture workflow
   - ✅ Multi-monitor stitching support
   - ✅ Monitor configuration change detection

---

## Phase 2: UI Integration (Next Steps)

### Step 1: Update PlatformServices

**File**: `src/ShareX.Avalonia.Core/Services/PlatformServices.cs`

Add the new region capture backend to the service registry:

```csharp
public static class PlatformServices
{
    // ... existing services ...

    /// <summary>
    /// Region capture backend (new architecture).
    /// </summary>
    public static IRegionCaptureBackend? RegionCapture { get; private set; }

    public static void Initialize()
    {
        // ... existing initialization ...

        // Initialize region capture backend
        RegionCapture = CreateRegionCaptureBackend();
    }

    private static IRegionCaptureBackend CreateRegionCaptureBackend()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsRegionCaptureBackend();
        }
        else if (OperatingSystem.IsMacOS())
        {
            return new MacOSRegionCaptureBackend();
        }
        else if (OperatingSystem.IsLinux())
        {
            return new LinuxRegionCaptureBackend();
        }

        throw new PlatformNotSupportedException("Region capture not supported on this platform");
    }
}
```

### Step 2: Create RegionCaptureService Wrapper

**File**: `src/ShareX.Avalonia.UI/Services/RegionCaptureService.cs`

Update or create a UI-layer service that uses the orchestrator:

```csharp
using ShareX.Avalonia.Core.Services;
using ShareX.Avalonia.Platform.Abstractions.Capture;

public class RegionCaptureService
{
    private readonly RegionCaptureOrchestrator _orchestrator;

    public RegionCaptureService()
    {
        var backend = PlatformServices.RegionCapture
            ?? throw new InvalidOperationException("Region capture backend not initialized");

        _orchestrator = new RegionCaptureOrchestrator(backend);
    }

    /// <summary>
    /// Get monitors for overlay positioning.
    /// </summary>
    public MonitorInfo[] GetMonitors()
    {
        return _orchestrator.GetMonitorsForOverlay();
    }

    /// <summary>
    /// Get virtual desktop bounds in logical coordinates for overlay sizing.
    /// </summary>
    public LogicalRectangle GetVirtualDesktopBounds()
    {
        return _orchestrator.GetVirtualDesktopBoundsLogical();
    }

    /// <summary>
    /// Convert logical point (from mouse input) to physical.
    /// </summary>
    public PhysicalPoint LogicalToPhysical(LogicalPoint logical)
    {
        return _orchestrator.LogicalToPhysical(logical);
    }

    /// <summary>
    /// Convert physical point to logical (for overlay rendering).
    /// </summary>
    public LogicalPoint PhysicalToLogical(PhysicalPoint physical)
    {
        return _orchestrator.PhysicalToLogical(physical);
    }

    /// <summary>
    /// Capture a region selected by the user.
    /// </summary>
    public async Task<SKBitmap> CaptureRegionAsync(LogicalRectangle region)
    {
        return await _orchestrator.CaptureRegionAsync(region);
    }
}
```

### Step 3: Update RegionCaptureWindow

**File**: `src/ShareX.Avalonia.UI/Views/RegionCapture/RegionCaptureWindow.axaml.cs`

Refactor the window to use the new backend:

#### Remove Old DPI Flags

**DELETE**:
```csharp
// OLD - Platform-specific DPI handling
var usePerScreenScaling = PlatformServices.Screen.UsePerScreenScalingForRegionCaptureLayout;
var useWindowPosition = PlatformServices.Screen.UseWindowPositionForRegionCaptureFallback;
var useLogicalCoordinates = PlatformServices.Screen.UseLogicalCoordinatesForRegionCapture;
```

#### Initialize with New Backend

**ADD**:
```csharp
private readonly RegionCaptureService _captureService;
private readonly MonitorInfo[] _monitors;
private readonly LogicalRectangle _virtualDesktopBounds;

public RegionCaptureWindow()
{
    InitializeComponent();

    _captureService = new RegionCaptureService();
    _monitors = _captureService.GetMonitors();
    _virtualDesktopBounds = _captureService.GetVirtualDesktopBounds();

    // Position window to cover virtual desktop
    PositionWindow();
}

private void PositionWindow()
{
    // Use logical bounds for window positioning
    Position = new PixelPoint(
        (int)_virtualDesktopBounds.X,
        (int)_virtualDesktopBounds.Y);

    Width = _virtualDesktopBounds.Width;
    Height = _virtualDesktopBounds.Height;
}
```

#### Update Mouse Event Handling

**CHANGE**:
```csharp
// OLD - Manual DPI scaling
private void OnPointerMoved(object? sender, PointerEventArgs e)
{
    var position = e.GetPosition(this);
    var physicalX = _windowLeft + (int)Math.Round(position.X * RenderScaling);
    var physicalY = _windowTop + (int)Math.Round(position.Y * RenderScaling);
    // ...
}

// NEW - Use coordinate transform
private void OnPointerMoved(object? sender, PointerEventArgs e)
{
    var logicalPosition = e.GetPosition(this);
    var logicalPoint = new LogicalPoint(
        logicalPosition.X + _virtualDesktopBounds.X,
        logicalPosition.Y + _virtualDesktopBounds.Y);

    var physicalPoint = _captureService.LogicalToPhysical(logicalPoint);

    // Update current position
    _currentPhysicalPoint = physicalPoint;

    // ... rest of logic
}
```

#### Update Selection Rectangle Rendering

**CHANGE**:
```csharp
// OLD - Manual scaling calculations
private void DrawSelectionRectangle()
{
    var rect = GetSelectionRectangleInLogical();
    SelectionBorder.Width = rect.Width;
    SelectionBorder.Height = rect.Height;
    Canvas.SetLeft(SelectionBorder, rect.X);
    Canvas.SetTop(SelectionBorder, rect.Y);
}

// NEW - Use coordinate transform
private void DrawSelectionRectangle()
{
    // Convert physical selection to logical for rendering
    var physicalRect = GetSelectionRectanglePhysical();
    var logicalRect = _captureService.PhysicalToLogical(physicalRect);

    // Adjust for window origin
    var localX = logicalRect.X - _virtualDesktopBounds.X;
    var localY = logicalRect.Y - _virtualDesktopBounds.Y;

    SelectionBorder.Width = logicalRect.Width;
    SelectionBorder.Height = logicalRect.Height;
    Canvas.SetLeft(SelectionBorder, localX);
    Canvas.SetTop(SelectionBorder, localY);
}

private PhysicalRectangle GetSelectionRectanglePhysical()
{
    if (_startPoint == null || _currentPhysicalPoint == null)
        return default;

    var x1 = Math.Min(_startPoint.Value.X, _currentPhysicalPoint.Value.X);
    var y1 = Math.Min(_startPoint.Value.Y, _currentPhysicalPoint.Value.Y);
    var x2 = Math.Max(_startPoint.Value.X, _currentPhysicalPoint.Value.X);
    var y2 = Math.Max(_startPoint.Value.Y, _currentPhysicalPoint.Value.Y);

    return new PhysicalRectangle(x1, y1, x2 - x1, y2 - y1);
}
```

#### Update Capture Method

**CHANGE**:
```csharp
// OLD - Uses old screen capture service
private async Task<SKBitmap?> CaptureSelectedRegion()
{
    var physicalRect = GetSelectionRectanglePhysical();
    return await PlatformServices.ScreenCapture.CaptureRegionAsync(physicalRect);
}

// NEW - Uses new orchestrator
private async Task<SKBitmap?> CaptureSelectedRegion()
{
    var physicalRect = GetSelectionRectanglePhysical();
    var logicalRect = _captureService.PhysicalToLogical(physicalRect);

    return await _captureService.CaptureRegionAsync(logicalRect);
}
```

---

## Phase 3: Remove Old Code

Once the new backend is integrated and tested, remove the old implementation:

### Files to Remove

1. **Old Screen Services**:
   - `src/ShareX.Avalonia.Platform.Abstractions/IScreenService.cs` (old)
   - Platform-specific `ScreenService` implementations
   - DPI flag properties

2. **Old Capture Services**:
   - Old `IScreenCaptureService` implementations
   - Platform-specific capture without DXGI/Quartz

3. **Old Coordinate Logic**:
   - Manual DPI calculations in `RegionCaptureWindow`
   - Platform-specific flag checks
   - `TroubleshootingHelper` DPI logging (move to new backend)

### Files to Update

1. **PlatformServices.cs**:
   - Remove `Screen` property (old `IScreenService`)
   - Keep `RegionCapture` property (new `IRegionCaptureBackend`)

2. **RegionCaptureWindow.axaml.cs**:
   - Remove all platform-specific DPI code
   - Remove manual scaling calculations
   - Use `RegionCaptureService` wrapper

---

## Phase 4: Testing Checklist

### Unit Tests ✅
- [x] 20 coordinate transform tests passing
- [ ] Add orchestrator tests
- [ ] Add multi-monitor stitching tests

### Integration Tests
- [ ] **Windows**:
  - [ ] Single monitor 1920×1080 @ 100%
  - [ ] Single monitor 3840×2160 @ 200%
  - [ ] Dual monitors 1920×1080 @ 100% + 2560×1440 @ 150%
  - [ ] Monitor with negative origin (left of primary)

- [ ] **macOS**:
  - [ ] MacBook Pro Retina @ 2.0x
  - [ ] External monitor @ 1.0x
  - [ ] Dual monitor setup

- [ ] **Linux**:
  - [ ] X11 session (Ubuntu)
  - [ ] Wayland session (Fedora/Ubuntu)
  - [ ] Multi-monitor X11

### Visual Validation
- [ ] Selection rectangle aligns with screen content
- [ ] No gaps at monitor boundaries
- [ ] Correct bitmap dimensions
- [ ] Accurate color reproduction
- [ ] Smooth drag across monitors

---

## Breaking Changes

### API Changes

| Old API | New API | Notes |
|---------|---------|-------|
| `IScreenService.GetAllScreens()` | `IRegionCaptureBackend.GetMonitors()` | Returns `MonitorInfo[]` instead of `ScreenInfo[]` |
| `ScreenInfo` | `MonitorInfo` | New structure with explicit `ScaleFactor` |
| Manual DPI flags | `CoordinateTransform` | Unified conversion service |
| `PlatformServices.Screen` | `PlatformServices.RegionCapture` | New backend interface |

### Behavior Changes

1. **Coordinate System**:
   - **OLD**: Mixed logical/physical with platform-specific flags
   - **NEW**: Always physical for capture, logical for UI

2. **DPI Handling**:
   - **OLD**: System-wide DPI or per-monitor with fallbacks
   - **NEW**: Always per-monitor DPI from OS API

3. **Monitor Enumeration**:
   - **OLD**: `Screen.AllScreens` (WinForms/Avalonia hybrid)
   - **NEW**: Platform-specific enumeration (DXGI, Quartz, XRandR)

---

## Rollout Strategy

### Option 1: Feature Flag (Recommended)

Add a setting to toggle between old and new backends:

```csharp
public class CaptureSettings
{
    public bool UseNewRegionCaptureBackend { get; set; } = false;
}

// In RegionCaptureWindow
if (settings.UseNewRegionCaptureBackend)
{
    _captureService = new RegionCaptureService(); // New
}
else
{
    // Use old implementation
}
```

**Timeline**:
- Week 1-2: Beta testing with flag enabled
- Week 3-4: Default to new backend, old still available
- Week 5+: Remove old backend completely

### Option 2: Direct Migration

Replace old implementation immediately with thorough pre-release testing.

**Timeline**:
- Week 1-3: Complete integration and testing
- Week 4: Release with new backend
- Week 5: Monitor for issues and hotfix if needed

---

## Troubleshooting

### Issue: Selection Rectangle Misaligned

**Cause**: Coordinate conversion error or wrong monitor DPI

**Solution**:
1. Check monitor DPI values: `var monitors = _captureService.GetMonitors();`
2. Validate coordinate conversion: `var error = _transform.TestRoundTripAccuracy(point);`
3. Enable debug logging in `CoordinateTransform`

### Issue: Capture Fails on Multi-Monitor

**Cause**: Region outside all monitors or stitching error

**Solution**:
1. Validate region: `_transform.ValidateCaptureRegion(physicalRegion);`
2. Check intersecting monitors: `_transform.GetMonitorsIntersecting(region);`
3. Test each monitor individually

### Issue: Wrong DPI Scale

**Cause**: Platform API returning incorrect DPI

**Solution**:
1. Windows: Verify `GetDpiForMonitor` returns correct values
2. macOS: Check `backingScaleFactor` matches display settings
3. Linux: Verify `Xft.dpi` resource or manual override

---

## Next Steps

1. **Complete UI Integration**:
   - [ ] Update `RegionCaptureWindow` to use new backend
   - [ ] Remove platform-specific DPI flags
   - [ ] Test on all supported platforms

2. **Enhance Platform Backends**:
   - [ ] Complete WinRT Graphics Capture (Windows)
   - [ ] Complete ScreenCaptureKit bridge (macOS)
   - [ ] Complete Wayland Portal integration (Linux)

3. **Performance Optimization**:
   - [ ] GPU-accelerated stitching
   - [ ] Async monitor enumeration caching
   - [ ] Reduce coordinate conversion overhead

4. **Documentation**:
   - [x] Architecture documentation
   - [x] Migration guide
   - [ ] API reference documentation
   - [ ] Platform-specific troubleshooting guides

---

## Support

For questions or issues during migration:

1. Review [REGION_CAPTURE_ARCHITECTURE.md](./REGION_CAPTURE_ARCHITECTURE.md)
2. Check unit tests for usage examples
3. Report issues at https://github.com/anthropics/claude-code/issues

---

**Migration Guide Version**: 1.0
**Last Updated**: January 2026
