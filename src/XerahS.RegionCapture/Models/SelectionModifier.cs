namespace XerahS.RegionCapture.Models;

/// <summary>
/// Represents keyboard modifiers that affect selection behavior.
/// </summary>
[Flags]
public enum SelectionModifier
{
    None = 0,

    /// <summary>
    /// Shift key - locks aspect ratio during drag.
    /// </summary>
    LockAspectRatio = 1,

    /// <summary>
    /// Ctrl key - enables pixel nudge mode with arrow keys.
    /// </summary>
    PixelNudge = 2,

    /// <summary>
    /// Alt key - expands selection from center.
    /// </summary>
    FromCenter = 4
}
