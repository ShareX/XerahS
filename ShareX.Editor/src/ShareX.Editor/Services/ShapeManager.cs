using SkiaSharp;

namespace ShareX.Editor;

/// <summary>
/// Manages the collection of shapes in the editor.
/// </summary>
public class ShapeManager : IDisposable
{
    private readonly List<BaseShape> _shapes = new();
    private BaseShape? _currentShape;
    private BaseShape? _selectedShape;
    private bool _isCreating;
    private bool _isMoving;

    /// <summary>
    /// Event raised when a shape is added or removed.
    /// </summary>
    public event Action? ShapesChanged;

    /// <summary>
    /// Event raised when the selection changes.
    /// </summary>
    public event Action? SelectionChanged;

    /// <summary>
    /// All shapes in the editor.
    /// </summary>
    public IReadOnlyList<BaseShape> Shapes => _shapes.AsReadOnly();

    /// <summary>
    /// Drawing shapes only.
    /// </summary>
    public IEnumerable<BaseShape> DrawingShapes => _shapes.Where(s => s.ShapeCategory == ShapeCategory.Drawing);

    /// <summary>
    /// Effect shapes only.
    /// </summary>
    public IEnumerable<BaseShape> EffectShapes => _shapes.Where(s => s.ShapeCategory == ShapeCategory.Effect);

    /// <summary>
    /// The currently selected shape.
    /// </summary>
    public BaseShape? SelectedShape
    {
        get => _selectedShape;
        set
        {
            if (_selectedShape != value)
            {
                _selectedShape = value;
                SelectionChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// The shape currently being created.
    /// </summary>
    public BaseShape? CurrentShape => _currentShape;

    /// <summary>
    /// Whether a shape is currently being created.
    /// </summary>
    public bool IsCreating => _isCreating;

    /// <summary>
    /// Whether a shape is currently being moved.
    /// </summary>
    public bool IsMoving => _isMoving;

    /// <summary>
    /// Adds a shape to the collection.
    /// </summary>
    public void AddShape(BaseShape shape)
    {
        shape.Manager = this;
        _shapes.Add(shape);
        ShapesChanged?.Invoke();
    }

    /// <summary>
    /// Removes a shape from the collection.
    /// </summary>
    public bool RemoveShape(BaseShape shape)
    {
        if (_shapes.Remove(shape))
        {
            if (_selectedShape == shape)
                _selectedShape = null;
            shape.Dispose();
            ShapesChanged?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clears all shapes.
    /// </summary>
    public void Clear()
    {
        foreach (var shape in _shapes)
            shape.Dispose();
        _shapes.Clear();
        _selectedShape = null;
        _currentShape = null;
        ShapesChanged?.Invoke();
    }

    /// <summary>
    /// Starts creating a new shape at the given position.
    /// </summary>
    public void StartCreating(BaseShape shape, SKPoint startPos)
    {
        shape.Manager = this;
        shape.OnCreating(startPos);
        _currentShape = shape;
        _isCreating = true;
    }

    /// <summary>
    /// Updates the shape being created.
    /// </summary>
    public void UpdateCreating(SKPoint currentPos)
    {
        if (_isCreating && _currentShape != null)
        {
            _currentShape.OnCreatingUpdate(currentPos);
        }
    }

    /// <summary>
    /// Finishes creating the current shape.
    /// </summary>
    public void EndCreating()
    {
        if (_isCreating && _currentShape != null)
        {
            _currentShape.OnCreated();
            if (_currentShape.IsValidShape)
            {
                _shapes.Add(_currentShape);
                _selectedShape = _currentShape;
                ShapesChanged?.Invoke();
            }
            else
            {
                _currentShape.Dispose();
            }
            _currentShape = null;
            _isCreating = false;
        }
    }

    /// <summary>
    /// Cancels creating the current shape.
    /// </summary>
    public void CancelCreating()
    {
        if (_isCreating && _currentShape != null)
        {
            _currentShape.Dispose();
            _currentShape = null;
            _isCreating = false;
        }
    }

    /// <summary>
    /// Starts moving the selected shape.
    /// </summary>
    public void StartMoving()
    {
        if (_selectedShape != null)
        {
            _isMoving = true;
        }
    }

    /// <summary>
    /// Moves the selected shape by the given delta.
    /// </summary>
    public void UpdateMoving(float dx, float dy)
    {
        if (_isMoving && _selectedShape != null)
        {
            _selectedShape.Move(dx, dy);
        }
    }

    /// <summary>
    /// Ends moving the selected shape.
    /// </summary>
    public void EndMoving()
    {
        _isMoving = false;
    }

    /// <summary>
    /// Finds the shape at the given point (topmost first).
    /// </summary>
    public BaseShape? GetShapeAtPoint(SKPoint point)
    {
        for (int i = _shapes.Count - 1; i >= 0; i--)
        {
            if (_shapes[i].Contains(point))
                return _shapes[i];
        }
        return null;
    }

    /// <summary>
    /// Selects the shape at the given point.
    /// </summary>
    public bool SelectShapeAtPoint(SKPoint point)
    {
        var shape = GetShapeAtPoint(point);
        SelectedShape = shape;
        return shape != null;
    }

    /// <summary>
    /// Moves the selected shape up in z-order.
    /// </summary>
    public void MoveSelectedUp()
    {
        if (_selectedShape == null) return;
        int index = _shapes.IndexOf(_selectedShape);
        if (index < _shapes.Count - 1)
        {
            _shapes.RemoveAt(index);
            _shapes.Insert(index + 1, _selectedShape);
            ShapesChanged?.Invoke();
        }
    }

    /// <summary>
    /// Moves the selected shape down in z-order.
    /// </summary>
    public void MoveSelectedDown()
    {
        if (_selectedShape == null) return;
        int index = _shapes.IndexOf(_selectedShape);
        if (index > 0)
        {
            _shapes.RemoveAt(index);
            _shapes.Insert(index - 1, _selectedShape);
            ShapesChanged?.Invoke();
        }
    }

    /// <summary>
    /// Deletes the selected shape.
    /// </summary>
    public void DeleteSelected()
    {
        if (_selectedShape != null)
        {
            RemoveShape(_selectedShape);
        }
    }

    /// <summary>
    /// Duplicates the selected shape.
    /// </summary>
    public BaseShape? DuplicateSelected()
    {
        if (_selectedShape == null) return null;
        var duplicate = _selectedShape.Duplicate();
        if (duplicate != null)
        {
            duplicate.Move(10, 10); // Offset slightly
            AddShape(duplicate);
            SelectedShape = duplicate;
        }
        return duplicate;
    }

    public void Dispose()
    {
        Clear();
        GC.SuppressFinalize(this);
    }
}
