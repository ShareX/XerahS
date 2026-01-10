using System;
using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions.Capture;

namespace ShareX.Avalonia.Platform.Linux.Capture;

/// <summary>
/// Linux implementation with automatic X11/Wayland detection.
/// Strategy selection: Wayland portal → X11 XGetImage → CLI tools
/// </summary>
public sealed class LinuxRegionCaptureBackend : IRegionCaptureBackend
{
    private readonly ICaptureStrategy _strategy;
    private readonly string _sessionType;
    private bool _disposed;

    public event EventHandler<MonitorConfigurationChangedEventArgs>? ConfigurationChanged;

    public LinuxRegionCaptureBackend()
    {
        _sessionType = DetectSessionType();
        _strategy = SelectBestStrategy();
    }

    private static string DetectSessionType()
    {
        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        return sessionType?.ToLower() ?? "x11"; // Default to X11 if unknown
    }

    private ICaptureStrategy SelectBestStrategy()
    {
        if (_sessionType == "wayland")
        {
            // Wayland: try portal, fall back to XWayland/CLI
            if (WaylandPortalStrategy.IsSupported())
            {
                try
                {
                    return new WaylandPortalStrategy();
                }
                catch
                {
                    // Fall through
                }
            }

            // Try XWayland fallback
            if (X11GetImageStrategy.IsSupported())
            {
                try
                {
                    return new X11GetImageStrategy();
                }
                catch
                {
                    // Fall through
                }
            }
        }
        else // X11 or unknown
        {
            // X11: try direct capture
            if (X11GetImageStrategy.IsSupported())
            {
                try
                {
                    return new X11GetImageStrategy();
                }
                catch
                {
                    // Fall through
                }
            }
        }

        // Universal CLI fallback
        return new LinuxCliCaptureStrategy();
    }

    public MonitorInfo[] GetMonitors()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _strategy.GetMonitors();
    }

    public Task<CapturedBitmap> CaptureRegionAsync(
        PhysicalRectangle physicalRegion,
        RegionCaptureOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (physicalRegion.Width <= 0 || physicalRegion.Height <= 0)
            throw new ArgumentException($"Invalid region size: {physicalRegion.Width}×{physicalRegion.Height}");

        return _strategy.CaptureRegionAsync(physicalRegion, options);
    }

    public BackendCapabilities GetCapabilities()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _strategy.GetCapabilities();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _strategy?.Dispose();
    }
}
