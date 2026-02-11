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

using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.Core;

/// <summary>
/// Represents parsed clipboard content with detected data type.
/// Shared between headless ClipboardUpload and interactive UploadContentWindow.
/// </summary>
public class ClipboardContent
{
    public EDataType DataType { get; set; }
    public SKBitmap? Image { get; set; }
    public string? Text { get; set; }
    public string[]? Files { get; set; }
}

/// <summary>
/// Shared clipboard parsing utility. Detects clipboard content type
/// using the same priority order as ShareX: image > text > files.
/// </summary>
public static class ClipboardContentHelper
{
    /// <summary>
    /// Parses the current clipboard contents and returns a structured
    /// <see cref="ClipboardContent"/> result, or null if clipboard is empty
    /// or contains unsupported data.
    /// </summary>
    public static ClipboardContent? ParseClipboard(IClipboardService clipboard)
    {
        if (clipboard.ContainsImage())
        {
            var image = clipboard.GetImage();
            if (image != null)
            {
                return new ClipboardContent
                {
                    DataType = EDataType.Image,
                    Image = image
                };
            }
        }

        if (clipboard.ContainsText())
        {
            var text = clipboard.GetText();
            if (!string.IsNullOrEmpty(text))
            {
                return new ClipboardContent
                {
                    DataType = EDataType.Text,
                    Text = text
                };
            }
        }

        if (clipboard.ContainsFileDropList())
        {
            var files = clipboard.GetFileDropList();
            if (files != null && files.Length > 0)
            {
                var validFiles = files
                    .Where(f => !string.IsNullOrWhiteSpace(f) && File.Exists(f))
                    .ToArray();

                if (validFiles.Length > 0)
                {
                    return new ClipboardContent
                    {
                        DataType = EDataType.File,
                        Files = validFiles
                    };
                }
            }
        }

        return null;
    }
}
