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

namespace XerahS.Platform.Abstractions;

/// <summary>
/// Cross-platform replacement for System.Drawing.ContentAlignment.
/// Specifies alignment of content within a container.
/// </summary>
public enum ContentPlacement
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    MiddleCenter,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

/// <summary>
/// Cross-platform replacement for System.Drawing.Drawing2D.LinearGradientMode.
/// Specifies the direction of a linear gradient.
/// </summary>
public enum GradientDirection
{
    /// <summary>
    /// Specifies a gradient from left to right.
    /// </summary>
    Horizontal = 0,

    /// <summary>
    /// Specifies a gradient from top to bottom.
    /// </summary>
    Vertical = 1,

    /// <summary>
    /// Specifies a gradient from upper left to lower right.
    /// </summary>
    ForwardDiagonal = 2,

    /// <summary>
    /// Specifies a gradient from upper right to lower left.
    /// </summary>
    BackwardDiagonal = 3
}

/// <summary>
/// Cross-platform size structure using integers.
/// </summary>
public readonly struct SizeI : IEquatable<SizeI>
{
    public int Width { get; }
    public int Height { get; }

    public SizeI(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public static SizeI Empty => new(0, 0);

    public bool IsEmpty => Width == 0 && Height == 0;

    public bool Equals(SizeI other) => Width == other.Width && Height == other.Height;

    public override bool Equals(object? obj) => obj is SizeI other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Width, Height);

    public static bool operator ==(SizeI left, SizeI right) => left.Equals(right);

    public static bool operator !=(SizeI left, SizeI right) => !left.Equals(right);

    public override string ToString() => $"{{Width={Width}, Height={Height}}}";
}
