#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions.Capture;
using SkiaSharp;

namespace ShareX.Avalonia.Platform.macOS.Capture;

/// <summary>
/// Capture strategy using Quartz/CoreGraphics API (CGDisplayCreateImage).
/// Available since macOS 10.6, reliable GPU-based capture.
/// </summary>
internal sealed class QuartzCaptureStrategy : ICaptureStrategy
{
    public string Name => "Quartz/CoreGraphics";

    public static bool IsSupported()
    {
        // Always available on macOS 10.6+
        return OperatingSystem.IsMacOS();
    }

    public MonitorInfo[] GetMonitors()
    {
        // Get all active displays
        uint[] displays = new uint[16];
        CGGetActiveDisplayList(16, displays, out uint count);

        var monitors = new System.Collections.Generic.List<MonitorInfo>();
        uint mainDisplayId = CGMainDisplayID();

        for (int i = 0; i < count; i++)
        {
            var displayId = displays[i];
            var bounds = CGDisplayBounds(displayId);

            // Get display mode for physical dimensions
            IntPtr mode = CGDisplayCopyDisplayMode(displayId);
            if (mode == IntPtr.Zero)
                continue;

            try
            {
                var pixelWidth = (int)CGDisplayModeGetPixelWidth(mode);
                var pixelHeight = (int)CGDisplayModeGetPixelHeight(mode);

                // Calculate scale factor (Retina = 2.0, non-Retina = 1.0)
                var scaleFactor = pixelWidth / bounds.width;

                var physicalBounds = new PhysicalRectangle(
                    (int)(bounds.x * scaleFactor),
                    (int)(bounds.y * scaleFactor),
                    pixelWidth,
                    pixelHeight);

                monitors.Add(new MonitorInfo
                {
                    Id = displayId.ToString(),
                    Name = GetDisplayName(displayId),
                    IsPrimary = displayId == mainDisplayId,
                    Bounds = physicalBounds,
                    WorkingArea = physicalBounds, // macOS doesn't expose working area easily via CG
                    ScaleFactor = scaleFactor,
                    RefreshRate = (int)CGDisplayModeGetRefreshRate(mode),
                    BitsPerPixel = 32
                });
            }
            finally
            {
                CGDisplayModeRelease(mode);
            }
        }

        return monitors.ToArray();
    }

    public async Task<CapturedBitmap> CaptureRegionAsync(
        PhysicalRectangle physicalRegion,
        RegionCaptureOptions options)
    {
        return await Task.Run(() =>
        {
            var monitors = GetMonitors();
            var monitor = monitors.FirstOrDefault(m => m.Bounds.Intersect(physicalRegion) != null);

            if (monitor == null)
                throw new InvalidOperationException($"Region {physicalRegion} does not intersect any monitor");

            uint displayId = uint.Parse(monitor.Id);

            // Capture entire display
            IntPtr displayImage = CGDisplayCreateImage(displayId);
            if (displayImage == IntPtr.Zero)
                throw new InvalidOperationException("Failed to capture display");

            try
            {
                // Calculate region in logical coordinates (macOS works in points, not pixels)
                var logicalRect = new CGRect
                {
                    x = (physicalRegion.X - monitor.Bounds.X) / monitor.ScaleFactor,
                    y = (physicalRegion.Y - monitor.Bounds.Y) / monitor.ScaleFactor,
                    width = physicalRegion.Width / monitor.ScaleFactor,
                    height = physicalRegion.Height / monitor.ScaleFactor
                };

                // Extract region from display image
                IntPtr regionImage = CGImageCreateWithImageInRect(displayImage, logicalRect);
                if (regionImage == IntPtr.Zero)
                    throw new InvalidOperationException("Failed to extract region");

                try
                {
                    // Convert CGImage to PNG data
                    var pngData = ConvertCGImageToPNG(regionImage);

                    // Decode to SKBitmap
                    using var stream = new MemoryStream(pngData);
                    var bitmap = SKBitmap.Decode(stream);

                    if (bitmap == null)
                        throw new InvalidOperationException("Failed to decode captured image");

                    return new CapturedBitmap(bitmap, physicalRegion, monitor.ScaleFactor);
                }
                finally
                {
                    CGImageRelease(regionImage);
                }
            }
            finally
            {
                CGImageRelease(displayImage);
            }
        });
    }

    public BackendCapabilities GetCapabilities()
    {
        return new BackendCapabilities
        {
            BackendName = "Quartz/CoreGraphics",
            Version = "10.6+",
            SupportsHardwareAcceleration = true,
            SupportsCursorCapture = false,
            SupportsHDR = false,
            SupportsPerMonitorDpi = true,
            SupportsMonitorHotplug = true,
            MaxCaptureResolution = 16384,
            RequiresPermission = true // macOS requires screen recording permission
        };
    }

    private byte[] ConvertCGImageToPNG(IntPtr cgImage)
    {
        // Create a CFMutableData to hold the PNG
        IntPtr data = CFDataCreateMutable(IntPtr.Zero, 0);
        if (data == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create CFMutableData");

        try
        {
            // Create an image destination for PNG
            IntPtr type = CreateCFString("public.png");
            IntPtr dest = CGImageDestinationCreateWithData(data, type, 1, IntPtr.Zero);
            CFRelease(type);

            if (dest == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create image destination");

            try
            {
                // Add the image and finalize
                CGImageDestinationAddImage(dest, cgImage, IntPtr.Zero);
                if (!CGImageDestinationFinalize(dest))
                    throw new InvalidOperationException("Failed to finalize PNG");

                // Copy CFData to byte array
                int length = (int)CFDataGetLength(data);
                byte[] result = new byte[length];
                IntPtr bytes = CFDataGetBytePtr(data);
                Marshal.Copy(bytes, result, 0, length);

                return result;
            }
            finally
            {
                CFRelease(dest);
            }
        }
        finally
        {
            CFRelease(data);
        }
    }

    private string GetDisplayName(uint displayId)
    {
        // Try to get a friendly name; fallback to Display ID
        return $"Display {displayId}";
    }

    public void Dispose()
    {
        // No resources to clean up
    }

    #region CoreGraphics P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect
    {
        public double x, y, width, height;
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern int CGGetActiveDisplayList(uint maxDisplays, uint[] displays, out uint displayCount);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern uint CGMainDisplayID();

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern CGRect CGDisplayBounds(uint displayId);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGDisplayCopyDisplayMode(uint displayId);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGDisplayModeRelease(IntPtr mode);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern long CGDisplayModeGetPixelWidth(IntPtr mode);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern long CGDisplayModeGetPixelHeight(IntPtr mode);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern double CGDisplayModeGetRefreshRate(IntPtr mode);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGDisplayCreateImage(uint displayId);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGImageCreateWithImageInRect(IntPtr image, CGRect rect);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGImageRelease(IntPtr image);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFDataCreateMutable(IntPtr allocator, long capacity);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern long CFDataGetLength(IntPtr data);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFDataGetBytePtr(IntPtr data);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);

    [DllImport("/System/Library/Frameworks/ImageIO.framework/ImageIO")]
    private static extern IntPtr CGImageDestinationCreateWithData(IntPtr data, IntPtr type, long count, IntPtr options);

    [DllImport("/System/Library/Frameworks/ImageIO.framework/ImageIO")]
    private static extern void CGImageDestinationAddImage(IntPtr dest, IntPtr image, IntPtr properties);

    [DllImport("/System/Library/Frameworks/ImageIO.framework/ImageIO")]
    private static extern bool CGImageDestinationFinalize(IntPtr dest);

    private static IntPtr CreateCFString(string str)
    {
        return CFStringCreateWithCString(IntPtr.Zero, str, 0x08000100); // kCFStringEncodingUTF8
    }

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string str, uint encoding);

    #endregion
}
