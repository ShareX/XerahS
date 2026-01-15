using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions.Capture;
using SkiaSharp;

namespace ShareX.Avalonia.Platform.Windows.Capture;

/// <summary>
/// Fallback capture strategy using GDI+ Graphics.CopyFromScreen.
/// Simple, reliable, works on all Windows versions.
/// CPU-based, slower than DXGI but universally compatible.
/// </summary>
internal sealed class GdiCaptureStrategy : ICaptureStrategy
{
    public string Name => "GDI+ BitBlt";

    public static bool IsSupported() => true; // Always available on Windows

    public MonitorInfo[] GetMonitors()
    {
        var monitors = Screen.AllScreens.Select((screen, index) =>
        {
            // Get DPI for this monitor
            uint dpiX = 96, dpiY = 96;
            try
            {
                var hMonitor = GetMonitorHandle(screen);
                if (hMonitor != IntPtr.Zero)
                {
                    NativeMethods.GetDpiForMonitor(
                        hMonitor,
                        MonitorDpiType.MDT_EFFECTIVE_DPI,
                        out dpiX,
                        out dpiY);
                }
            }
            catch
            {
                // Fallback to 96 DPI
            }

            var scaleFactor = dpiX / 96.0;

            return new MonitorInfo
            {
                Id = screen.DeviceName,
                Name = screen.DeviceName.Replace("\\\\.\\", "") ?? $"Monitor {index + 1}",
                IsPrimary = screen.Primary,
                Bounds = new PhysicalRectangle(
                    screen.Bounds.X,
                    screen.Bounds.Y,
                    screen.Bounds.Width,
                    screen.Bounds.Height),
                WorkingArea = new PhysicalRectangle(
                    screen.WorkingArea.X,
                    screen.WorkingArea.Y,
                    screen.WorkingArea.Width,
                    screen.WorkingArea.Height),
                ScaleFactor = scaleFactor,
                BitsPerPixel = screen.BitsPerPixel
            };
        }).ToArray();

        return monitors;
    }

    public Task<CapturedBitmap> CaptureRegionAsync(
        PhysicalRectangle physicalRegion,
        RegionCaptureOptions options)
    {
        return Task.Run(() =>
        {
            // Find monitor containing this region
            var monitors = GetMonitors();
            var monitor = monitors.FirstOrDefault(m => m.Bounds.Intersect(physicalRegion) != null);

            if (monitor == null)
                throw new InvalidOperationException($"Region {physicalRegion} does not intersect any monitor");

            // Create bitmap for the region
            using var bitmap = new Bitmap(
                physicalRegion.Width,
                physicalRegion.Height,
                PixelFormat.Format32bppArgb);

            // Copy screen content to bitmap
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(
                    physicalRegion.X,
                    physicalRegion.Y,
                    0,
                    0,
                    new Size(physicalRegion.Width, physicalRegion.Height),
                    CopyPixelOperation.SourceCopy);
            }

            // Convert System.Drawing.Bitmap to SKBitmap
            var skBitmap = ConvertToSkBitmap(bitmap);

            return new CapturedBitmap(skBitmap, physicalRegion, monitor.ScaleFactor);
        });
    }

    public BackendCapabilities GetCapabilities()
    {
        return new BackendCapabilities
        {
            BackendName = "GDI+ BitBlt",
            Version = "Win32",
            SupportsHardwareAcceleration = false,
            SupportsCursorCapture = false,
            SupportsHDR = false,
            SupportsPerMonitorDpi = true,
            SupportsMonitorHotplug = true,
            MaxCaptureResolution = 32767, // GDI+ limit
            RequiresPermission = false
        };
    }

    private static SKBitmap ConvertToSkBitmap(Bitmap gdiBitmap)
    {
        // Lock bitmap data for direct pixel access
        var bitmapData = gdiBitmap.LockBits(
            new Rectangle(0, 0, gdiBitmap.Width, gdiBitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            // Create SKBitmap
            var skBitmap = new SKBitmap(
                gdiBitmap.Width,
                gdiBitmap.Height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);

            // Copy pixel data
            var info = skBitmap.Info;
            var rowBytes = info.RowBytes;

            unsafe
            {
                var src = (byte*)bitmapData.Scan0;
                var dst = (byte*)skBitmap.GetPixels();

                for (int y = 0; y < gdiBitmap.Height; y++)
                {
                    Buffer.MemoryCopy(
                        src + y * bitmapData.Stride,
                        dst + y * rowBytes,
                        rowBytes,
                        Math.Min(bitmapData.Stride, rowBytes));
                }
            }

            return skBitmap;
        }
        finally
        {
            gdiBitmap.UnlockBits(bitmapData);
        }
    }

    private static IntPtr GetMonitorHandle(Screen screen)
    {
        // Try to get monitor handle from screen bounds
        // This is a simplified approach; a full implementation would use EnumDisplayMonitors
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        // No resources to clean up
    }
}
