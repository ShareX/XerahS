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

using System.Drawing;

namespace XerahS.Platform.Abstractions;

/// <summary>
/// Configuration for displaying a toast notification.
/// Uses primitive types to avoid Avalonia dependencies in the abstraction layer.
/// </summary>
public class ToastConfig
{
    /// <summary>
    /// Display duration in seconds before fade starts
    /// </summary>
    public float Duration { get; set; } = 3f;

    /// <summary>
    /// Fade-out duration in seconds
    /// </summary>
    public float FadeDuration { get; set; } = 1f;

    /// <summary>
    /// Screen placement for the toast window
    /// </summary>
    public ContentAlignment Placement { get; set; } = ContentAlignment.BottomRight;

    /// <summary>
    /// Offset from screen edge in pixels
    /// </summary>
    public int Offset { get; set; } = 5;

    /// <summary>
    /// Size of the toast window
    /// </summary>
    public Size Size { get; set; } = new Size(400, 300);

    /// <summary>
    /// Path to thumbnail image file (optional). 
    /// The UI layer will load this as an Avalonia Bitmap.
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// Title text (optional, displayed above main text)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Main text message (displayed when no image or as URL overlay)
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// File path associated with this notification (for actions)
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// URL associated with this notification (for actions)
    /// </summary>
    public string? URL { get; set; }

    /// <summary>
    /// Action to perform on left click
    /// </summary>
    public ToastClickAction LeftClickAction { get; set; } = ToastClickAction.OpenUrl;

    /// <summary>
    /// Action to perform on right click
    /// </summary>
    public ToastClickAction RightClickAction { get; set; } = ToastClickAction.CloseNotification;

    /// <summary>
    /// Action to perform on middle click
    /// </summary>
    public ToastClickAction MiddleClickAction { get; set; } = ToastClickAction.AnnotateImage;

    /// <summary>
    /// Whether the toast should auto-hide when mouse is not over it
    /// </summary>
    public bool AutoHide { get; set; } = true;

    /// <summary>
    /// Checks if this configuration is valid for display
    /// </summary>
    public bool IsValid => (Duration > 0 || FadeDuration > 0) && Size.Width > 0 && Size.Height > 0;
}

/// <summary>
/// Actions that can be performed when clicking on a toast notification
/// </summary>
public enum ToastClickAction
{
    CloseNotification,
    AnnotateImage,
    CopyImageToClipboard,
    CopyFile,
    CopyFilePath,
    CopyUrl,
    OpenFile,
    OpenFolder,
    OpenUrl,
    Upload,
    PinToScreen,
    DeleteFile
}
