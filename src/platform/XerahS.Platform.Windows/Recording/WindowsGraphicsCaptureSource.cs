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

using System.Runtime.InteropServices;
using System.Threading;
using WGC = global::Windows.Graphics.Capture;
using WD3D = global::Windows.Graphics.DirectX.Direct3D11;
using XerahS.RegionCapture.ScreenRecording;
using Vortice.Direct3D11;
using XerahS.Platform.Windows.Capture.Wgc;

namespace XerahS.Platform.Windows.Recording;

/// <summary>
/// Windows.Graphics.Capture implementation for modern screen recording
/// Requires Windows 10 version 1803 (build 17134) or later
/// </summary>
public class WindowsGraphicsCaptureSource : ICaptureSource
{
    private WGC.GraphicsCaptureItem? _captureItem;
    private WGC.Direct3D11CaptureFramePool? _framePool;
    private WGC.GraphicsCaptureSession? _session;
    private ID3D11Device? _d3dDevice;
    private ID3D11Texture2D? _stagingTexture;
    private WD3D.IDirect3DDevice? _device;
    private readonly object _lock = new();
    private bool _isCapturing;
    private bool _disposed;
    private int _frameCount = 0;

    // [2026-01-10 11:30] Dedicated System Thread support
    private global::Windows.System.DispatcherQueueController? _dispatcherQueueController;
    private global::Windows.System.DispatcherQueue? _dispatcherQueue;

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, out IntPtr dispatcherQueueController);

    [StructLayout(LayoutKind.Sequential)]
    struct DispatcherQueueOptions
    {
        public int dwSize;
        public int threadType;
        public int apartmentType;
    }

    private void EnsureCaptureThread()
    {
        lock (_lock)
        {
            if (_dispatcherQueueController != null) return;

            try 
            {
                var options = new DispatcherQueueOptions
                {
                    dwSize = Marshal.SizeOf<DispatcherQueueOptions>(),
                    threadType = 1, // DQTYPE_THREAD_DEDICATED
                    apartmentType = 2 // DQTAT_COM_STA
                };

                int hr = CreateDispatcherQueueController(options, out var controllerPtr);
                if (hr != 0 || controllerPtr == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"Failed to create DispatcherQueueController. HRESULT: 0x{hr:X}");
                }

                // Marshal the native pointer to the managed WinRT object
                _dispatcherQueueController = WinRT.MarshalInterface<global::Windows.System.DispatcherQueueController>.FromAbi(controllerPtr);
                _dispatcherQueue = _dispatcherQueueController.DispatcherQueue;
                
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "THREAD", "Dedicated DispatcherQueue thread created successfully.");
            }
            catch (Exception ex)
            {
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "THREAD", $"Failed to create dedicated thread: {ex}");
                throw;
            }
        }
    }
    // Manual CaptureThreadProc removed


    private void RunOnCaptureThread(Action action)
    {
        EnsureCaptureThread();
        if (_dispatcherQueue == null) throw new InvalidOperationException("DispatcherQueue not initialized");
        
        bool enqueued = _dispatcherQueue.TryEnqueue(() =>
        {
            try { action(); }
            catch (Exception ex) { XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "THREAD", $"Action failed: {ex}"); }
        });

        if (!enqueued) throw new InvalidOperationException("Failed to enqueue operation");
    }

    private async Task RunOnCaptureThreadAsync(Func<Task> action)
    {
        EnsureCaptureThread();
        if (_dispatcherQueue == null) throw new InvalidOperationException("DispatcherQueue not initialized");

        var tcs = new TaskCompletionSource<bool>();
        bool enqueued = _dispatcherQueue.TryEnqueue(async () =>
        {
            try 
            { 
                await action(); 
                tcs.SetResult(true);
            }
            catch (Exception ex) 
            { 
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "THREAD", $"Async Action failed: {ex}");
                tcs.SetException(ex);
            }
        });

        if (!enqueued) throw new InvalidOperationException("Failed to enqueue operation");
        await tcs.Task;
    }

    private void RunOnCaptureThreadAndWait(Action action)
    {
        EnsureCaptureThread();
        if (_dispatcherQueue == null) throw new InvalidOperationException("DispatcherQueue not initialized");

        var tcs = new TaskCompletionSource<bool>();
        bool enqueued = _dispatcherQueue.TryEnqueue(() =>
        {
            try 
            { 
                action(); 
                tcs.SetResult(true);
            }
            catch (Exception ex) 
            { 
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "THREAD", $"Action failed: {ex}");
                tcs.SetException(ex);
            }
        });

        if (!enqueued) throw new InvalidOperationException("Failed to enqueue operation");
        tcs.Task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Check if Windows.Graphics.Capture is supported on this system
    /// </summary>
    public static bool IsSupported
    {
        get
        {
            try
            {
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC", "Checking Windows.Graphics.Capture support...");

                // Check Windows version >= 10.0.17134 (1803)
                var version = Environment.OSVersion.Version;
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC", $"OS Version: {version.Major}.{version.Minor}.{version.Build}");

                if (version.Major < 10)
                {
                    XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC", $"✗ OS Major version {version.Major} < 10");
                    return false;
                }

                if (version.Build < 17134)
                {
                    XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC", $"✗ OS Build {version.Build} < 17134 (requires Windows 10 1803+)");
                    return false;
                }

                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC", $"✓ OS version check passed (10.{version.Minor}.{version.Build})");

                // Try to create a test capture item to verify API availability
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC", "Calling GraphicsCaptureSession.IsSupported()...");
                bool apiSupported = WGC.GraphicsCaptureSession.IsSupported();
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC", $"GraphicsCaptureSession.IsSupported() = {apiSupported}");

                if (apiSupported)
                {
                    XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC", "✓ Windows.Graphics.Capture is fully supported");
                }
                else
                {
                    XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC", "✗ GraphicsCaptureSession.IsSupported() returned false");
                }

                return apiSupported;
            }
            catch (Exception ex)
            {
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC", $"✗ EXCEPTION checking WGC support: {ex.GetType().Name}: {ex.Message}");
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC", $"Stack trace: {ex.StackTrace}");
                return false;
            }
        }
    }

    public event EventHandler<FrameArrivedEventArgs>? FrameArrived;

    /// <summary>
    /// Whether to capture the mouse cursor in the recording
    /// Stage 2: Window & Region Parity
    /// Default: true
    /// </summary>
    public bool ShowCursor { get; set; } = true;

    /// <summary>
    /// Initialize capture for a specific window
    /// </summary>
    public void InitializeForWindow(IntPtr hwnd)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WindowsGraphicsCaptureSource));

        RunOnCaptureThreadAndWait(() =>
        {
            try
            {
                // Create Direct3D device
                _d3dDevice = Direct3D11Interop.CreateHardwareDevice();
                _device = Direct3D11Interop.CreateWinRTDevice(_d3dDevice);

                // Create capture item from window handle
                _captureItem = CaptureHelper.CreateItemForWindow(hwnd);
                if (_captureItem == null)
                {
                    throw new InvalidOperationException("Failed to create capture item for window");
                }

                // Create frame pool
                _framePool = WGC.Direct3D11CaptureFramePool.Create(
                    _device,
                    global::Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2, // Number of buffers
                    _captureItem.Size);

                _framePool.FrameArrived += OnFrameArrived;
            }
            catch (Exception ex)
            {
                Dispose();
                throw new PlatformNotSupportedException(
                    "Failed to initialize Windows.Graphics.Capture. This may be due to Windows version < 1803 or missing permissions.",
                    ex);
            }
        });
    }

    /// <summary>
    /// Initialize capture for the primary monitor
    /// Note: Monitor capture requires Windows 10 20H1 (build 19041) or later
    /// </summary>
    public void InitializeForPrimaryMonitor()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WindowsGraphicsCaptureSource));

        // Check if running on Windows 10 20H1+ for monitor capture
        var version = Environment.OSVersion.Version;
        XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", $"Windows version: {version.Major}.{version.Minor}.{version.Build}");
        
        if (version.Build < 19041)
        {
            XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", $"✗ Build {version.Build} < 19041, monitor capture not supported");
            throw new PlatformNotSupportedException(
                $"Monitor capture requires Windows 10 20H1 (build 19041) or later. Current build: {version.Build}");
        }

        RunOnCaptureThreadAndWait(() =>
        {
            try
            {
                System.Console.WriteLine("[WGC] Creating D3D11 device (on Capture Thread)...");
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", "Creating D3D11 device (on Capture Thread)...");
                _d3dDevice = Direct3D11Interop.CreateHardwareDevice();
                System.Console.WriteLine("[WGC] ✓ D3D device created successfully");
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", "✓ D3D device created successfully");

                System.Console.WriteLine("[WGC] Creating IDirect3DDevice from D3D11...");
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", "Creating IDirect3DDevice from D3D11...");
                _device = Direct3D11Interop.CreateWinRTDevice(_d3dDevice);
                System.Console.WriteLine("[WGC] ✓ IDirect3DDevice created successfully");
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", "✓ IDirect3DDevice created successfully");

                System.Console.WriteLine("[WGC] Getting primary monitor handle...");
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", "Getting primary monitor handle...");
                var monitorHandle = GetPrimaryMonitorHandle();
                System.Console.WriteLine($"[WGC] Primary monitor handle: 0x{monitorHandle:X}");
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", $"Primary monitor handle: 0x{monitorHandle:X}");

                if (monitorHandle == IntPtr.Zero)
                {
                    System.Console.WriteLine("[WGC] ✗ Monitor handle is NULL");
                    XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", "✗ Monitor handle is NULL");
                    throw new InvalidOperationException("Failed to get primary monitor handle");
                }

                System.Console.WriteLine("[WGC] Calling CaptureHelper.CreateItemForMonitor...");
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", "Calling CaptureHelper.CreateItemForMonitor...");
                _captureItem = CaptureHelper.CreateItemForMonitor(monitorHandle);
                System.Console.WriteLine($"[WGC] ✓ Capture item created: {_captureItem?.DisplayName ?? "null"}");
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", $"✓ Capture item created: {_captureItem?.DisplayName ?? "null"}");

                if (_captureItem == null)
                {
                    System.Console.WriteLine("[WGC] ✗ Capture item is NULL");
                    XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", "✗ Capture item is NULL");
                    throw new InvalidOperationException("Failed to create capture item for monitor");
                }

                System.Console.WriteLine($"[WGC] Creating frame pool (size: {_captureItem.Size.Width}x{_captureItem.Size.Height})...");
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", $"Creating frame pool (size: {_captureItem.Size.Width}x{_captureItem.Size.Height})...");
                _framePool = WGC.Direct3D11CaptureFramePool.Create(
                    _device,
                    global::Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    _captureItem.Size);

                _framePool.FrameArrived += OnFrameArrived;
                System.Console.WriteLine("[WGC] ✓ Frame pool created and event wired");
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", "✓ Frame pool created and event wired");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[WGC] ✗ FAILED: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Console.WriteLine($"[WGC]   Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
                System.Console.WriteLine($"[WGC]   Stack: {ex.StackTrace}");

                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", $"✗ InitializeForPrimaryMonitor FAILED: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", $"  Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", $"  Stack trace: {ex.StackTrace}");
                Dispose();
                throw new PlatformNotSupportedException(
                    "Failed to initialize Windows.Graphics.Capture for monitor.",
                    ex);
            }
        });
    }

    public Task StartCaptureAsync()
    {
        return RunOnCaptureThreadAsync(() =>
        {
            lock (_lock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(WindowsGraphicsCaptureSource));
                if (_isCapturing) return Task.CompletedTask;
                if (_captureItem == null || _framePool == null)
                {
                    throw new InvalidOperationException("Capture source not initialized. Call InitializeForWindow or InitializeForPrimaryMonitor first.");
                }

                _session = _framePool.CreateCaptureSession(_captureItem);
                _session.IsCursorCaptureEnabled = ShowCursor; // Stage 2: Configurable cursor capture
                
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC", "StartCaptureAsync: Calling _session.StartCapture()...");
                System.Console.WriteLine("WGC: Calling _session.StartCapture()...");
                _session.StartCapture();
                XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC", "StartCaptureAsync: _session.StartCapture() returned.");
                System.Console.WriteLine("WGC: _session.StartCapture() returned. Capture started.");
                
                _isCapturing = true;
                return Task.CompletedTask;
            }
        });
    }

    public Task StopCaptureAsync()
    {
        return RunOnCaptureThreadAsync(() =>
        {
            lock (_lock)
            {
                if (!_isCapturing) return Task.CompletedTask;

                _session?.Dispose();
                _session = null;
                _isCapturing = false;
                return Task.CompletedTask;
            }
        });
    }

    private void OnFrameArrived(WGC.Direct3D11CaptureFramePool sender, object args)
    {
        if (_disposed || !_isCapturing) return;

        // Log sparingly
        if ((_frameCount % 30) == 0)
        {
             XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC", $"OnFrameArrived! Frame {_frameCount}");
        }

        try
        {
            using var frame = sender.TryGetNextFrame();
            if (frame?.Surface == null) return;

            // Read the frame back through a reused staging texture. The frame pool raises this event on the
            // capture thread that owns the device, so the immediate context is never used concurrently.
            using ID3D11Texture2D texture = Direct3D11Interop.GetTexture(frame.Surface);
            Texture2DDescription description = texture.Description;
            ID3D11Texture2D staging = GetStagingTexture(description);
            ID3D11DeviceContext context = _d3dDevice!.ImmediateContext;
            context.CopyResource(staging, texture);

            MappedSubresource mapped = context.Map(staging, 0, MapMode.Read);
            try
            {
                var frameData = new FrameData
                {
                    DataPtr = mapped.DataPointer,
                    Stride = (int)mapped.RowPitch,
                    Width = (int)description.Width,
                    Height = (int)description.Height,
                    Timestamp = (long)(frame.SystemRelativeTime.TotalMilliseconds * 10000),
                    Format = PixelFormat.Bgra32
                };

                // Handlers are synchronous and must finish with the pointer before the texture is unmapped.
                FrameArrived?.Invoke(this, new FrameArrivedEventArgs(frameData));
            }
            finally
            {
                context.Unmap(staging, 0);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash capture thread
            XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC", $"Error processing captured frame: {ex.Message}");
        }

        Interlocked.Increment(ref _frameCount);
    }

    private ID3D11Texture2D GetStagingTexture(Texture2DDescription source)
    {
        if (_stagingTexture != null)
        {
            Texture2DDescription current = _stagingTexture.Description;
            if (current.Width == source.Width && current.Height == source.Height && current.Format == source.Format)
            {
                return _stagingTexture;
            }

            _stagingTexture.Dispose();
            _stagingTexture = null;
        }

        _stagingTexture = _d3dDevice!.CreateTexture2D(new Texture2DDescription
        {
            Width = source.Width,
            Height = source.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = source.Format,
            SampleDescription = new Vortice.DXGI.SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None
        });

        return _stagingTexture;
    }

    private static IntPtr GetPrimaryMonitorHandle()
    {
        return MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTOPRIMARY);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    private const uint MONITOR_DEFAULTTOPRIMARY = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            _disposed = true;
            _isCapturing = false;

            try
            {
                // Run cleanup on capture thread if it exists
                if (_dispatcherQueue != null)
                {
                    var cleanupTask = RunOnCaptureThreadAsync(() =>
                    {
                        if (_framePool != null)
                        {
                            _framePool.FrameArrived -= OnFrameArrived;
                            _framePool.Dispose();
                            _framePool = null;
                        }

                        _session?.Dispose();
                        _session = null;
                        _captureItem = null;
                        _device = null;
                        _stagingTexture?.Dispose();
                        _stagingTexture = null;
                        _d3dDevice?.Dispose();
                        _d3dDevice = null;
                        return Task.CompletedTask;
                    });
                    
                    // Wait for cleanup with timeout
                    cleanupTask.Wait(1000);
                }

                // Shutdown the dedicated thread
                if (_dispatcherQueueController != null)
                {
                    _ = _dispatcherQueueController.ShutdownQueueAsync();
                }
            }
            catch (Exception ex)
            {
                 XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "DISPOSE", $"Error disposing: {ex}");
            }
            finally
            {
                _dispatcherQueueController = null;
                _dispatcherQueue = null;
            }
        }

        GC.SuppressFinalize(this);
    }
}
