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
using Vortice.Direct3D11;
using Vortice.DXGI;
using WGC = global::Windows.Graphics.Capture;
using WD3D = global::Windows.Graphics.DirectX.Direct3D11;

namespace XerahS.Platform.Windows.Capture.Wgc;

/// <summary>
/// Direct3D 11 device helpers shared by Windows.Graphics.Capture screenshots and recording.
/// </summary>
internal static class Direct3D11Interop
{
    /// <summary>IID of Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess.</summary>
    private static readonly Guid DxgiInterfaceAccessInterfaceId = new("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");

    /// <summary>Returns the D3D11 texture behind a Windows.Graphics.Capture frame surface. The caller owns it.</summary>
    public static ID3D11Texture2D GetTexture(WD3D.IDirect3DSurface surface)
    {
        IntPtr surfacePointer = WinRT.MarshalInterface<WD3D.IDirect3DSurface>.FromManaged(surface);
        try
        {
            Guid accessInterfaceId = DxgiInterfaceAccessInterfaceId;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(surfacePointer, in accessInterfaceId, out IntPtr accessPointer));
            using var access = new IDirect3DDxgiInterfaceAccess(accessPointer);
            return access.GetInterface<ID3D11Texture2D>();
        }
        finally
        {
            Marshal.Release(surfacePointer);
        }
    }

    /// <summary>Creates a BGRA-capable hardware device on the default adapter.</summary>
    public static ID3D11Device CreateHardwareDevice()
    {
        var result = D3D11.D3D11CreateDevice(
            null,
            Vortice.Direct3D.DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            null!,
            out var device,
            out _,
            out _);

        if (result.Failure || device == null)
        {
            throw new InvalidOperationException($"Failed to create Direct3D11 device: {result}");
        }

        return device;
    }

    /// <summary>Wraps a D3D11 device as the WinRT device Windows.Graphics.Capture expects.</summary>
    public static WD3D.IDirect3DDevice CreateWinRTDevice(ID3D11Device d3dDevice)
    {
        // Use Windows.Graphics.Capture interop to create IDirect3DDevice
        using var dxgiDevice = d3dDevice.QueryInterface<IDXGIDevice>();
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
}

/// <summary>
/// Helper class for creating GraphicsCaptureItem from HWND/HMONITOR
/// Uses IGraphicsCaptureItemInterop COM interface (works without Windows App SDK)
/// </summary>
internal static class CaptureHelper
{
    private static readonly Guid GraphicsCaptureItemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    
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
        System.Console.WriteLine($"[WGC_INTEROP] CreateItemForMonitor called with handle: 0x{hmonitor:X}");
        XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INTEROP", $"CreateItemForMonitor called with handle: 0x{hmonitor:X}");

        var interop = GraphicsCaptureItemInterop.GetInterop();
        if (interop == null)
        {
            System.Console.WriteLine("[WGC_INTEROP] ✗ GetInterop() returned null - WGC activation factory not available");
            XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INTEROP", "✗ GetInterop() returned null - WGC activation factory not available");
            throw new InvalidOperationException("Failed to get IGraphicsCaptureItemInterop activation factory. Windows.Graphics.Capture may not be supported.");
        }

        System.Console.WriteLine("[WGC_INTEROP] ✓ Interop factory obtained, calling CreateForMonitor...");
        XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INTEROP", "✓ Interop factory obtained, calling CreateForMonitor...");
        var guid = GraphicsCaptureItemGuid;
        var hr = interop.CreateForMonitor(hmonitor, ref guid, out var itemPtr);

        System.Console.WriteLine($"[WGC_INTEROP] CreateForMonitor returned HRESULT: 0x{hr:X8}, itemPtr=0x{itemPtr:X}");
        XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INTEROP", $"CreateForMonitor returned HRESULT: 0x{hr:X8}, itemPtr=0x{itemPtr:X}");

        if (hr != 0)
        {
            System.Console.WriteLine($"[WGC_INTEROP] ✗ CreateForMonitor FAILED with HRESULT 0x{hr:X8}");
            XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INTEROP", $"✗ CreateForMonitor FAILED with HRESULT 0x{hr:X8}");
            throw new InvalidOperationException($"CreateForMonitor failed with HRESULT 0x{hr:X8}. Monitor handle: 0x{hmonitor:X}");
        }

        if (itemPtr == IntPtr.Zero)
        {
            System.Console.WriteLine("[WGC_INTEROP] ✗ CreateForMonitor returned null pointer");
            XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INTEROP", "✗ CreateForMonitor returned null pointer");
            throw new InvalidOperationException($"CreateForMonitor returned null for monitor handle 0x{hmonitor:X}");
        }

        // Use CsWinRT marshaling - properly handles WinRT type projection from COM interface pointer
        System.Console.WriteLine("[WGC_INTEROP] Marshaling IntPtr to GraphicsCaptureItem using CsWinRT...");
        XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INTEROP", "Marshaling IntPtr to GraphicsCaptureItem using CsWinRT...");
        var item = WinRT.MarshalInterface<WGC.GraphicsCaptureItem>.FromAbi(itemPtr);
        System.Console.WriteLine($"[WGC_INTEROP] ✓ GraphicsCaptureItem created successfully: {item.DisplayName}");
        XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "WGC_INTEROP", $"✓ GraphicsCaptureItem created successfully: {item.DisplayName}");
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

