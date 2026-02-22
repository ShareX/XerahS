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
/// Parameters for <see cref="IUploaderExplorer.ListAsync"/>.
/// </summary>
public class ExplorerQuery
{
    /// <summary>
    /// Serialised JSON settings for the specific uploader instance (bucket name, credentials key, etc.).
    /// Populated by <c>ProviderExplorerViewModel</c> from <c>UploaderInstance.SettingsJson</c>
    /// before every call, so the provider can access per-instance configuration without
    /// storing mutable state.
    /// </summary>
    public string? SettingsJson { get; set; }

    /// <summary>Remote folder path to list. Null or empty means root.</summary>
    public string? FolderPath { get; set; }

    /// <summary>Optional text filter applied to file names.</summary>
    public string? SearchText { get; set; }

    /// <summary>MIME-type prefix filter (e.g. "image/*", "text/*"). Null = all types.</summary>
    public string? FileTypeFilter { get; set; }

    /// <summary>Field to sort results by.</summary>
    public ExplorerSortField SortBy { get; set; } = ExplorerSortField.Date;

    /// <summary>When true, sort in descending order (newest/largest first).</summary>
    public bool SortDescending { get; set; } = true;

    /// <summary>Maximum number of items to return per page.</summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Cursor for the next page.
    /// Null on the first request; set to the value returned in the previous
    /// <see cref="ExplorerPage.ContinuationToken"/> for subsequent pages.
    /// Supports both cursor-based (S3) and offset-based (Imgur) pagination.
    /// </summary>
    public string? ContinuationToken { get; set; }
}

/// <summary>Fields available for sorting explorer results.</summary>
public enum ExplorerSortField
{
    Name,
    Date,
    Size
}
