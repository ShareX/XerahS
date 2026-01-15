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

namespace XerahS.Services.Abstractions;

/// <summary>
/// Service for file dialog operations
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Opens a file browser dialog
    /// </summary>
    Task<string?> BrowseFileAsync(string title, string? initialDirectory = null, FileDialogFilter[]? filters = null);

    /// <summary>
    /// Opens a save file dialog
    /// </summary>
    Task<string?> SaveFileAsync(string title, string? defaultFileName = null, FileDialogFilter[]? filters = null);

    /// <summary>
    /// Opens a folder browser dialog
    /// </summary>
    Task<string?> BrowseFolderAsync(string title, string? initialDirectory = null);

    /// <summary>
    /// Opens a multi-file browser dialog
    /// </summary>
    Task<string[]?> BrowseMultipleFilesAsync(string title, string? initialDirectory = null, FileDialogFilter[]? filters = null);
}

/// <summary>
/// File dialog filter for file type selection
/// </summary>
public class FileDialogFilter
{
    public required string Name { get; init; }
    public required string[] Extensions { get; init; }
}
