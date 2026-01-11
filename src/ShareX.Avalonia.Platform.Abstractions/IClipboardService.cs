#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

namespace XerahS.Platform.Abstractions
{
    /// <summary>
    /// Platform-agnostic clipboard service
    /// </summary>
    public interface IClipboardService
    {
        /// <summary>
        /// Clears the clipboard
        /// </summary>
        void Clear();

        /// <summary>
        /// Checks if the clipboard contains text
        /// </summary>
        bool ContainsText();

        /// <summary>
        /// Checks if the clipboard contains an image
        /// </summary>
        bool ContainsImage();

        /// <summary>
        /// Checks if the clipboard contains file drop list
        /// </summary>
        bool ContainsFileDropList();

        /// <summary>
        /// Gets text from the clipboard
        /// </summary>
        string? GetText();

        /// <summary>
        /// Sets text to the clipboard
        /// </summary>
        void SetText(string text);

        /// <summary>
        /// Gets an image from the clipboard
        /// </summary>
        SKBitmap? GetImage();

        /// <summary>
        /// Sets an image to the clipboard
        /// </summary>
        void SetImage(SKBitmap image);

        /// <summary>
        /// Gets file drop list from the clipboard
        /// </summary>
        string[]? GetFileDropList();

        /// <summary>
        /// Sets file drop list to the clipboard
        /// </summary>
        void SetFileDropList(string[] files);

        /// <summary>
        /// Gets data from the clipboard in a specific format
        /// </summary>
        object? GetData(string format);

        /// <summary>
        /// Sets data to the clipboard in a specific format
        /// </summary>
        void SetData(string format, object data);

        /// <summary>
        /// Checks if the clipboard contains data in a specific format
        /// </summary>
        bool ContainsData(string format);

        /// <summary>
        /// Asynchronously gets text from the clipboard (for platforms that require async clipboard access)
        /// </summary>
        Task<string?> GetTextAsync();

        /// <summary>
        /// Asynchronously sets text to the clipboard (for platforms that require async clipboard access)
        /// </summary>
        Task SetTextAsync(string text);
    }
}
