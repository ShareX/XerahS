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

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Optional interface for providers that support browsing remote files.
/// Implement alongside IUploaderProvider to enable the Media Explorer.
/// Binary compatibility is preserved: existing providers that do not
/// implement this interface continue to work without modification.
/// </summary>
public interface IUploaderExplorer
{
    /// <summary>
    /// Whether this provider supports hierarchical folders.
    /// </summary>
    bool SupportsFolders { get; }

    /// <summary>
    /// Lists files and folders at the given remote path.
    /// Pass null or empty FolderPath for root.
    /// Credentials and per-instance settings are supplied via <see cref="ExplorerQuery.SettingsJson"/>.
    /// </summary>
    Task<ExplorerPage> ListAsync(ExplorerQuery query, CancellationToken cancellation = default);

    /// <summary>
    /// Returns thumbnail bytes (JPEG/PNG) for the given item.
    /// Returns null when thumbnails are not supported or the item is not an image.
    /// </summary>
    Task<byte[]?> GetThumbnailAsync(MediaItem item, int maxWidthPx = 180, CancellationToken cancellation = default);

    /// <summary>
    /// Returns the full content stream for preview or download.
    /// Returns null when content cannot be fetched.
    /// </summary>
    Task<Stream?> GetContentAsync(MediaItem item, CancellationToken cancellation = default);

    /// <summary>
    /// Deletes the remote file. Returns true on success.
    /// </summary>
    Task<bool> DeleteAsync(MediaItem item, CancellationToken cancellation = default);

    /// <summary>
    /// Creates a folder at the given parent path.
    /// Only meaningful when <see cref="SupportsFolders"/> is true.
    /// </summary>
    Task<bool> CreateFolderAsync(string parentPath, string folderName, CancellationToken cancellation = default);
}
