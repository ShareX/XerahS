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
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Windows.Capture;
using XerahS.Platform.Windows.Capture.Backends;
using XerahS.Platform.Windows.Capture.Engine;

namespace XerahS.Platform.Windows
{
    /// <summary>
    /// Windows screen capture through GDI BitBlt only. Works on every Windows version.
    /// Also the base of <see cref="WindowsModernCaptureService"/>: this facade only translates
    /// <see cref="IScreenCaptureService"/> calls into capture requests for a <see cref="ScreenCaptureEngine"/>.
    /// </summary>
    public class WindowsScreenCaptureService : IScreenCaptureService
    {
        private readonly ScreenCaptureEngine _engine;
        private readonly bool _windowCapturesClampToDesktop;

        public WindowsScreenCaptureService(IScreenService screenService)
            : this(new ScreenCaptureEngine(new[] { new GdiScreenCaptureBackend(screenService) }), windowCapturesClampToDesktop: false)
        {
        }

        /// <param name="engine">Backend chain that serves every capture.</param>
        /// <param name="windowCapturesClampToDesktop">
        /// True to capture windows as screen rectangles clamped to the desktop; false to read their bounds as-is.
        /// </param>
        private protected WindowsScreenCaptureService(ScreenCaptureEngine engine, bool windowCapturesClampToDesktop)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _windowCapturesClampToDesktop = windowCapturesClampToDesktop;
        }

        /// <summary>
        /// Shows the region selector UI and returns the selected rectangle.
        /// This is a platform stub - actual UI is handled by the UI layer.
        /// </summary>
        public Task<SKRectI> SelectRegionAsync(CaptureOptions? options = null) => Task.FromResult(SKRectI.Empty);

        /// <summary>
        /// Captures a region of the screen. Without the UI layer's region selector this captures the full screen.
        /// </summary>
        public Task<SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null) => CaptureFullScreenAsync(options);

        public Task<SKBitmap?> CaptureRectAsync(SKRect rect, CaptureOptions? options = null) =>
            _engine.CaptureAsync(ScreenCaptureRequest.ForRectangle(rect, options));

        public Task<SKBitmap?> CaptureFullScreenAsync(CaptureOptions? options = null) =>
            _engine.CaptureAsync(ScreenCaptureRequest.ForVirtualDesktop(options));

        public Task<SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService, CaptureOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(windowService);
            return CaptureWindowAsync(windowService.GetForegroundWindow(), windowService, options);
        }

        public Task<SKBitmap?> CaptureWindowAsync(IntPtr windowHandle, IWindowService windowService, CaptureOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(windowService);
            if (windowHandle == IntPtr.Zero) return Task.FromResult<SKBitmap?>(null);

            System.Drawing.Rectangle bounds;
            try
            {
                // Read current window bounds (fresh, not stale)
                bounds = windowService.GetWindowBounds(windowHandle);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Screen capture: failed to read window bounds");
                return Task.FromResult<SKBitmap?>(null);
            }

            if (bounds.Width <= 0 || bounds.Height <= 0) return Task.FromResult<SKBitmap?>(null);

            ScreenCaptureRequest request = _windowCapturesClampToDesktop
                ? ScreenCaptureRequest.ForRectangle(new SKRect(bounds.X, bounds.Y, bounds.Right, bounds.Bottom), options)
                : ScreenCaptureRequest.ForWindowBounds(bounds, options);

            return _engine.CaptureAsync(request);
        }

        public Task<CursorInfo?> CaptureCursorAsync() => Task.Run(CursorImageCapture.Capture);
    }
}
