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

/// <summary>
/// Opens HDR images in a dedicated Win32 window whose swapchain uses the scRGB colour space, so
/// the desktop compositor receives values above SDR white and an HDR display can show them. The
/// Avalonia shell renders into an SDR surface, which is why this bypasses it entirely.
/// </summary>
public sealed class WindowsHdrPreviewService : IHdrPreviewService
{
    public bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041);

    public bool TryShow(ReadOnlySpan<byte> rgbaFloat16, int width, int height, int rowBytes, string title)
    {
        if (!IsSupported || width <= 0 || height <= 0 || rowBytes < width * 8 || rgbaFloat16.Length < rowBytes * height)
        {
            return false;
        }

        try
        {
            return HdrPreviewWindow.Open(rgbaFloat16.ToArray(), width, height, rowBytes, title);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[HDR] native HDR preview window failed");
            return false;
        }
    }
}

/// <summary>
/// One HDR window at a time: a Win32 window on its own thread presenting an FP16 scRGB flip chain.
/// The image is uploaded once and copied into the back buffer on every present, which is cheap and
/// avoids any shader work.
/// </summary>
internal static class HdrPreviewWindow
{
    private const string ClassName = "XerahS.HdrPreview";
    private const uint WsOverlappedWindow = 0x00CF0000;
    private const uint WsThickFrame = 0x00040000;
    private const uint WsMaximizeBox = 0x00010000;
    private const uint WmDestroy = 0x0002;
    private const uint WmClose = 0x0010;
    private const uint WmPaint = 0x000F;
    private const uint WmEraseBackground = 0x0014;
    private const uint WmTimer = 0x0113;
    private const int SwShow = 5;
    private const int CwUseDefault = unchecked((int)0x80000000);
    private const nint TimerId = 1;

    private static readonly object Sync = new();
    private static readonly ManualResetEventSlim WindowReady = new(false);
    private static WndProcDelegate? _wndProc;
    private static ushort _classAtom;
    private static string? _failure;
    private static bool _isOpen;

    // Created and used only on the preview thread; the window procedure also runs there.
    private static ID3D11Device? _device;
    private static ID3D11DeviceContext? _context;
    private static IDXGISwapChain1? _swapChain;
    private static ID3D11Texture2D? _source;

    public static bool Open(byte[] pixels, int width, int height, int rowBytes, string title)
    {
        lock (Sync)
        {
            if (_isOpen)
            {
                // One preview window is enough; a second click should not stack windows.
                return true;
            }

            WindowReady.Reset();
            _failure = null;

            var thread = new Thread(() => Run(pixels, width, height, rowBytes, title))
            {
                IsBackground = true,
                Name = "XerahS HDR preview"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            // Wait for the window and swapchain so the caller can fall back when creation fails.
            if (!WindowReady.Wait(TimeSpan.FromSeconds(5)))
            {
                _failure = "timed out while creating the HDR window";
            }

            if (_failure != null)
            {
                DebugHelper.WriteLine($"[HDR] HDR preview window not shown: {_failure}");
                return false;
            }

            _isOpen = true;
            return true;
        }
    }

    private static void Run(byte[] pixels, int width, int height, int rowBytes, string title)
    {
        try
        {
            if (!EnsureWindowClass())
            {
                Fail("RegisterClassW failed: " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
                return;
            }

            uint style = WsOverlappedWindow & ~WsThickFrame & ~WsMaximizeBox;
            RECT rect = new() { Left = 0, Top = 0, Right = width, Bottom = height };
            AdjustWindowRect(ref rect, style, false);

            nint hwnd = CreateWindowExW(
                0, ClassName, title, style,
                CwUseDefault, CwUseDefault,
                rect.Right - rect.Left, rect.Bottom - rect.Top,
                0, 0, GetModuleHandleW(null), 0);

            if (hwnd == 0)
            {
                Fail("CreateWindowExW failed: " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
                return;
            }

            if (!CreateGraphics(hwnd, pixels, width, height, rowBytes))
            {
                return;
            }

            ShowWindow(hwnd, SwShow);
            UpdateWindow(hwnd);
            SetTimer(hwnd, TimerId, 250, 0);

            WindowReady.Set();

            while (GetMessageW(out MSG message, 0, 0, 0) > 0)
            {
                TranslateMessage(ref message);
                DispatchMessageW(ref message);
            }

            DestroyWindow(hwnd);
        }
        catch (Exception ex)
        {
            Fail(ex.Message);
            DebugHelper.WriteException(ex, "[HDR] HDR preview thread failed");
        }
        finally
        {
            DisposeGraphics();
            lock (Sync)
            {
                _isOpen = false;
            }

            WindowReady.Set();
        }
    }

    private static void Fail(string reason)
    {
        _failure ??= reason;
        WindowReady.Set();
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

    private static bool CreateGraphics(nint hwnd, byte[] pixels, int width, int height, int rowBytes)
    {
        try
        {
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

            _swapChain = factory.CreateSwapChainForHwnd(device, hwnd, description);

            // A flip-model R16G16B16A16_FLOAT chain defaults to RGB_FULL_G10_NONE_P709 (scRGB),
            // where 1.0 is SDR white and values above it are real HDR. Vortice 3.8 does not expose
            // IDXGISwapChain3::SetColorSpace1, so the default is what this window relies on.

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
            return true;
        }
        catch (Exception ex)
        {
            Fail("creating the HDR swapchain failed: " + ex.Message);
            DebugHelper.WriteException(ex, "[HDR] HDR swapchain creation failed");
            return false;
        }
    }

    private static void Present()
    {
        if (_swapChain == null || _context == null || _source == null)
        {
            return;
        }

        using ID3D11Texture2D backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        _context.CopyResource(backBuffer, _source);
        _swapChain.Present(1, PresentFlags.None);
    }

    private static nint WindowProcedure(nint hwnd, uint message, nint wParam, nint lParam)
    {
        switch (message)
        {
            case WmPaint:
                Present();
                ValidateRect(hwnd, 0);
                return 0;

            case WmTimer:
                Present();
                return 0;

            case WmEraseBackground:
                return 1;

            case WmClose:
                DestroyWindow(hwnd);
                return 0;

            case WmDestroy:
                KillTimer(hwnd, TimerId);
                PostQuitMessage(0);
                return 0;
        }

        return DefWindowProcW(hwnd, message, wParam, lParam);
    }

    private static void DisposeGraphics()
    {
        _source?.Dispose();
        _source = null;
        _swapChain?.Dispose();
        _swapChain = null;
        _context?.Dispose();
        _context = null;
        _device?.Dispose();
        _device = null;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassW(ref WNDCLASSW windowClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowExW(
        uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height,
        nint parent, nint menu, nint instance, nint param);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AdjustWindowRect(ref RECT rect, uint style, bool menu);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hwnd, int command);

    [DllImport("user32.dll")]
    private static extern bool UpdateWindow(nint hwnd);

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

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out MSG message, nint hwnd, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG message);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessageW(ref MSG message);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int exitCode);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? moduleName);
}
