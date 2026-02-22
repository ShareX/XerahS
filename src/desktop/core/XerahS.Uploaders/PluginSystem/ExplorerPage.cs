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
/// A single page of results from <see cref="IUploaderExplorer.ListAsync"/>.
/// </summary>
public class ExplorerPage
{
    /// <summary>Items in this page (files and/or folders).</summary>
    public IReadOnlyList<MediaItem> Items { get; set; } = Array.Empty<MediaItem>();

    /// <summary>
    /// Token to retrieve the next page.
    /// Null when this is the last page.
    /// Pass to <see cref="ExplorerQuery.ContinuationToken"/> on the next request.
    /// </summary>
    public string? ContinuationToken { get; set; }

    /// <summary>
    /// Total item count across all pages, when known by the provider.
    /// Null when the provider cannot determine the total (e.g. S3).
    /// </summary>
    public int? TotalCount { get; set; }
}
