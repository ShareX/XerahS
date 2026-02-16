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

using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.UI.ViewModels;

/// <summary>
/// Wraps a <see cref="MediaItem"/> with Avalonia-specific display properties
/// (thumbnail bitmap, formatted size/date) for use in the Media Explorer view.
/// </summary>
public partial class MediaItemViewModel : ObservableObject
{
    public MediaItem Item { get; }

    /// <summary>Decoded thumbnail, populated by background loading in ProviderExplorerViewModel.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail))]
    private Bitmap? _thumbnail;

    public MediaItemViewModel(MediaItem item)
    {
        Item = item;
    }

    /// <summary>Emoji icon representing the file type, shown when no thumbnail is available.</summary>
    public string FileTypeIcon => Item.IsFolder
        ? "📁"
        : GetMimeIcon(Item.MimeType);

    /// <summary>Human-readable file size (e.g. "1.2 MB"). Empty for folders.</summary>
    public string SizeDisplay => Item.IsFolder ? "" : FormatBytes(Item.SizeBytes);

    /// <summary>Formatted modification date.</summary>
    public string DateDisplay => Item.ModifiedAt?.ToString("yyyy-MM-dd") ?? "";

    /// <summary>True when a thumbnail has been loaded or is available via ThumbnailUrl.</summary>
    public bool HasThumbnail => Thumbnail != null;

    private static string GetMimeIcon(string? mime)
    {
        if (string.IsNullOrEmpty(mime)) return "📄";
        if (mime.StartsWith("image/")) return "🖼️";
        if (mime.StartsWith("video/")) return "🎬";
        if (mime.StartsWith("audio/")) return "🎵";
        if (mime.StartsWith("text/")) return "📝";
        if (mime.Contains("pdf")) return "📕";
        if (mime.Contains("zip") || mime.Contains("tar") || mime.Contains("gzip")) return "📦";
        return "📄";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double value = bytes;
        while (value >= 1024 && i < units.Length - 1) { value /= 1024; i++; }
        return $"{value:0.##} {units[i]}";
    }
}
