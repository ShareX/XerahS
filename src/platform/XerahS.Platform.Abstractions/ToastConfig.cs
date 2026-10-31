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
    public ContentPlacement Placement { get; set; } = ContentPlacement.BottomRight;

    /// <summary>
    /// Offset from screen edge in pixels
    /// </summary>
    public int Offset { get; set; } = 5;

    /// <summary>
    /// Size of the toast window
    /// </summary>
    public SizeI Size { get; set; } = new SizeI(400, 300);

    /// <summary>
    /// Where the task's output ended up. Drives the small output chip on the toast.
    /// None (the default) shows no chip.
    /// </summary>
    public ToastOutputKind OutputKind { get; set; } = ToastOutputKind.None;

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
    /// Detailed error payload for failed operations. When present, UI can expose "Copy Errors".
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// File path associated with this notification (for actions)
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// URL associated with this notification (for actions)
    /// </summary>
    public string? URL { get; set; }

    /// <summary>
    /// Durable history row backing this completion notification, when one was created.
    /// </summary>
    public long? HistoryItemId { get; set; }

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
    public ToastClickAction MiddleClickAction { get; set; } = ToastClickAction.AnnotateMedia;

    /// <summary>
    /// Actions shown as buttons in the toast's hover toolbar, in display order.
    /// Actions that cannot run for this toast's file/URL are hidden.
    /// </summary>
    public List<ToastClickAction> ActionButtons { get; set; } = [];

    /// <summary>
    /// Width and height of each hover toolbar button in pixels
    /// </summary>
    public int ActionButtonSize { get; set; } = 40;

    /// <summary>
    /// Whether the toast should auto-hide when mouse is not over it
    /// </summary>
    public bool AutoHide { get; set; } = true;

    /// <summary>
    /// Whether this toast communicates a critical action the user must take and should be shown
    /// even when ordinary toast notifications are disabled.
    /// </summary>
    public bool IgnoreGlobalDisable { get; set; }

    /// <summary>
    /// Checks if this configuration is valid for display
    /// </summary>
    public bool IsValid =>
        float.IsFinite(Duration) &&
        float.IsFinite(FadeDuration) &&
        Duration >= 0 &&
        FadeDuration >= 0 &&
        (!AutoHide || Duration > 0 || FadeDuration > 0) &&
        Size.Width > 0 &&
        Size.Height > 0;
}

/// <summary>
/// Actions that can be performed when clicking on a toast notification
/// </summary>
public enum ToastClickAction
{
    CloseNotification,
    AnnotateMedia,
    [Obsolete("Renamed to AnnotateMedia. Kept for JSON backwards-compatibility.")]
    AnnotateImage = AnnotateMedia,
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

/// <summary>
/// Where a completed task's output ended up, shown as a chip on the toast.
/// </summary>
public enum ToastOutputKind
{
    /// <summary>No chip (failed tasks and non-result toasts).</summary>
    None,
    /// <summary>Uploaded; a link exists.</summary>
    Uploaded,
    /// <summary>Saved locally only; no upload was attempted.</summary>
    Local,
    /// <summary>Upload was attempted but no link came back; the local file is kept.</summary>
    UploadFailedLocal,
    /// <summary>Copied to the clipboard only; no file and no link.</summary>
    ClipboardOnly
}

/// <summary>
/// Pure rules for picking a <see cref="ToastOutputKind"/>.
/// </summary>
public static class ToastOutputClassifier
{
    public static ToastOutputKind Resolve(
        bool isError,
        string? url,
        bool fileExists,
        bool uploadAttempted,
        bool copiedToClipboard)
    {
        if (isError)
        {
            return ToastOutputKind.None;
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            return ToastOutputKind.Uploaded;
        }

        if (fileExists)
        {
            return uploadAttempted ? ToastOutputKind.UploadFailedLocal : ToastOutputKind.Local;
        }

        return copiedToClipboard ? ToastOutputKind.ClipboardOnly : ToastOutputKind.None;
    }
}
