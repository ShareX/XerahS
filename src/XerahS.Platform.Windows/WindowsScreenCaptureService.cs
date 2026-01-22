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

using XerahS.Common;
using XerahS.Platform.Abstractions;
using SkiaSharp;
using System.Runtime.InteropServices;
using System.Threading;

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
                bool cursorHidden = false;
                try
                {
                    int x = (int)rect.Left;
                    int y = (int)rect.Top;
                    int width = (int)rect.Width;
                    int height = (int)rect.Height;

                    // Validate and clamp capture region to screen bounds
                    var screenBounds = _screenService.GetVirtualScreenBounds();
                    x = Math.Max(x, screenBounds.X);
                    y = Math.Max(y, screenBounds.Y);
                    width = Math.Min(width, screenBounds.Right - x);
                    height = Math.Min(height, screenBounds.Bottom - y);

                    if (width <= 0 || height <= 0)
                    {
                        DebugHelper.WriteLine("Capture region outside screen bounds");
                        return null;
                    }

                    if (options?.ShowCursor == false)
                    {
                        cursorHidden = HideSystemCursors();
                        if (cursorHidden)
                        {
                            Thread.Sleep(150);
                        }
                    }

                    // Get screen DC (entire virtual desktop)
                    IntPtr screenDC = GetDC(IntPtr.Zero);
                    if (screenDC == IntPtr.Zero)
                    {
                        DebugHelper.WriteLine("WindowsScreenCaptureService: Failed to get screen DC");
                        return null;
                    }

                    try
                    {
                        // Create compatible DC and bitmap
                        IntPtr memDC = CreateCompatibleDC(screenDC);
                        if (memDC == IntPtr.Zero)
                        {
                            DebugHelper.WriteLine("WindowsScreenCaptureService: Failed to create compatible DC");
                            return null;
                        }

                        try
                        {
                            IntPtr hBitmap = CreateCompatibleBitmap(screenDC, width, height);
                            if (hBitmap == IntPtr.Zero)
                            {
                                DebugHelper.WriteLine("WindowsScreenCaptureService: Failed to create compatible bitmap");
                                return null;
                            }

                            try
                            {
                                // Select bitmap into DC
                                IntPtr oldBitmap = SelectObject(memDC, hBitmap);

                                // BitBlt from screen to memory DC (physical pixels)
                                bool success = BitBlt(memDC, 0, 0, width, height, screenDC, x, y, SRCCOPY);

                                if (!success)
                                {
                                    DebugHelper.WriteLine("WindowsScreenCaptureService: BitBlt failed");
                                    return null;
                                }

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
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "WindowsScreenCaptureService: Capture failed");
                    return null;
                }
                finally
                {
                    if (cursorHidden)
                    {
                        RestoreSystemCursors();
                    }
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
                bool cursorHidden = false;
                try
                {
                    var bounds = _screenService.GetVirtualScreenBounds();
                    if (options?.ShowCursor == false)
                    {
                        cursorHidden = HideSystemCursors();
                        if (cursorHidden)
                        {
                            Thread.Sleep(150);
                        }
                    }

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
                finally
                {
                    if (cursorHidden)
                    {
                        RestoreSystemCursors();
                    }
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
                bool cursorHidden = false;
                try
                {
                    var hwnd = windowService.GetForegroundWindow();
                    if (hwnd == IntPtr.Zero) return null;

                    var bounds = windowService.GetWindowBounds(hwnd);
                    if (bounds.Width <= 0 || bounds.Height <= 0) return null;

                    if (options?.ShowCursor == false)
                    {
                        cursorHidden = HideSystemCursors();
                        if (cursorHidden)
                        {
                            Thread.Sleep(150);
                        }
                    }

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
                finally
                {
                    if (cursorHidden)
                    {
                        RestoreSystemCursors();
                    }
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
                bool cursorHidden = false;
                try
                {
                    if (windowHandle == IntPtr.Zero) return null;

                    // Get current window bounds (fresh, not stale)
                    var bounds = windowService.GetWindowBounds(windowHandle);
                    if (bounds.Width <= 0 || bounds.Height <= 0) return null;

                    if (options?.ShowCursor == false)
                    {
                        cursorHidden = HideSystemCursors();
                        if (cursorHidden)
                        {
                            Thread.Sleep(150);
                        }
                    }

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
                finally
                {
                    if (cursorHidden)
                    {
                        RestoreSystemCursors();
                    }
                }
            });
        }

        /// <summary>
        /// Captures the current mouse cursor
        /// </summary>
        public async Task<CursorInfo?> CaptureCursorAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var cursor = new CursorData();
                    if (!cursor.IsVisible || cursor.Handle == IntPtr.Zero) return null;

                    var position = cursor.Position;
                    var hotspot = cursor.Hotspot;
                    int width = cursor.Size.Width > 0 ? cursor.Size.Width : 32;
                    int height = cursor.Size.Height > 0 ? cursor.Size.Height : 32;

                    // Create a 32-bit ARGB bitmap for proper transparency
                    using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (var g = System.Drawing.Graphics.FromImage(bitmap))
                    {
                        // Clear to transparent
                        g.Clear(System.Drawing.Color.Transparent);
                        
                        // Get HDC and draw cursor
                        IntPtr hdc = g.GetHdc();
                        try
                        {
                            // Draw the cursor icon at (0,0)
                            DrawIconEx(hdc, 0, 0, cursor.Handle, width, height, 0, IntPtr.Zero, DI_NORMAL);
                        }
                        finally
                        {
                            g.ReleaseHdc(hdc);
                        }
                    }

                    // Convert to SKBitmap
                    using var stream = new MemoryStream();
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    stream.Seek(0, SeekOrigin.Begin);
                    var skBitmap = SKBitmap.Decode(stream);

                    return new CursorInfo(skBitmap, position, hotspot);
                }
                catch
                {
                    return null;
                }
            });
        }

        private const int DI_NORMAL = 0x0003;

        [DllImport("user32.dll")]
        private static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyHeight, int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);

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

        [DllImport("user32.dll")]
        private static extern bool SetSystemCursor(IntPtr hcur, uint id);

        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        private const uint SPI_SETCURSORS = 0x0057;
        private const uint IDC_ARROW = 32512;
        private const uint IDC_IBEAM = 32513;
        private const uint IDC_WAIT = 32514;
        private const uint IDC_CROSS = 32515;
        private const uint IDC_UPARROW = 32516;
        private const uint IDC_SIZENWSE = 32642;
        private const uint IDC_SIZENESW = 32643;
        private const uint IDC_SIZEWE = 32644;
        private const uint IDC_SIZENS = 32645;
        private const uint IDC_SIZEALL = 32646;
        private const uint IDC_NO = 32648;
        private const uint IDC_HAND = 32649;
        private const uint IDC_APPSTARTING = 32650;

        private static bool HideSystemCursors()
        {
            try
            {
                SetSystemCursor(IntPtr.Zero, IDC_ARROW);
                SetSystemCursor(IntPtr.Zero, IDC_IBEAM);
                SetSystemCursor(IntPtr.Zero, IDC_WAIT);
                SetSystemCursor(IntPtr.Zero, IDC_CROSS);
                SetSystemCursor(IntPtr.Zero, IDC_UPARROW);
                SetSystemCursor(IntPtr.Zero, IDC_SIZENWSE);
                SetSystemCursor(IntPtr.Zero, IDC_SIZENESW);
                SetSystemCursor(IntPtr.Zero, IDC_SIZEWE);
                SetSystemCursor(IntPtr.Zero, IDC_SIZENS);
                SetSystemCursor(IntPtr.Zero, IDC_SIZEALL);
                SetSystemCursor(IntPtr.Zero, IDC_NO);
                SetSystemCursor(IntPtr.Zero, IDC_HAND);
                SetSystemCursor(IntPtr.Zero, IDC_APPSTARTING);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void RestoreSystemCursors()
        {
            try
            {
                SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
            }
            catch
            {
                // Ignore cursor restore errors
            }
        }

        #endregion
    }
}
