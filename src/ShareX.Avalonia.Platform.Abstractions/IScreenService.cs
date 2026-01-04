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

using System.Drawing;

namespace ShareX.Ava.Platform.Abstractions
{
    /// <summary>
    /// Platform-agnostic screen information service
    /// </summary>
    public interface IScreenService
    {
        /// <summary>
        /// Use per-screen scaling for region capture layout calculations.
        /// </summary>
        bool UsePerScreenScalingForRegionCaptureLayout { get; }

        /// <summary>
        /// Use actual window position when converting fallback mouse coordinates.
        /// </summary>
        bool UseWindowPositionForRegionCaptureFallback { get; }

        /// <summary>
        /// Use logical (screen coordinate) points when computing region capture rectangles.
        /// </summary>
        bool UseLogicalCoordinatesForRegionCapture { get; }

        /// <summary>
        /// Gets the bounds of the virtual screen (all screens combined)
        /// </summary>
        Rectangle GetVirtualScreenBounds();

        /// <summary>
        /// Gets the working area of all screens combined (excluding taskbars)
        /// </summary>
        Rectangle GetWorkingArea();

        /// <summary>
        /// Gets the bounds of the screen containing the cursor
        /// </summary>
        Rectangle GetActiveScreenBounds();

        /// <summary>
        /// Gets the working area of the screen containing the cursor
        /// </summary>
        Rectangle GetActiveScreenWorkingArea();

        /// <summary>
        /// Gets the bounds of the primary screen
        /// </summary>
        Rectangle GetPrimaryScreenBounds();

        /// <summary>
        /// Gets the working area of the primary screen
        /// </summary>
        Rectangle GetPrimaryScreenWorkingArea();

        /// <summary>
        /// Gets information about all available screens
        /// </summary>
        ScreenInfo[] GetAllScreens();

        /// <summary>
        /// Gets the screen containing the specified point
        /// </summary>
        ScreenInfo GetScreenFromPoint(Point point);

        /// <summary>
        /// Gets the screen containing the specified rectangle
        /// </summary>
        ScreenInfo GetScreenFromRectangle(Rectangle rectangle);
    }

    /// <summary>
    /// Information about a display screen
    /// </summary>
    public class ScreenInfo
    {
        public Rectangle Bounds { get; set; }
        public Rectangle WorkingArea { get; set; }
        public bool IsPrimary { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public int BitsPerPixel { get; set; }
    }
}
