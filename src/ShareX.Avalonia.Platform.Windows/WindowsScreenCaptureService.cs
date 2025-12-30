#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
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
using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions;

namespace ShareX.Avalonia.Platform.Windows
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
        /// Captures a region of the screen.
        /// Currently captures the entire primary screen - region selection UI to be added.
        /// </summary>
        public async Task<Image?> CaptureRegionAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Get primary screen bounds
                    var bounds = _screenService.GetPrimaryScreenBounds();
                    
                    // Create bitmap to hold the capture
                    var bitmap = new Bitmap(bounds.Width, bounds.Height);
                    
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        // Capture screen
                        graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
                    }
                    
                    return (Image)bitmap;
                }
                catch (Exception)
                {
                    // TODO: Log error
                    return null;
                }
            });
        }

        /// <summary>
        /// Captures the entire screen
        /// </summary>
        public async Task<Image?> CaptureFullScreenAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var bounds = _screenService.GetVirtualScreenBounds();
                    var bitmap = new Bitmap(bounds.Width, bounds.Height);
                    
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
                    }
                    
                    return (Image)bitmap;
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
        public async Task<Image?> CaptureActiveWindowAsync(IWindowService windowService)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var hwnd = windowService.GetForegroundWindow();
                    if (hwnd == IntPtr.Zero) return null;

                    var bounds = windowService.GetWindowBounds(hwnd);
                    if (bounds.Width <= 0 || bounds.Height <= 0) return null;

                    var bitmap = new Bitmap(bounds.Width, bounds.Height);
                    
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
                    }
                    
                    return (Image)bitmap;
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }
    }
}
