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

namespace XerahS.UI.Services.SettingsSearch;

/// <summary>
/// Hand-maintained navigation aliases and area/tab guide entries.
/// Visual-tree indexing covers checkbox labels; this catalog covers intent keywords.
/// </summary>
public static class SettingsSearchCatalog
{
    public const string AppNavigationTag = "Settings_App";
    public const string DestNavigationTag = "Settings_Dest";

    public static IReadOnlyList<SettingsSearchEntry> CreateEntries()
    {
        return
        [
            App("app-root", "Application Settings", null, ["app", "application", "preferences", "options"]),
            App("app-general", "General", "General", ["theme", "dark", "light", "updates", "tray", "taskbar"]),
            App("app-paths", "Paths", "Paths", ["screenshots", "folder", "subfolder", "save path"]),
            App("app-watch", "Watch Folders", "Watch Folders", ["watch", "daemon", "monitor folder", "mov", "mp4"]),
            App("app-integration", "Integration", "Integration", ["startup", "clipboard", "file association", "mcp", "palette"]),
            App("app-assistant", "Assistant", "Assistant", ["assistant", "ai", "api key", "provider", "openai", "anthropic", "gemini", "ocr", "privacy", "overlay"]),
            App("app-history", "History", "History", ["recent", "ocr", "tasks"]),
            App("app-proxy", "Proxy", "Proxy", ["proxy", "network", "http", "socks"]),
            App("app-advanced", "Advanced", "Advanced", ["capture", "engine", "linux", "macos", "wayland", "hotkey"]),

            Dest("dest-root", "Destination Settings", null, ["upload", "destination", "uploader", "provider", "plugin"]),
            Dest("dest-image", "Image Uploaders", "Image Uploaders", ["image", "imgur", "immich", "screenshot upload"]),
            Dest("dest-text", "Text Uploaders", "Text Uploaders", ["text", "paste", "gist"]),
            Dest("dest-file", "File Uploaders", "File Uploaders", ["file", "ftp", "s3", "dropbox", "nextcloud"]),
            Dest("dest-url", "URL Shorteners", "URL Shorteners", ["url", "shortener", "short link"])
        ];
    }

    private static SettingsSearchEntry App(string id, string title, string? tab, string[] keywords)
    {
        string path = tab == null
            ? "Application Settings"
            : $"Application Settings → {tab}";

        return new SettingsSearchEntry(
            id,
            title,
            SettingsSearchArea.Application,
            path,
            AppNavigationTag,
            SettingsSearchSource.Catalog,
            appTab: tab,
            keywords: keywords);
    }

    private static SettingsSearchEntry Dest(string id, string title, string? category, string[] keywords)
    {
        string path = category == null
            ? "Destination Settings"
            : $"Destination Settings → {category}";

        return new SettingsSearchEntry(
            id,
            title,
            SettingsSearchArea.Destination,
            path,
            DestNavigationTag,
            SettingsSearchSource.Catalog,
            destinationCategory: category,
            keywords: keywords);
    }
}
