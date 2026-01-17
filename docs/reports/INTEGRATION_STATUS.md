# Region Capture Backend Integration Status

## Overview

The new region capture backend has been successfully integrated into the UI layer with a feature flag approach. The core architecture is complete, but the platform-specific backends require API compatibility fixes before the new system can be enabled.

## Current Status

### ✅ Completed

1. **Core Architecture** (Phase 1-3)
   - Coordinate system types (PhysicalRectangle, LogicalRectangle, etc.)
   - CoordinateTransform service with per-monitor DPI handling
   - IRegionCaptureBackend interface
   - RegionCaptureOrchestrator for multi-monitor stitching
   - Unit tests: 20/20 passing ✅

2. **Platform Backend Structure** (Phase 4)
   - Windows backend with DXGI, GDI+, WinRT strategies
   - macOS backend with Quartz, ScreenCaptureKit, CLI strategies
   - Linux backend with X11, Wayland Portal, CLI strategies
   - Backend capability detection
   - Monitor configuration change events

3. **UI Integration** (Phase 5)
   - [RegionCaptureService.cs](../src/ShareX.Avalonia.UI/Services/RegionCaptureService.cs) - UI-layer wrapper
   - [RegionCaptureWindowNew.cs](../src/ShareX.Avalonia.UI/Views/RegionCapture/RegionCaptureWindowNew.cs) - New backend methods as partial class
   - [RegionCaptureWindow.axaml.cs](../src/ShareX.Avalonia.UI/Views/RegionCapture/RegionCaptureWindow.axaml.cs) - Modified to delegate to new backend when enabled
   - Feature flag: `USE_NEW_BACKEND` constant (currently `false`)
   - Cleanup/disposal in `OnClosed()`

4. **Documentation** (Phase 5)
   - [REGION_CAPTURE_ARCHITECTURE.md](REGION_CAPTURE_ARCHITECTURE.md) - Complete technical architecture
   - [UI_INTEGRATION_GUIDE.md](technical/UI_INTEGRATION_GUIDE.md) - Step-by-step integration instructions
   - [MIGRATION_GUIDE.md](MIGRATION_GUIDE.md) - Migration phases and strategies
   - [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) - Project status and metrics
   - [README_REGION_CAPTURE.md](README_REGION_CAPTURE.md) - Main documentation hub

### ⚠️ Pending

**Platform Backend Compilation Issues**

The platform-specific backends have been created but require API compatibility fixes:

1. **Vortice.DXGI API Changes**
   - File: [DxgiCaptureStrategy.cs](../src/ShareX.Avalonia.Platform.Windows/Capture/DxgiCaptureStrategy.cs)
   - Issue: Type naming and API signatures changed between Vortice versions
   - Errors:
     - `D3D11.D3D11CreateDevice()` signature mismatch
     - `IDXGIOutputDuplication.TryAcquireNextFrame()` method not found
     - Type conversion issues (int → uint)
   - Required: Update to match Vortice.Direct3D11 v3.7.0 and Vortice.DXGI v3.7.0 APIs

2. **Project References**
   - Platform backend project references are commented out in [ShareX.Avalonia.UI.csproj](../src/ShareX.Avalonia.UI/ShareX.Avalonia.UI.csproj)
   - Uncomment when backends compile successfully

## Build Status

- **ShareX.Avalonia.Core**: ✅ Builds successfully
- **ShareX.Avalonia.UI**: ✅ Builds successfully (37 warnings, 0 errors)
- **ShareX.Avalonia.Platform.Windows**: ❌ Compilation errors (DXGI API compatibility)
- **ShareX.Avalonia.Platform.macOS**: ⚠️ Not tested (macOS-specific P/Invoke)
- **ShareX.Avalonia.Platform.Linux**: ⚠️ Not tested (X11/Wayland P/Invoke)

## Integration Details

### Feature Flag Approach

The integration uses a feature flag pattern to allow the new backend to coexist with the legacy code:

```csharp
// In RegionCaptureWindowNew.cs
private const bool USE_NEW_BACKEND = false; // Currently disabled
```

When `USE_NEW_BACKEND = true`:
1. `TryInitializeNewBackend()` creates platform-specific backend in constructor
2. `PositionWindowWithNewBackend()` is called in `OnOpened()`
3. Mouse events delegate to new methods:
   - `OnPointerPressedNew()`
   - `OnPointerMovedNew()`
   - `OnPointerReleasedNew()`
4. New coordinate conversion methods are used:
   - `ConvertLogicalToPhysicalNew()`
   - `ConvertPhysicalToLogicalNew()`

When `USE_NEW_BACKEND = false` (current state):
- Legacy code path is used
- New backend code compiles but doesn't execute
- No behavioral changes

### Files Modified

1. **[RegionCaptureWindow.axaml.cs](../src/ShareX.Avalonia.UI/Views/RegionCapture/RegionCaptureWindow.axaml.cs)**
   - Added backend initialization call in constructor (lines 93-101)
   - Added backend check in `OnOpened()` (lines 202-208)
   - Added delegation in `OnPointerPressed()` (lines 627-632)
   - Added delegation in `OnPointerMoved()` (lines 687-692)
   - Added delegation in `OnPointerReleased()` (lines 738-743)
   - Added `OnClosed()` for cleanup (lines 1137-1143)
   - Fixed duplicate code block (removed lines 922-948)

2. **[RegionCaptureWindowNew.cs](../src/ShareX.Avalonia.UI/Views/RegionCapture/RegionCaptureWindowNew.cs)**
   - Created as partial class with new backend methods
   - Platform backend initialization commented out until compilation issues resolved
   - All new coordinate conversion and event handling methods ready to use

3. **[ShareX.Avalonia.UI.csproj](../src/ShareX.Avalonia.UI/ShareX.Avalonia.UI.csproj)**
   - Platform backend project references commented out (lines 20-24)

## Next Steps

### Immediate (Before Enabling New Backend)

1. **Fix Vortice.DXGI API Compatibility**
   - Update `DxgiCaptureStrategy.cs` to match Vortice v3.7.0 API
   - Reference Vortice documentation: https://github.com/amerkoleci/Vortice.Windows
   - Key changes needed:
     ```csharp
     // Old (incorrect)
     var device = D3D11.D3D11CreateDevice(adapter, DriverType.Unknown, ...);

     // New (correct) - needs verification
     var result = D3D11.D3D11CreateDevice(null, DriverType.Hardware,
         DeviceCreationFlags.None, featureLevels, out var device);
     ```

2. **Test Platform Backends**
   - Windows: Fix DXGI errors, test on Windows 10/11
   - macOS: Test Quartz strategy on macOS with Retina display
   - Linux: Test X11 strategy on Ubuntu/Fedora

3. **Uncomment Project References**
   - Restore platform backend references in ShareX.Avalonia.UI.csproj
   - Set `USE_NEW_BACKEND = true` in RegionCaptureWindowNew.cs

### Testing Phase

Once backends compile:

1. **Single Monitor Tests**
   - 100% DPI (96 DPI)
   - 125% DPI (120 DPI)
   - 150% DPI (144 DPI)
   - 200% DPI (192 DPI)

2. **Multi-Monitor Tests**
   - Same DPI (100% + 100%)
   - Mixed DPI (100% + 150%)
   - Negative origin (monitor left of primary)
   - Vertical arrangement
   - Three+ monitors

3. **Edge Cases**
   - Selection spanning multiple monitors
   - Window detection on high-DPI monitors
   - Monitor hotplug/unplug
   - DPI change while window is open

### Future Work (Phase 6)

1. **Complete Stub Implementations**
   - Windows WinRT Graphics Capture (requires WinRT SDK)
   - macOS ScreenCaptureKit (requires Objective-C bridge)
   - Linux Wayland Portal (requires D-Bus integration)

2. **Performance Optimization**
   - Hardware acceleration validation
   - Parallel capture benchmarking
   - Memory usage profiling

3. **Remove Legacy Code**
   - Once new backend is stable, remove old DPI handling code
   - Clean up feature flags
   - Update documentation

## Known Issues

1. **DXGI Capture Strategy**
   - Vortice API compatibility issues
   - Needs update to match v3.7.0 signatures

2. **Unreachable Code Warning**
   - `RegionCaptureWindowNew.cs:38` - Expected due to `USE_NEW_BACKEND = false`
   - Will resolve when backend is enabled

3. **Unused Field Warning**
   - `_newVirtualDesktopLogical` never assigned
   - Will resolve when backend initialization is uncommented

## Contact

For questions about this integration:
- Architecture decisions: See [REGION_CAPTURE_ARCHITECTURE.md](REGION_CAPTURE_ARCHITECTURE.md)
- Integration steps: See [UI_INTEGRATION_GUIDE.md](technical/UI_INTEGRATION_GUIDE.md)
- Migration strategy: See [MIGRATION_GUIDE.md](MIGRATION_GUIDE.md)

---

**Last Updated**: 2026-01-10
**Status**: Phase 5 Complete (UI Integration), Phase 6 Pending (Platform Backend Fixes)
