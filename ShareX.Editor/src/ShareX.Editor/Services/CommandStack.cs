namespace ShareX.Editor;

/// <summary>
/// Record representing a command for undo/redo.
/// </summary>
public abstract record EditorCommand
{
    public abstract void Execute();
    public abstract void Undo();
}

/// <summary>
/// Command for adding a shape.
/// </summary>
public record AddShapeCommand(ShapeManager Manager, BaseShape Shape) : EditorCommand
{
    public override void Execute() => Manager.AddShape(Shape);
    public override void Undo() => Manager.RemoveShape(Shape);
}

/// <summary>
/// Command for removing a shape.
/// </summary>
public record RemoveShapeCommand(ShapeManager Manager, BaseShape Shape) : EditorCommand
{
    public override void Execute() => Manager.RemoveShape(Shape);
    public override void Undo() => Manager.AddShape(Shape);
}

/// <summary>
/// Manages undo/redo command stack.
/// </summary>
public class CommandStack
{
    private readonly Stack<EditorCommand> _undoStack = new();
    private readonly Stack<EditorCommand> _redoStack = new();

    /// <summary>
    /// Event raised when the stack changes.
    /// </summary>
    public event Action? StackChanged;

    /// <summary>
    /// Whether undo is available.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Whether redo is available.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Executes a command and adds it to the undo stack.
    /// </summary>
    public void Execute(EditorCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
        StackChanged?.Invoke();
    }

    /// <summary>
    /// Undoes the last command.
    /// </summary>
    public void Undo()
    {
        if (_undoStack.TryPop(out var command))
        {
            command.Undo();
            _redoStack.Push(command);
            StackChanged?.Invoke();
        }
    }

    /// <summary>
    /// Redoes the last undone command.
    /// </summary>
    public void Redo()
    {
        if (_redoStack.TryPop(out var command))
        {
            command.Execute();
            _undoStack.Push(command);
            StackChanged?.Invoke();
        }
    }

    /// <summary>
    /// Clears all commands.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StackChanged?.Invoke();
    }
}
