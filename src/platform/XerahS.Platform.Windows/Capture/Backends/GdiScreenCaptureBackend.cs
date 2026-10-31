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
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Windows.Capture.Engine;

namespace XerahS.Platform.Windows.Capture.Backends;

/// <summary>
/// Captures with GDI BitBlt into a top-down 32-bit DIB section, whose pixels are copied straight into Skia.
/// Works on every Windows version and is the last resort of every chain.
/// </summary>
internal sealed class GdiScreenCaptureBackend : IScreenCaptureBackend
{
    private const int CursorSettleMilliseconds = 150;

    private readonly IScreenService _screenService;

    public GdiScreenCaptureBackend(IScreenService screenService)
    {
        _screenService = screenService ?? throw new ArgumentNullException(nameof(screenService));
    }

    public CaptureBackendKind Kind => CaptureBackendKind.Gdi;

    public string Name => "GDI BitBlt";

    public SKBitmap? TryCapture(ScreenCaptureRequest request)
    {
        if (!CaptureRectResolver.TryResolve(request, _screenService.GetVirtualScreenBounds(), out Rectangle captureRect))
        {
            DebugHelper.WriteLine("Capture region outside screen bounds");
            return null;
        }

        using var cursorScope = CursorVisibilityScope.HideIf(request.CursorExplicitlyExcluded, CursorSettleMilliseconds);

        SKBitmap? captured = CaptureScreenRect(captureRect, request.DrawCursor);
        if (captured == null)
        {
            return null;
        }

        return HdrScreenshotColorCorrector.ApplyIfEnabled(captured, captureRect, request.HdrColorCorrection);
    }

    private static SKBitmap? CaptureScreenRect(Rectangle captureRect, bool drawCursor)
    {
        int width = captureRect.Width;
        int height = captureRect.Height;

        // Get screen DC (entire virtual desktop)
        IntPtr screenDC = NativeMethods.GetDC(IntPtr.Zero);
        if (screenDC == IntPtr.Zero)
        {
            DebugHelper.WriteLine("GdiScreenCaptureBackend: Failed to get screen DC");
            return null;
        }

        try
        {
            IntPtr memDC = NativeMethods.CreateCompatibleDC(screenDC);
            if (memDC == IntPtr.Zero)
            {
                DebugHelper.WriteLine("GdiScreenCaptureBackend: Failed to create compatible DC");
                return null;
            }

            try
            {
                var bitmapInfo = new NativeMethods.BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height, // Top-down rows match Skia's layout.
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = NativeMethods.BI_RGB
                };

                IntPtr hBitmap = NativeMethods.CreateDIBSection(screenDC, ref bitmapInfo, NativeMethods.DIB_RGB_COLORS, out IntPtr bits, IntPtr.Zero, 0);
                if (hBitmap == IntPtr.Zero || bits == IntPtr.Zero)
                {
                    DebugHelper.WriteLine("GdiScreenCaptureBackend: Failed to create DIB section");
                    return null;
                }

                IntPtr oldBitmap = IntPtr.Zero;
                try
                {
                    // Select bitmap into DC before writing into it.
                    oldBitmap = NativeMethods.SelectObject(memDC, hBitmap);
                    if (oldBitmap == IntPtr.Zero)
                    {
                        DebugHelper.WriteLine("GdiScreenCaptureBackend: Failed to select bitmap into capture DC");
                        return null;
                    }

                    // BitBlt from screen to memory DC (physical pixels)
                    if (!NativeMethods.BitBlt(memDC, 0, 0, width, height, screenDC, captureRect.X, captureRect.Y, NativeMethods.SRCCOPY))
                    {
                        DebugHelper.WriteLine("GdiScreenCaptureBackend: BitBlt failed");
                        return null;
                    }

                    if (drawCursor)
                    {
                        var cursor = new CursorData();
                        cursor.DrawCursor(memDC, new System.Drawing.Point(captureRect.X, captureRect.Y));
                    }

                    NativeMethods.GdiFlush();

                    var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                    int bytesPerRow = width * 4;
                    BgraRowCopyHelper.CopyRows(bits, bytesPerRow, bitmap.GetPixels(), bitmap.RowBytes, bytesPerRow, height);
                    BgraRowCopyHelper.SetOpaque(bitmap.GetPixels(), bitmap.RowBytes, width, height);
                    bitmap.NotifyPixelsChanged();
                    return bitmap;
                }
                finally
                {
                    if (oldBitmap != IntPtr.Zero)
                    {
                        NativeMethods.SelectObject(memDC, oldBitmap);
                    }

                    NativeMethods.DeleteObject(hBitmap);
                }
            }
            finally
            {
                NativeMethods.DeleteDC(memDC);
            }
        }
        finally
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDC);
        }
    }

    private static class NativeMethods
    {
        public const int SRCCOPY = 0x00CC0020;
        public const uint BI_RGB = 0;
        public const uint DIB_RGB_COLORS = 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
            IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GdiFlush();

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr hObject);
    }
}
