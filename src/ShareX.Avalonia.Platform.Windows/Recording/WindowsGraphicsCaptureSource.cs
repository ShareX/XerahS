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

using System.Runtime.InteropServices;
using System.Threading;
using WGC = global::Windows.Graphics.Capture;
using WD3D = global::Windows.Graphics.DirectX.Direct3D11;
using Vortice.Direct3D11;
using Vortice.DXGI;
using XerahS.ScreenCapture.ScreenRecording;

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
                
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "THREAD", "Dedicated DispatcherQueue thread created successfully.");
            }
            catch (Exception ex)
            {
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "THREAD", $"Failed to create dedicated thread: {ex}");
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
            catch (Exception ex) { Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "THREAD", $"Action failed: {ex}"); }
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
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "THREAD", $"Async Action failed: {ex}");
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
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "THREAD", $"Action failed: {ex}");
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
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC", "Checking Windows.Graphics.Capture support...");

                // Check Windows version >= 10.0.17134 (1803)
                var version = Environment.OSVersion.Version;
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC", $"OS Version: {version.Major}.{version.Minor}.{version.Build}");

                if (version.Major < 10)
                {
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC", $"✗ OS Major version {version.Major} < 10");
                    return false;
                }

                if (version.Build < 17134)
                {
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC", $"✗ OS Build {version.Build} < 17134 (requires Windows 10 1803+)");
                    return false;
                }

                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC", $"✓ OS version check passed (10.{version.Minor}.{version.Build})");

                // Try to create a test capture item to verify API availability
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC", "Calling GraphicsCaptureSession.IsSupported()...");
                bool apiSupported = WGC.GraphicsCaptureSession.IsSupported();
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC", $"GraphicsCaptureSession.IsSupported() = {apiSupported}");

                if (apiSupported)
                {
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC", "✓ Windows.Graphics.Capture is fully supported");
                }
                else
                {
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC", "✗ GraphicsCaptureSession.IsSupported() returned false");
                }

                return apiSupported;
            }
            catch (Exception ex)
            {
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC", $"✗ EXCEPTION checking WGC support: {ex.GetType().Name}: {ex.Message}");
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC", $"Stack trace: {ex.StackTrace}");
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
                _d3dDevice = CreateD3DDevice();
                _device = CreateDirect3DDeviceFromD3D11Device(_d3dDevice);

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
        Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", $"Windows version: {version.Major}.{version.Minor}.{version.Build}");
        
        if (version.Build < 19041)
        {
            Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", $"✗ Build {version.Build} < 19041, monitor capture not supported");
            throw new PlatformNotSupportedException(
                $"Monitor capture requires Windows 10 20H1 (build 19041) or later. Current build: {version.Build}");
        }

        RunOnCaptureThreadAndWait(() =>
        {
            try
            {
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", "Creating D3D11 device (on Capture Thread)...");
                _d3dDevice = CreateD3DDevice();
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", "✓ D3D device created successfully");
                
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", "Creating IDirect3DDevice from D3D11...");
                _device = CreateDirect3DDeviceFromD3D11Device(_d3dDevice);
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", "✓ IDirect3DDevice created successfully");

                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", "Getting primary monitor handle...");
                var monitorHandle = GetPrimaryMonitorHandle();
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", $"Primary monitor handle: 0x{monitorHandle:X}");
                
                if (monitorHandle == IntPtr.Zero)
                {
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", "✗ Monitor handle is NULL");
                    throw new InvalidOperationException("Failed to get primary monitor handle");
                }

                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", "Calling CaptureHelper.CreateItemForMonitor...");
                _captureItem = CaptureHelper.CreateItemForMonitor(monitorHandle);
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", $"✓ Capture item created: {_captureItem?.DisplayName ?? "null"}");
                
                if (_captureItem == null)
                {
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", "✗ Capture item is NULL");
                    throw new InvalidOperationException("Failed to create capture item for monitor");
                }

                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", $"Creating frame pool (size: {_captureItem.Size.Width}x{_captureItem.Size.Height})...");
                _framePool = WGC.Direct3D11CaptureFramePool.Create(
                    _device,
                    global::Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    _captureItem.Size);

                _framePool.FrameArrived += OnFrameArrived;
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", "✓ Frame pool created and event wired");
            }
            catch (Exception ex)
            {
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", $"✗ InitializeForPrimaryMonitor FAILED: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", $"  Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INIT", $"  Stack trace: {ex.StackTrace}");
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
                
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC", "StartCaptureAsync: Calling _session.StartCapture()...");
                System.Console.WriteLine("WGC: Calling _session.StartCapture()...");
                _session.StartCapture();
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC", "StartCaptureAsync: _session.StartCapture() returned.");
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

    private async void OnFrameArrived(WGC.Direct3D11CaptureFramePool sender, object args)
    {
        if (_disposed || !_isCapturing) return;

        // Log sparingly
        if ((_frameCount % 30) == 0)
        {
             Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC", $"OnFrameArrived! Frame {_frameCount}");
             System.Console.WriteLine($"WGC: OnFrameArrived! Frame {_frameCount}");
        }

        try
        {
            using var frame = sender.TryGetNextFrame();
            if (frame == null) return;

            // Get Direct3D surface
            var surface = frame.Surface;
            if (surface == null) return;

            // Use SoftwareBitmap to get access to pixel data (Standard WinRT way, robust to interop issues)
            using var softwareBitmap = await global::Windows.Graphics.Imaging.SoftwareBitmap.CreateCopyFromSurfaceAsync(surface);
            
            int width = softwareBitmap.PixelWidth;
            int height = softwareBitmap.PixelHeight;
            uint size = (uint)(width * height * 4);
            
            // Create IBuffer
            var buffer = new global::Windows.Storage.Streams.Buffer(size);
            buffer.Length = size; // Must set length to receive data
            softwareBitmap.CopyToBuffer(buffer);
            
            // Get pointer via IBufferByteAccess
            var bufferUnknown = Marshal.GetIUnknownForObject(buffer);
            IntPtr dataPtr;
            
            try
            {
                var iidBytes = typeof(IBufferByteAccess).GUID;
                if (Marshal.QueryInterface(bufferUnknown, ref iidBytes, out var pByteAccess) != 0)
                {
                    throw new InvalidCastException("Failed to query IBufferByteAccess");
                }
                
                try
                {
                    var byteAccess = (IBufferByteAccess)Marshal.GetObjectForIUnknown(pByteAccess);
                    byteAccess.Buffer(out dataPtr);
                }
                finally
                {
                    Marshal.Release(pByteAccess);
                }
            }
            finally
            {
                Marshal.Release(bufferUnknown);
            }

            // Calculate stride
            int stride = width * 4; // Buffer is tightly packed
            
            // Create FrameData (DataPtr is valid only because buffer is alive in this scope? Reference counting?)
            // IBuffer object 'buffer' is managed wrapper. As long as 'buffer' is alive, dataPtr should be valid.
            // But we didn't use 'using var buffer'. 'buffer' will be GC'd?
            // No, we should invoke event BEFORE buffer is GC'd.
            
            var frameData = new FrameData
            {
                DataPtr = dataPtr,
                Stride = stride,
                Width = width,
                Height = height,
                Timestamp = (long)(frame.SystemRelativeTime.TotalMilliseconds * 10000),
                Format = PixelFormat.Bgra32
            };

            // Raise event synchronously so we can use the pointer
            FrameArrived?.Invoke(this, new FrameArrivedEventArgs(frameData));
            
            // End of method, buffer is eligible for GC.
            // But FrameArrived handlers are synchronous, right?
            // Yes.
            // Wait, IBuffer is a COM object.
            // dataPtr points to its internal memory.
            // If buffer wrapper is GC'd, the COM object might be released.
            // I should ensure buffer stays alive.
            GC.KeepAlive(buffer);
        }
        catch (Exception ex)
        {
            // Log error but don't crash capture thread
            System.Console.WriteLine($"WGC Error processing captured frame: {ex.Message}");
        }

        Interlocked.Increment(ref _frameCount);
    }

    private static ID3D11Device CreateD3DDevice()
    {
        var result = D3D11.D3D11CreateDevice(
            null,
            Vortice.Direct3D.DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            null,
            out var device,
            out _,
            out _);

        if (result.Failure || device == null)
        {
            throw new InvalidOperationException($"Failed to create Direct3D11 device: {result}");
        }

        return device;
    }

    private static WD3D.IDirect3DDevice CreateDirect3DDeviceFromD3D11Device(ID3D11Device d3dDevice)
    {
        // Use Windows.Graphics.Capture interop to create IDirect3DDevice
        var dxgiDevice = d3dDevice.QueryInterface<IDXGIDevice>();
        return CreateDirect3DDeviceFromDXGIDevice(dxgiDevice);
    }

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern uint CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    private static WD3D.IDirect3DDevice CreateDirect3DDeviceFromDXGIDevice(IDXGIDevice dxgiDevice)
    {
        var hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var pUnknown);
        if (hr != 0)
        {
            throw new COMException("Failed to create Direct3D11 device from DXGI device", (int)hr);
        }

        // Use CsWinRT marshaling for proper WinRT type projection
        return WinRT.MarshalInterface<WD3D.IDirect3DDevice>.FromAbi(pUnknown);
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
                 Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "DISPOSE", $"Error disposing: {ex}");
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

/// <summary>
/// COM interface for accessing DXGI interfaces from WinRT Direct3D objects
/// This interface must be defined manually as it's not provided by the WinRT SDK
/// </summary>
[ComImport]
[Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDirect3DDxgiInterfaceAccess
{
    [PreserveSig]
    int GetInterface([In] ref Guid iid, out IntPtr p);
}


/// <summary>
/// Helper class for creating GraphicsCaptureItem from HWND/HMONITOR
/// Uses IGraphicsCaptureItemInterop COM interface (works without Windows App SDK)
/// </summary>
internal static class CaptureHelper
{
    private static readonly Guid GraphicsCaptureItemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    
    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    public static WGC.GraphicsCaptureItem? CreateItemForWindow(IntPtr hwnd)
    {
        try
        {
            var interop = GraphicsCaptureItemInterop.GetInterop();
            if (interop == null) return null;
            
            var guid = GraphicsCaptureItemGuid;
            var hr = interop.CreateForWindow(hwnd, ref guid, out var itemPtr);
            if (hr != 0 || itemPtr == IntPtr.Zero) return null;
            
            // Use CsWinRT marshaling - properly handles WinRT type projection from COM interface pointer
            return WinRT.MarshalInterface<WGC.GraphicsCaptureItem>.FromAbi(itemPtr);
        }
        catch
        {
            return null;
        }
    }

    public static WGC.GraphicsCaptureItem? CreateItemForMonitor(IntPtr hmonitor)
    {
        Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INTEROP", $"CreateItemForMonitor called with handle: 0x{hmonitor:X}");
        
        var interop = GraphicsCaptureItemInterop.GetInterop();
        if (interop == null)
        {
            Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INTEROP", "✗ GetInterop() returned null - WGC activation factory not available");
            throw new InvalidOperationException("Failed to get IGraphicsCaptureItemInterop activation factory. Windows.Graphics.Capture may not be supported.");
        }
        
        Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INTEROP", "✓ Interop factory obtained, calling CreateForMonitor...");
        var guid = GraphicsCaptureItemGuid;
        var hr = interop.CreateForMonitor(hmonitor, ref guid, out var itemPtr);
        
        Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INTEROP", $"CreateForMonitor returned HRESULT: 0x{hr:X8}, itemPtr=0x{itemPtr:X}");
        
        if (hr != 0)
        {
            Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INTEROP", $"✗ CreateForMonitor FAILED with HRESULT 0x{hr:X8}");
            throw new InvalidOperationException($"CreateForMonitor failed with HRESULT 0x{hr:X8}. Monitor handle: 0x{hmonitor:X}");
        }
        
        if (itemPtr == IntPtr.Zero)
        {
            Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INTEROP", "✗ CreateForMonitor returned null pointer");
            throw new InvalidOperationException($"CreateForMonitor returned null for monitor handle 0x{hmonitor:X}");
        }
        
        // Use CsWinRT marshaling - properly handles WinRT type projection from COM interface pointer
        Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INTEROP", "Marshaling IntPtr to GraphicsCaptureItem using CsWinRT...");
        var item = WinRT.MarshalInterface<WGC.GraphicsCaptureItem>.FromAbi(itemPtr);
        Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INTEROP", $"✓ GraphicsCaptureItem created successfully: {item.DisplayName}");
        return item;
    }
}

/// <summary>
/// COM interface for creating GraphicsCaptureItem from Win32 handles
/// This avoids the need for Windows App SDK WindowId/DisplayId types
/// NOTE: Uses IntPtr for output because WinRT marshaling doesn't work with COM interop attributes
/// </summary>
[ComImport]
[Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IGraphicsCaptureItemInterop
{
    [PreserveSig]
    int CreateForWindow(
        IntPtr window,
        [In] ref Guid riid,
        out IntPtr result);

    [PreserveSig]
    int CreateForMonitor(
        IntPtr monitor,
        [In] ref Guid riid,
        out IntPtr result);
}

/// <summary>
/// Helper to get the IGraphicsCaptureItemInterop activation factory
/// </summary>
internal static class GraphicsCaptureItemInterop
{
    private static IGraphicsCaptureItemInterop? _interop;

    public static IGraphicsCaptureItemInterop? GetInterop()
    {
        if (_interop != null) return _interop;

        try
        {
            // Get the activation factory for GraphicsCaptureItem
            var hString = WindowsRuntimeMarshal.StringToHString("Windows.Graphics.Capture.GraphicsCaptureItem");
            var iid = typeof(IGraphicsCaptureItemInterop).GUID;
            var hr = RoGetActivationFactory(hString, ref iid, out var factory);
            WindowsRuntimeMarshal.FreeHString(hString);
            
            if (hr != 0) return null;
            
            _interop = (IGraphicsCaptureItemInterop?)Marshal.GetObjectForIUnknown(factory);
            Marshal.Release(factory);
            
            return _interop;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int RoGetActivationFactory(
        IntPtr activatableClassId,
        [In] ref Guid iid,
        out IntPtr factory);
}

/// <summary>
/// Helper for WinRT string marshaling
/// </summary>
internal static class WindowsRuntimeMarshal
{
    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
        int length,
        out IntPtr hstring);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int WindowsDeleteString(IntPtr hstring);

    public static IntPtr StringToHString(string str)
    {
        WindowsCreateString(str, str.Length, out var hstring);
        return hstring;
    }

    public static void FreeHString(IntPtr hstring)
    {
        WindowsDeleteString(hstring);
    }
}

