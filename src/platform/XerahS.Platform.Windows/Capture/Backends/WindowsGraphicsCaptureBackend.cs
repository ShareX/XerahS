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

using System.Drawing;
using System.Runtime.InteropServices;
using SkiaSharp;
using Vortice.Direct3D11;
using Vortice.DXGI;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Windows.Capture.Engine;
using XerahS.Platform.Windows.Capture.Wgc;
using WGC = global::Windows.Graphics.Capture;
using WD3D = global::Windows.Graphics.DirectX.Direct3D11;

namespace XerahS.Platform.Windows.Capture.Backends;

/// <summary>
/// Captures through Windows.Graphics.Capture, the compositor's own capture API. Each monitor under the
/// request yields one frame, which is cropped on the GPU before it is read back.
/// Used only where the capture border can be turned off (Windows 11 / Server 2022, build 20348+),
/// so a still capture never flashes the yellow border.
/// </summary>
internal sealed class WindowsGraphicsCaptureBackend : IScreenCaptureBackend
{
    private const int BorderlessMinimumBuild = 20348;
    private const int FrameTimeoutMilliseconds = 1000;

    private static readonly Lazy<bool> Supported = new(DetectSupport);
    private static readonly Lazy<bool> BorderlessAccessGranted = new(RequestBorderlessAccess);

    private readonly IScreenService _screenService;
    private readonly object _deviceLock = new();
    private ID3D11Device? _device;
    private WD3D.IDirect3DDevice? _winRTDevice;

    public WindowsGraphicsCaptureBackend(IScreenService screenService)
    {
        _screenService = screenService ?? throw new ArgumentNullException(nameof(screenService));
    }

    public static bool IsSupported => Supported.Value;

    public CaptureBackendKind Kind => CaptureBackendKind.GraphicsCapture;

    public string Name => "Windows.Graphics.Capture";

    public SKBitmap? TryCapture(ScreenCaptureRequest request)
    {
        if (!BorderlessAccessGranted.Value)
        {
            return null;
        }

        if (!CaptureRectResolver.TryResolve(request, _screenService.GetVirtualScreenBounds(), out Rectangle captureRect))
        {
            return null;
        }

        var monitors = WindowsDisplayEnumeration.GetMonitors()
            .Where(monitor => monitor.Bounds.IntersectsWith(captureRect))
            .ToList();
        if (monitors.Count == 0)
        {
            return null;
        }

        var bitmap = new SKBitmap(captureRect.Width, captureRect.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Black);
        }

        bool captured;
        lock (_deviceLock)
        {
            try
            {
                EnsureDevice();
                captured = monitors.All(monitor => TryCaptureMonitor(monitor, captureRect, request.DrawCursor, bitmap));
            }
            catch
            {
                // A lost or removed device is rebuilt on the next capture.
                ResetDevice();
                bitmap.Dispose();
                throw;
            }
        }

        if (!captured)
        {
            bitmap.Dispose();
            return null;
        }

        bitmap.NotifyPixelsChanged();
        DebugHelper.WriteLine($"Screen capture: Windows.Graphics.Capture succeeded ({bitmap.Width}x{bitmap.Height}, {monitors.Count} monitors)");

        return HdrScreenshotColorCorrector.ApplyIfEnabled(bitmap, captureRect, request.HdrColorCorrection);
    }

    private bool TryCaptureMonitor(
        WindowsDisplayEnumeration.MonitorData monitor,
        Rectangle captureRect,
        bool drawCursor,
        SKBitmap destination)
    {
        WGC.GraphicsCaptureItem? item = CaptureHelper.CreateItemForMonitor(monitor.Handle);
        if (item == null)
        {
            return false;
        }

        var itemSize = item.Size;
        if (itemSize.Width != monitor.Bounds.Width || itemSize.Height != monitor.Bounds.Height)
        {
            // The frame would need scaling to line up with the desktop; leave this capture to GDI.
            DebugHelper.WriteLine(
                $"Windows.Graphics.Capture: {monitor.DeviceName} frame {itemSize.Width}x{itemSize.Height} does not match bounds {monitor.Bounds.Width}x{monitor.Bounds.Height}.");
            return false;
        }

        // Declared first so it is disposed last: the pool can raise FrameArrived until it is closed.
        using var frameArrived = new ManualResetEventSlim(false);
        using var framePool = WGC.Direct3D11CaptureFramePool.CreateFreeThreaded(
            _winRTDevice!,
            global::Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
            1,
            itemSize);
        using var session = framePool.CreateCaptureSession(item);
        session.IsBorderRequired = false;
        session.IsCursorCaptureEnabled = drawCursor;

        framePool.FrameArrived += (_, _) => frameArrived.Set();
        session.StartCapture();

        if (!frameArrived.Wait(FrameTimeoutMilliseconds))
        {
            DebugHelper.WriteLine($"Windows.Graphics.Capture: no frame from {monitor.DeviceName} within {FrameTimeoutMilliseconds} ms.");
            return false;
        }

        using var frame = framePool.TryGetNextFrame();
        if (frame == null)
        {
            return false;
        }

        using ID3D11Texture2D texture = Direct3D11Interop.GetTexture(frame.Surface);
        CopyVisibleRegion(texture, monitor.Bounds, captureRect, destination);
        return true;
    }

    private void CopyVisibleRegion(ID3D11Texture2D texture, Rectangle monitorBounds, Rectangle captureRect, SKBitmap destination)
    {
        Rectangle visible = Rectangle.Intersect(monitorBounds, captureRect);
        var sourceRegion = new Rectangle(visible.X - monitorBounds.X, visible.Y - monitorBounds.Y, visible.Width, visible.Height);
        TextureRegionReader.CopyRegion(
            _device!,
            texture,
            sourceRegion,
            destination,
            visible.X - captureRect.X,
            visible.Y - captureRect.Y);
    }

    private void EnsureDevice()
    {
        if (_device != null && _winRTDevice != null)
        {
            return;
        }

        ResetDevice();
        _device = Direct3D11Interop.CreateHardwareDevice();
        _winRTDevice = Direct3D11Interop.CreateWinRTDevice(_device);
    }

    private void ResetDevice()
    {
        _winRTDevice?.Dispose();
        _winRTDevice = null;
        _device?.Dispose();
        _device = null;
    }

    private static bool DetectSupport()
    {
        try
        {
            return Environment.OSVersion.Version.Build >= BorderlessMinimumBuild &&
                WGC.GraphicsCaptureSession.IsSupported();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"Windows.Graphics.Capture: support check failed. {ex.Message}");
            return false;
        }
    }

    private static bool RequestBorderlessAccess()
    {
        try
        {
            var status = WGC.GraphicsCaptureAccess
                .RequestAccessAsync(WGC.GraphicsCaptureAccessKind.Borderless)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            bool granted = status == global::Windows.Security.Authorization.AppCapabilityAccess.AppCapabilityAccessStatus.Allowed;
            DebugHelper.WriteLine($"Windows.Graphics.Capture: borderless capture access {status}.");
            return granted;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"Windows.Graphics.Capture: borderless capture access unavailable. {ex.Message}");
            return false;
        }
    }
}
