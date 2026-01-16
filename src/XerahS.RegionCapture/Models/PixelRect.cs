using System.Numerics;
using System.Runtime.CompilerServices;

namespace XerahS.RegionCapture.Models;

/// <summary>
/// Represents a rectangle in physical (device) pixels with sub-pixel precision support.
/// This is the "truth" layer for coordinates across different DPI monitors.
/// </summary>
public readonly record struct PixelRect(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public PixelPoint TopLeft => new(X, Y);
    public PixelPoint TopRight => new(Right, Y);
    public PixelPoint BottomLeft => new(X, Bottom);
    public PixelPoint BottomRight => new(Right, Bottom);
    public PixelPoint Center => new(X + Width / 2, Y + Height / 2);

    public double Area => Width * Height;

    public static PixelRect Empty => default;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(PixelPoint point) =>
        point.X >= X && point.X < Right &&
        point.Y >= Y && point.Y < Bottom;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(double px, double py) =>
        px >= X && px < Right &&
        py >= Y && py < Bottom;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IntersectsWith(PixelRect other) =>
        X < other.Right && Right > other.X &&
        Y < other.Bottom && Bottom > other.Y;

    public PixelRect Intersect(PixelRect other)
    {
        var x = Math.Max(X, other.X);
        var y = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);

        if (right > x && bottom > y)
            return new PixelRect(x, y, right - x, bottom - y);

        return Empty;
    }

    public PixelRect Union(PixelRect other)
    {
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;

        var x = Math.Min(X, other.X);
        var y = Math.Min(Y, other.Y);
        var right = Math.Max(Right, other.Right);
        var bottom = Math.Max(Bottom, other.Bottom);

        return new PixelRect(x, y, right - x, bottom - y);
    }

    public PixelRect Inflate(double horizontal, double vertical) =>
        new(X - horizontal, Y - vertical, Width + 2 * horizontal, Height + 2 * vertical);

    public PixelRect Offset(double dx, double dy) =>
        new(X + dx, Y + dy, Width, Height);

    public PixelRect Normalize()
    {
        var x = Width < 0 ? X + Width : X;
        var y = Height < 0 ? Y + Height : Y;
        return new PixelRect(x, y, Math.Abs(Width), Math.Abs(Height));
    }

    /// <summary>
    /// Creates a PixelRect from two corner points.
    /// </summary>
    public static PixelRect FromCorners(PixelPoint p1, PixelPoint p2)
    {
        var x = Math.Min(p1.X, p2.X);
        var y = Math.Min(p1.Y, p2.Y);
        var width = Math.Abs(p2.X - p1.X);
        var height = Math.Abs(p2.Y - p1.Y);
        return new PixelRect(x, y, width, height);
    }

    /// <summary>
    /// Converts to integer bounds (rounds outward to ensure full coverage).
    /// </summary>
    public (int X, int Y, int Width, int Height) ToIntegerBounds()
    {
        var left = (int)Math.Floor(X);
        var top = (int)Math.Floor(Y);
        var right = (int)Math.Ceiling(Right);
        var bottom = (int)Math.Ceiling(Bottom);
        return (left, top, right - left, bottom - top);
    }

    public override string ToString() => $"PixelRect({X:F2}, {Y:F2}, {Width:F2}, {Height:F2})";
}
