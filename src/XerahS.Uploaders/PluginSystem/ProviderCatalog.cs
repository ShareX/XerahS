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

using XerahS.Common;
using XerahS.Uploaders.CustomUploader;

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Static registry of available uploader providers (loaded from plugin DLLs)
/// </summary>
public static class ProviderCatalog
{
    private static readonly Dictionary<string, IUploaderProvider> _providers = new();
    private static readonly Dictionary<string, PluginMetadata> _pluginMetadata = new();
    private static readonly object _lock = new();
    private static bool _pluginsLoaded = false;
    private static readonly PluginLoader _pluginLoader = new(); // Keep contexts alive
    private static IProviderContext? _providerContext;

    public static void SetProviderContext(IProviderContext context)
    {
        _providerContext = context;

        foreach (var provider in _providers.Values)
        {
            ApplyContext(provider);
        }
    }

    public static IProviderContext? GetProviderContext() => _providerContext;

    /// <summary>
    /// Load plugins from the specified directories
    /// </summary>
    /// <param name="pluginDirectories">List of directories to scan</param>
    public static void LoadPlugins(IEnumerable<string> pluginDirectories, bool forceReload = false)
    {
        lock (_lock)
        {
            if (_pluginsLoaded && !forceReload)
            {
                DebugHelper.WriteLine("[Plugins] Already loaded, skipping");
                return;
            }

            DebugHelper.WriteLine($"[Plugins] ========================================");
            
            var discovery = new PluginDiscovery();
            var allDiscovered = new List<PluginMetadata>();

            foreach (var pluginsDirectory in pluginDirectories)
            {
                DebugHelper.WriteLine($"[Plugins] Scanning directory: {pluginsDirectory}");
                if (Directory.Exists(pluginsDirectory))
                {
                    var discovered = discovery.DiscoverPlugins(pluginsDirectory);
                    allDiscovered.AddRange(discovered);
                    DebugHelper.WriteLine($"[Plugins] Discovered {discovered.Count} plugin(s) in {pluginsDirectory}");
                }
                else
                {
                    DebugHelper.WriteLine($"[Plugins] Directory does not exist: {pluginsDirectory}");
                }
            }

            DebugHelper.WriteLine($"[Plugins] Total plugins discovered: {allDiscovered.Count}");

            // Load each plugin
            int successCount = 0;
            int failureCount = 0;

            foreach (var metadata in allDiscovered)
            {
                if (_pluginMetadata.ContainsKey(metadata.Manifest.PluginId))
                {
                    DebugHelper.WriteLine($"[Plugins] Plugin already loaded: {metadata.Manifest.PluginId}");
                    continue;
                }

                try
                {
                    DebugHelper.WriteLine($"[Plugins] Attempting to load: {metadata.Manifest.Name} (id: {metadata.Manifest.PluginId})");
                    var provider = _pluginLoader.LoadPlugin(metadata);

                    if (provider != null && metadata.IsLoaded)
                    {
                        // Register the provider
                        _providers[provider.ProviderId] = provider;
                        ApplyContext(provider);
                        _pluginMetadata[provider.ProviderId] = metadata;
                        successCount++;
                        DebugHelper.WriteLine($"[Plugins] ✓ SUCCESS: {metadata.Manifest.Name} (categories: {string.Join(", ", provider.SupportedCategories)})");
                    }
                    else
                    {
                        failureCount++;
                        DebugHelper.WriteLine($"[Plugins] ✗ FAILED: {metadata.Manifest.Name} - {metadata.LoadError}");
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    DebugHelper.WriteLine($"[Plugins] ✗ ERROR loading {metadata.Manifest.Name}: {ex.Message}");
                    DebugHelper.WriteLine($"[Plugins]   Stack: {ex.StackTrace}");
                }
            }

            _pluginsLoaded = true;
            DebugHelper.WriteLine($"[Plugins] Complete: {successCount} succeeded, {failureCount} failed");

            // Also load custom uploaders (.sxcu files) from the same directories
            int customCount = 0;
            foreach (var pluginsDirectory in pluginDirectories)
            {
                if (Directory.Exists(pluginsDirectory))
                {
                    customCount += LoadCustomUploaders(pluginsDirectory);
                }
            }

            DebugHelper.WriteLine($"[Plugins] Custom uploaders loaded: {customCount}");
            DebugHelper.WriteLine($"[Plugins] Total providers in catalog: {_providers.Count}");
            DebugHelper.WriteLine($"[Plugins] ========================================");
        }
    }

    /// <summary>
    /// Load plugins from the specified directory
    /// </summary>
    /// <param name="pluginsDirectory">Path to Plugins/ directory</param>
    public static void LoadPlugins(string pluginsDirectory, bool forceReload = false)
    {
        LoadPlugins(new[] { pluginsDirectory }, forceReload);
    }

    /// <summary>
    /// Register a provider in the catalog (for programmatic registration)
    /// </summary>
    public static void RegisterProvider(IUploaderProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));

        lock (_lock)
        {
            if (!_providers.ContainsKey(provider.ProviderId))
            {
                _providers[provider.ProviderId] = provider;
                ApplyContext(provider);
                DebugHelper.WriteLine($"[Plugins] Registered provider: {provider.Name} ({provider.ProviderId})");
            }
        }
    }

    /// <summary>
    /// Get a provider by its ID
    /// </summary>
    public static IUploaderProvider? GetProvider(string providerId)
    {
        lock (_lock)
        {
            return _providers.TryGetValue(providerId, out var provider) ? provider : null;
        }
    }

    /// <summary>
    /// Get all registered providers
    /// </summary>
    public static List<IUploaderProvider> GetAllProviders()
    {
        lock (_lock)
        {
            return _providers.Values.ToList();
        }
    }

    /// <summary>
    /// Get providers that support a specific category
    /// </summary>
    public static List<IUploaderProvider> GetProvidersByCategory(UploaderCategory category)
    {
        lock (_lock)
        {
            return _providers.Values
                .Where(p => p.SupportedCategories.Contains(category))
                .ToList();
        }
    }

    /// <summary>
    /// Get plugin metadata for a provider
    /// </summary>
    public static PluginMetadata? GetPluginMetadata(string providerId)
    {
        lock (_lock)
        {
            return _pluginMetadata.TryGetValue(providerId, out var metadata) ? metadata : null;
        }
    }

    /// <summary>
    /// Get all plugin metadata
    /// </summary>
    public static List<PluginMetadata> GetAllPluginMetadata()
    {
        lock (_lock)
        {
            return _pluginMetadata.Values.ToList();
        }
    }

    /// <summary>
    /// Check if plugins have been loaded
    /// </summary>
    public static bool ArePluginsLoaded() => _pluginsLoaded;

    private static bool _builtInInitialized = false;

    /// <summary>
    /// Initialize built-in providers by scanning the current assembly
    /// </summary>
    public static void InitializeBuiltInProviders()
    {
        InitializeBuiltInProviders(Array.Empty<System.Reflection.Assembly>());
    }

    /// <summary>
    /// Initialize built-in providers by scanning the current assembly and additional assemblies.
    /// On mobile, plugin assemblies are bundled with the app and passed here instead of using LoadPlugins().
    /// </summary>
    public static void InitializeBuiltInProviders(params System.Reflection.Assembly[] additionalAssemblies)
    {
        lock (_lock)
        {
            if (_builtInInitialized) return;

            var assemblies = new List<System.Reflection.Assembly> { typeof(ProviderCatalog).Assembly };
            assemblies.AddRange(additionalAssemblies);

            foreach (var assembly in assemblies)
            {
                try
                {
                    var providerTypes = assembly.GetTypes()
                        .Where(t => typeof(IUploaderProvider).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    foreach (var type in providerTypes)
                    {
                        try
                        {
                            if (type.GetConstructor(Type.EmptyTypes) != null)
                            {
                                var provider = Activator.CreateInstance(type) as IUploaderProvider;
                                if (provider != null)
                                {
                                    RegisterProvider(provider);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            DebugHelper.WriteLine($"Error initializing built-in provider {type.Name}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteLine($"Error scanning assembly {assembly.GetName().Name}: {ex.Message}");
                }
            }

            _builtInInitialized = true;
        }
    }

    /// <summary>
    /// Load custom uploaders (.sxcu files) from a directory
    /// </summary>
    /// <param name="directory">Directory to scan for .sxcu and .json files</param>
    /// <returns>Number of custom uploaders loaded</returns>
    public static int LoadCustomUploaders(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        int loadedCount = 0;

        lock (_lock)
        {
            var loaded = CustomUploaderRepository.DiscoverUploaders(directory);

            foreach (var uploader in loaded.Where(u => u.IsValid))
            {
                try
                {
                    var provider = new CustomUploaderProvider(uploader);

                    if (_providers.ContainsKey(provider.ProviderId))
                    {
                        DebugHelper.WriteLine($"[CustomUploader] Provider already exists: {provider.ProviderId}");
                        continue;
                    }

                    _providers[provider.ProviderId] = provider;
                    ApplyContext(provider);

                    // Create synthetic metadata for custom uploaders
                    var metadata = CreateCustomUploaderMetadata(provider, uploader);
                    _pluginMetadata[provider.ProviderId] = metadata;

                    loadedCount++;
                    DebugHelper.WriteLine($"[CustomUploader] ✓ Loaded: {provider.Name} ({provider.ProviderId}) - Categories: {string.Join(", ", provider.SupportedCategories)}");
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteLine($"[CustomUploader] ✗ Error creating provider for {uploader.FilePath}: {ex.Message}");
                }
            }

            // Log failures
            foreach (var uploader in loaded.Where(u => !u.IsValid))
            {
                DebugHelper.WriteLine($"[CustomUploader] ✗ Failed to load {uploader.FilePath}: {uploader.LoadError}");
            }
        }

        return loadedCount;
    }

    /// <summary>
    /// Creates synthetic plugin metadata for a custom uploader
    /// </summary>
    private static PluginMetadata CreateCustomUploaderMetadata(CustomUploaderProvider provider, LoadedCustomUploader uploader)
    {
        var manifest = new PluginManifest
        {
            PluginId = provider.ProviderId,
            Name = provider.Name,
            Version = provider.Version.ToString(),
            Author = "Custom Uploader",
            Description = provider.Description,
            ApiVersion = "1.0",
            EntryPoint = "CustomUploader",
            AssemblyFileName = Path.GetFileName(uploader.FilePath),
            SupportedCategories = provider.SupportedCategories.Select(c => c.ToString()).ToList()
        };

        var pluginDir = Path.GetDirectoryName(uploader.FilePath) ?? "";
        var metadata = new PluginMetadata(manifest, pluginDir, uploader.FilePath)
        {
            Provider = provider // This sets IsLoaded to true
        };

        return metadata;
    }

    /// <summary>
    /// Checks if a provider is a custom uploader
    /// </summary>
    /// <param name="providerId">The provider ID to check</param>
    /// <returns>True if the provider is a custom uploader</returns>
    public static bool IsCustomUploader(string providerId)
    {
        return providerId?.StartsWith("custom_", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Gets all custom uploader providers
    /// </summary>
    /// <returns>List of custom uploader providers</returns>
    public static List<CustomUploaderProvider> GetCustomUploaderProviders()
    {
        lock (_lock)
        {
            return _providers.Values
                .OfType<CustomUploaderProvider>()
                .ToList();
        }
    }

    /// <summary>
    /// Reloads a specific custom uploader file
    /// </summary>
    /// <param name="filePath">Path to the .sxcu file to reload</param>
    /// <returns>True if reload was successful</returns>
    public static bool ReloadCustomUploader(string filePath)
    {
        var loaded = CustomUploaderRepository.ReloadFile(filePath);

        if (loaded == null)
        {
            return false;
        }

        lock (_lock)
        {
            var provider = new CustomUploaderProvider(loaded);

            // Remove old provider with same file if exists
            var existingKey = _providers.Keys.FirstOrDefault(k =>
                _providers[k] is CustomUploaderProvider cp && cp.FilePath == filePath);

            if (existingKey != null)
            {
                _providers.Remove(existingKey);
                _pluginMetadata.Remove(existingKey);
            }

            _providers[provider.ProviderId] = provider;
            ApplyContext(provider);
            _pluginMetadata[provider.ProviderId] = CreateCustomUploaderMetadata(provider, loaded);

            DebugHelper.WriteLine($"[CustomUploader] Reloaded: {provider.Name} ({provider.ProviderId})");
            return true;
        }
    }

    /// <summary>
    /// Removes a custom uploader provider
    /// </summary>
    /// <param name="providerId">The provider ID to remove</param>
    /// <returns>True if removed successfully</returns>
    public static bool RemoveCustomUploader(string providerId)
    {
        if (!IsCustomUploader(providerId))
        {
            return false;
        }

        lock (_lock)
        {
            if (_providers.TryGetValue(providerId, out var provider) && provider is CustomUploaderProvider customProvider)
            {
                CustomUploaderRepository.RemoveFile(customProvider.FilePath);
                _providers.Remove(providerId);
                _pluginMetadata.Remove(providerId);
                DebugHelper.WriteLine($"[CustomUploader] Removed: {customProvider.Name} ({providerId})");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Clear all providers (for testing)
    /// </summary>
    internal static void Clear()
    {
        lock (_lock)
        {
            _providers.Clear();
            _pluginMetadata.Clear();
            _pluginsLoaded = false;
            _builtInInitialized = false;
            CustomUploaderRepository.Clear();
        }
    }

    /// <summary>
    /// Get the <see cref="IUploaderExplorer"/> implementation for a provider, or null if not supported.
    /// </summary>
    public static IUploaderExplorer? GetExplorer(string providerId)
    {
        lock (_lock)
        {
            if (_providers.TryGetValue(providerId, out var provider) && provider is IUploaderExplorer explorer)
            {
                return explorer;
            }
            return null;
        }
    }

    /// <summary>
    /// Get all providers that implement <see cref="IUploaderExplorer"/> (support Media Explorer browsing).
    /// </summary>
    public static List<IUploaderProvider> GetBrowsableProviders()
    {
        lock (_lock)
        {
            return _providers.Values
                .Where(p => p is IUploaderExplorer)
                .ToList();
        }
    }

    private static void ApplyContext(IUploaderProvider provider)
    {
        if (_providerContext == null)
        {
            return;
        }

        if (provider is IProviderContextAware contextAware)
        {
            contextAware.SetContext(_providerContext);
        }
    }
}
