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

using System.ComponentModel;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Windows.Hdr;

/// <summary>Creates HDR surfaces that live inside an existing host window.</summary>
public sealed class WindowsHdrSurfaceService : IHdrSurfaceService
{
    public bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041);

    public IHdrSurface? Create(nint parentWindowHandle, ReadOnlySpan<byte> rgbaFloat16, int width, int height, int rowBytes)
    {
        if (!IsSupported || parentWindowHandle == 0 || width <= 0 || height <= 0 ||
            rowBytes < width * 8 || rgbaFloat16.Length < rowBytes * height)
        {
            return null;
        }

        try
        {
            return HdrSurfaceWindow.Create(parentWindowHandle, rgbaFloat16.ToArray(), width, height, rowBytes);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[HDR] embedded HDR surface failed");
            return null;
        }
    }
}

/// <summary>
/// A WS_CHILD window with an FP16 scRGB flip chain. The swapchain keeps the image's own size and
/// DXGI stretches it to the child window, so resizing the host never needs a buffer reallocation
/// and the present path stays a single CopyResource.
/// </summary>
internal sealed class HdrSurfaceWindow : IHdrSurface
{
    private const string ClassName = "XerahS.HdrSurface";
    private const uint WsChild = 0x40000000;
    private const uint WsVisible = 0x10000000;
    private const uint WsClipSiblings = 0x04000000;
    private const uint WmDestroy = 0x0002;
    private const uint WmPaint = 0x000F;
    private const uint WmEraseBackground = 0x0014;
    private const uint WmTimer = 0x0113;
    private const int SwShow = 5;
    private const int SwHide = 0;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const nint TimerId = 1;

    private static readonly object Sync = new();
    private static HdrSurfaceWindow? _active;
    private static WndProcDelegate? _wndProc;
    private static ushort _classAtom;

    private nint _hwnd;
    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGISwapChain1? _swapChain;
    private ID3D11Texture2D? _source;
    private bool _disposed;

    public static HdrSurfaceWindow? Create(nint parent, byte[] pixels, int width, int height, int rowBytes)
    {
        lock (Sync)
        {
            if (_active != null)
            {
                // One embedded surface is enough for the comparison window.
                return null;
            }

            var surface = new HdrSurfaceWindow();
            try
            {
                surface.CreateCore(parent, pixels, width, height, rowBytes);
                _active = surface;
                return surface;
            }
            catch (Exception ex)
            {
                surface.Dispose();
                DebugHelper.WriteException(ex, "[HDR] embedded HDR surface creation failed");
                return null;
            }
        }
    }

    private void CreateCore(nint parent, byte[] pixels, int width, int height, int rowBytes)
    {
        if (!EnsureWindowClass())
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "RegisterClassW failed");
        }

        _hwnd = CreateWindowExW(
            0, ClassName, "HDR surface", WsChild | WsVisible | WsClipSiblings,
            0, 0, width, height,
            parent, 0, GetModuleHandleW(null), 0);

        if (_hwnd == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateWindowExW failed");
        }

        D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.None,
            new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 },
            out ID3D11Device device,
            out ID3D11DeviceContext context).CheckError();

        _device = device;
        _context = context;

        using IDXGIFactory2 factory = DXGI.CreateDXGIFactory2<IDXGIFactory2>(false);
        SwapChainDescription1 description = new()
        {
            Width = (uint)width,
            Height = (uint)height,
            Format = Format.R16G16B16A16_Float,
            Stereo = false,
            SampleDescription = new SampleDescription(1, 0),
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = 2,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipSequential,
            AlphaMode = AlphaMode.Ignore,
            Flags = SwapChainFlags.None
        };

        _swapChain = factory.CreateSwapChainForHwnd(device, _hwnd, description);

        Texture2DDescription texture = new()
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.R16G16B16A16_Float,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Immutable,
            BindFlags = BindFlags.ShaderResource,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.None
        };

        unsafe
        {
            fixed (byte* pointer = pixels)
            {
                var data = new SubresourceData((nint)pointer, (uint)rowBytes, (uint)(rowBytes * height));
                _source = device.CreateTexture2D(texture, new[] { data });
            }
        }

        Present();
        SetTimer(_hwnd, TimerId, 200, 0);
    }

    public void SetBounds(int x, int y, int width, int height)
    {
        if (_disposed || _hwnd == 0 || width <= 0 || height <= 0)
        {
            return;
        }

        SetWindowPos(_hwnd, 0, x, y, width, height, SwpNoActivate | SwpShowWindow);
        Present();
    }

    public void SetVisible(bool visible)
    {
        if (_disposed || _hwnd == 0)
        {
            return;
        }

        ShowWindow(_hwnd, visible ? SwShow : SwHide);
        if (visible)
        {
            Present();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_hwnd != 0)
        {
            KillTimer(_hwnd, TimerId);
            DestroyWindow(_hwnd);
            _hwnd = 0;
        }

        _source?.Dispose();
        _source = null;
        _swapChain?.Dispose();
        _swapChain = null;
        _context?.Dispose();
        _context = null;
        _device?.Dispose();
        _device = null;

        lock (Sync)
        {
            if (ReferenceEquals(_active, this))
            {
                _active = null;
            }
        }
    }

    private void Present()
    {
        if (_swapChain == null || _context == null || _source == null)
        {
            return;
        }

        using ID3D11Texture2D backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        _context.CopyResource(backBuffer, _source);
        _swapChain.Present(1, PresentFlags.None);
    }

    private static bool EnsureWindowClass()
    {
        if (_classAtom != 0)
        {
            return true;
        }

        _wndProc = WindowProcedure;

        WNDCLASSW windowClass = new()
        {
            lpfnWndProc = _wndProc,
            hInstance = GetModuleHandleW(null),
            lpszClassName = ClassName,
            hbrBackground = 0
        };

        _classAtom = RegisterClassW(ref windowClass);
        return _classAtom != 0;
    }

    private static nint WindowProcedure(nint hwnd, uint message, nint wParam, nint lParam)
    {
        switch (message)
        {
            case WmPaint:
                _active?.Present();
                ValidateRect(hwnd, 0);
                return 0;

            case WmTimer:
                _active?.Present();
                return 0;

            case WmEraseBackground:
                return 1;

            case WmDestroy:
                KillTimer(hwnd, TimerId);
                return 0;
        }

        return DefWindowProcW(hwnd, message, wParam, lParam);
    }

    private delegate nint WndProcDelegate(nint hWnd, uint message, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSW
    {
        public uint style;
        public WndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassW(ref WNDCLASSW windowClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowExW(
        uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height,
        nint parent, nint menu, nint instance, nint param);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hwnd, int command);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern bool ValidateRect(nint hwnd, nint rect);

    [DllImport("user32.dll")]
    private static extern nint SetTimer(nint hwnd, nint id, uint timeout, nint procedure);

    [DllImport("user32.dll")]
    private static extern bool KillTimer(nint hwnd, nint id);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(nint hwnd);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProcW(nint hwnd, uint message, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? moduleName);
}
