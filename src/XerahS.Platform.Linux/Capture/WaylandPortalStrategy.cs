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
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions.Capture;
using SkiaSharp;
using Tmds.DBus;
using XerahS.Common;

namespace XerahS.Platform.Linux.Capture;

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

            var options = new Dictionary<string, object>
            {
                ["modal"] = false,
                ["interactive"] = false
            };

            var (bitmap, response) = await TryPortalScreenshotAsync(connection, portal, options).ConfigureAwait(false);
            if (bitmap == null && response == 2)
            {
                DebugHelper.WriteLine("WaylandPortalStrategy: Portal non-interactive capture failed; retrying interactive.");
                options["interactive"] = true;
                options["modal"] = true;
                (bitmap, _) = await TryPortalScreenshotAsync(connection, portal, options).ConfigureAwait(false);
            }

            if (bitmap == null)
            {
                if (response != 0)
                {
                    DebugHelper.WriteLine($"WaylandPortalStrategy: Portal request failed with response code: {response}");
                }
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

    private static async Task<(SKBitmap? bitmap, uint response)> TryPortalScreenshotAsync(Connection connection, IScreenshotPortal portal, IDictionary<string, object> options)
    {
        var requestPath = await portal.ScreenshotAsync(string.Empty, options).ConfigureAwait(false);
        var request = connection.CreateProxy<IPortalRequest>(PortalBusName, requestPath);
        var (response, results) = await request.WaitForResponseAsync().ConfigureAwait(false);

        if (response != 0)
        {
            return (null, response);
        }

        if (!results.TryGetValue("uri", out var uriValue) || uriValue is not string uriStr)
        {
            return (null, response);
        }

        var uri = new Uri(uriStr);
        if (!uri.IsFile || string.IsNullOrEmpty(uri.LocalPath) || !File.Exists(uri.LocalPath))
        {
            return (null, response);
        }

        using var stream = File.OpenRead(uri.LocalPath);
        var bitmap = SKBitmap.Decode(stream);
        return (bitmap, response);
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
            canvas.DrawBitmap(source, new SKRectI(clamped.X, clamped.Y, clamped.X + clamped.Width, clamped.Y + clamped.Height), new SKRect(0, 0, clamped.Width, clamped.Height));

            return new CapturedBitmap(cropped, new PhysicalRectangle(clamped.X, clamped.Y, clamped.Width, clamped.Height), 1.0);
    }

    private static PhysicalRectangle ClampRegion(PhysicalRectangle region, int width, int height)
    {
        var left = Math.Max(0, region.X);
        var top = Math.Max(0, region.Y);
        var right = Math.Min(width, region.X + region.Width);
        var bottom = Math.Min(height, region.Y + region.Height);

        return new PhysicalRectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }
}
