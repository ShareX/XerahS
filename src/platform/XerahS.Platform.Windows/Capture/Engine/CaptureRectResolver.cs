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

namespace XerahS.Platform.Windows.Capture.Engine;

/// <summary>
/// Turns a request into the desktop rectangle a screen-copy backend (GDI, Windows.Graphics.Capture) reads.
/// </summary>
internal static class CaptureRectResolver
{
    /// <param name="request">The capture request.</param>
    /// <param name="virtualScreenBounds">Current virtual screen bounds in physical pixels.</param>
    /// <param name="captureRect">Desktop rectangle to read.</param>
    /// <returns>False when the request covers no pixels.</returns>
    public static bool TryResolve(ScreenCaptureRequest request, Rectangle virtualScreenBounds, out Rectangle captureRect)
    {
        switch (request.Area)
        {
            case CaptureArea.VirtualDesktop:
                captureRect = virtualScreenBounds;
                break;

            case CaptureArea.Rectangle:
                // Outward rounding keeps a fractional, caller-selected edge, and clamping keeps the read on screen.
                if (!GdiCaptureRectHelper.TryCreateCaptureRect(request.Bounds, virtualScreenBounds, out captureRect))
                {
                    return false;
                }
                break;

            case CaptureArea.WindowBounds:
                // Window bounds are read as-is; parts outside every monitor come back black.
                captureRect = Rectangle.FromLTRB(
                    (int)request.Bounds.Left,
                    (int)request.Bounds.Top,
                    (int)request.Bounds.Right,
                    (int)request.Bounds.Bottom);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.Area, "Unknown capture area.");
        }

        return captureRect.Width > 0 && captureRect.Height > 0;
    }
}
