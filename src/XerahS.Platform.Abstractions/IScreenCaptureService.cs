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
using SkiaSharp;

namespace XerahS.Platform.Abstractions
{
    public interface IScreenCaptureService
    {
        /// <summary>
        /// Shows the region selector UI and returns the selected rectangle without capturing.
        /// Used by screen recording to get the capture area before starting recording.
        /// </summary>
        /// <returns>SKRectI of the selected region, or SKRectI.Empty if cancelled.</returns>
        Task<SKRectI> SelectRegionAsync(CaptureOptions? options = null);

        /// <summary>
        /// Captures a region of the screen.
        /// </summary>
        /// <returns>SkiaSharp.SKBitmap if successful, null otherwise.</returns>
        Task<SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null);

        /// <summary>
        /// Captures a specific region of the screen
        /// </summary>
        Task<SKBitmap?> CaptureRectAsync(SKRect rect, CaptureOptions? options = null);

        /// <summary>
        /// Captures the full screen
        /// </summary>
        Task<SKBitmap?> CaptureFullScreenAsync(CaptureOptions? options = null);

        /// <summary>
        /// Captures the active window
        /// </summary>
        Task<SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService, CaptureOptions? options = null);

        /// <summary>
        /// Captures a specific window by its handle
        /// </summary>
        /// <param name="windowHandle">The native window handle to capture</param>
        /// <param name="windowService">Window service for bounds/state queries</param>
        /// <param name="options">Capture options</param>
        /// <returns>SKBitmap of the window, or null on failure</returns>
        Task<SKBitmap?> CaptureWindowAsync(IntPtr windowHandle, IWindowService windowService, CaptureOptions? options = null);
    }
}
