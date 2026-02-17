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
/// Represents a remote file or folder returned by <see cref="IUploaderExplorer.ListAsync"/>.
/// </summary>
public class MediaItem
{
    /// <summary>Provider-specific unique identifier (e.g. S3 object key, Imgur image id).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name of the file or folder.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Full remote path (e.g. "photos/2026/02/image.jpg").</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>True when this item represents a folder / prefix.</summary>
    public bool IsFolder { get; set; }

    /// <summary>File size in bytes. 0 for folders.</summary>
    public long SizeBytes { get; set; }

    /// <summary>MIME type when known (e.g. "image/jpeg"). Null for folders.</summary>
    public string? MimeType { get; set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>Last modification timestamp.</summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>Direct public URL when available.</summary>
    public string? Url { get; set; }

    /// <summary>
    /// URL of a pre-computed provider thumbnail when available
    /// (e.g. Imgur medium thumbnail URL).
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Arbitrary provider-specific key-value metadata.
    /// Providers store credentials references and other context here so that
    /// subsequent calls (<see cref="IUploaderExplorer.GetThumbnailAsync"/>,
    /// <see cref="IUploaderExplorer.DeleteAsync"/>, etc.) have all required information.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
