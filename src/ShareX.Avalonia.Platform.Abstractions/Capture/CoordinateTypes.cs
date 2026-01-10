using System;

namespace ShareX.Avalonia.Platform.Abstractions.Capture;

/// <summary>
/// Represents a rectangle in physical pixel coordinates.
/// Physical pixels are the raw hardware pixels as reported by the OS and capture APIs.
/// </summary>
public readonly record struct PhysicalRectangle
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    public PhysicalRectangle(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public PhysicalPoint TopLeft => new(X, Y);
    public PhysicalPoint TopRight => new(X + Width, Y);
    public PhysicalPoint BottomLeft => new(X, Y + Height);
    public PhysicalPoint BottomRight => new(X + Width, Y + Height);
    public PhysicalPoint Center => new(X + Width / 2, Y + Height / 2);

    public int Area => Width * Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>
    /// Creates a PhysicalRectangle from two corner points.
    /// </summary>
    public static PhysicalRectangle FromCorners(PhysicalPoint topLeft, PhysicalPoint bottomRight)
    {
        return new PhysicalRectangle(
            topLeft.X,
            topLeft.Y,
            bottomRight.X - topLeft.X,
            bottomRight.Y - topLeft.Y);
    }

    /// <summary>
    /// Checks if this rectangle contains the given point.
    /// </summary>
    public bool Contains(PhysicalPoint point)
    {
        return point.X >= X
            && point.X < X + Width
            && point.Y >= Y
            && point.Y < Y + Height;
    }

    /// <summary>
    /// Calculates the intersection with another rectangle.
    /// Returns null if there is no intersection.
    /// </summary>
    public PhysicalRectangle? Intersect(PhysicalRectangle other)
    {
        int x1 = Math.Max(X, other.X);
        int y1 = Math.Max(Y, other.Y);
        int x2 = Math.Min(X + Width, other.X + other.Width);
        int y2 = Math.Min(Y + Height, other.Y + other.Height);

        if (x2 <= x1 || y2 <= y1)
            return null;

        return new PhysicalRectangle(x1, y1, x2 - x1, y2 - y1);
    }

    public override string ToString() => $"({X}, {Y}, {Width}, {Height})";
}

/// <summary>
/// Represents a point in physical pixel coordinates.
/// </summary>
public readonly record struct PhysicalPoint
{
    public int X { get; init; }
    public int Y { get; init; }

    public PhysicalPoint(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override string ToString() => $"({X}, {Y})";
}

/// <summary>
/// Represents a rectangle in logical (DPI-independent) coordinates.
/// Logical coordinates are normalized to 96 DPI and used by UI frameworks like Avalonia.
/// </summary>
public readonly record struct LogicalRectangle
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }

    public LogicalRectangle(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public LogicalPoint TopLeft => new(X, Y);
    public LogicalPoint TopRight => new(X + Width, Y);
    public LogicalPoint BottomLeft => new(X, Y + Height);
    public LogicalPoint BottomRight => new(X + Width, Y + Height);
    public LogicalPoint Center => new(X + Width / 2, Y + Height / 2);

    public double Area => Width * Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>
    /// Creates a LogicalRectangle from two corner points.
    /// </summary>
    public static LogicalRectangle FromCorners(LogicalPoint topLeft, LogicalPoint bottomRight)
    {
        return new LogicalRectangle(
            topLeft.X,
            topLeft.Y,
            bottomRight.X - topLeft.X,
            bottomRight.Y - topLeft.Y);
    }

    /// <summary>
    /// Checks if this rectangle contains the given point.
    /// </summary>
    public bool Contains(LogicalPoint point)
    {
        return point.X >= X
            && point.X < X + Width
            && point.Y >= Y
            && point.Y < Y + Height;
    }

    public override string ToString() => $"({X:F2}, {Y:F2}, {Width:F2}, {Height:F2})";
}

/// <summary>
/// Represents a point in logical (DPI-independent) coordinates.
/// </summary>
public readonly record struct LogicalPoint
{
    public double X { get; init; }
    public double Y { get; init; }

    public LogicalPoint(double x, double y)
    {
        X = x;
        Y = y;
    }

    public override string ToString() => $"({X:F2}, {Y:F2})";
}
