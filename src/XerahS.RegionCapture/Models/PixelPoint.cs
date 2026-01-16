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
