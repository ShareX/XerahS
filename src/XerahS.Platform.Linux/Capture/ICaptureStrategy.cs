using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions.Capture;

namespace ShareX.Avalonia.Platform.Linux.Capture;

/// <summary>
/// Internal interface for Linux capture strategies.
/// </summary>
internal interface ICaptureStrategy
{
    static abstract bool IsSupported();
    string Name { get; }
    MonitorInfo[] GetMonitors();
    Task<CapturedBitmap> CaptureRegionAsync(PhysicalRectangle physicalRegion, RegionCaptureOptions options);
    BackendCapabilities GetCapabilities();
    void Dispose();
}
