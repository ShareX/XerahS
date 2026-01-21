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
using System.Numerics;
using System.Runtime.CompilerServices;

namespace XerahS.RegionCapture.Models;

/// <summary>
/// Represents a point in physical (device) pixels with sub-pixel precision.
/// </summary>
public readonly record struct PixelPoint(double X, double Y)
{
    public static PixelPoint Origin => default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double DistanceTo(PixelPoint other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double DistanceSquaredTo(PixelPoint other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        return dx * dx + dy * dy;
    }

    public PixelPoint Offset(double dx, double dy) => new(X + dx, Y + dy);

    public static PixelPoint operator +(PixelPoint left, PixelPoint right) =>
        new(left.X + right.X, left.Y + right.Y);

    public static PixelPoint operator -(PixelPoint left, PixelPoint right) =>
        new(left.X - right.X, left.Y - right.Y);

    public static PixelPoint operator *(PixelPoint point, double scalar) =>
        new(point.X * scalar, point.Y * scalar);

    public static PixelPoint operator /(PixelPoint point, double scalar) =>
        new(point.X / scalar, point.Y / scalar);

    /// <summary>
    /// Rounds to nearest integer coordinates.
    /// </summary>
    public (int X, int Y) ToInteger() => ((int)Math.Round(X), (int)Math.Round(Y));

    /// <summary>
    /// Truncates to integer coordinates.
    /// </summary>
    public (int X, int Y) Truncate() => ((int)X, (int)Y);

    public override string ToString() => $"PixelPoint({X:F2}, {Y:F2})";
}
