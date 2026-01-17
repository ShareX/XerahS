# Troubleshooting: No Image After Region Selection

## Issue Description

After selecting a region with the dashed rectangle, no image appears in the Editor view.

## Root Cause

The new backend is currently only handling **UI selection** (the rectangle selection interface), but the actual **screen capture** is still being performed by the old platform implementation (`PlatformServices.ScreenCapture.CaptureRectAsync()`).

The new backend has its own capture capability (`RegionCaptureService.CaptureRegionAsync()`), but it's not being used in the current workflow.

## Architecture Gap

**Current Flow:**
```
1. RegionCaptureWindow (NEW backend) → User selects region → Returns SKRectI
2. ScreenCaptureService.CaptureRegionWithUIAsync() → Receives SKRectI
3. Calls _platformImpl.CaptureRectAsync(skRect) → OLD capture method
4. OLD method may have compatibility issues with coordinates from NEW backend
```

**Intended Flow:**
```
1. RegionCaptureWindow (NEW backend) → User selects region → Returns SKRectI
2. ScreenCaptureService → Calls NEW backend's CaptureRegionAsync()
3. NEW backend uses DXGI/Quartz/X11 for hardware-accelerated capture
4. Returns SKBitmap directly
```

## Temporary Workarounds

### Option 1: Disable New Backend (Quick Fix)

Set `USE_NEW_BACKEND = false` in `RegionCaptureWindowNew.cs`:

```csharp
// Line 27 in RegionCaptureWindowNew.cs
private const bool USE_NEW_BACKEND = false; // Temporarily disable
```

This will revert to the fully working legacy code path.

### Option 2: Fix Coordinate Compatibility (Diagnostic)

The old capture method might be having issues with the coordinates. Add logging to see what's being captured:

In `ScreenCaptureService.cs` around line 103:
```csharp
TroubleshootingHelper.Log("RegionCapture", "DEBUG", $"Calling CaptureRectAsync with: {skRect}");
TroubleshootingHelper.Log("RegionCapture", "DEBUG", $"  Left={selection.Left}, Top={selection.Top}");
TroubleshootingHelper.Log("RegionCapture", "DEBUG", $"  Right={selection.Right}, Bottom={selection.Bottom}");
TroubleshootingHelper.Log("RegionCapture", "DEBUG", $"  Width={selection.Width}, Height={selection.Height}");
```

Check the debug output to see if coordinates are correct.

## Permanent Solution

Integrate the new backend's capture method into `ScreenCaptureService.CaptureRegionWithUIAsync()`:

```csharp
// In ScreenCaptureService.cs
public async Task<SKBitmap?> CaptureRegionWithUIAsync(CaptureOptions? options = null)
{
    SKRectI selection = SKRectI.Empty;
    var regionStopwatch = Stopwatch.StartNew();

    await Dispatcher.UIThread.InvokeAsync(async () =>
    {
        var window = new RegionCaptureWindow();
        window.Show();
        selection = await window.GetResultAsync();
    });

    if (selection.IsEmpty || selection.Width <= 0 || selection.Height <= 0)
    {
        TroubleshootingHelper.Log("RegionCapture", "CANCEL", "Selection cancelled");
        return null;
    }

    // Small delay to allow window to close
    await Task.Delay(200);

    // NEW: Check if new backend is available
    if (RegionCaptureWindow.HasNewBackend) // Would need to add this property
    {
        using var backend = CreatePlatformBackend();
        using var service = new ShareX.Avalonia.UI.Services.RegionCaptureService(backend);

        var physicalRect = new PhysicalRectangle(
            selection.Left, selection.Top,
            selection.Width, selection.Height);

        var captured = await service.CaptureRegionAsync(physicalRect, options);
        return captured;
    }
    else
    {
        // Fallback to old method
        var skRect = new SKRect(selection.Left, selection.Top, selection.Right, selection.Bottom);
        return await _platformImpl.CaptureRectAsync(skRect, options);
    }
}
```

## Current Status

**What Works:**
- ✅ New backend UI selection (rectangle drawing, window detection)
- ✅ Per-monitor DPI coordinate conversion
- ✅ Hardware-accelerated capture (DXGI on Windows) - **not being used**
- ✅ Returns correct selection coordinates

**What Doesn't Work:**
- ❌ Old capture method may not handle coordinates correctly
- ❌ New backend's capture capability is not integrated into the workflow
- ❌ No image appears in Editor after selection

## Recommended Action

The quickest fix is **Option 1** (disable new backend temporarily) while we implement the permanent solution to properly integrate the new backend's capture method into the `ScreenCaptureService` workflow.

The new backend is 99% complete - it just needs to be wired up to actually use its capture functionality instead of delegating to the old platform implementation.

---

**Status**: Known Issue - Integration Incomplete
**Impact**: High - Capture doesn't work with new backend enabled
**Priority**: Critical
**Estimated Fix Time**: 30-60 minutes
