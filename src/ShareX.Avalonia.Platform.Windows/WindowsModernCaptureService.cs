#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

using XerahS.Platform.Abstractions;
using SkiaSharp;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace XerahS.Platform.Windows
{
    /// <summary>
    /// Modern Windows screen capture using Direct3D11 and DXGI Output Duplication.
    /// Falls back to GDI+ on older Windows versions.
    /// </summary>
#pragma warning disable CA1416 // Platform compatibility - we do runtime checks via IsSupported
    public class WindowsModernCaptureService : IScreenCaptureService
    {
        private readonly IScreenService _screenService;
        private readonly WindowsScreenCaptureService _fallbackService;

        /// <summary>
        /// Minimum Windows version for DXGI 1.2 OutputDuplication (Windows 8+)
        /// </summary>
        private static readonly Version MinimumDxgiVersion = new Version(6, 2);

        /// <summary>
        /// Check if the current OS supports DXGI output duplication
        /// </summary>
        public static bool IsSupported => Environment.OSVersion.Version >= MinimumDxgiVersion;

        public WindowsModernCaptureService(IScreenService screenService)
        {
            _screenService = screenService;
            _fallbackService = new WindowsScreenCaptureService(screenService);
        }

        public async Task<SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null)
        {
            return await CaptureFullScreenAsync(options);
        }

        public async Task<SKBitmap?> CaptureRectAsync(SKRect rect, CaptureOptions? options = null)
        {
            var fallbackTaskSettings = XerahS.Core.SettingManager.GetOrCreateWorkflowTaskSettings(XerahS.Core.HotkeyType.None);
            bool useModern = options?.UseModernCapture ?? fallbackTaskSettings.CaptureSettings.UseModernCapture;

            if (!IsSupported || !useModern)
            {
                return await _fallbackService.CaptureRectAsync(rect, options);
            }

            return await Task.Run(() =>
            {
                try
                {
                    // For rect capture, we capture fullscreen then crop
                    using var fullBitmap = CaptureFullScreenDxgi();
                    if (fullBitmap == null) return null;

                    var cropRect = new SKRectI(
                        (int)rect.Left,
                        (int)rect.Top,
                        (int)rect.Right,
                        (int)rect.Bottom
                    );

                    // Clamp to image bounds
                    cropRect.Left = Math.Max(0, cropRect.Left);
                    cropRect.Top = Math.Max(0, cropRect.Top);
                    cropRect.Right = Math.Min(fullBitmap.Width, cropRect.Right);
                    cropRect.Bottom = Math.Min(fullBitmap.Height, cropRect.Bottom);

                    if (cropRect.Width <= 0 || cropRect.Height <= 0)
                        return null;

                    var cropped = new SKBitmap(cropRect.Width, cropRect.Height);
                    using var canvas = new SKCanvas(cropped);
                    canvas.DrawBitmap(fullBitmap, cropRect, new SKRect(0, 0, cropRect.Width, cropRect.Height));
                    return cropped;
                }
                catch (Exception)
                {
                    // Fall back to GDI+ on error
                    return null;
                }
            }) ?? await _fallbackService.CaptureRectAsync(rect, options);
        }

        public async Task<SKBitmap?> CaptureFullScreenAsync(CaptureOptions? options = null)
        {
            var fallbackTaskSettings = XerahS.Core.SettingManager.GetOrCreateWorkflowTaskSettings(XerahS.Core.HotkeyType.None);
            bool useModern = options?.UseModernCapture ?? fallbackTaskSettings.CaptureSettings.UseModernCapture;

            if (!IsSupported || !useModern)
            {
                return await _fallbackService.CaptureFullScreenAsync(options);
            }

            return await Task.Run(() =>
            {
                try
                {
                    return CaptureFullScreenDxgi();
                }
                catch (Exception)
                {
                    return null;
                }
            }) ?? await _fallbackService.CaptureFullScreenAsync(options);
        }

        public async Task<SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService, CaptureOptions? options = null)
        {
            // For window capture, we capture the window bounds
            var hwnd = windowService.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            var bounds = windowService.GetWindowBounds(hwnd);
            if (bounds.Width <= 0 || bounds.Height <= 0) return null;

            return await CaptureRectAsync(new SKRect(bounds.X, bounds.Y, bounds.Right, bounds.Bottom), options);
        }

        public async Task<SKBitmap?> CaptureWindowAsync(IntPtr windowHandle, IWindowService windowService, CaptureOptions? options = null)
        {
            if (windowHandle == IntPtr.Zero) return null;

            var bounds = windowService.GetWindowBounds(windowHandle);
            if (bounds.Width <= 0 || bounds.Height <= 0) return null;

            return await CaptureRectAsync(new SKRect(bounds.X, bounds.Y, bounds.Right, bounds.Bottom), options);
        }

        /// <summary>
        /// Captures the entire virtual screen using DXGI Output Duplication
        /// </summary>
        private SKBitmap? CaptureFullScreenDxgi()
        {
            // Create DXGI factory
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            if (factory == null) return null;

            // Enumerate adapters and outputs
            var outputs = EnumerateOutputs(factory);
            if (outputs.Count == 0) return null;

            // Calculate captured virtual screen bounds
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (var (output, adapter, bounds) in outputs)
            {
                minX = Math.Min(minX, bounds.Left);
                minY = Math.Min(minY, bounds.Top);
                maxX = Math.Max(maxX, bounds.Right);
                maxY = Math.Max(maxY, bounds.Bottom);
            }

            int totalWidth = maxX - minX;
            int totalHeight = maxY - minY;

            if (totalWidth <= 0 || totalHeight <= 0) return null;

            // Create combined bitmap
            var combinedBitmap = new SKBitmap(totalWidth, totalHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(combinedBitmap);
            canvas.Clear(SKColors.Black);

            // Group outputs by adapter to share ID3D11Device
            var outputsByAdapter = outputs.GroupBy(x => x.Adapter).ToList();

            // Track resources for batch processing
            var activeDuplications = new List<(IDXGIOutputDuplication Duplication, ID3D11Device Device, ID3D11Texture2D Staging, System.Drawing.Rectangle Bounds)>();
            var devicesToDispose = new List<ID3D11Device>();

            try
            {
                // 1. Initialize all duplications (Create Devices & Duplications)
                foreach (var group in outputsByAdapter)
                {
                    var adapter = group.Key;

                    // Create one device per adapter
                    if (D3D11.D3D11CreateDevice(adapter, DriverType.Unknown, DeviceCreationFlags.BgraSupport,
                        new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 }, out var device).Failure || device == null)
                    {
                        continue;
                    }
                    devicesToDispose.Add(device);

                    // using var deviceContext = device.ImmediateContext; // REMOVED: Premature disposal causes NRE later

                    foreach (var (output, _, bounds) in group)
                    {
                        try
                        {
                            // Duplicate output
                            var duplication = output.DuplicateOutput(device);

                            // Create staging texture for CPU access
                            var textureDesc = new Texture2DDescription
                            {
                                Width = (uint)bounds.Width,
                                Height = (uint)bounds.Height,
                                MipLevels = 1,
                                ArraySize = 1,
                                Format = Format.B8G8R8A8_UNorm,
                                SampleDescription = new SampleDescription(1, 0),
                                Usage = ResourceUsage.Staging,
                                BindFlags = BindFlags.None,
                                CPUAccessFlags = CpuAccessFlags.Read,
                                MiscFlags = ResourceOptionFlags.None
                            };
                            var staging = device.CreateTexture2D(textureDesc);

                            activeDuplications.Add((duplication, device, staging, bounds));
                        }
                        catch (Exception ex)
                        {
                            // Output might be disconnected or in use
                            XerahS.Common.DebugHelper.WriteLine($"CaptureFullScreenDxgi: Setup failed for output. {ex}");
                        }
                        finally
                        {
                            output.Dispose(); // Dispose IDXGIOutput1 immediately after use
                        }
                    }
                }

                // 2. Wait for next frame (important for first capture initialization)
                // Doing this ONCE creates a small delay that allows all duplications to accumulate a frame
                if (activeDuplications.Count > 0)
                {
                    System.Threading.Thread.Sleep(50); // Reduced from 100ms per screen to 50ms global
                }

                // 3. Acquire & Process Frames
                foreach (var (duplication, device, staging, bounds) in activeDuplications)
                {
                    try
                    {
                        // Acquire frame
                        var acquireResult = duplication.AcquireNextFrame(250, out var frameInfo, out var desktopResource);

                        if (acquireResult.Success && desktopResource != null)
                        {
                            using (desktopResource)
                            {
                                using var desktopTex = desktopResource.QueryInterface<ID3D11Texture2D>();
                                device.ImmediateContext.CopyResource(staging, desktopTex);
                            }
                            duplication.ReleaseFrame();

                            // Map staging texture
                            var dataBox = device.ImmediateContext.Map(staging, 0, MapMode.Read);
                            try
                            {
                                // Draw to combined bitmap
                                DrawMappedTextureToCanvas(dataBox, bounds.Width, bounds.Height, bounds.Left - minX, bounds.Top - minY, canvas);
                            }
                            finally
                            {
                                device.ImmediateContext.Unmap(staging, 0);
                            }
                        }
                        else
                        {
                            XerahS.Common.DebugHelper.WriteLine($"CaptureFullScreenDxgi: AcquireFrame failed or timed out.");
                        }
                    }
                    catch (Exception ex)
                    {
                        XerahS.Common.DebugHelper.WriteLine($"CaptureFullScreenDxgi: Frame capture failed. {ex}");
                    }
                    finally
                    {
                        duplication.Dispose();
                        staging.Dispose();
                    }
                }
            }
            finally
            {
                foreach (var d in devicesToDispose) d.Dispose();

                // Dispose unique adapters
                foreach (var group in outputsByAdapter)
                {
                    group.Key.Dispose();
                }
            }

            return combinedBitmap;
        }

        private void DrawMappedTextureToCanvas(MappedSubresource dataBox, int width, int height, int destX, int destY, SKCanvas canvas)
        {
            // Create a temporary SKBitmap wrapping the data
            // Note: We cannot wrap directly because SKBitmap doesn't support strided data easily without copy or specialized installPixels
            // For simplicity and safety (handling pitch), we copy row by row to a temp bitmap

            using var tempBitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var destPixels = tempBitmap.GetPixels();
            int srcPitch = (int)dataBox.RowPitch;
            int destPitch = width * 4;

            unsafe
            {
                for (int y = 0; y < height; y++)
                {
                    IntPtr srcRow = IntPtr.Add(dataBox.DataPointer, y * srcPitch);
                    IntPtr destRow = IntPtr.Add(destPixels, y * destPitch);

                    Buffer.MemoryCopy((void*)srcRow, (void*)destRow, destPitch, destPitch);
                }
            }

            canvas.DrawBitmap(tempBitmap, destX, destY);
        }

        /// <summary>
        /// Enumerates all DXGI outputs from all adapters
        /// </summary>
        private System.Collections.Generic.List<(IDXGIOutput1 Output, IDXGIAdapter1 Adapter, System.Drawing.Rectangle Bounds)>
            EnumerateOutputs(IDXGIFactory1 factory)
        {
            var outputs = new System.Collections.Generic.List<(IDXGIOutput1, IDXGIAdapter1, System.Drawing.Rectangle)>();

            for (uint adapterIndex = 0; factory.EnumAdapters1(adapterIndex, out var adapter).Success; adapterIndex++)
            {
                var desc = adapter.Description1;

                // Skip software adapters
                if ((desc.Flags & AdapterFlags.Software) != 0)
                {
                    adapter.Dispose();
                    continue;
                }

                // Enumerate outputs for this adapter
                for (uint outputIndex = 0; adapter.EnumOutputs(outputIndex, out var output).Success; outputIndex++)
                {
                    var output1 = output.QueryInterface<IDXGIOutput1>();
                    var outputDesc = output1.Description;

                    var rect = outputDesc.DesktopCoordinates;
                    var bounds = new System.Drawing.Rectangle(
                        rect.Left,
                        rect.Top,
                        rect.Right - rect.Left,
                        rect.Bottom - rect.Top
                    );

                    outputs.Add((output1, adapter, bounds));
                    output.Dispose();
                }

                // Only dispose adapter if no outputs were added from it
                if (outputs.Count == 0 || outputs[^1].Item2 != adapter)
                {
                    adapter.Dispose();
                }
            }

            return outputs;
        }
    }
#pragma warning restore CA1416
}
