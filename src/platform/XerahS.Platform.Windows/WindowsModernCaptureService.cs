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
using XerahS.Platform.Windows.Capture.Backends;
using XerahS.Platform.Windows.Capture.Engine;

namespace XerahS.Platform.Windows
{
    /// <summary>
    /// Windows screen capture through the native compositor APIs: DXGI Desktop Duplication first,
    /// then Windows.Graphics.Capture where it runs borderless, then GDI BitBlt.
    /// <see cref="CaptureOptions.UseModernCapture"/> set to false restricts a capture to GDI.
    /// </summary>
    public sealed class WindowsModernCaptureService : WindowsScreenCaptureService
    {
        /// <summary>
        /// Check if the current OS supports DXGI output duplication
        /// </summary>
        public static bool IsSupported => DxgiDesktopDuplicationBackend.IsSupported;

        public WindowsModernCaptureService(IScreenService screenService)
            : base(new ScreenCaptureEngine(CreateBackends(screenService)), windowCapturesClampToDesktop: true)
        {
        }

        private static IEnumerable<IScreenCaptureBackend> CreateBackends(IScreenService screenService)
        {
            ArgumentNullException.ThrowIfNull(screenService);

            if (DxgiDesktopDuplicationBackend.IsSupported)
            {
                yield return new DxgiDesktopDuplicationBackend(screenService);
            }

            if (WindowsGraphicsCaptureBackend.IsSupported)
            {
                yield return new WindowsGraphicsCaptureBackend(screenService);
            }

            yield return new GdiScreenCaptureBackend(screenService);
        }
    }
}
