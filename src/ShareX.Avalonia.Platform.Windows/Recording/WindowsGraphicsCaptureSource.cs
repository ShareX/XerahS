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

    /// <summary>
    /// Check if Windows.Graphics.Capture is supported on this system
    /// </summary>
    public static bool IsSupported
    {
        get
        {
            try
            {
                // Check Windows version >= 10.0.17134 (1803)
                var version = Environment.OSVersion.Version;
                if (version.Major < 10) return false;
                if (version.Build < 17134) return false;

                // Try to create a test capture item to verify API availability
                return WGC.GraphicsCaptureSession.IsSupported();
            }
            catch
            {
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
        System.Diagnostics.Debug.WriteLine($"Windows version: {version.Major}.{version.Minor}.{version.Build}");
        
        if (version.Build < 19041)
        {
            throw new PlatformNotSupportedException(
                $"Monitor capture requires Windows 10 20H1 (build 19041) or later. Current build: {version.Build}");
        }

        try
        {
            _d3dDevice = CreateD3DDevice();
            System.Diagnostics.Debug.WriteLine("D3D device created successfully");
            
            _device = CreateDirect3DDeviceFromD3D11Device(_d3dDevice);
            System.Diagnostics.Debug.WriteLine("IDirect3DDevice created successfully");

            var monitorHandle = GetPrimaryMonitorHandle();
            System.Diagnostics.Debug.WriteLine($"Primary monitor handle: 0x{monitorHandle:X}");
            
            if (monitorHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to get primary monitor handle");
            }

            _captureItem = CaptureHelper.CreateItemForMonitor(monitorHandle);
            System.Diagnostics.Debug.WriteLine($"Capture item created: {_captureItem?.DisplayName ?? "null"}");
            
            if (_captureItem == null)
            {
                throw new InvalidOperationException("Failed to create capture item for monitor");
            }

            _framePool = WGC.Direct3D11CaptureFramePool.Create(
                _device,
                global::Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                _captureItem.Size);

            _framePool.FrameArrived += OnFrameArrived;
            System.Diagnostics.Debug.WriteLine("Frame pool created and event wired");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"InitializeForPrimaryMonitor failed: {ex}");
            Dispose();
            throw new PlatformNotSupportedException(
                "Failed to initialize Windows.Graphics.Capture for monitor.",
                ex);
        }
    }

    public Task StartCaptureAsync()
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
            _session.StartCapture();
            _isCapturing = true;
        }

        return Task.CompletedTask;
    }

    public Task StopCaptureAsync()
    {
        lock (_lock)
        {
            if (!_isCapturing) return Task.CompletedTask;

            _session?.Dispose();
            _session = null;
            _isCapturing = false;
        }

        return Task.CompletedTask;
    }

    private void OnFrameArrived(WGC.Direct3D11CaptureFramePool sender, object args)
    {
        if (_disposed || !_isCapturing) return;

        try
        {
            using var frame = sender.TryGetNextFrame();
            if (frame == null) return;

            // Get Direct3D surface
            var surface = frame.Surface;
            if (surface == null) return;

            // Convert to FrameData
            var frameData = ConvertSurfaceToFrameData(surface, frame.SystemRelativeTime);

            // Raise event on capture thread
            // Note: Encoder must handle thread marshaling if needed
            FrameArrived?.Invoke(this, new FrameArrivedEventArgs(frameData));
        }
        catch (Exception ex)
        {
            // Log error but don't crash capture thread
            System.Diagnostics.Debug.WriteLine($"Error processing captured frame: {ex.Message}");
        }
    }

    private FrameData ConvertSurfaceToFrameData(WD3D.IDirect3DSurface surface, TimeSpan systemRelativeTime)
    {
        // Get the underlying Direct3D11 texture via COM interop
        // IDirect3DDxgiInterfaceAccess is a COM interface for accessing DXGI interfaces from WinRT objects
        var surfacePtr = Marshal.GetIUnknownForObject(surface);
        
        try
        {
            var iidAccess = typeof(IDirect3DDxgiInterfaceAccess).GUID;
            Marshal.QueryInterface(surfacePtr, ref iidAccess, out var accessPtr);
            
            if (accessPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to get IDirect3DDxgiInterfaceAccess");
            }

            try
            {
                var access = (IDirect3DDxgiInterfaceAccess)Marshal.GetObjectForIUnknown(accessPtr);
                var dxgiGuid = typeof(IDXGISurface).GUID;
                var hr = access.GetInterface(ref dxgiGuid, out var dxgiPtr);
                
                if (hr != 0 || dxgiPtr == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"Failed to get DXGI surface: HRESULT {hr}");
                }

                var dxgiSurface = (IDXGISurface)Marshal.GetObjectForIUnknown(dxgiPtr);
                Marshal.Release(dxgiPtr);

                try
                {
                    var desc = dxgiSurface.Description;

                    // Map surface for CPU access
                    var mapped = dxgiSurface.Map(Vortice.DXGI.MapFlags.Read);

                    return new FrameData
                    {
                        DataPtr = mapped.DataPointer,
                        Stride = (int)mapped.Pitch,
                        Width = (int)desc.Width,
                        Height = (int)desc.Height,
                        Timestamp = (long)(systemRelativeTime.TotalMilliseconds * 10000), // Convert to 100ns units
                        Format = PixelFormat.Bgra32 // WGC always provides BGRA32
                    };
                }
                finally
                {
                    dxgiSurface.Unmap();
                }
            }
            finally
            {
                Marshal.Release(accessPtr);
            }
        }
        finally
        {
            Marshal.Release(surfacePtr);
        }
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
        var inspectable = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice);
        return (WD3D.IDirect3DDevice)inspectable;
    }

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern uint CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    private static object CreateDirect3D11DeviceFromDXGIDevice(IDXGIDevice dxgiDevice)
    {
        var hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var pUnknown);
        if (hr != 0)
        {
            throw new COMException("Failed to create Direct3D11 device from DXGI device", (int)hr);
        }

        return Marshal.GetObjectForIUnknown(pUnknown);
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
            var hr = interop.CreateForWindow(hwnd, ref guid, out var item);
            if (hr != 0 || item == null) return null;
            
            return item;
        }
        catch
        {
            return null;
        }
    }

    public static WGC.GraphicsCaptureItem? CreateItemForMonitor(IntPtr hmonitor)
    {
        var interop = GraphicsCaptureItemInterop.GetInterop();
        if (interop == null)
        {
            throw new InvalidOperationException("Failed to get IGraphicsCaptureItemInterop activation factory. Windows.Graphics.Capture may not be supported.");
        }
        
        var guid = GraphicsCaptureItemGuid;
        var hr = interop.CreateForMonitor(hmonitor, ref guid, out var item);
        
        if (hr != 0)
        {
            throw new InvalidOperationException($"CreateForMonitor failed with HRESULT 0x{hr:X8}. Monitor handle: 0x{hmonitor:X}");
        }
        
        if (item == null)
        {
            throw new InvalidOperationException($"CreateForMonitor returned null for monitor handle 0x{hmonitor:X}");
        }
        
        return item;
    }
}

/// <summary>
/// COM interface for creating GraphicsCaptureItem from Win32 handles
/// This avoids the need for Windows App SDK WindowId/DisplayId types
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
        [Out, MarshalAs(UnmanagedType.Interface)] out WGC.GraphicsCaptureItem result);

    [PreserveSig]
    int CreateForMonitor(
        IntPtr monitor,
        [In] ref Guid riid,
        [Out, MarshalAs(UnmanagedType.Interface)] out WGC.GraphicsCaptureItem result);
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

