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
using SkiaSharp;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Windows.Capture.Engine;

namespace XerahS.Platform.Windows.Capture.Backends;

/// <summary>
/// Captures through DXGI Desktop Duplication (Windows 8+). Only the outputs under the requested area are
/// duplicated, and on unrotated SDR outputs only the requested pixels are copied off the GPU.
/// </summary>
internal sealed class DxgiDesktopDuplicationBackend : IScreenCaptureBackend
{
    /// <summary>Minimum Windows version for DXGI 1.2 OutputDuplication (Windows 8+).</summary>
    private static readonly Version MinimumVersion = new(6, 2);

    private const int CursorSettleMilliseconds = 50;
    private const int FrameAccumulationMilliseconds = 50;
    private const uint FirstAcquireTimeoutMilliseconds = 250;
    private const uint RetryAcquireTimeoutMilliseconds = 500;

    private readonly IScreenService _screenService;

    public DxgiDesktopDuplicationBackend(IScreenService screenService)
    {
        _screenService = screenService ?? throw new ArgumentNullException(nameof(screenService));
    }

    public static bool IsSupported => Environment.OSVersion.Version >= MinimumVersion;

    public CaptureBackendKind Kind => CaptureBackendKind.DesktopDuplication;

    public string Name => "DXGI Desktop Duplication";

    public SKBitmap? TryCapture(ScreenCaptureRequest request)
    {
        // DWM can render the cursor into the duplicated surface. Hide it globally unless it was asked for;
        // a requested cursor is composited afterwards at its exact position.
        using var cursorScope = CursorVisibilityScope.HideIf(!request.DrawCursor, CursorSettleMilliseconds);

        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        if (factory == null) return null;

        List<DxgiOutputInfo> outputs = DxgiOutputEnumerator.Enumerate(factory);
        try
        {
            if (outputs.Count == 0) return null;

            Rectangle desktopBounds = DxgiOutputEnumerator.GetDesktopBounds(outputs);
            if (desktopBounds.Width <= 0 || desktopBounds.Height <= 0) return null;

            if (request.Area == CaptureArea.VirtualDesktop)
            {
                return CaptureDesktopArea(outputs, desktopBounds, request.DrawCursor);
            }

            Rectangle virtualBounds = _screenService.GetVirtualScreenBounds();
            if (!DxgiCropRectHelper.TryCreateCropRect(request.Bounds, virtualBounds, desktopBounds.Width, desktopBounds.Height, out SKRectI cropRect))
            {
                return null;
            }

            if (DxgiCropRectHelper.TryMapCropToDesktop(cropRect, desktopBounds, virtualBounds, out Rectangle targetBounds))
            {
                return CaptureDesktopArea(outputs, targetBounds, request.DrawCursor);
            }

            // The duplicated outputs do not match the reported virtual screen, so the crop is scaled.
            // Capture everything and crop exactly as a whole-desktop capture would.
            using SKBitmap? fullBitmap = CaptureDesktopArea(outputs, desktopBounds, request.DrawCursor);
            return fullBitmap == null ? null : Crop(fullBitmap, cropRect);
        }
        finally
        {
            DxgiOutputEnumerationCleanupHelper.DisposeOutputsAndAdapters(
                outputs,
                item => item.Output,
                item => item.Adapter);
        }
    }

    /// <summary>
    /// Captures <paramref name="targetBounds"/> (desktop coordinates) from the outputs that intersect it.
    /// Areas no output covers stay black.
    /// </summary>
    private static SKBitmap? CaptureDesktopArea(IReadOnlyList<DxgiOutputInfo> outputs, Rectangle targetBounds, bool drawCursor)
    {
        var targetOutputs = outputs.Where(output => output.Bounds.IntersectsWith(targetBounds)).ToList();

        var bitmap = new SKBitmap(targetBounds.Width, targetBounds.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Black);
        }

        var duplications = new List<OutputDuplication>();
        var devices = new List<ID3D11Device>();
        int capturedOutputCount = 0;

        try
        {
            // 1. One device per adapter, one duplication per output.
            foreach (var group in targetOutputs.GroupBy(output => output.Adapter))
            {
                if (D3D11.D3D11CreateDevice(group.Key, DriverType.Unknown, DeviceCreationFlags.BgraSupport,
                    new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 }, out var device).Failure || device == null)
                {
                    continue;
                }
                devices.Add(device);

                foreach (DxgiOutputInfo output in group)
                {
                    try
                    {
                        duplications.Add(new OutputDuplication(
                            DxgiOutputDuplicationHelper.Create(output.Output, device),
                            device,
                            output,
                            HdrToneMapContext.FromOutput(output.Output)));
                    }
                    catch (Exception ex)
                    {
                        // Output might be disconnected or in use
                        DebugHelper.WriteLine($"CaptureFullScreenDxgi: Setup failed for output. {ex}");
                    }
                }
            }

            // 2. One global wait lets every new duplication accumulate its first frame.
            if (duplications.Count > 0)
            {
                Thread.Sleep(FrameAccumulationMilliseconds);
            }

            // 3. Acquire and compose frames.
            foreach (OutputDuplication duplication in duplications)
            {
                if (TryComposeOutput(duplication, targetBounds, bitmap))
                {
                    capturedOutputCount++;
                }
            }
        }
        finally
        {
            foreach (OutputDuplication duplication in duplications) duplication.Duplication.Dispose();
            foreach (ID3D11Device device in devices) device.Dispose();
        }

        if (DxgiFrameAcquisitionHelper.ShouldFallbackToGdi(duplications.Count, capturedOutputCount))
        {
            bitmap.Dispose();
            DebugHelper.WriteLine(
                $"CaptureFullScreenDxgi: Captured {capturedOutputCount}/{duplications.Count} outputs; using fallback.");
            return null;
        }

        bitmap.NotifyPixelsChanged();

        // Log success so the log file verifies DXGI (Vortice.Direct3D11/DXGI) was actually used
        DebugHelper.WriteLine(
            $"Screen capture: DXGI Output Duplication succeeded ({bitmap.Width}x{bitmap.Height}, {capturedOutputCount}/{outputs.Count} outputs)");

        if (drawCursor)
        {
            TryCompositeCursor(bitmap, targetBounds);
        }

        return bitmap;
    }

    private static bool TryComposeOutput(OutputDuplication duplication, Rectangle targetBounds, SKBitmap destination)
    {
        IDXGIOutputDuplication outputDuplication = duplication.Duplication;
        DxgiOutputInfo output = duplication.Output;
        bool frameAcquired = false;

        try
        {
            var acquireResult = outputDuplication.AcquireNextFrame(FirstAcquireTimeoutMilliseconds, out var frameInfo, out var desktopResource);

            if (DxgiFrameAcquisitionHelper.ShouldRetryFrameAcquisition(
                acquireResult.Success, desktopResource != null, frameInfo.LastPresentTime))
            {
                if (acquireResult.Success)
                {
                    ReleaseFrameQuietly(outputDuplication);
                }

                desktopResource?.Dispose();
                acquireResult = outputDuplication.AcquireNextFrame(RetryAcquireTimeoutMilliseconds, out frameInfo, out desktopResource);
            }

            frameAcquired = acquireResult.Success;

            if (!DxgiFrameAcquisitionHelper.IsUsableFrame(acquireResult.Success, desktopResource != null, frameInfo.LastPresentTime))
            {
                DebugHelper.WriteLine("CaptureFullScreenDxgi: AcquireFrame failed or timed out after retry.");
                return false;
            }

            using var resource = desktopResource!;
            using var desktopTexture = resource.QueryInterface<ID3D11Texture2D>();
            var sourceDesc = desktopTexture.Description;
            DebugHelper.WriteLine(
                $"CaptureFullScreenDxgi: Output {output.DeviceName} frame source={sourceDesc.Width}x{sourceDesc.Height}, target={output.Bounds.Width}x{output.Bounds.Height}, rotation={output.Rotation}, dxgiRotation={output.DxgiRotation}");

            if (CanCopyVisibleRegionOnly(output, sourceDesc))
            {
                CopyVisibleRegion(duplication.Device, desktopTexture, output.Bounds, targetBounds, destination);
                return true;
            }

            return ComposeWholeOutput(duplication, desktopTexture, sourceDesc, targetBounds, destination);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"CaptureFullScreenDxgi: Frame capture failed. {ex}");
            return false;
        }
        finally
        {
            if (frameAcquired)
            {
                ReleaseFrameQuietly(outputDuplication);
            }
        }
    }

    /// <summary>
    /// An unrotated SDR surface maps 1:1 onto the desktop, so the visible part can be cropped on the GPU.
    /// Rotated and HDR outputs go through the whole-output path.
    /// </summary>
    private static bool CanCopyVisibleRegionOnly(DxgiOutputInfo output, Texture2DDescription sourceDesc) =>
        DxgiOutputEnumerator.ToClockwiseDegrees(output.Rotation) == 0 &&
        !DxgiHdrToneMapper.IsHdrFormat(sourceDesc.Format) &&
        sourceDesc.Format == Format.B8G8R8A8_UNorm &&
        sourceDesc.Width == (uint)output.Bounds.Width &&
        sourceDesc.Height == (uint)output.Bounds.Height;

    private static void CopyVisibleRegion(
        ID3D11Device device,
        ID3D11Texture2D desktopTexture,
        Rectangle outputBounds,
        Rectangle targetBounds,
        SKBitmap destination)
    {
        Rectangle visible = Rectangle.Intersect(outputBounds, targetBounds);
        var sourceRegion = new Rectangle(visible.X - outputBounds.X, visible.Y - outputBounds.Y, visible.Width, visible.Height);
        TextureRegionReader.CopyRegion(
            device,
            desktopTexture,
            sourceRegion,
            destination,
            visible.X - targetBounds.X,
            visible.Y - targetBounds.Y);
    }

    private static bool ComposeWholeOutput(
        OutputDuplication duplication,
        ID3D11Texture2D desktopTexture,
        Texture2DDescription sourceDesc,
        Rectangle targetBounds,
        SKBitmap destination)
    {
        DxgiOutputInfo output = duplication.Output;
        ID3D11Device device = duplication.Device;

        // For rotated outputs, Desktop Duplication can return an unrotated surface
        // whose dimensions differ from desktop bounds. Always match staging to source.
        var stagingDesc = new Texture2DDescription
        {
            Width = sourceDesc.Width,
            Height = sourceDesc.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = sourceDesc.Format,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None
        };

        using var staging = device.CreateTexture2D(stagingDesc);
        device.ImmediateContext.CopyResource(staging, desktopTexture);

        var dataBox = device.ImmediateContext.Map(staging, 0, MapMode.Read);
        try
        {
            SKBitmap? sourceBitmap;
            if (DxgiHdrToneMapper.IsHdrFormat(sourceDesc.Format))
            {
                sourceBitmap = DxgiHdrToneMapper.TryConvertToBgra(dataBox, sourceDesc, duplication.HdrContext);
                if (sourceBitmap == null)
                {
                    DebugHelper.WriteLine(
                        $"CaptureFullScreenDxgi: HDR tone-map failed for {output.DeviceName} ({sourceDesc.Format}).");
                    return false;
                }
            }
            else
            {
                sourceBitmap = new SKBitmap((int)sourceDesc.Width, (int)sourceDesc.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                int bytesPerRow = (int)sourceDesc.Width * 4;
                BgraRowCopyHelper.CopyRows(
                    dataBox.DataPointer,
                    (int)dataBox.RowPitch,
                    sourceBitmap.GetPixels(),
                    bytesPerRow,
                    bytesPerRow,
                    (int)sourceDesc.Height);
            }

            using (sourceBitmap)
            {
                // Rotate the output frame back into desktop orientation when DXGI provides it unrotated.
                using SKBitmap bitmapToDraw = BitmapRotationHelper.RotateClockwise(
                    sourceBitmap,
                    DxgiOutputEnumerator.ToClockwiseDegrees(output.Rotation));

                int destinationX = output.Bounds.Left - targetBounds.Left;
                int destinationY = output.Bounds.Top - targetBounds.Top;
                var destinationRect = new SKRect(
                    destinationX,
                    destinationY,
                    destinationX + output.Bounds.Width,
                    destinationY + output.Bounds.Height);

                using var canvas = new SKCanvas(destination);
                canvas.DrawBitmap(bitmapToDraw, destinationRect, SKSamplingOptions.Default);
            }

            return true;
        }
        finally
        {
            device.ImmediateContext.Unmap(staging, 0);
        }
    }

    private static SKBitmap Crop(SKBitmap source, SKRectI cropRect)
    {
        var cropped = new SKBitmap(cropRect.Width, cropRect.Height);
        using var canvas = new SKCanvas(cropped);
        canvas.DrawBitmap(source, cropRect, new SKRect(0, 0, cropRect.Width, cropRect.Height), SKSamplingOptions.Default);
        return cropped;
    }

    private static void TryCompositeCursor(SKBitmap bitmap, Rectangle captureBounds)
    {
        try
        {
            var cursor = new CursorData();
            DxgiCursorCompositionHelper.TryCompositeCursor(
                bitmap,
                cursor.IsVisible,
                cursor.Position,
                cursor.Hotspot,
                cursor.Size,
                captureBounds,
                cursor.DrawCursor);
        }
        catch
        {
            // Cursor composition is best-effort; the capture must still succeed if cursor APIs fail.
        }
    }

    private static void ReleaseFrameQuietly(IDXGIOutputDuplication duplication)
    {
        try
        {
            duplication.ReleaseFrame();
        }
        catch
        {
            // Ignore frame release failures during cleanup.
        }
    }

    private sealed record OutputDuplication(
        IDXGIOutputDuplication Duplication,
        ID3D11Device Device,
        DxgiOutputInfo Output,
        HdrToneMapContext HdrContext);
}
