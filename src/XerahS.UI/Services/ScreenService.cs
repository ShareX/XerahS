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
using System.Drawing;

namespace XerahS.UI.Services
{
    /// <summary>
    /// UI-layer stub for ScreenService - delegates to platform implementation
    /// This is kept as a fallback for when PlatformServices might not be fully initialized
    /// </summary>
    public class ScreenService : IScreenService
    {
        private readonly IScreenService? _platformImpl;

        public ScreenService(IScreenService? platformImpl = null)
        {
            _platformImpl = platformImpl;
        }

        private IScreenService GetImpl() => _platformImpl ?? throw new System.InvalidOperationException("No platform screen service registered");

        public bool UsePerScreenScalingForRegionCaptureLayout => GetImpl().UsePerScreenScalingForRegionCaptureLayout;
        public bool UseWindowPositionForRegionCaptureFallback => GetImpl().UseWindowPositionForRegionCaptureFallback;
        public bool UseLogicalCoordinatesForRegionCapture => GetImpl().UseLogicalCoordinatesForRegionCapture;

        public Rectangle GetVirtualScreenBounds() => GetImpl().GetVirtualScreenBounds();
        public Rectangle GetWorkingArea() => GetImpl().GetWorkingArea();
        public Rectangle GetActiveScreenBounds() => GetImpl().GetActiveScreenBounds();
        public Rectangle GetActiveScreenWorkingArea() => GetImpl().GetActiveScreenWorkingArea();
        public Rectangle GetPrimaryScreenBounds() => GetImpl().GetPrimaryScreenBounds();
        public Rectangle GetPrimaryScreenWorkingArea() => GetImpl().GetPrimaryScreenWorkingArea();
        public ScreenInfo[] GetAllScreens() => GetImpl().GetAllScreens();
        public ScreenInfo GetScreenFromPoint(Point point) => GetImpl().GetScreenFromPoint(point);
        public ScreenInfo GetScreenFromRectangle(Rectangle rectangle) => GetImpl().GetScreenFromRectangle(rectangle);
    }
}
