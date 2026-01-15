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

using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Windows
{
    /// <summary>
    /// Windows implementation of IScreenService using System.Windows.Forms.Screen
    /// </summary>
    public class WindowsScreenService : IScreenService
    {
        public bool UsePerScreenScalingForRegionCaptureLayout => false;

        public bool UseWindowPositionForRegionCaptureFallback => false;

        public bool UseLogicalCoordinatesForRegionCapture => false;

        public Rectangle GetVirtualScreenBounds()
        {
            return SystemInformation.VirtualScreen;
        }

        public Rectangle GetWorkingArea()
        {
            return CombineRectangles(Screen.AllScreens.Select(s => s.WorkingArea));
        }

        public Rectangle GetActiveScreenBounds()
        {
            Point cursorPos = Cursor.Position;
            return Screen.FromPoint(cursorPos).Bounds;
        }

        public Rectangle GetActiveScreenWorkingArea()
        {
            Point cursorPos = Cursor.Position;
            return Screen.FromPoint(cursorPos).WorkingArea;
        }

        public Rectangle GetPrimaryScreenBounds()
        {
            return Screen.PrimaryScreen?.Bounds ?? Rectangle.Empty;
        }

        public Rectangle GetPrimaryScreenWorkingArea()
        {
            return Screen.PrimaryScreen?.WorkingArea ?? Rectangle.Empty;
        }

        public ScreenInfo[] GetAllScreens()
        {
            return Screen.AllScreens.Select(s => new ScreenInfo
            {
                Bounds = s.Bounds,
                WorkingArea = s.WorkingArea,
                IsPrimary = s.Primary,
                DeviceName = s.DeviceName,
                BitsPerPixel = s.BitsPerPixel
            }).ToArray();
        }

        public ScreenInfo GetScreenFromPoint(Point point)
        {
            var screen = Screen.FromPoint(point);
            return new ScreenInfo
            {
                Bounds = screen.Bounds,
                WorkingArea = screen.WorkingArea,
                IsPrimary = screen.Primary,
                DeviceName = screen.DeviceName,
                BitsPerPixel = screen.BitsPerPixel
            };
        }

        public ScreenInfo GetScreenFromRectangle(Rectangle rectangle)
        {
            var screen = Screen.FromRectangle(rectangle);
            return new ScreenInfo
            {
                Bounds = screen.Bounds,
                WorkingArea = screen.WorkingArea,
                IsPrimary = screen.Primary,
                DeviceName = screen.DeviceName,
                BitsPerPixel = screen.BitsPerPixel
            };
        }

        private Rectangle CombineRectangles(System.Collections.Generic.IEnumerable<Rectangle> rectangles)
        {
            if (!rectangles.Any())
                return Rectangle.Empty;

            int left = rectangles.Min(r => r.Left);
            int top = rectangles.Min(r => r.Top);
            int right = rectangles.Max(r => r.Right);
            int bottom = rectangles.Max(r => r.Bottom);

            return new Rectangle(left, top, right - left, bottom - top);
        }
    }
}
