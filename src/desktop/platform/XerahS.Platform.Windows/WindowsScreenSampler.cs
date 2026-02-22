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
    /// Windows implementation of IScreenSampler using GDI BitBlt.
    /// Uses a different code path than WindowsScreenCaptureService to provide
    /// independent verification for testing.
    /// </summary>
    public class WindowsScreenSampler : IScreenSampler
    {
        public async Task<SKBitmap?> SampleScreenAsync(SKRect rect)
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

                    // Get screen DC
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

                                // BitBlt from screen to memory DC
                                bool success = BitBlt(memDC, 0, 0, width, height, screenDC, x, y, SRCCOPY);

                                if (!success) return null;

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
