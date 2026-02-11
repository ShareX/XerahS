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

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Windows
{
    [SupportedOSPlatform("windows")]
    public class WindowsScrollingCaptureService : IScrollingCaptureService
    {
        public bool IsSupported => true;

        public async Task ScrollWindowAsync(IntPtr windowHandle, ScrollMethod method, int amount)
        {
            switch (method)
            {
                case ScrollMethod.MouseWheel:
                    // Move mouse to center of target window's client area for reliable wheel delivery
                    var clientRect = NativeMethods.GetClientRect(windowHandle);
                    if (clientRect.Width > 0 && clientRect.Height > 0)
                    {
                        POINT centerPoint = new POINT
                        {
                            X = clientRect.Left + clientRect.Width / 2,
                            Y = clientRect.Top + clientRect.Height / 2
                        };
                        NativeMethods.ClientToScreen(windowHandle, ref centerPoint);
                        InputHelpers.SendMouseMove(centerPoint.X, centerPoint.Y);
                        await Task.Delay(50);
                    }
                    // WHEEL_DELTA = 120; negative = scroll down
                    InputHelpers.SendMouseWheel(-120 * amount);
                    break;

                case ScrollMethod.DownArrow:
                    for (int i = 0; i < amount; i++)
                    {
                        InputHelpers.SendKeyPress(VirtualKeyCode.DOWN);
                    }
                    break;

                case ScrollMethod.PageDown:
                    InputHelpers.SendKeyPress(VirtualKeyCode.NEXT);
                    break;

                case ScrollMethod.ScrollMessage:
                    for (int i = 0; i < amount; i++)
                    {
                        NativeMethods.SendMessage(
                            windowHandle,
                            (uint)WindowsMessages.WM_VSCROLL,
                            (IntPtr)ScrollBarCommand.SB_LINEDOWN,
                            IntPtr.Zero);
                    }
                    break;
            }

            await Task.CompletedTask;
        }

        public async Task ScrollToTopAsync(IntPtr windowHandle)
        {
            // Send HOME key to scroll to top
            InputHelpers.SendKeyPress(VirtualKeyCode.HOME);
            await Task.Delay(100);

            // Also send WM_VSCROLL SB_TOP as fallback
            NativeMethods.SendMessage(
                windowHandle,
                (uint)WindowsMessages.WM_VSCROLL,
                (IntPtr)ScrollBarCommand.SB_TOP,
                IntPtr.Zero);
        }

        public ScrollBarInfo? GetScrollBarInfo(IntPtr windowHandle)
        {
            SCROLLINFO scrollInfo = new SCROLLINFO();
            scrollInfo.cbSize = (uint)Marshal.SizeOf<SCROLLINFO>();
            scrollInfo.fMask = ScrollInfoMask.SIF_ALL;

            if (!NativeMethods.GetScrollInfo(windowHandle, (int)ScrollBarOrientation.SB_VERT, ref scrollInfo))
            {
                return null;
            }

            return new ScrollBarInfo(
                Position: scrollInfo.nTrackPos,
                MinRange: scrollInfo.nMin,
                MaxRange: scrollInfo.nMax,
                PageSize: (int)scrollInfo.nPage);
        }
    }
}
