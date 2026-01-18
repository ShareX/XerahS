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

using XerahS.Platform.Abstractions;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace XerahS.Platform.Windows
{
    /// <summary>
    /// Windows-specific screen capture implementation using GDI
    /// </summary>
    public class WindowsScreenCaptureService : IScreenCaptureService
    {
        private readonly IScreenService _screenService;

        public WindowsScreenCaptureService(IScreenService screenService)
        {
            _screenService = screenService;
        }

        /// <summary>
        /// Captures a specific region of the screen using GDI BitBlt for physical pixel accuracy.
        /// Uses raw GDI to ensure consistent behavior across DPI settings and multi-monitor setups.
        /// </summary>
        public async Task<SKBitmap?> CaptureRectAsync(SKRect rect, CaptureOptions? options = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    int x = (int)rect.Left;
                    int y = (int)rect.Top;
                    int width = (int)rect.Width;
                    int height = (int)rect.Height;

                    if (width <= 0 || height <= 0) return null;

                    // Get screen DC (entire virtual desktop)
                    IntPtr screenDC = GetDC(IntPtr.Zero);
                    if (screenDC == IntPtr.Zero) return null;

                    try
                    {
                        // Create compatible DC and bitmap
                        IntPtr memDC = CreateCompatibleDC(screenDC);
                        if (memDC == IntPtr.Zero) return null;

                        try
                        {
                            IntPtr hBitmap = CreateCompatibleBitmap(screenDC, width, height);
                            if (hBitmap == IntPtr.Zero) return null;

                            try
                            {
                                // Select bitmap into DC
                                IntPtr oldBitmap = SelectObject(memDC, hBitmap);

                                // BitBlt from screen to memory DC (physical pixels)
                                bool success = BitBlt(memDC, 0, 0, width, height, screenDC, x, y, SRCCOPY);

                                if (!success) return null;

                                if (options?.ShowCursor == true)
                                {
                                    var cursor = new CursorData();
                                    cursor.DrawCursor(memDC, new System.Drawing.Point(x, y));
                                }

                                // Convert to SKBitmap
                                using var bitmap = System.Drawing.Image.FromHbitmap(hBitmap);
                                using var stream = new MemoryStream();
                                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                                stream.Seek(0, SeekOrigin.Begin);

                                // Restore old bitmap
                                SelectObject(memDC, oldBitmap);

                                return SKBitmap.Decode(stream);
                            }
                            finally
                            {
                                DeleteObject(hBitmap);
                            }
                        }
                        finally
                        {
                            DeleteDC(memDC);
                        }
                    }
                    finally
                    {
                        ReleaseDC(IntPtr.Zero, screenDC);
                    }
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        /// <summary>
        /// Shows the region selector UI and returns the selected rectangle.
        /// This is a platform stub - actual UI is handled by the UI layer.
        /// </summary>
        public Task<SKRectI> SelectRegionAsync(CaptureOptions? options = null)
        {
            // This method should only be called from the UI layer wrapper
            return Task.FromResult(SKRectI.Empty);
        }

        /// <summary>
        /// Captures a region of the screen.
        /// On Windows platform layer, this just falls back to fullscreen or throws,
        /// as UI interaction should be handled by the UI layer wrapper.
        /// </summary>
        public async Task<SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null)
        {
            // Default to fullscreen if called directly without UI wrapper
            return await CaptureFullScreenAsync(options);
        }

        /// <summary>
        /// Captures the entire screen
        /// </summary>
        public async Task<SKBitmap?> CaptureFullScreenAsync(CaptureOptions? options = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var bounds = _screenService.GetVirtualScreenBounds();
                    using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
                    {
                        using (var graphics = Graphics.FromImage(bitmap))
                        {
                            graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);

                            if (options?.ShowCursor == true)
                            {
                                var cursor = new CursorData();
                                cursor.DrawCursor(bitmap, new System.Drawing.Point(bounds.X, bounds.Y));
                            }
                        }

                        return ToSKBitmap(bitmap);
                    }
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        /// <summary>
        /// Captures the active window
        /// </summary>
        public async Task<SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService, CaptureOptions? options = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var hwnd = windowService.GetForegroundWindow();
                    if (hwnd == IntPtr.Zero) return null;

                    var bounds = windowService.GetWindowBounds(hwnd);
                    if (bounds.Width <= 0 || bounds.Height <= 0) return null;

                    using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
                    {
                        using (var graphics = Graphics.FromImage(bitmap))
                        {
                            graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
                        }

                        if (options?.ShowCursor == true)
                        {
                            var cursor = new CursorData();
                            cursor.DrawCursor(bitmap, new System.Drawing.Point(bounds.X, bounds.Y));
                        }

                        return ToSKBitmap(bitmap);
                    }
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        /// <summary>
        /// Captures a specific window by its handle
        /// </summary>
        public async Task<SKBitmap?> CaptureWindowAsync(IntPtr windowHandle, IWindowService windowService, CaptureOptions? options = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (windowHandle == IntPtr.Zero) return null;

                    // Get current window bounds (fresh, not stale)
                    var bounds = windowService.GetWindowBounds(windowHandle);
                    if (bounds.Width <= 0 || bounds.Height <= 0) return null;

                    using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
                    {
                        using (var graphics = Graphics.FromImage(bitmap))
                        {
                            graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
                        }

                        if (options?.ShowCursor == true)
                        {
                            var cursor = new CursorData();
                            cursor.DrawCursor(bitmap, new System.Drawing.Point(bounds.X, bounds.Y));
                        }

                        return ToSKBitmap(bitmap);
                    }
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        private SKBitmap? ToSKBitmap(Bitmap bitmap)
        {
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Seek(0, SeekOrigin.Begin);
                return SKBitmap.Decode(stream);
            }
        }

        #region Native Methods

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
            IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private const int SRCCOPY = 0x00CC0020;

        #endregion
    }
}
