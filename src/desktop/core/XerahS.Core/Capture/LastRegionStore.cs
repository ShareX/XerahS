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

namespace XerahS.Core.Capture;

/// <summary>
/// Process-lifetime store for the last confirmed region-capture rectangle.
/// Independent of <c>CaptureCustomRegion</c>, which is a user-configured preset.
/// </summary>
public static class LastRegionStore
{
    private static readonly Lock Gate = new();
    private static Rectangle _region;

    public static void Set(int x, int y, int width, int height)
        => Set(new Rectangle(x, y, width, height));

    public static void Set(Rectangle region)
    {
        lock (Gate)
        {
            _region = region;
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            _region = Rectangle.Empty;
        }
    }

    public static bool TryGet(out Rectangle region)
    {
        lock (Gate)
        {
            region = _region;
            return region.Width > 0 && region.Height > 0;
        }
    }
}
