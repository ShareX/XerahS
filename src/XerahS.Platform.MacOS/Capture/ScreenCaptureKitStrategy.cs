using System;
using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions.Capture;

namespace ShareX.Avalonia.Platform.macOS.Capture;

/// <summary>
/// Capture strategy using ScreenCaptureKit framework (macOS 12.3+).
/// Modern API with HDR support, per-window exclusion, and better performance.
/// Requires native Objective-C bridge implementation.
/// </summary>
internal sealed class ScreenCaptureKitStrategy : ICaptureStrategy
{
    public string Name => "ScreenCaptureKit";

    public static bool IsSupported()
    {
        // macOS 12.3+ (Monterey)
        return OperatingSystem.IsMacOSVersionAtLeast(12, 3);
    }

    public MonitorInfo[] GetMonitors()
    {
        // TODO: Implement native ScreenCaptureKit monitor enumeration
        // This requires an Objective-C bridge (libscreencapturekit_bridge.dylib)
        // For now, fall back to Quartz strategy
        var quartzStrategy = new QuartzCaptureStrategy();
        return quartzStrategy.GetMonitors();
    }

    public Task<CapturedBitmap> CaptureRegionAsync(
        PhysicalRectangle physicalRegion,
        RegionCaptureOptions options)
    {
        // TODO: Implement native ScreenCaptureKit capture
        // This requires:
        // 1. SCShareableContent query for displays
        // 2. SCStreamConfiguration setup
        // 3. SCStream creation and frame capture
        // 4. CVPixelBuffer → SKBitmap conversion
        //
        // For now, fall back to Quartz strategy
        var quartzStrategy = new QuartzCaptureStrategy();
        return quartzStrategy.CaptureRegionAsync(physicalRegion, options);
    }

    public BackendCapabilities GetCapabilities()
    {
        return new BackendCapabilities
        {
            BackendName = "ScreenCaptureKit",
            Version = "12.3+",
            SupportsHardwareAcceleration = true,
            SupportsCursorCapture = true,
            SupportsHDR = true,
            SupportsPerMonitorDpi = true,
            SupportsMonitorHotplug = true,
            MaxCaptureResolution = 16384,
            RequiresPermission = true
        };
    }

    public void Dispose()
    {
        // TODO: Cleanup native resources
    }
}
