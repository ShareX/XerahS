using XerahS.RegionCapture.Models;

namespace XerahS.RegionCapture.Services;

/// <summary>
/// State machine for managing region capture interaction states.
/// Handles transitions: Hovering &lt;-&gt; Dragging -&gt; Selected -&gt; Confirmed/Cancelled
/// </summary>
public sealed class SelectionStateMachine
{
    private CaptureState _currentState = CaptureState.Hovering;
    private PixelPoint _startPoint;
    private PixelPoint _currentPoint;
    private PixelRect _selectionRect;
    private WindowInfo? _hoveredWindow;
    private SelectionModifier _modifiers = SelectionModifier.None;
    private double _aspectRatio = 1.0;

    /// <summary>
    /// Gets the current capture state.
    /// </summary>
    public CaptureState CurrentState => _currentState;

    /// <summary>
    /// Gets the current selection rectangle (in physical pixels).
    /// </summary>
    public PixelRect SelectionRect => _selectionRect;

    /// <summary>
    /// Gets the currently hovered window (if any).
    /// </summary>
    public WindowInfo? HoveredWindow => _hoveredWindow;

    /// <summary>
    /// Gets the current cursor position (in physical pixels).
    /// </summary>
    public PixelPoint CurrentPoint => _currentPoint;

    /// <summary>
    /// Gets the active modifiers.
    /// </summary>
    public SelectionModifier Modifiers => _modifiers;

    /// <summary>
    /// Event fired when selection is confirmed.
    /// </summary>
    public event Action<PixelRect>? SelectionConfirmed;

    /// <summary>
    /// Event fired when selection is cancelled.
    /// </summary>
    public event Action? SelectionCancelled;

    /// <summary>
    /// Event fired when state changes.
    /// </summary>
    public event Action<CaptureState>? StateChanged;

    /// <summary>
    /// Updates cursor position during hover or drag.
    /// </summary>
    public void UpdateCursorPosition(PixelPoint physicalPoint)
    {
        _currentPoint = physicalPoint;

        if (_currentState == CaptureState.Dragging)
        {
            UpdateSelectionRect();
        }
    }

    /// <summary>
    /// Updates the hovered window.
    /// </summary>
    public void UpdateHoveredWindow(WindowInfo? window)
    {
        if (_currentState == CaptureState.Hovering)
        {
            _hoveredWindow = window;
        }
    }

    /// <summary>
    /// Sets the active keyboard modifiers.
    /// </summary>
    public void SetModifiers(SelectionModifier modifiers)
    {
        _modifiers = modifiers;

        if (_currentState == CaptureState.Dragging)
        {
            UpdateSelectionRect();
        }
    }

    /// <summary>
    /// Starts a drag operation from the current point.
    /// </summary>
    public void BeginDrag(PixelPoint startPoint)
    {
        if (_currentState != CaptureState.Hovering)
            return;

        _startPoint = startPoint;
        _currentPoint = startPoint;
        _selectionRect = new PixelRect(startPoint.X, startPoint.Y, 0, 0);
        _aspectRatio = 1.0;

        TransitionTo(CaptureState.Dragging);
    }

    /// <summary>
    /// Ends the current drag operation.
    /// </summary>
    public void EndDrag()
    {
        if (_currentState != CaptureState.Dragging)
            return;

        _selectionRect = _selectionRect.Normalize();

        // Minimum selection size of 3x3 pixels
        if (_selectionRect.Width > 3 && _selectionRect.Height > 3)
        {
            TransitionTo(CaptureState.Selected);
            ConfirmSelection();
        }
        else
        {
            // Selection too small, go back to hovering
            TransitionTo(CaptureState.Hovering);
            _selectionRect = PixelRect.Empty;
        }
    }

    /// <summary>
    /// Snaps to the currently hovered window.
    /// </summary>
    public void SnapToWindow()
    {
        if (_currentState != CaptureState.Hovering || _hoveredWindow is null)
            return;

        _selectionRect = _hoveredWindow.SnapBounds;
        TransitionTo(CaptureState.Selected);
        ConfirmSelection();
    }

    /// <summary>
    /// Cancels the current operation.
    /// </summary>
    public void Cancel()
    {
        TransitionTo(CaptureState.Cancelled);
        SelectionCancelled?.Invoke();
    }

    /// <summary>
    /// Nudges the selection by the specified delta (for arrow key handling).
    /// </summary>
    public void NudgeSelection(int dx, int dy)
    {
        if (_currentState == CaptureState.Selected)
        {
            _selectionRect = _selectionRect.Offset(dx, dy);
        }
        else if (_currentState == CaptureState.Dragging)
        {
            _currentPoint = _currentPoint.Offset(dx, dy);
            UpdateSelectionRect();
        }
    }

    /// <summary>
    /// Resizes the selection by the specified delta (for Ctrl+Arrow handling).
    /// </summary>
    public void ResizeSelection(int dWidth, int dHeight)
    {
        if (_currentState != CaptureState.Selected)
            return;

        var newWidth = Math.Max(1, _selectionRect.Width + dWidth);
        var newHeight = Math.Max(1, _selectionRect.Height + dHeight);

        _selectionRect = new PixelRect(
            _selectionRect.X,
            _selectionRect.Y,
            newWidth,
            newHeight);
    }

    private void UpdateSelectionRect()
    {
        var endPoint = _currentPoint;

        // Apply aspect ratio lock if Shift is held
        if (_modifiers.HasFlag(SelectionModifier.LockAspectRatio) && _aspectRatio > 0)
        {
            endPoint = ApplyAspectRatioLock(_startPoint, _currentPoint, _aspectRatio);
        }

        // Apply center expansion if Alt is held
        if (_modifiers.HasFlag(SelectionModifier.FromCenter))
        {
            var dx = endPoint.X - _startPoint.X;
            var dy = endPoint.Y - _startPoint.Y;

            _selectionRect = new PixelRect(
                _startPoint.X - dx,
                _startPoint.Y - dy,
                dx * 2,
                dy * 2).Normalize();
        }
        else
        {
            _selectionRect = PixelRect.FromCorners(_startPoint, endPoint);
        }
    }

    private static PixelPoint ApplyAspectRatioLock(PixelPoint start, PixelPoint end, double ratio)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;

        // Determine which dimension to constrain
        var absDx = Math.Abs(dx);
        var absDy = Math.Abs(dy);

        if (absDx / ratio > absDy)
        {
            // Constrain height based on width
            dy = Math.Sign(dy) * absDx / ratio;
        }
        else
        {
            // Constrain width based on height
            dx = Math.Sign(dx) * absDy * ratio;
        }

        return new PixelPoint(start.X + dx, start.Y + dy);
    }

    private void ConfirmSelection()
    {
        TransitionTo(CaptureState.Confirmed);
        SelectionConfirmed?.Invoke(_selectionRect);
    }

    private void TransitionTo(CaptureState newState)
    {
        if (_currentState == newState)
            return;

        _currentState = newState;
        StateChanged?.Invoke(newState);
    }
}
