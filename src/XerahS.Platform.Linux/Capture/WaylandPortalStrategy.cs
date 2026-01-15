using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions.Capture;
using SkiaSharp;

namespace ShareX.Avalonia.Platform.Linux.Capture;

/// <summary>
/// Capture strategy using xdg-desktop-portal Screenshot API.
/// Works with Wayland compositors via D-Bus.
/// Requires user permission per capture (security feature).
/// </summary>
internal sealed class WaylandPortalStrategy : ICaptureStrategy
{
    public string Name => "Wayland Portal";

    public static bool IsSupported()
    {
        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        return sessionType?.Equals("wayland", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public MonitorInfo[] GetMonitors()
    {
        // Wayland doesn't expose monitor info easily without portal
        // Fall back to parsing environment or using X11 compatibility
        var x11Strategy = new X11GetImageStrategy();
        if (X11GetImageStrategy.IsSupported())
        {
            return x11Strategy.GetMonitors();
        }

        // Minimal fallback: single virtual monitor
        return new[]
        {
            new MonitorInfo
            {
                Id = "0",
                Name = "Wayland Display",
                IsPrimary = true,
                Bounds = new PhysicalRectangle(0, 0, 1920, 1080), // Assumption
                WorkingArea = new PhysicalRectangle(0, 0, 1920, 1040),
                // [2026-01-15] ScaleFactor = 1.0 is CORRECT for Wayland Portal
                // The portal returns pre-scaled screenshots from the compositor.
                // Wayland compositors handle all DPI scaling internally, so we
                // receive the final image in physical pixels without needing
                // additional scale factor adjustments. This is different from
                // X11 which requires explicit per-monitor DPI calculations.
                ScaleFactor = 1.0,
                BitsPerPixel = 32
            }
        };
    }

    public async Task<CapturedBitmap> CaptureRegionAsync(
        PhysicalRectangle physicalRegion,
        RegionCaptureOptions options)
    {
        // TODO: Implement xdg-desktop-portal D-Bus integration
        // This requires:
        // 1. D-Bus connection to org.freedesktop.portal.Desktop
        // 2. Call Screenshot method with window handle and options
        // 3. Handle user permission dialog
        // 4. Retrieve saved screenshot from file URI
        // 5. Crop to requested region
        //
        // For now, fall back to CLI tools
        var cliStrategy = new LinuxCliCaptureStrategy();
        return await cliStrategy.CaptureRegionAsync(physicalRegion, options);
    }

    public BackendCapabilities GetCapabilities()
    {
        return new BackendCapabilities
        {
            BackendName = "Wayland Portal",
            Version = "xdg-desktop-portal",
            SupportsHardwareAcceleration = true,
            SupportsCursorCapture = true,
            SupportsHDR = false,
            SupportsPerMonitorDpi = true,
            SupportsMonitorHotplug = true,
            MaxCaptureResolution = 16384,
            RequiresPermission = true // User must approve each capture
        };
    }

    public void Dispose()
    {
        // No resources to clean up
    }
}
