using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions;
using ShareX.Avalonia.Platform.Abstractions.Capture;
using SkiaSharp;
using Tmds.DBus;
using XerahS.Common;

namespace ShareX.Avalonia.Platform.Linux.Capture;

/// <summary>
/// Capture strategy using xdg-desktop-portal Screenshot API.
/// Works with Wayland compositors via D-Bus.
/// Requires user permission per capture (security feature).
/// </summary>
internal sealed class WaylandPortalStrategy : ICaptureStrategy
{
    public string Name => "Wayland Portal";

    private const string PortalBusName = "org.freedesktop.portal.Desktop";
    private static readonly ObjectPath PortalObjectPath = new("/org/freedesktop/portal/desktop");

    public static bool IsSupported()
    {
        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        return sessionType?.Equals("wayland", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public MonitorInfo[] GetMonitors()
    {
        var x11Strategy = new X11GetImageStrategy();
        if (X11GetImageStrategy.IsSupported())
        {
            return x11Strategy.GetMonitors();
        }

        return new[]
        {
            new MonitorInfo
            {
                Id = "0",
                Name = "Wayland Display",
                IsPrimary = true,
                Bounds = new PhysicalRectangle(0, 0, 1920, 1080),
                WorkingArea = new PhysicalRectangle(0, 0, 1920, 1040),
                ScaleFactor = 1.0,
                BitsPerPixel = 32
            }
        };
    }

    public async Task<CapturedBitmap> CaptureRegionAsync(PhysicalRectangle physicalRegion, RegionCaptureOptions options)
    {
        var captured = await CaptureWithPortalAsync(physicalRegion);
        if (captured != null)
        {
            return captured;
        }

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
            RequiresPermission = true
        };
    }

    public void Dispose()
    {
    }

    private static async Task<CapturedBitmap?> CaptureWithPortalAsync(PhysicalRectangle region)
    {
        try
        {
            using var connection = new Connection(Address.Session);
            await connection.ConnectAsync();

            var portal = connection.CreateProxy<IScreenshotPortal>(PortalBusName, PortalObjectPath);
            var requestPath = await portal.ScreenshotAsync(string.Empty, new Dictionary<string, object> { ["modal"] = false });

            var request = connection.CreateProxy<IPortalRequest>(PortalBusName, requestPath);
            var (response, results) = await WaitForResponseAsync(request);

            if (response != 0)
            {
                return null;
            }

            if (!results.TryGetValue("uri", out var uriValue) || uriValue is not string uriStr)
            {
                return null;
            }

            var uri = new Uri(uriStr);
            if (!uri.IsFile || string.IsNullOrEmpty(uri.LocalPath) || !File.Exists(uri.LocalPath))
            {
                return null;
            }

            using var stream = File.OpenRead(uri.LocalPath);
            var bitmap = SKBitmap.Decode(stream);
            if (bitmap == null)
            {
                return null;
            }

            try
            {
                return CropToRegion(bitmap, region);
            }
            finally
            {
                bitmap.Dispose();
            }
        }
        catch (DBusException ex)
        {
            DebugHelper.WriteException(ex, "WaylandPortalStrategy: Portal capture failed");
            return null;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "WaylandPortalStrategy: Unexpected capture failure");
            return null;
        }
    }

    private static CapturedBitmap? CropToRegion(SKBitmap source, PhysicalRectangle region)
    {
        var clamped = ClampRegion(region, source.Width, source.Height);
        if (clamped.Width <= 0 || clamped.Height <= 0)
        {
            return null;
        }

        var cropped = new SKBitmap(clamped.Width, clamped.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(cropped);
        canvas.DrawBitmap(source, new SKRectI(clamped.Left, clamped.Top, clamped.Right, clamped.Bottom), new SKRect(0, 0, clamped.Width, clamped.Height));

        return new CapturedBitmap(cropped, new PhysicalRectangle(clamped.Left, clamped.Top, clamped.Width, clamped.Height), 1.0);
    }

    private static PhysicalRectangle ClampRegion(PhysicalRectangle region, int width, int height)
    {
        var left = (int)Math.Max(0, Math.Floor(region.Left));
        var top = (int)Math.Max(0, Math.Floor(region.Top));
        var right = (int)Math.Min(width, Math.Ceiling(region.Left + region.Width));
        var bottom = (int)Math.Min(height, Math.Ceiling(region.Top + region.Height));

        return new PhysicalRectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private static async Task<(uint response, IDictionary<string, object> results)> WaitForResponseAsync(IPortalRequest request)
    {
        var tcs = new TaskCompletionSource<(uint, IDictionary<string, object>)>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watch = await request.WatchResponseAsync((response, results) => tcs.TrySetResult((response, results)));

        var result = await tcs.Task;
        return (result.Item1, result.Item2);
    }

    [DBusInterface("org.freedesktop.portal.Screenshot")]
    private interface IScreenshotPortal : IDBusObject
    {
        Task<ObjectPath> ScreenshotAsync(string parentWindow, IDictionary<string, object> options);
    }

    [DBusInterface("org.freedesktop.portal.Request")]
    private interface IPortalRequest : IDBusObject
    {
        Task<IAsyncDisposable> WatchResponseAsync(Action<uint, IDictionary<string, object>> handler);
    }
}
