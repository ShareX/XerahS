namespace XerahS.RegionCapture.Models;

/// <summary>
/// Represents the current state of the region capture interaction.
/// </summary>
public enum CaptureState
{
    /// <summary>
    /// User is hovering, window snapping is active.
    /// </summary>
    Hovering,

    /// <summary>
    /// User is actively dragging to select a region.
    /// </summary>
    Dragging,

    /// <summary>
    /// Selection is complete, ready to confirm.
    /// </summary>
    Selected,

    /// <summary>
    /// Capture was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Capture was confirmed.
    /// </summary>
    Confirmed
}
