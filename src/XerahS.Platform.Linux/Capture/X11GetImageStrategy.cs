using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions.Capture;
using SkiaSharp;

namespace ShareX.Avalonia.Platform.Linux.Capture;

/// <summary>
/// Capture strategy using X11 XGetImage API.
/// Direct framebuffer access, fast and reliable for X11 sessions.
/// </summary>
internal sealed class X11GetImageStrategy : ICaptureStrategy
{
    private IntPtr _display;
    private IntPtr _rootWindow;
    private bool _initialized;

    public string Name => "X11 XGetImage";

    public static bool IsSupported()
    {
        try
        {
            var display = XOpenDisplay(null);
            if (display != IntPtr.Zero)
            {
                XCloseDisplay(display);
                return true;
            }
        }
        catch
        {
            // X11 library not available
        }
        return false;
    }

    public MonitorInfo[] GetMonitors()
    {
        EnsureInitialized();

        var monitors = new System.Collections.Generic.List<MonitorInfo>();

        // Get XRandR screen resources
        IntPtr resources = XRRGetScreenResourcesCurrent(_display, _rootWindow);
        if (resources == IntPtr.Zero)
            return Array.Empty<MonitorInfo>();

        try
        {
            unsafe
            {
                var res = (XRRScreenResources*)resources;

                for (int i = 0; i < res->noutput; i++)
                {
                    IntPtr output = res->outputs[i];
                    IntPtr outputInfo = XRRGetOutputInfo(_display, resources, output);

                    if (outputInfo == IntPtr.Zero)
                        continue;

                    var outInfo = (XRROutputInfo*)outputInfo;

                    try
                    {
                        // Skip disconnected outputs
                        if (outInfo->connection != 0 || outInfo->crtc == IntPtr.Zero)
                            continue;

                        IntPtr crtcInfo = XRRGetCrtcInfo(_display, resources, outInfo->crtc);
                        if (crtcInfo == IntPtr.Zero)
                            continue;

                        var crtc = (XRRCrtcInfo*)crtcInfo;

                        try
                        {
                            // Get DPI scaling from Xft.dpi resource
                            var scaleFactor = GetXftDpi() / 96.0;

                            var bounds = new PhysicalRectangle(
                                crtc->x,
                                crtc->y,
                                (int)crtc->width,
                                (int)crtc->height);

                            var name = Marshal.PtrToStringAnsi(outInfo->name) ?? "Unknown";

                            monitors.Add(new MonitorInfo
                            {
                                Id = output.ToString(),
                                Name = name,
                                IsPrimary = i == 0, // First output is typically primary
                                Bounds = bounds,
                                WorkingArea = bounds, // X11 doesn't expose this uniformly
                                ScaleFactor = scaleFactor,
                                BitsPerPixel = 32
                            });
                        }
                        finally
                        {
                            XRRFreeCrtcInfo(crtcInfo);
                        }
                    }
                    finally
                    {
                        XRRFreeOutputInfo(outputInfo);
                    }
                }
            }
        }
        finally
        {
            XRRFreeScreenResources(resources);
        }

        return monitors.ToArray();
    }

    public async Task<CapturedBitmap> CaptureRegionAsync(
        PhysicalRectangle physicalRegion,
        RegionCaptureOptions options)
    {
        return await Task.Run(() =>
        {
            EnsureInitialized();

            var monitors = GetMonitors();
            var monitor = monitors.FirstOrDefault(m => m.Bounds.Intersect(physicalRegion) != null);

            if (monitor == null)
                throw new InvalidOperationException($"Region {physicalRegion} does not intersect any monitor");

            // XGetImage uses physical pixel coordinates directly
            IntPtr xImage = XGetImage(
                _display,
                _rootWindow,
                physicalRegion.X,
                physicalRegion.Y,
                (uint)physicalRegion.Width,
                (uint)physicalRegion.Height,
                AllPlanes,
                ZPixmap);

            if (xImage == IntPtr.Zero)
                throw new InvalidOperationException("XGetImage failed");

            try
            {
                unsafe
                {
                    var img = (XImage*)xImage;

                    // Create SKBitmap
                    var bitmap = new SKBitmap(
                        physicalRegion.Width,
                        physicalRegion.Height,
                        SKColorType.Bgra8888,
                        SKAlphaType.Opaque);

                    // Copy pixel data
                    var pixels = (byte*)bitmap.GetPixels();
                    var srcData = (byte*)img->data;

                    for (int y = 0; y < physicalRegion.Height; y++)
                    {
                        Buffer.MemoryCopy(
                            srcData + y * img->bytes_per_line,
                            pixels + y * bitmap.RowBytes,
                            bitmap.RowBytes,
                            Math.Min(img->bytes_per_line, bitmap.RowBytes));
                    }

                    return new CapturedBitmap(bitmap, physicalRegion, monitor.ScaleFactor);
                }
            }
            finally
            {
                XDestroyImage(xImage);
            }
        });
    }

    public BackendCapabilities GetCapabilities()
    {
        return new BackendCapabilities
        {
            BackendName = "X11 XGetImage",
            Version = "X11R6+",
            SupportsHardwareAcceleration = true,
            SupportsCursorCapture = false,
            SupportsHDR = false,
            SupportsPerMonitorDpi = true,
            SupportsMonitorHotplug = true,
            MaxCaptureResolution = 32767,
            RequiresPermission = false
        };
    }

    private void EnsureInitialized()
    {
        if (_initialized)
            return;

        _display = XOpenDisplay(null);
        if (_display == IntPtr.Zero)
            throw new InvalidOperationException("Cannot open X display");

        _rootWindow = XRootWindow(_display, 0);
        _initialized = true;
    }

    private double GetXftDpi()
    {
        // Read Xft.dpi from X resources
        var resourceString = XResourceManagerString(_display);
        if (resourceString == IntPtr.Zero)
            return 96.0;

        var resources = Marshal.PtrToStringAnsi(resourceString);
        if (string.IsNullOrEmpty(resources))
            return 96.0;

        // Parse Xft.dpi: 96 (or similar)
        var match = Regex.Match(resources, @"Xft\.dpi:\s*(\d+)");
        if (match.Success && double.TryParse(match.Groups[1].Value, out var dpi))
            return dpi;

        return 96.0;
    }

    public void Dispose()
    {
        if (_initialized && _display != IntPtr.Zero)
        {
            XCloseDisplay(_display);
            _display = IntPtr.Zero;
            _initialized = false;
        }
    }

    #region X11 P/Invoke

    private const ulong AllPlanes = ~0UL;
    private const int ZPixmap = 2;

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct XImage
    {
        public int width, height;
        public int xoffset;
        public int format;
        public IntPtr data;
        public int byte_order;
        public int bitmap_unit;
        public int bitmap_bit_order;
        public int bitmap_pad;
        public int depth;
        public int bytes_per_line;
        public int bits_per_pixel;
        public ulong red_mask;
        public ulong green_mask;
        public ulong blue_mask;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct XRRScreenResources
    {
        public long timestamp;
        public long configTimestamp;
        public int ncrtc;
        public IntPtr* crtcs;
        public int noutput;
        public IntPtr* outputs;
        public int nmode;
        public IntPtr modes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct XRROutputInfo
    {
        public long timestamp;
        public IntPtr crtc;
        public IntPtr name;
        public int nameLen;
        public ulong mm_width;
        public ulong mm_height;
        public ushort connection;
        // ... more fields exist but not needed
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XRRCrtcInfo
    {
        public long timestamp;
        public int x, y;
        public uint width, height;
        public IntPtr mode;
        public ushort rotation;
        // ... more fields exist
    }

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(string? displayName);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XRootWindow(IntPtr display, int screenNumber);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XGetImage(
        IntPtr display, IntPtr drawable,
        int x, int y, uint width, uint height,
        ulong planeMask, int format);

    [DllImport("libX11.so.6")]
    private static extern int XDestroyImage(IntPtr image);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XResourceManagerString(IntPtr display);

    [DllImport("libXrandr.so.2")]
    private static extern IntPtr XRRGetScreenResourcesCurrent(IntPtr display, IntPtr window);

    [DllImport("libXrandr.so.2")]
    private static extern void XRRFreeScreenResources(IntPtr resources);

    [DllImport("libXrandr.so.2")]
    private static extern IntPtr XRRGetOutputInfo(IntPtr display, IntPtr resources, IntPtr output);

    [DllImport("libXrandr.so.2")]
    private static extern void XRRFreeOutputInfo(IntPtr outputInfo);

    [DllImport("libXrandr.so.2")]
    private static extern IntPtr XRRGetCrtcInfo(IntPtr display, IntPtr resources, IntPtr crtc);

    [DllImport("libXrandr.so.2")]
    private static extern void XRRFreeCrtcInfo(IntPtr crtcInfo);

    #endregion
}
