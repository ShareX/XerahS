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

using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using ShareX.Ava.Platform.Abstractions;
using SkiaSharp;

namespace ShareX.Ava.Platform.Windows
{
    /// <summary>
    /// Windows-specific screen capture implementation using GDI+
    /// </summary>
    public class WindowsScreenCaptureService : IScreenCaptureService
    {
        private readonly IScreenService _screenService;

        public WindowsScreenCaptureService(IScreenService screenService)
        {
            _screenService = screenService;
        }

        /// <summary>
        /// Captures a specific region of the screen
        /// </summary>
        public async Task<SKBitmap?> CaptureRectAsync(SKRect rect, CaptureOptions? options = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (rect.Width <= 0 || rect.Height <= 0) return null;

                    using (var bitmap = new Bitmap((int)rect.Width, (int)rect.Height))
                    {
                        using (var graphics = Graphics.FromImage(bitmap))
                        {
                            graphics.CopyFromScreen((int)rect.Left, (int)rect.Top, 0, 0, new Size((int)rect.Width, (int)rect.Height));
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
    }
}
