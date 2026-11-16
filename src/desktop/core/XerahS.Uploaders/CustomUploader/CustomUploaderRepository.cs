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
using Newtonsoft.Json.Linq;
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
    /// Optional XerahS-specific metadata stored alongside the ShareX-compatible root fields.
    /// </summary>
    public CustomUploaderXerahSMetadata? Metadata { get; }

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

    public LoadedCustomUploader(CustomUploaderItem item, string filePath, CustomUploaderXerahSMetadata? metadata = null)
    {
        Item = item;
        Metadata = metadata;
        FilePath = filePath;
        FileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        LoadError = null;
    }

    public LoadedCustomUploader(string filePath, string error)
    {
        Item = CustomUploaderItem.Init();
        Metadata = null;
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
    private static readonly Lock _lock = new();
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
        var normalizedDirectory = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var scannedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var extension in SupportedExtensions)
        {
            try
            {
                var files = Directory.GetFiles(directory, "*" + extension, searchOption);
                foreach (var file in files)
                {
                    string normalizedFilePath = Path.GetFullPath(file);
                    scannedFiles.Add(normalizedFilePath);

                    var loaded = LoadFromFile(normalizedFilePath);
                    results.Add(loaded);

                    lock (_lock)
                    {
                        if (loaded.IsValid)
                        {
                            _loadedUploaders[normalizedFilePath] = loaded;
                        }
                        else
                        {
                            _loadedUploaders.Remove(normalizedFilePath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"[CustomUploader] Error scanning for {extension} files: {ex.Message}");
            }
        }

        lock (_lock)
        {
            var stalePaths = _loadedUploaders.Keys
                .Where(path => IsPathWithinDiscoveryScope(path, normalizedDirectory, recursive) && !scannedFiles.Contains(path))
                .ToList();

            foreach (var stalePath in stalePaths)
            {
                _loadedUploaders.Remove(stalePath);
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
        string normalizedFilePath = NormalizePath(filePath);

        try
        {
            if (!File.Exists(normalizedFilePath))
            {
                return new LoadedCustomUploader(normalizedFilePath, "File not found");
            }

            string json = File.ReadAllText(normalizedFilePath);
            return LoadFromJson(json, normalizedFilePath);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[CustomUploader] Error reading file {normalizedFilePath}: {ex.Message}");
            return new LoadedCustomUploader(normalizedFilePath, $"Error reading file: {ex.Message}");
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
            var root = JObject.Parse(json);
            var metadata = ExtractMetadata(root);
            var item = root.ToObject<CustomUploaderItem>();

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

            return new LoadedCustomUploader(item, sourcePath, metadata);
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
        return SaveToFile(item, filePath, metadata: null);
    }

    /// <summary>
    /// Saves a custom uploader to a file with optional XerahS-specific metadata.
    /// </summary>
    /// <param name="item">The custom uploader item to save.</param>
    /// <param name="filePath">Destination file path.</param>
    /// <param name="metadata">Optional metadata persisted under the root XerahS object.</param>
    /// <returns>True if saved successfully.</returns>
    public static bool SaveToFile(CustomUploaderItem item, string filePath, CustomUploaderXerahSMetadata? metadata)
    {
        try
        {
            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            });
            var root = JObject.FromObject(item, serializer);
            var normalizedMetadata = metadata?.Normalize();

            if (normalizedMetadata?.HasBindings == true)
            {
                root["XerahS"] = JObject.FromObject(normalizedMetadata, serializer);
            }

            string normalizedFilePath = Path.GetFullPath(filePath);
            string? directory = Path.GetDirectoryName(normalizedFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(normalizedFilePath, root.ToString(Formatting.Indented));
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
        string normalizedFilePath = NormalizePath(filePath);
        var loaded = LoadFromFile(normalizedFilePath);

        lock (_lock)
        {
            if (loaded.IsValid)
            {
                _loadedUploaders[normalizedFilePath] = loaded;
            }
            else
            {
                _loadedUploaders.Remove(normalizedFilePath);
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
        string normalizedFilePath = NormalizePath(filePath);

        lock (_lock)
        {
            _loadedUploaders.Remove(normalizedFilePath);
        }
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsPathWithinDiscoveryScope(string filePath, string normalizedDirectory, bool recursive)
    {
        string normalizedFilePath = Path.GetFullPath(filePath);

        if (recursive)
        {
            return normalizedFilePath.StartsWith(normalizedDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalizedFilePath, normalizedDirectory, StringComparison.OrdinalIgnoreCase);
        }

        string? parentDirectory = Path.GetDirectoryName(normalizedFilePath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(parentDirectory, normalizedDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static CustomUploaderXerahSMetadata? ExtractMetadata(JObject root)
    {
        var metadataProperty = root.Properties()
            .FirstOrDefault(property => string.Equals(property.Name, "XerahS", StringComparison.OrdinalIgnoreCase));

        if (metadataProperty == null)
        {
            return null;
        }

        try
        {
            var metadata = metadataProperty.Value.ToObject<CustomUploaderXerahSMetadata>()?.Normalize();
            metadataProperty.Remove();
            return metadata?.HasBindings == true ? metadata : null;
        }
        catch
        {
            metadataProperty.Remove();
            return null;
        }
    }
}
