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

namespace XerahS.Platform.Windows.Recording.Native;

internal readonly record struct RecordingRegionSlice(nint Monitor, Rectangle Source, Point Destination);

/// <summary>Physical desktop intersections; contains no Windows API calls.</summary>
internal static class RecordingRegionLayout
{
    internal static List<RecordingRegionSlice> Compute(Rectangle region, IEnumerable<(nint Monitor, Rectangle Bounds)> monitors)
    {
        List<RecordingRegionSlice> slices = new();
        foreach ((nint monitor, Rectangle bounds) in monitors)
        {
            Rectangle intersection = Rectangle.Intersect(region, bounds);
            if (intersection.Width <= 0 || intersection.Height <= 0) continue;
            slices.Add(new(monitor, new Rectangle(intersection.X - bounds.X, intersection.Y - bounds.Y, intersection.Width, intersection.Height),
                new Point(intersection.X - region.X, intersection.Y - region.Y)));
        }
        return slices;
    }

    internal static Rectangle Normalize(Rectangle region)
    {
        int width = region.Width & ~1, height = region.Height & ~1;
        if (width < 2 || height < 2 || width > 16384 || height > 16384)
            throw new ArgumentException("Recording dimensions must be between 2 and 16384 physical pixels.", nameof(region));
        return new Rectangle(region.Location, new Size(width, height));
    }
}
