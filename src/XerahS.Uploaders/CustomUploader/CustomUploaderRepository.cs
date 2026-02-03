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

namespace XerahS.Uploaders.CustomUploader;

/// <summary>
/// Represents a loaded custom uploader with its source file information.
/// </summary>
public class LoadedCustomUploader
{
    /// <summary>
    /// The parsed CustomUploaderItem configuration.
    /// </summary>
    public CustomUploaderItem Item { get; }

    /// <summary>
    /// Full path to the source .sxcu or .json file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// File name without extension, used for generating stable IDs.
    /// </summary>
    public string FileNameWithoutExtension { get; }

    /// <summary>
    /// Any error that occurred during loading (null if successful).
    /// </summary>
    public string? LoadError { get; }

    /// <summary>
    /// Whether the uploader was loaded successfully.
    /// </summary>
    public bool IsValid => LoadError == null;

    public LoadedCustomUploader(CustomUploaderItem item, string filePath)
    {
        Item = item;
        FilePath = filePath;
        FileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        LoadError = null;
    }

    public LoadedCustomUploader(string filePath, string error)
    {
        Item = CustomUploaderItem.Init();
        FilePath = filePath;
        FileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        LoadError = error;
    }
}

/// <summary>
/// Repository for discovering, loading, validating, and saving custom uploader definitions (.sxcu files).
/// </summary>
public static class CustomUploaderRepository
{
    private static readonly string[] SupportedExtensions = { ".sxcu", ".json" };
    private static readonly object _lock = new();
    private static readonly Dictionary<string, LoadedCustomUploader> _loadedUploaders = new();

    /// <summary>
    /// Discovers and loads all custom uploaders from the specified directory.
    /// </summary>
    /// <param name="directory">Directory to scan for .sxcu and .json files.</param>
    /// <param name="recursive">Whether to scan subdirectories.</param>
    /// <returns>List of loaded custom uploaders (both valid and invalid).</returns>
    public static List<LoadedCustomUploader> DiscoverUploaders(string directory, bool recursive = false)
    {
        var results = new List<LoadedCustomUploader>();

        if (!Directory.Exists(directory))
        {
            DebugHelper.WriteLine($"[CustomUploader] Directory does not exist: {directory}");
            return results;
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        foreach (var extension in SupportedExtensions)
        {
            try
            {
                var files = Directory.GetFiles(directory, "*" + extension, searchOption);
                foreach (var file in files)
                {
                    var loaded = LoadFromFile(file);
                    results.Add(loaded);

                    if (loaded.IsValid)
                    {
                        lock (_lock)
                        {
                            _loadedUploaders[file] = loaded;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"[CustomUploader] Error scanning for {extension} files: {ex.Message}");
            }
        }

        DebugHelper.WriteLine($"[CustomUploader] Discovered {results.Count(u => u.IsValid)} valid uploader(s) from {directory}");
        return results;
    }

    /// <summary>
    /// Loads a custom uploader from a file.
    /// </summary>
    /// <param name="filePath">Path to the .sxcu or .json file.</param>
    /// <returns>Loaded custom uploader (check IsValid for success).</returns>
    public static LoadedCustomUploader LoadFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new LoadedCustomUploader(filePath, "File not found");
            }

            string json = File.ReadAllText(filePath);
            return LoadFromJson(json, filePath);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[CustomUploader] Error reading file {filePath}: {ex.Message}");
            return new LoadedCustomUploader(filePath, $"Error reading file: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads a custom uploader from JSON content.
    /// </summary>
    /// <param name="json">JSON content of the custom uploader.</param>
    /// <param name="sourcePath">Optional source file path for reference.</param>
    /// <returns>Loaded custom uploader (check IsValid for success).</returns>
    public static LoadedCustomUploader LoadFromJson(string json, string sourcePath = "")
    {
        try
        {
            var item = JsonConvert.DeserializeObject<CustomUploaderItem>(json);

            if (item == null)
            {
                return new LoadedCustomUploader(sourcePath, "Failed to deserialize JSON");
            }

            // Validate minimum requirements
            var validationError = ValidateItem(item);
            if (validationError != null)
            {
                return new LoadedCustomUploader(sourcePath, validationError);
            }

            // Run backward compatibility checks
            try
            {
                item.CheckBackwardCompatibility();
            }
            catch (Exception ex)
            {
                return new LoadedCustomUploader(sourcePath, $"Compatibility check failed: {ex.Message}");
            }

            return new LoadedCustomUploader(item, sourcePath);
        }
        catch (JsonException ex)
        {
            return new LoadedCustomUploader(sourcePath, $"Invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new LoadedCustomUploader(sourcePath, $"Error loading: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates a CustomUploaderItem for minimum requirements.
    /// </summary>
    /// <param name="item">Item to validate.</param>
    /// <returns>Error message if invalid, null if valid.</returns>
    public static string? ValidateItem(CustomUploaderItem item)
    {
        if (string.IsNullOrWhiteSpace(item.RequestURL))
        {
            return "RequestURL is required";
        }

        if (!Uri.TryCreate(item.RequestURL, UriKind.Absolute, out var uri))
        {
            // Allow URLs with syntax placeholders like {input}
            if (!item.RequestURL.Contains("{"))
            {
                return "RequestURL is not a valid URL";
            }
        }

        if (item.DestinationType == CustomUploaderDestinationType.None)
        {
            // Default to FileUploader if not specified
            item.DestinationType = CustomUploaderDestinationType.FileUploader;
        }

        return null;
    }

    /// <summary>
    /// Saves a custom uploader to a file.
    /// </summary>
    /// <param name="item">The custom uploader item to save.</param>
    /// <param name="filePath">Destination file path.</param>
    /// <returns>True if saved successfully.</returns>
    public static bool SaveToFile(CustomUploaderItem item, string filePath)
    {
        try
        {
            var json = JsonConvert.SerializeObject(item, Formatting.Indented, new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            });

            File.WriteAllText(filePath, json);
            DebugHelper.WriteLine($"[CustomUploader] Saved uploader to: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[CustomUploader] Error saving to {filePath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets all currently loaded custom uploaders.
    /// </summary>
    /// <returns>List of valid loaded uploaders.</returns>
    public static List<LoadedCustomUploader> GetLoadedUploaders()
    {
        lock (_lock)
        {
            return _loadedUploaders.Values.Where(u => u.IsValid).ToList();
        }
    }

    /// <summary>
    /// Clears the repository cache (used for testing or hot-reload).
    /// </summary>
    public static void Clear()
    {
        lock (_lock)
        {
            _loadedUploaders.Clear();
        }
    }

    /// <summary>
    /// Reloads uploaders from a specific file (for hot-reload support).
    /// </summary>
    /// <param name="filePath">Path to the file to reload.</param>
    /// <returns>The reloaded uploader, or null if reload failed.</returns>
    public static LoadedCustomUploader? ReloadFile(string filePath)
    {
        var loaded = LoadFromFile(filePath);

        lock (_lock)
        {
            if (loaded.IsValid)
            {
                _loadedUploaders[filePath] = loaded;
            }
            else
            {
                _loadedUploaders.Remove(filePath);
            }
        }

        return loaded.IsValid ? loaded : null;
    }

    /// <summary>
    /// Removes an uploader from the cache (e.g., when file is deleted).
    /// </summary>
    /// <param name="filePath">Path to the file to remove.</param>
    public static void RemoveFile(string filePath)
    {
        lock (_lock)
        {
            _loadedUploaders.Remove(filePath);
        }
    }
}
