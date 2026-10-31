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
using System.Text;
using System.Security.Cryptography;
using XerahS.Common;

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

    internal const string ConfigFileName = "uploader-instances.json";
    internal const string ConfigLockFileName = "uploader-instances.lock";
    internal static readonly TimeSpan ConfigLockTimeout = TimeSpan.FromSeconds(5);
    private const int ConfigLockRetryDelayMs = 50;

    private InstanceManager()
    {
        Directory.CreateDirectory(PathsManager.SettingsFolder);
        _configuration = LoadConfiguration();
    }

    public void MigrateSecretsIfNeeded()
    {
        lock (_lock)
        {
            var context = ProviderCatalog.GetProviderContext();
            var secrets = context?.Secrets;
            if (secrets == null)
            {
                return;
            }

            bool updated = false;
            int migratedInstances = 0;
            int migratedSecrets = 0;

            foreach (var instance in _configuration.Instances)
            {
                if (string.IsNullOrWhiteSpace(instance.SettingsJson))
                {
                    continue;
                }

                var provider = ProviderCatalog.GetProvider(instance.ProviderId);
                if (provider is not IInstanceSecretMigrator migrator)
                {
                    continue;
                }

                if (migrator.TryMigrateSecrets(instance.SettingsJson, secrets, out string updatedJson, out int count))
                {
                    instance.SettingsJson = updatedJson;
                    updated = true;
                    migratedInstances++;
                    migratedSecrets += count;
                }
            }

            if (updated)
            {
                SaveConfiguration();
                DebugHelper.WriteLine($"[Secrets] Migration completed: {migratedInstances} instance(s), {migratedSecrets} secret(s).");
            }
        }
    }

    private static bool TrySetSecret(ISecretStore secrets, string providerId, string secretKey, string name, string value)
    {
        try
        {
            secrets.SetSecret(providerId, secretKey, name, value);
            return secrets.HasSecret(providerId, secretKey, name);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"[Secrets] Failed to set secret {providerId}:{name}");
            return false;
        }
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
    /// Serializes the complete destination instance configuration for portable backup.
    /// </summary>
    public string ExportConfigurationJson()
    {
        lock (_lock)
        {
            return JsonConvert.SerializeObject(_configuration, Formatting.Indented);
        }
    }

    /// <summary>
    /// Replaces the destination instance configuration and persists it atomically.
    /// </summary>
    public void ImportConfigurationJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        InstanceConfiguration configuration = JsonConvert.DeserializeObject<InstanceConfiguration>(json)
            ?? throw new InvalidDataException("Destination instance configuration is empty.");
        NormalizeConfiguration(configuration);

        lock (_lock)
        {
            _configuration = configuration;
            SaveConfiguration(throwOnError: true);
        }
    }

    /// <summary>
    /// Reloads destination instances after settings files have been restored.
    /// </summary>
    public void ReloadConfiguration()
    {
        lock (_lock)
        {
            _configuration = LoadConfiguration();
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
            return _configuration.Instances.FirstOrDefault(i => InstanceIdsEqual(i.InstanceId, instanceId));
        }
    }

    /// <summary>
    /// Add a new uploader instance
    /// </summary>
    public void AddInstance(UploaderInstance instance)
    {
        lock (_lock)
        {
            NormalizeInstance(instance);

            if (string.IsNullOrWhiteSpace(instance.InstanceId))
            {
                instance.CreatedAt = DateTime.UtcNow;
                instance.InstanceId = GenerateInstanceId(instance.ProviderId, instance.DisplayName, instance.CreatedAt);
            }

            if (_configuration.Instances.Any(i => InstanceIdsEqual(i.InstanceId, instance.InstanceId)))
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
            NormalizeInstance(instance);

            var existing = _configuration.Instances.FirstOrDefault(i => InstanceIdsEqual(i.InstanceId, instance.InstanceId));
            if (existing == null)
            {
                throw new InvalidOperationException($"Instance with ID {instance.InstanceId} not found");
            }

            // Remove stale default mapping when category changes
            if (existing.Category != instance.Category)
            {
                var staleDefaults = _configuration.DefaultInstances
                    .Where(kvp => kvp.Key == existing.Category && InstanceIdsEqual(kvp.Value, existing.InstanceId))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var category in staleDefaults)
                {
                    _configuration.DefaultInstances.Remove(category);
                    LogStaleDefaultRemoved(category, existing.InstanceId, $"category changed from {existing.Category} to {instance.Category}");
                }
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
            var instance = _configuration.Instances.FirstOrDefault(i => InstanceIdsEqual(i.InstanceId, instanceId));
            if (instance != null)
            {
                _configuration.Instances.Remove(instance);

                // Remove from defaults if it was set
                var defaultsToRemove = _configuration.DefaultInstances
                    .Where(kvp => InstanceIdsEqual(kvp.Value, instanceId))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var category in defaultsToRemove)
                {
                    _configuration.DefaultInstances.Remove(category);
                    LogStaleDefaultRemoved(category, instanceId, "instance was removed");
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
            var source = _configuration.Instances.FirstOrDefault(i => InstanceIdsEqual(i.InstanceId, sourceInstanceId));
            if (source == null)
            {
                throw new InvalidOperationException($"Instance with ID {sourceInstanceId} not found");
            }

            NormalizeInstance(source);

            var createdAt = DateTime.UtcNow;
            var duplicate = new UploaderInstance
            {
                InstanceId = GenerateInstanceId(source.ProviderId, newDisplayName ?? $"{source.DisplayName} (Copy)", createdAt),
                ProviderId = source.ProviderId,
                Category = source.Category,
                DisplayName = newDisplayName ?? $"{source.DisplayName} (Copy)",
                SettingsJson = source.SettingsJson,
                FileTypeRouting = new FileTypeScope
                {
                    AllFileTypes = source.FileTypeRouting.AllFileTypes,
                    TextFiles = source.FileTypeRouting.TextFiles,
                    TextFileExceptions = source.FileTypeRouting.TextFileExceptions?.ToList(),
                    FileExtensions = source.FileTypeRouting.FileExtensions.ToList()
                },
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
            var instance = _configuration.Instances.FirstOrDefault(i => InstanceIdsEqual(i.InstanceId, instanceId));
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
                var instance = _configuration.Instances.FirstOrDefault(i => InstanceIdsEqual(i.InstanceId, instanceId));

                // Verify the persisted default still resolves to a usable instance.
                if (instance == null || instance.Category != category || !instance.IsAvailable)
                {
                    _configuration.DefaultInstances.Remove(category);
                    LogStaleDefaultRemoved(category, instanceId, GetStaleDefaultReason(instance, category));
                    SaveConfiguration();
                    return null;
                }

                return instance;
            }
            return null;
        }
    }

    /// <summary>
    /// Check whether an instance ID is the current default for a category.
    /// This is a pure read and does NOT clean stale mappings (unlike GetDefaultInstance).
    /// </summary>
    public bool IsDefaultInstance(UploaderCategory category, string instanceId)
    {
        lock (_lock)
        {
            return _configuration.DefaultInstances.TryGetValue(category, out var defaultId) &&
                   InstanceIdsEqual(defaultId, instanceId);
        }
    }

    public static bool IsAutoProvider(string? providerId)
    {
        return string.Equals(providerId, ProviderIds.Auto, StringComparison.OrdinalIgnoreCase);
    }

    public UploaderInstance? ResolveAutoInstance(UploaderCategory category, string? autoInstanceId = null)
    {
        lock (_lock)
        {
            if (_configuration.DefaultInstances.TryGetValue(category, out var defaultId))
            {
                var defaultInstance = _configuration.Instances.FirstOrDefault(i =>
                    string.Equals(i.InstanceId, defaultId, StringComparison.OrdinalIgnoreCase));

                if (defaultInstance != null &&
                    defaultInstance.Category == category &&
                    defaultInstance.IsAvailable &&
                    !IsAutoProvider(defaultInstance.ProviderId) &&
                    !string.Equals(defaultInstance.InstanceId, autoInstanceId, StringComparison.OrdinalIgnoreCase))
                {
                    return defaultInstance;
                }
            }

            return _configuration.Instances.FirstOrDefault(i =>
                i.Category == category &&
                i.IsAvailable &&
                !IsAutoProvider(i.ProviderId) &&
                !string.Equals(i.InstanceId, autoInstanceId, StringComparison.OrdinalIgnoreCase));
        }
    }

    #region File-Type Routing

    /// <summary>
    /// Get the destination instance for a specific file based on category and extension.
    /// Precedence: an explicit extension, then "Text files", then the "All File Types" catch-all.
    /// Returns null if no match found.
    /// </summary>
    /// <param name="category">Upload category</param>
    /// <param name="fileExtension">File extension (with or without leading dot, case-insensitive)</param>
    public UploaderInstance? GetDestinationForFile(UploaderCategory category, string? fileExtension)
    {
        lock (_lock)
        {
            var ext = NormalizeFileExtension(fileExtension);
            if (ext == null)
            {
                return null;
            }

            return GetSpecificDestinationForFile(category, ext) ??
                GetInstancesByCategory(category).FirstOrDefault(i => i.IsAvailable && GetFileTypeRouting(i).AllFileTypes);
        }
    }

    /// <summary>
    /// Get the instance that claims this extension explicitly or through "Text files", ignoring the
    /// "All File Types" catch-all. Used to let a specialist take a file over from the default destination.
    /// </summary>
    public UploaderInstance? GetSpecificDestinationForFile(UploaderCategory category, string? fileExtension)
    {
        lock (_lock)
        {
            var ext = NormalizeFileExtension(fileExtension);
            if (ext == null)
            {
                return null;
            }

            var instances = GetInstancesByCategory(category)
                .Where(i => i.IsAvailable && !IsAutoProvider(i.ProviderId) && !GetFileTypeRouting(i).AllFileTypes)
                .ToList();

            return instances.FirstOrDefault(i =>
                    GetFileTypeRouting(i).FileExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase))) ??
                instances.FirstOrDefault(i => GetFileTypeRouting(i).ClaimsAsTextFile(ext));
        }
    }

    /// <summary>
    /// Check if a specific file type can be added to an instance in a category.
    /// Returns false if another instance already lists that extension. An "All File Types" instance
    /// does not block it: it stays the catch-all for everything else.
    /// </summary>
    /// <param name="category">Upload category</param>
    /// <param name="excludeInstanceId">Instance ID to exclude from check (when editing existing instance)</param>
    /// <param name="fileExtension">File extension to check</param>
    public bool CanAddFileType(UploaderCategory category, string excludeInstanceId, string? fileExtension)
    {
        lock (_lock)
        {
            var ext = NormalizeFileExtension(fileExtension);
            if (ext == null)
            {
                return false;
            }

            return !GetOtherRoutedInstances(category, excludeInstanceId).Any(i =>
                !GetFileTypeRouting(i).AllFileTypes &&
                GetFileTypeRouting(i).FileExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)));
        }
    }

    /// <summary>
    /// Check if an instance can set "All File Types" for its category.
    /// Returns false if another instance is already the catch-all.
    /// </summary>
    /// <param name="category">Upload category</param>
    /// <param name="currentInstanceId">Instance ID requesting "All File Types"</param>
    public bool CanSetAllFileTypes(UploaderCategory category, string currentInstanceId)
    {
        lock (_lock)
        {
            return !GetOtherRoutedInstances(category, currentInstanceId).Any(i => GetFileTypeRouting(i).AllFileTypes);
        }
    }

    /// <summary>
    /// Get file types that are already handled by other instances in a category.
    /// Used for UI to show which types are unavailable. The "All File Types" catch-all blocks nothing.
    /// </summary>
    /// <param name="category">Upload category</param>
    /// <param name="excludeInstanceId">Instance ID to exclude from check</param>
    public Dictionary<string, string> GetBlockedFileTypes(UploaderCategory category, string excludeInstanceId)
    {
        lock (_lock)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var instance in GetOtherRoutedInstances(category, excludeInstanceId))
            {
                var routing = GetFileTypeRouting(instance);
                if (routing.AllFileTypes)
                {
                    continue;
                }

                foreach (var ext in routing.FileExtensions)
                {
                    result[ext] = instance.DisplayName;
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
            var otherInstances = GetOtherRoutedInstances(instance.Category, instance.InstanceId).ToList();
            var instanceRouting = GetFileTypeRouting(instance);

            if (instanceRouting.AllFileTypes)
            {
                var otherCatchAll = otherInstances.FirstOrDefault(i => GetFileTypeRouting(i).AllFileTypes);
                return otherCatchAll == null
                    ? null
                    : $"Cannot set 'All File Types' - '{otherCatchAll.DisplayName}' already handles all other file types in {instance.Category}";
            }

            if (instanceRouting.TextFiles)
            {
                var otherTextFiles = otherInstances.FirstOrDefault(i =>
                    !GetFileTypeRouting(i).AllFileTypes && GetFileTypeRouting(i).TextFiles);
                if (otherTextFiles != null)
                {
                    return $"Text files are already handled by '{otherTextFiles.DisplayName}'";
                }
            }

            foreach (var ext in instanceRouting.FileExtensions)
            {
                var conflictingInstance = otherInstances.FirstOrDefault(i =>
                    !GetFileTypeRouting(i).AllFileTypes &&
                    GetFileTypeRouting(i).FileExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)));

                if (conflictingInstance != null)
                {
                    return $"File type '{ext}' is already handled by '{conflictingInstance.DisplayName}'";
                }
            }

            return null; // Valid
        }
    }

    private IEnumerable<UploaderInstance> GetOtherRoutedInstances(UploaderCategory category, string? excludeInstanceId) =>
        _configuration.Instances.Where(i =>
            i.Category == category && i.IsAvailable && !IsAutoProvider(i.ProviderId) &&
            !InstanceIdsEqual(i.InstanceId, excludeInstanceId));

    #endregion

    internal static string ConfigFilePath =>
        Path.Combine(PathsManager.SettingsFolder, ConfigFileName);

    internal static string ConfigLockFilePath =>
        Path.Combine(PathsManager.SettingsFolder, ConfigLockFileName);

    /// <summary>
    /// Runs <paramref name="action"/> while holding the cross-process exclusive lock
    /// on <see cref="ConfigLockFilePath"/>. Intended for tests and for load/save.
    /// </summary>
    internal static void WithConfigLock(Action action, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        using (AcquireConfigLock(timeout))
        {
            action();
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/> while holding the cross-process exclusive lock
    /// on <see cref="ConfigLockFilePath"/>.
    /// </summary>
    internal static T WithConfigLock<T>(Func<T> action, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        using (AcquireConfigLock(timeout))
        {
            return action();
        }
    }

    /// <summary>
    /// Acquires an exclusive FileShare.None lock on the sidecar lock file.
    /// Waits up to <paramref name="timeout"/> (default 5s) then throws IOException.
    /// The lock is the open handle; leftover .lock files after a crash are harmless.
    /// </summary>
    internal static FileStream AcquireConfigLock(TimeSpan? timeout = null)
    {
        var lockPath = ConfigLockFilePath;
        var waitFor = timeout ?? ConfigLockTimeout;
        var deadline = DateTime.UtcNow + waitFor;

        Directory.CreateDirectory(PathsManager.SettingsFolder);

        while (true)
        {
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException ex)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    throw new IOException(
                        $"Timed out waiting for exclusive lock on uploader instance configuration '{lockPath}'. Retry timeout: {waitFor.TotalSeconds:0}s.",
                        ex);
                }

                Thread.Sleep(ConfigLockRetryDelayMs);
            }
        }
    }

    private InstanceConfiguration LoadConfiguration()
    {
        using var configLock = AcquireConfigLock();
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var configuration = JsonConvert.DeserializeObject<InstanceConfiguration>(json) ?? new InstanceConfiguration();
                NormalizeConfiguration(configuration);
                return configuration;
            }
        }
        catch
        {
            // If loading fails, return empty configuration
        }

        return new InstanceConfiguration();
    }

    private static void NormalizeConfiguration(InstanceConfiguration configuration)
    {
        configuration.Instances ??= new List<UploaderInstance>();
        configuration.DefaultInstances ??= new Dictionary<UploaderCategory, string>();

        foreach (var instance in configuration.Instances)
        {
            NormalizeInstance(instance);
        }
    }

    private static FileTypeScope GetFileTypeRouting(UploaderInstance instance)
    {
        NormalizeInstance(instance);
        return instance.FileTypeRouting;
    }

    private static void NormalizeInstance(UploaderInstance instance)
    {
        instance.FileTypeRouting ??= new FileTypeScope();
        instance.FileTypeRouting.FileExtensions = NormalizeFileExtensions(instance.FileTypeRouting.FileExtensions);
    }

    private static List<string> NormalizeFileExtensions(IEnumerable<string>? fileExtensions)
    {
        if (fileExtensions == null)
        {
            return new List<string>();
        }

        return fileExtensions
            .Select(NormalizeFileExtension)
            .Where(ext => ext != null)
            .Select(ext => ext!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeFileExtension(string? fileExtension)
    {
        if (string.IsNullOrWhiteSpace(fileExtension))
        {
            return null;
        }

        var normalized = fileExtension.Trim().TrimStart('.').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static bool InstanceIdsEqual(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string GetStaleDefaultReason(UploaderInstance? instance, UploaderCategory category)
    {
        if (instance == null)
        {
            return "instance no longer exists";
        }

        if (instance.Category != category)
        {
            return $"category is {instance.Category}";
        }

        if (!instance.IsAvailable)
        {
            return "instance is unavailable";
        }

        return "instance is not usable";
    }

    private static void LogStaleDefaultRemoved(UploaderCategory category, string instanceId, string reason)
    {
        DebugHelper.WriteLine($"[Uploaders] Removed stale default {category} uploader '{instanceId}': {reason}.");
    }

    private void SaveConfiguration(bool throwOnError = false)
    {
        using var configLock = AcquireConfigLock();
        try
        {
            var json = JsonConvert.SerializeObject(_configuration, Formatting.Indented);
            string configFilePath = ConfigFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(configFilePath) ?? PathsManager.SettingsFolder);
            string tempFilePath = configFilePath + ".temp";
            File.WriteAllText(tempFilePath, json);
            File.Move(tempFilePath, configFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            if (throwOnError)
            {
                throw new IOException("Failed to save destination instance configuration.", ex);
            }

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
