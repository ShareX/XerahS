using SkiaSharp;

namespace ShareX.Editor;

/// <summary>
/// Abstract base class for all shapes in the editor.
/// </summary>
public abstract class BaseShape : IDisposable
{
    public abstract ShapeCategory ShapeCategory { get; }
    public abstract ShapeType ShapeType { get; }

    private SKRect _rectangle;

    /// <summary>
    /// The bounding rectangle of this shape.
    /// </summary>
    public SKRect Rectangle
    {
        get => _rectangle;
        set
        {
            _rectangle = value;
            StartPosition = new SKPoint(value.Left, value.Top);
            EndPosition = new SKPoint(value.Right, value.Bottom);
        }
    }

    /// <summary>
    /// The starting point used during shape creation (typically top-left).
    /// </summary>
    public SKPoint StartPosition { get; internal set; }

    /// <summary>
    /// The ending point used during shape creation (typically bottom-right).
    /// </summary>
    public SKPoint EndPosition { get; internal set; }

    /// <summary>
    /// Initial size of the shape when created (for proportional resizing).
    /// </summary>
    public SKSize InitialSize { get; set; }

    /// <summary>
    /// Reference to the parent shape manager.
    /// </summary>
    public ShapeManager? Manager { get; internal set; }

    /// <summary>
    /// Whether this shape is valid (has non-zero area).
    /// </summary>
    public virtual bool IsValidShape => !Rectangle.IsEmpty && Rectangle.Width >= 1 && Rectangle.Height >= 1;

    /// <summary>
    /// Checks if a point is inside this shape's bounds.
    /// </summary>
    public virtual bool Contains(SKPoint point)
    {
        return Rectangle.Contains(point);
    }

    /// <summary>
    /// Moves the shape by the given offset.
    /// </summary>
    public virtual void Move(float dx, float dy)
    {
        Rectangle = new SKRect(
            Rectangle.Left + dx,
            Rectangle.Top + dy,
            Rectangle.Right + dx,
            Rectangle.Bottom + dy
        );
    }

    /// <summary>
    /// Renders this shape to the given canvas.
    /// </summary>
    public abstract void OnDraw(SKCanvas canvas);

    /// <summary>
    /// Called when the shape is first being created (mouse down).
    /// </summary>
    public virtual void OnCreating(SKPoint startPos)
    {
        StartPosition = startPos;
        EndPosition = startPos;
        Rectangle = new SKRect(startPos.X, startPos.Y, startPos.X + 1, startPos.Y + 1);
    }

    /// <summary>
    /// Called during shape creation (mouse move while dragging).
    /// </summary>
    public virtual void OnCreatingUpdate(SKPoint currentPos)
    {
        EndPosition = currentPos;
        Rectangle = CreateRectangle(StartPosition, EndPosition);
    }

    /// <summary>
    /// Called when shape creation is complete (mouse up).
    /// </summary>
    public virtual void OnCreated()
    {
        InitialSize = new SKSize(Rectangle.Width, Rectangle.Height);
    }

    /// <summary>
    /// Creates a normalized rectangle from two corner points.
    /// </summary>
    protected static SKRect CreateRectangle(SKPoint p1, SKPoint p2)
    {
        float left = Math.Min(p1.X, p2.X);
        float top = Math.Min(p1.Y, p2.Y);
        float right = Math.Max(p1.X, p2.X);
        float bottom = Math.Max(p1.Y, p2.Y);
        return new SKRect(left, top, right, bottom);
    }

    /// <summary>
    /// Creates a duplicate of this shape.
    /// </summary>
    public virtual BaseShape? Duplicate()
    {
        // Default implementation returns null; subclasses should override.
        return null;
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
