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

using XerahS.UI.Services.SettingsSearch;

namespace XerahS.UI.Helpers;

/// <summary>
/// Extra search aliases for main-nav nodes (tools + settings catalog keywords).
/// </summary>
internal static class NavigationSearchKeywords
{
    private static readonly Dictionary<string, string> ToolAliases = new(StringComparer.Ordinal)
    {
        ["Tools_ColorPicker"] = "color picker colour eyedropper",
        ["Tools_Ruler"] = "ruler measure measurement",
        ["Tools_IndexFolder"] = "index folder directory listing",
        ["Tools_QrGenerator"] = "qr barcode qrcode generate",
        ["Tools_QrScanScreen"] = "qr barcode qrcode scan screen decode",
        ["Tools_QrScanRegion"] = "qr barcode qrcode scan region decode",
        ["Tools_ImageCombiner"] = "combine stitch merge collage",
        ["Tools_ImageSplitter"] = "split crop tiles",
        ["Tools_ImageThumbnailer"] = "thumbnail thumbs",
        ["Tools_ImageResizer"] = "image resize scale dimensions batch",
        ["Tools_ImageConverter"] = "image convert format png jpeg webp batch",
        ["Tools_AnimatedGifMaker"] = "gif animated animation frames images slideshow",
        ["Tools_ImageWatermark"] = "image watermark text logo stamp batch",
        ["Tools_VideoEditor"] = "video edit ffmpeg",
        ["Tools_VideoConverter"] = "video convert ffmpeg transcode",
        ["Tools_VideoTrimmer"] = "video trim cut clip shorten ffmpeg",
        ["Tools_VideoThumbnailer"] = "video thumbnail thumbs",
        ["Tools_AnalyzeImage"] = "analyze analyse metadata exif",
        ["Tools_MonitorTest"] = "monitor display test pattern",
        ["Tools_NetworkMonitor"] = "network monitor internet disconnect connect ping latency uptime",
        ["Upload_FileUpload"] = "upload file",
        ["Upload_ClipboardUploadWithContentViewer"] = "upload clipboard content paste",
        ["Recording"] = "record screen video capture",
        ["Editor"] = "image editor annotate",
        ["History"] = "history recent tasks",
        ["Workflows"] = "workflow hotkey automation",
        ["Debug"] = "debug log diagnostics display monitor scaling dpi",
        ["About"] = "about version license"
    };

    public static string? ForTag(string? tag)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return null;
        }

        List<string> parts = [];

        if (ToolAliases.TryGetValue(tag, out string? aliases))
        {
            parts.Add(aliases);
        }

        if (string.Equals(tag, SettingsSearchCatalog.AppNavigationTag, StringComparison.Ordinal) ||
            string.Equals(tag, "Settings", StringComparison.Ordinal))
        {
            parts.Add(BuildCatalogBlob(SettingsSearchArea.Application));
        }

        if (string.Equals(tag, SettingsSearchCatalog.DestNavigationTag, StringComparison.Ordinal) ||
            string.Equals(tag, "Settings", StringComparison.Ordinal))
        {
            parts.Add(BuildCatalogBlob(SettingsSearchArea.Destination));
        }

        return parts.Count == 0 ? null : string.Join(' ', parts);
    }

    private static string BuildCatalogBlob(SettingsSearchArea area)
    {
        IEnumerable<string> tokens = SettingsSearchCatalog.CreateEntries()
            .Where(entry => entry.Area == area)
            .SelectMany(entry => entry.Keywords.Prepend(entry.Title));

        return string.Join(' ', tokens);
    }
}
