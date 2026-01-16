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

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Defines which file types an uploader instance handles within its category
/// </summary>
public class FileTypeScope
{
    /// <summary>
    /// If true, this instance handles ALL file types for its category.
    /// When true, FileExtensions is ignored.
    /// </summary>
    public bool AllFileTypes { get; set; }

    /// <summary>
    /// Specific file extensions this instance handles (e.g., ["png", "jpg", "gif"]).
    /// Extensions should be lowercase without the leading dot.
    /// Only used when AllFileTypes is false.
    /// </summary>
    public List<string> FileExtensions { get; set; } = new();

    /// <summary>
    /// Check if this scope handles a specific file extension
    /// </summary>
    public bool HandlesExtension(string extension)
    {
        if (AllFileTypes)
            return true;

        return FileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
