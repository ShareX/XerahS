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
/// Plugin manifest model (deserialized from plugin.json)
/// </summary>
public class PluginManifest
{
    /// <summary>
    /// Unique plugin identifier (e.g., "imgur", "amazons3")
    /// Must match IUploaderProvider.ProviderId
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable plugin name (e.g., "Imgur Uploader")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Plugin version (e.g., "1.0.0")
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Plugin author/organization
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Brief description of the plugin
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Plugin API version this plugin was built against (e.g., "1.0")
    /// Used for compatibility checking
    /// </summary>
    public string ApiVersion { get; set; } = "1.0";

    /// <summary>
    /// Fully-qualified type name of the provider class
    /// (e.g., "ShareX.Imgur.Plugin.ImgurProvider")
    /// </summary>
    public string EntryPoint { get; set; } = string.Empty;

    /// <summary>
    /// Optional: Assembly file name if different from plugin ID
    /// (e.g., "ShareX.Imgur.Plugin.dll")
    /// If empty, assumes "{PluginId}.dll"
    /// </summary>
    public string? AssemblyFileName { get; set; }

    /// <summary>
    /// List of uploader categories this plugin supports
    /// </summary>
    public List<string> SupportedCategories { get; set; } = new();

    /// <summary>
    /// Optional: Config view identifier for UI mapping
    /// </summary>
    public string? ConfigViewId { get; set; }

    /// <summary>
    /// Optional: Other plugins this plugin depends on
    /// </summary>
    public List<string> Dependencies { get; set; } = new();

    /// <summary>
    /// Optional: Plugin homepage URL
    /// </summary>
    public string? HomepageUrl { get; set; }

    /// <summary>
    /// Validate manifest has required fields
    /// </summary>
    public bool IsValid(out string? error)
    {
        if (string.IsNullOrWhiteSpace(PluginId))
        {
            error = "PluginId is required";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            error = "Name is required";
            return false;
        }

        if (string.IsNullOrWhiteSpace(EntryPoint))
        {
            error = "EntryPoint is required";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ApiVersion))
        {
            error = "ApiVersion is required";
            return false;
        }

        if (!SupportedCategories.Any())
        {
            error = "At least one SupportedCategory is required";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Check if this plugin is compatible with the current API version
    /// </summary>
    public bool IsCompatibleWith(string currentApiVersion)
    {
        // Simple major version check (1.x.x compatible with 1.y.y)
        var pluginMajor = GetMajorVersion(ApiVersion);
        var currentMajor = GetMajorVersion(currentApiVersion);

        return pluginMajor == currentMajor;
    }

    private static int GetMajorVersion(string version)
    {
        var parts = version.Split('.');
        return parts.Length > 0 && int.TryParse(parts[0], out var major) ? major : 0;
    }

    /// <summary>
    /// Get assembly file name (from explicit value or derived from plugin ID)
    /// </summary>
    public string GetAssemblyFileName()
    {
        return string.IsNullOrWhiteSpace(AssemblyFileName)
            ? $"{PluginId}.dll"
            : AssemblyFileName;
    }
}
