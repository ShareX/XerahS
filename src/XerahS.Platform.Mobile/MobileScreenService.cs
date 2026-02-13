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
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Mobile;

public class MobileScreenService : IScreenService
{
    private static readonly Rectangle DefaultBounds = new(0, 0, 1080, 1920);

    private static readonly ScreenInfo DefaultScreen = new()
    {
        Bounds = DefaultBounds,
        WorkingArea = DefaultBounds,
        IsPrimary = true,
        DeviceName = "Mobile",
        BitsPerPixel = 32,
        ScaleFactor = 1.0
    };

    public bool UsePerScreenScalingForRegionCaptureLayout => false;
    public bool UseWindowPositionForRegionCaptureFallback => false;
    public bool UseLogicalCoordinatesForRegionCapture => false;

    public Rectangle GetVirtualScreenBounds() => DefaultBounds;
    public Rectangle GetWorkingArea() => DefaultBounds;
    public Rectangle GetActiveScreenBounds() => DefaultBounds;
    public Rectangle GetActiveScreenWorkingArea() => DefaultBounds;
    public Rectangle GetPrimaryScreenBounds() => DefaultBounds;
    public Rectangle GetPrimaryScreenWorkingArea() => DefaultBounds;
    public ScreenInfo[] GetAllScreens() => [DefaultScreen];
    public ScreenInfo GetScreenFromPoint(Point point) => DefaultScreen;
    public ScreenInfo GetScreenFromRectangle(Rectangle rectangle) => DefaultScreen;
}
