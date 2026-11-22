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

using Avalonia;
using SkiaSharp;
using PixelPoint = XerahS.RegionCapture.Models.PixelPoint;
using PixelRect = XerahS.RegionCapture.Models.PixelRect;

namespace XerahS.RegionCapture.Services;

/// <summary>
/// Maps rectangles and points from virtual physical screen coordinates to bitmap pixel coordinates,
/// preserving exact 1:1 mapping when sizes match and proportional alignment when DPI/scale differs.
/// </summary>
public static class BackgroundBitmapMapper
{
    public static Rect MapRectToBitmap(PixelRect rect, PixelRect virtualBounds, int bitmapWidth, int bitmapHeight)
    {
        if (bitmapWidth <= 0 || bitmapHeight <= 0 || rect.IsEmpty)
        {
            return default;
        }

        if (virtualBounds.IsEmpty || virtualBounds.Width <= 0 || virtualBounds.Height <= 0)
        {
            double x = Math.Clamp(rect.X, 0, bitmapWidth);
            double y = Math.Clamp(rect.Y, 0, bitmapHeight);
            double right = Math.Clamp(rect.Right, x, bitmapWidth);
            double bottom = Math.Clamp(rect.Bottom, y, bitmapHeight);
            return new Rect(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
        }

        double scaleX = bitmapWidth / virtualBounds.Width;
        double scaleY = bitmapHeight / virtualBounds.Height;

        double mappedLeft = (rect.X - virtualBounds.X) * scaleX;
        double mappedTop = (rect.Y - virtualBounds.Y) * scaleY;
        double mappedRight = (rect.Right - virtualBounds.X) * scaleX;
        double mappedBottom = (rect.Bottom - virtualBounds.Y) * scaleY;

        double clampedLeft = Math.Clamp(mappedLeft, 0, bitmapWidth);
        double clampedTop = Math.Clamp(mappedTop, 0, bitmapHeight);
        double clampedRight = Math.Clamp(mappedRight, clampedLeft, bitmapWidth);
        double clampedBottom = Math.Clamp(mappedBottom, clampedTop, bitmapHeight);

        return new Rect(
            clampedLeft,
            clampedTop,
            Math.Max(0, clampedRight - clampedLeft),
            Math.Max(0, clampedBottom - clampedTop));
    }

    public static SKRectI MapRectToBitmapInt(PixelRect rect, PixelRect virtualBounds, int bitmapWidth, int bitmapHeight)
    {
        if (bitmapWidth <= 0 || bitmapHeight <= 0 || rect.IsEmpty)
        {
            return SKRectI.Empty;
        }

        double scaleX = virtualBounds.Width > 0 ? bitmapWidth / virtualBounds.Width : 1.0;
        double scaleY = virtualBounds.Height > 0 ? bitmapHeight / virtualBounds.Height : 1.0;
        double originX = virtualBounds.IsEmpty ? 0 : virtualBounds.X;
        double originY = virtualBounds.IsEmpty ? 0 : virtualBounds.Y;

        int left = (int)Math.Round((rect.X - originX) * scaleX);
        int top = (int)Math.Round((rect.Y - originY) * scaleY);
        int right = (int)Math.Round((rect.Right - originX) * scaleX);
        int bottom = (int)Math.Round((rect.Bottom - originY) * scaleY);

        var mapped = new SKRectI(left, top, right, bottom);
        mapped.Intersect(new SKRectI(0, 0, bitmapWidth, bitmapHeight));
        return mapped;
    }

    public static (int X, int Y) MapPointToBitmap(PixelPoint point, PixelRect virtualBounds, int bitmapWidth, int bitmapHeight)
    {
        if (bitmapWidth <= 0 || bitmapHeight <= 0)
        {
            return (0, 0);
        }

        double scaleX = virtualBounds.Width > 0 ? bitmapWidth / virtualBounds.Width : 1.0;
        double scaleY = virtualBounds.Height > 0 ? bitmapHeight / virtualBounds.Height : 1.0;
        double originX = virtualBounds.IsEmpty ? 0 : virtualBounds.X;
        double originY = virtualBounds.IsEmpty ? 0 : virtualBounds.Y;

        int mappedX = (int)Math.Round((point.X - originX) * scaleX);
        int mappedY = (int)Math.Round((point.Y - originY) * scaleY);

        return (
            Math.Clamp(mappedX, 0, bitmapWidth - 1),
            Math.Clamp(mappedY, 0, bitmapHeight - 1));
    }
}
