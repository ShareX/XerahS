using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions.Capture;
using SkiaSharp;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace ShareX.Avalonia.Platform.Windows.Capture;

/// <summary>
/// Capture strategy using DXGI Desktop Duplication API.
/// Provides hardware-accelerated screen capture with minimal CPU usage.
/// Requires Windows 8+ and DXGI 1.2+.
/// </summary>
internal sealed class DxgiCaptureStrategy : ICaptureStrategy
{
    private readonly Dictionary<string, DxgiMonitorContext> _monitorContexts = new();
    private bool _disposed;

    public string Name => "DXGI Desktop Duplication";

    public static bool IsSupported()
    {
        // DXGI 1.2+ required (Windows 8+)
        return Environment.OSVersion.Version >= new Version(6, 2);
    }

    public MonitorInfo[] GetMonitors()
    {
        var monitors = new List<MonitorInfo>();

        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

        uint adapterIndex = 0;
        while (factory.EnumAdapters1(adapterIndex, out var adapter).Success)
        {
            uint outputIndex = 0;
            while (adapter.EnumOutputs(outputIndex, out var output).Success)
            {
                var desc = output.Description;

                // Get per-monitor DPI via GetDpiForMonitor
                var hMonitor = desc.Monitor;
                uint dpiX = 96, dpiY = 96;

                try
                {
                    NativeMethods.GetDpiForMonitor(
                        hMonitor,
                        MonitorDpiType.MDT_EFFECTIVE_DPI,
                        out dpiX,
                        out dpiY);
                }
                catch
                {
                    // Fallback to 96 DPI if GetDpiForMonitor fails
                }

                var scaleFactor = dpiX / 96.0;

                // Get monitor device name
                var deviceName = GetMonitorDeviceName(hMonitor);

                // Get working area (excluding taskbar)
                var workingArea = GetWorkingArea(hMonitor);

                monitors.Add(new MonitorInfo
                {
                    Id = desc.DeviceName,
                    Name = deviceName,
                    IsPrimary = desc.DesktopCoordinates.Left == 0 && desc.DesktopCoordinates.Top == 0,
                    Bounds = new PhysicalRectangle(
                        desc.DesktopCoordinates.Left,
                        desc.DesktopCoordinates.Top,
                        desc.DesktopCoordinates.Right - desc.DesktopCoordinates.Left,
                        desc.DesktopCoordinates.Bottom - desc.DesktopCoordinates.Top),
                    WorkingArea = workingArea,
                    ScaleFactor = scaleFactor,
                    Rotation = ConvertRotation(desc.Rotation),
                    BitsPerPixel = 32 // DXGI always uses 32-bit BGRA
                });

                // Pre-initialize DXGI duplication for this output
                try
                {
                    InitializeDuplication(output, adapter, desc.DeviceName);
                }
                catch
                {
                    // Ignore initialization failures for individual monitors
                }

                output.Dispose();
                outputIndex++;
            }

            adapter.Dispose();
            adapterIndex++;
        }

        return monitors.ToArray();
    }

    public async Task<CapturedBitmap> CaptureRegionAsync(
        PhysicalRectangle physicalRegion,
        RegionCaptureOptions options)
    {
        // Find which monitor contains this region
        var monitors = GetMonitors();
        var monitor = monitors.FirstOrDefault(m => m.Bounds.Intersect(physicalRegion) != null);

        if (monitor == null)
            throw new InvalidOperationException($"Region {physicalRegion} does not intersect any monitor");

        if (!_monitorContexts.TryGetValue(monitor.Id, out var context))
            throw new InvalidOperationException($"Monitor {monitor.Id} not initialized");

        // Capture the region using DXGI
        return await Task.Run(() => CaptureRegionInternal(context, monitor, physicalRegion, options));
    }

    private CapturedBitmap CaptureRegionInternal(
        DxgiMonitorContext context,
        MonitorInfo monitor,
        PhysicalRectangle region,
        RegionCaptureOptions options)
    {
        var duplication = context.IDXGIOutputDuplication;
        var device = context.Device;

        // Acquire next frame
        OutduplFrameInfo frameInfo;
        IDXGIResource desktopResource;

        try
        {
            duplication.AcquireNextFrame(100, out frameInfo, out desktopResource);
        }
        catch
        {
            // No frame available, try once more with longer timeout
            try
            {
                duplication.AcquireNextFrame(500, out frameInfo, out desktopResource);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to acquire frame: {ex.Message}", ex);
            }
        }

        try
        {
            using var texture = desktopResource.QueryInterface<ID3D11Texture2D>();

            // Calculate region relative to monitor
            var intersection = region.Intersect(monitor.Bounds);
            if (intersection == null || intersection.Value.IsEmpty)
                throw new ArgumentException($"Region {region} does not intersect monitor {monitor.Name}");

            var captureRegion = intersection.Value;
            var localX = captureRegion.X - monitor.Bounds.X;
            var localY = captureRegion.Y - monitor.Bounds.Y;

            // Create staging texture for CPU readback
            var stagingDesc = new Texture2DDescription
            {
                Width = (uint)captureRegion.Width,
                Height = (uint)captureRegion.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CPUAccessFlags = CpuAccessFlags.Read
            };

            using var staging = device.CreateTexture2D(stagingDesc);

            // Copy only the requested region
            var sourceBox = new Box(
                localX,
                localY,
                0,
                localX + captureRegion.Width,
                localY + captureRegion.Height,
                1);

            context.ImmediateContext.CopySubresourceRegion(
                staging,
                0,
                0, 0, 0,
                texture,
                0,
                sourceBox);

            // Map staging texture to CPU memory
            var mapped = context.ImmediateContext.Map(staging, 0, MapMode.Read);

            try
            {
                // Create SKBitmap from mapped data
                var bitmap = new SKBitmap(
                    captureRegion.Width,
                    captureRegion.Height,
                    SKColorType.Bgra8888,
                    SKAlphaType.Premul);

                var info = bitmap.Info;
                var rowBytes = info.RowBytes;

                // Copy row by row (handles pitch difference)
                unsafe
                {
                    var src = (byte*)mapped.DataPointer;
                    var dst = (byte*)bitmap.GetPixels();

                    for (int y = 0; y < captureRegion.Height; y++)
                    {
                        Buffer.MemoryCopy(
                            src + y * mapped.RowPitch,
                            dst + y * rowBytes,
                            rowBytes,
                            rowBytes);
                    }
                }

                return new CapturedBitmap(bitmap, captureRegion, monitor.ScaleFactor);
            }
            finally
            {
                context.ImmediateContext.Unmap(staging, 0);
            }
        }
        finally
        {
            desktopResource?.Dispose();
            duplication.ReleaseFrame();
        }
    }

    public BackendCapabilities GetCapabilities()
    {
        return new BackendCapabilities
        {
            BackendName = "DXGI Desktop Duplication",
            Version = "1.2+",
            SupportsHardwareAcceleration = true,
            SupportsCursorCapture = true, // DXGI supports cursor metadata
            SupportsHDR = false, // Would require additional format handling
            SupportsPerMonitorDpi = true,
            SupportsMonitorHotplug = true,
            MaxCaptureResolution = 16384, // D3D11 texture limit
            RequiresPermission = false
        };
    }

    private void InitializeDuplication(IDXGIOutput output, IDXGIAdapter1 adapter, string monitorId)
    {
        using var output1 = output.QueryInterface<IDXGIOutput1>();

        // Create D3D11 device for this adapter
        var featureLevels = new[] { FeatureLevel.Level_11_0, FeatureLevel.Level_10_0 };
        var result = D3D11.D3D11CreateDevice(
            adapter,
            DriverType.Unknown,
            DeviceCreationFlags.None,
            featureLevels,
            out var device);

        if (result.Failure || device == null)
            throw new InvalidOperationException($"Failed to create D3D11 device: {result}");

        // Create output duplication
        var duplication = output1.DuplicateOutput(device);

        _monitorContexts[monitorId] = new DxgiMonitorContext
        {
            Device = device,
            ImmediateContext = device.ImmediateContext,
            Output = output1,
            IDXGIOutputDuplication = duplication
        };
    }

    private string GetMonitorDeviceName(IntPtr hMonitor)
    {
        var mi = new NativeMethods.MONITORINFOEX();
        mi.cbSize = Marshal.SizeOf(mi);

        if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
        {
            // Get friendly display name
            var dd = new NativeMethods.DISPLAY_DEVICE();
            dd.cb = Marshal.SizeOf(dd);

            if (NativeMethods.EnumDisplayDevices(mi.szDevice, 0, ref dd, 0))
            {
                return dd.DeviceString;
            }
        }

        return "Unknown Monitor";
    }

    private PhysicalRectangle GetWorkingArea(IntPtr hMonitor)
    {
        var mi = new NativeMethods.MONITORINFOEX();
        mi.cbSize = Marshal.SizeOf(mi);

        if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
        {
            return new PhysicalRectangle(
                mi.rcWork.Left,
                mi.rcWork.Top,
                mi.rcWork.Right - mi.rcWork.Left,
                mi.rcWork.Bottom - mi.rcWork.Top);
        }

        // Fallback: return monitor bounds
        return default;
    }

    private static int ConvertRotation(Vortice.DXGI.ModeRotation rotation)
    {
        return rotation switch
        {
            Vortice.DXGI.ModeRotation.Rotate90 => 90,
            Vortice.DXGI.ModeRotation.Rotate180 => 180,
            Vortice.DXGI.ModeRotation.Rotate270 => 270,
            _ => 0
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var context in _monitorContexts.Values)
        {
            context.IDXGIOutputDuplication?.Dispose();
            context.Output?.Dispose();
            context.ImmediateContext?.Dispose();
            context.Device?.Dispose();
        }

        _monitorContexts.Clear();
    }
}

/// <summary>
/// Context information for a DXGI-monitored display.
/// </summary>
internal sealed class DxgiMonitorContext
{
    public required ID3D11Device Device { get; init; }
    public required ID3D11DeviceContext ImmediateContext { get; init; }
    public required IDXGIOutput1 Output { get; init; }
    public required IDXGIOutputDuplication IDXGIOutputDuplication { get; init; }
}

/// <summary>
/// Monitor DPI type for GetDpiForMonitor API.
/// </summary>
internal enum MonitorDpiType
{
    MDT_EFFECTIVE_DPI = 0,
    MDT_ANGULAR_DPI = 1,
    MDT_RAW_DPI = 2
}
