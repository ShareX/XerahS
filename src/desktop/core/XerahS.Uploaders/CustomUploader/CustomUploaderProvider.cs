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

using Newtonsoft.Json;
using XerahS.Common;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Uploaders.CustomUploader;

/// <summary>
/// Adapter that exposes a CustomUploaderItem as an IUploaderProvider.
/// Each .sxcu file becomes a separate provider in the catalog.
/// </summary>
public class CustomUploaderProvider : IUploaderProvider
{
    private readonly CustomUploaderItem _item;
    private readonly string _filePath;
    private readonly string _providerId;

    /// <summary>
    /// Creates a new CustomUploaderProvider from a loaded custom uploader.
    /// </summary>
    /// <param name="loadedUploader">The loaded custom uploader from repository.</param>
    public CustomUploaderProvider(LoadedCustomUploader loadedUploader)
        : this(loadedUploader.Item, loadedUploader.FilePath)
    {
    }

    /// <summary>
    /// Creates a new CustomUploaderProvider from a CustomUploaderItem.
    /// </summary>
    /// <param name="item">The custom uploader configuration.</param>
    /// <param name="filePath">Source file path (used for ID generation).</param>
    public CustomUploaderProvider(CustomUploaderItem item, string filePath)
    {
        _item = item ?? throw new ArgumentNullException(nameof(item));
        _filePath = filePath;

        // Generate a stable provider ID based on name and file
        _providerId = GenerateProviderId(item, filePath);
    }

    /// <summary>
    /// Generates a stable, unique provider ID for the custom uploader.
    /// </summary>
    private static string GenerateProviderId(CustomUploaderItem item, string filePath)
    {
        // Use file name without extension as base, or item name if no file
        string baseName = !string.IsNullOrEmpty(filePath)
            ? Path.GetFileNameWithoutExtension(filePath)
            : item.Name;

        // Slugify the name: lowercase, replace spaces/special chars with underscores
        string slug = Slugify(baseName);

        return $"custom_{slug}";
    }

    /// <summary>
    /// Converts a string to a URL-safe slug.
    /// </summary>
    private static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "unknown";

        // Convert to lowercase
        string result = input.ToLowerInvariant();

        // Replace spaces and special characters with underscores
        result = System.Text.RegularExpressions.Regex.Replace(result, @"[^a-z0-9]+", "_");

        // Remove leading/trailing underscores
        result = result.Trim('_');

        // Ensure not empty
        return string.IsNullOrEmpty(result) ? "unknown" : result;
    }

    /// <summary>
    /// Unique provider identifier based on the custom uploader name and file.
    /// </summary>
    public string ProviderId => _providerId;

    /// <summary>
    /// Display name from the CustomUploaderItem.
    /// </summary>
    public string Name => !string.IsNullOrEmpty(_item.Name) ? _item.Name : Path.GetFileNameWithoutExtension(_filePath);

    /// <summary>
    /// Description generated from the request configuration.
    /// </summary>
    public string Description
    {
        get
        {
            var host = URLHelpers.GetHostName(_item.RequestURL);
            return $"Custom uploader for {_item.RequestMethod} {host}";
        }
    }

    /// <summary>
    /// Version from the CustomUploaderItem, or 1.0.0 as default.
    /// </summary>
    public Version Version
    {
        get
        {
            if (!string.IsNullOrEmpty(_item.Version) && Version.TryParse(_item.Version, out var version))
            {
                return version;
            }
            return new Version(1, 0, 0);
        }
    }

    /// <summary>
    /// Categories supported by this custom uploader, derived from DestinationType.
    /// </summary>
    public UploaderCategory[] SupportedCategories => ConvertDestinationType(_item.DestinationType);

    /// <summary>
    /// The configuration model type is CustomUploaderItem itself.
    /// </summary>
    public Type ConfigModelType => typeof(CustomUploaderItem);

    /// <summary>
    /// Source file path for this custom uploader.
    /// </summary>
    public string FilePath => _filePath;

    /// <summary>
    /// The underlying CustomUploaderItem configuration.
    /// </summary>
    public CustomUploaderItem Item => _item;

    /// <inheritdoc/>
    public object? CreateConfigView()
    {
        // Custom uploaders use a generic config view (or null for property grid)
        // TODO: In Phase 3, implement CustomUploaderEditorView
        return null;
    }

    /// <inheritdoc/>
    public IUploaderConfigViewModel? CreateConfigViewModel()
    {
        // Custom uploaders don't need a custom ViewModel
        return null;
    }

    /// <inheritdoc/>
    public Uploader CreateInstance(string settingsJson)
    {
        // For custom uploaders, we use the item directly
        // The settingsJson could override specific values if needed
        CustomUploaderItem effectiveItem = _item;

        if (!string.IsNullOrWhiteSpace(settingsJson))
        {
            try
            {
                // Allow overriding the item configuration
                var overrides = JsonConvert.DeserializeObject<CustomUploaderItem>(settingsJson);
                if (overrides != null)
                {
                    effectiveItem = overrides;
                }
            }
            catch (JsonException)
            {
                // Use original item if JSON is invalid
                DebugHelper.WriteLine($"[CustomUploaderProvider] Invalid settings JSON, using original item");
            }
        }

        return new CustomUploaderExecutor(effectiveItem);
    }

    /// <inheritdoc/>
    public Dictionary<UploaderCategory, string[]> GetSupportedFileTypes()
    {
        var result = new Dictionary<UploaderCategory, string[]>();
        var categories = SupportedCategories;

        // Custom uploaders typically support all common file types
        if (categories.Contains(UploaderCategory.Image))
        {
            result[UploaderCategory.Image] = new[] { "png", "jpg", "jpeg", "gif", "bmp", "webp", "tiff", "ico" };
        }

        if (categories.Contains(UploaderCategory.Text))
        {
            result[UploaderCategory.Text] = new[] { "txt", "log", "cs", "js", "ts", "py", "json", "xml", "html", "css", "md" };
        }

        if (categories.Contains(UploaderCategory.File))
        {
            result[UploaderCategory.File] = new[] { "*" }; // All files
        }

        if (categories.Contains(UploaderCategory.UrlShortener))
        {
            result[UploaderCategory.UrlShortener] = Array.Empty<string>();
        }

        if (categories.Contains(UploaderCategory.UrlSharing))
        {
            result[UploaderCategory.UrlSharing] = Array.Empty<string>();
        }

        return result;
    }

    /// <inheritdoc/>
    public bool ValidateSettings(string settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            // Empty settings are valid - we use the default item
            return true;
        }

        try
        {
            var item = JsonConvert.DeserializeObject<CustomUploaderItem>(settingsJson);
            return item != null && !string.IsNullOrWhiteSpace(item.RequestURL);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public string GetDefaultSettings(UploaderCategory category)
    {
        // Return the current item as the default settings
        return JsonConvert.SerializeObject(_item, Formatting.Indented, new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        });
    }

    /// <inheritdoc/>
    public event EventHandler? ConfigChanged;

    /// <summary>
    /// Raises the ConfigChanged event.
    /// </summary>
    protected virtual void OnConfigChanged()
    {
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Converts CustomUploaderDestinationType flags to UploaderCategory array.
    /// </summary>
    /// <param name="destinationType">The destination type flags.</param>
    /// <returns>Array of supported uploader categories.</returns>
    public static UploaderCategory[] ConvertDestinationType(CustomUploaderDestinationType destinationType)
    {
        var categories = new List<UploaderCategory>();

        if (destinationType.HasFlag(CustomUploaderDestinationType.ImageUploader))
        {
            categories.Add(UploaderCategory.Image);
        }

        if (destinationType.HasFlag(CustomUploaderDestinationType.TextUploader))
        {
            categories.Add(UploaderCategory.Text);
        }

        if (destinationType.HasFlag(CustomUploaderDestinationType.FileUploader))
        {
            categories.Add(UploaderCategory.File);
        }

        if (destinationType.HasFlag(CustomUploaderDestinationType.URLShortener))
        {
            categories.Add(UploaderCategory.UrlShortener);
        }

        if (destinationType.HasFlag(CustomUploaderDestinationType.URLSharingService))
        {
            categories.Add(UploaderCategory.UrlSharing);
        }

        // Default to File if nothing specified
        if (categories.Count == 0)
        {
            categories.Add(UploaderCategory.File);
        }

        return categories.ToArray();
    }
}
