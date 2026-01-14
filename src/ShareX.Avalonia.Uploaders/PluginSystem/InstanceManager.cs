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

using Newtonsoft.Json;
using System.Text;
using System.Security.Cryptography;

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Manages uploader instances - lifecycle, persistence, default selection
/// </summary>
public class InstanceManager
{
    private static readonly Lazy<InstanceManager> _instance = new(() => new InstanceManager());
    public static InstanceManager Instance => _instance.Value;

    private readonly object _lock = new();
    private InstanceConfiguration _configuration;
    private readonly string _configFilePath;

    private InstanceManager()
    {
        // TODO: Get proper config path from app settings
        var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShareX.Ava");
        Directory.CreateDirectory(configDir);
        _configFilePath = Path.Combine(configDir, "uploader-instances.json");

        _configuration = LoadConfiguration();
    }

    /// <summary>
    /// Get all configured uploader instances
    /// </summary>
    public List<UploaderInstance> GetInstances()
    {
        lock (_lock)
        {
            return new List<UploaderInstance>(_configuration.Instances);
        }
    }

    /// <summary>
    /// Get instances for a specific category
    /// </summary>
    public List<UploaderInstance> GetInstancesByCategory(UploaderCategory category)
    {
        lock (_lock)
        {
            return _configuration.Instances
                .Where(i => i.Category == category)
                .ToList();
        }
    }

    /// <summary>
    /// Get an instance by its ID
    /// </summary>
    public UploaderInstance? GetInstance(string instanceId)
    {
        lock (_lock)
        {
            return _configuration.Instances.FirstOrDefault(i => i.InstanceId == instanceId);
        }
    }

    /// <summary>
    /// Add a new uploader instance
    /// </summary>
    public void AddInstance(UploaderInstance instance)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(instance.InstanceId))
            {
                instance.CreatedAt = DateTime.UtcNow;
                instance.InstanceId = GenerateInstanceId(instance.ProviderId, instance.DisplayName, instance.CreatedAt);
            }

            if (_configuration.Instances.Any(i => i.InstanceId == instance.InstanceId))
            {
                throw new InvalidOperationException($"Instance with ID {instance.InstanceId} already exists");
            }

            instance.CreatedAt = DateTime.UtcNow;
            instance.ModifiedAt = DateTime.UtcNow;
            _configuration.Instances.Add(instance);
            SaveConfiguration();
        }
    }

    /// <summary>
    /// Update an existing instance
    /// </summary>
    public void UpdateInstance(UploaderInstance instance)
    {
        lock (_lock)
        {
            var existing = _configuration.Instances.FirstOrDefault(i => i.InstanceId == instance.InstanceId);
            if (existing == null)
            {
                throw new InvalidOperationException($"Instance with ID {instance.InstanceId} not found");
            }

            var index = _configuration.Instances.IndexOf(existing);
            instance.ModifiedAt = DateTime.UtcNow;
            _configuration.Instances[index] = instance;
            SaveConfiguration();
        }
    }

    /// <summary>
    /// Remove an instance
    /// </summary>
    public void RemoveInstance(string instanceId)
    {
        lock (_lock)
        {
            var instance = _configuration.Instances.FirstOrDefault(i => i.InstanceId == instanceId);
            if (instance != null)
            {
                _configuration.Instances.Remove(instance);

                // Remove from defaults if it was set
                var defaultsToRemove = _configuration.DefaultInstances
                    .Where(kvp => kvp.Value == instanceId)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var category in defaultsToRemove)
                {
                    _configuration.DefaultInstances.Remove(category);
                }

                SaveConfiguration();
            }
        }
    }

    /// <summary>
    /// Duplicate an instance with new ID and optional display name
    /// </summary>
    public UploaderInstance DuplicateInstance(string sourceInstanceId, string? newDisplayName = null)
    {
        lock (_lock)
        {
            var source = _configuration.Instances.FirstOrDefault(i => i.InstanceId == sourceInstanceId);
            if (source == null)
            {
                throw new InvalidOperationException($"Instance with ID {sourceInstanceId} not found");
            }

            var createdAt = DateTime.UtcNow;
            var duplicate = new UploaderInstance
            {
                InstanceId = GenerateInstanceId(source.ProviderId, newDisplayName ?? $"{source.DisplayName} (Copy)", createdAt),
                ProviderId = source.ProviderId,
                Category = source.Category,
                DisplayName = newDisplayName ?? $"{source.DisplayName} (Copy)",
                SettingsJson = source.SettingsJson,
                CreatedAt = createdAt,
                ModifiedAt = createdAt,
                IsAvailable = source.IsAvailable
            };

            _configuration.Instances.Add(duplicate);
            SaveConfiguration();

            return duplicate;
        }
    }

    /// <summary>
    /// Set the default instance for a category
    /// </summary>
    public void SetDefaultInstance(UploaderCategory category, string instanceId)
    {
        lock (_lock)
        {
            var instance = _configuration.Instances.FirstOrDefault(i => i.InstanceId == instanceId);
            if (instance == null)
            {
                throw new InvalidOperationException($"Instance with ID {instanceId} not found");
            }

            if (instance.Category != category)
            {
                throw new InvalidOperationException($"Instance category {instance.Category} does not match target category {category}");
            }

            _configuration.DefaultInstances[category] = instanceId;
            SaveConfiguration();
        }
    }

    /// <summary>
    /// Get the default instance for a category
    /// </summary>
    public UploaderInstance? GetDefaultInstance(UploaderCategory category)
    {
        lock (_lock)
        {
            if (_configuration.DefaultInstances.TryGetValue(category, out var instanceId))
            {
                return _configuration.Instances.FirstOrDefault(i => i.InstanceId == instanceId);
            }
            return null;
        }
    }

    #region File-Type Routing

    /// <summary>
    /// Get the destination instance for a specific file based on category and extension.
    /// Returns null if no match found.
    /// </summary>
    /// <param name="category">Upload category</param>
    /// <param name="fileExtension">File extension (with or without leading dot, case-insensitive)</param>
    public UploaderInstance? GetDestinationForFile(UploaderCategory category, string fileExtension)
    {
        lock (_lock)
        {
            // Normalize extension (remove leading dot, lowercase)
            var ext = fileExtension.TrimStart('.').ToLowerInvariant();

            var instances = GetInstancesByCategory(category);

            // 1. Try exact file extension match first
            var exactMatch = instances.FirstOrDefault(i =>
                !i.FileTypeRouting.AllFileTypes &&
                i.FileTypeRouting.FileExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)));

            if (exactMatch != null)
                return exactMatch;

            // 2. Fallback to "All File Types" instance
            var allTypesMatch = instances.FirstOrDefault(i => i.FileTypeRouting.AllFileTypes);

            return allTypesMatch;
        }
    }

    /// <summary>
    /// Check if a specific file type can be added to an instance in a category.
    /// Returns false if type is already handled by another instance or if any instance has "All File Types".
    /// </summary>
    /// <param name="category">Upload category</param>
    /// <param name="excludeInstanceId">Instance ID to exclude from check (when editing existing instance)</param>
    /// <param name="fileExtension">File extension to check</param>
    public bool CanAddFileType(UploaderCategory category, string excludeInstanceId, string fileExtension)
    {
        lock (_lock)
        {
            var ext = fileExtension.TrimStart('.').ToLowerInvariant();

            var otherInstances = _configuration.Instances
                .Where(i => i.Category == category && i.InstanceId != excludeInstanceId);

            // Cannot add if any other instance has "All File Types"
            if (otherInstances.Any(i => i.FileTypeRouting.AllFileTypes))
                return false;

            // Cannot add if file type is already handled by another instance
            return !otherInstances.Any(i =>
                i.FileTypeRouting.FileExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)));
        }
    }

    /// <summary>
    /// Check if an instance can set "All File Types" for its category.
    /// Returns false if other instances exist in the same category.
    /// </summary>
    /// <param name="category">Upload category</param>
    /// <param name="currentInstanceId">Instance ID requesting "All File Types"</param>
    public bool CanSetAllFileTypes(UploaderCategory category, string currentInstanceId)
    {
        lock (_lock)
        {
            var otherInstances = _configuration.Instances
                .Where(i => i.Category == category && i.InstanceId != currentInstanceId);

            // Can only set "All File Types" if no other instances exist in this category
            return !otherInstances.Any();
        }
    }

    /// <summary>
    /// Get file types that are already handled by other instances in a category.
    /// Used for UI to show which types are unavailable.
    /// </summary>
    /// <param name="category">Upload category</param>
    /// <param name="excludeInstanceId">Instance ID to exclude from check</param>
    public Dictionary<string, string> GetBlockedFileTypes(UploaderCategory category, string excludeInstanceId)
    {
        lock (_lock)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var otherInstances = _configuration.Instances
                .Where(i => i.Category == category && i.InstanceId != excludeInstanceId);

            foreach (var instance in otherInstances)
            {
                if (instance.FileTypeRouting.AllFileTypes)
                {
                    result["*"] = instance.DisplayName;
                }
                else
                {
                    foreach (var ext in instance.FileTypeRouting.FileExtensions)
                    {
                        result[ext] = instance.DisplayName;
                    }
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Validate that an instance's file type configuration doesn't conflict with others.
    /// Returns error message if invalid, null if valid.
    /// </summary>
    public string? ValidateFileTypeConfiguration(UploaderInstance instance)
    {
        lock (_lock)
        {
            var otherInstances = _configuration.Instances
                .Where(i => i.Category == instance.Category && i.InstanceId != instance.InstanceId);

            if (instance.FileTypeRouting.AllFileTypes)
            {
                if (otherInstances.Any())
                {
                    return $"Cannot set 'All File Types' - {otherInstances.Count()} other instance(s) exist in {instance.Category} category";
                }
            }
            else
            {
                // Check for "All File Types" conflicts
                var allTypesInstance = otherInstances.FirstOrDefault(i => i.FileTypeRouting.AllFileTypes);
                if (allTypesInstance != null)
                {
                    return $"Cannot add file types - '{allTypesInstance.DisplayName}' handles all file types in {instance.Category}";
                }

                // Check for specific file type conflicts
                foreach (var ext in instance.FileTypeRouting.FileExtensions)
                {
                    var conflictingInstance = otherInstances.FirstOrDefault(i =>
                        i.FileTypeRouting.FileExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)));

                    if (conflictingInstance != null)
                    {
                        return $"File type '{ext}' is already handled by '{conflictingInstance.DisplayName}'";
                    }
                }
            }

            return null; // Valid
        }
    }

    #endregion

    private InstanceConfiguration LoadConfiguration()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
                return JsonConvert.DeserializeObject<InstanceConfiguration>(json) ?? new InstanceConfiguration();
            }
        }
        catch
        {
            // If loading fails, return empty configuration
        }

        return new InstanceConfiguration();
    }

    private void SaveConfiguration()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_configuration, Formatting.Indented);
            File.WriteAllText(_configFilePath, json);
        }
        catch
        {
            // TODO: Add proper logging
        }
    }

    private static string GenerateInstanceId(string providerId, string displayName, DateTime createdAtUtc)
    {
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        var input = $"{providerId}|{displayName}|{createdAtUtc:O}";
        var hash = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
