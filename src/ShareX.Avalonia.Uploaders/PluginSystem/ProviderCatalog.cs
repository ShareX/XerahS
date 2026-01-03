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

using ShareX.Ava.Common;

namespace ShareX.Ava.Uploaders.PluginSystem;

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

    /// <summary>
    /// Load plugins from the specified directory
    /// </summary>
    /// <param name="pluginsDirectory">Path to Plugins/ directory</param>
    public static void LoadPlugins(string pluginsDirectory, bool forceReload = false)
    {
        lock (_lock)
        {
            if (_pluginsLoaded && !forceReload)
            {
                DebugHelper.WriteLine("[Plugins] Already loaded, skipping");
                return;
            }

            DebugHelper.WriteLine($"[Plugins] ========================================");
            DebugHelper.WriteLine($"[Plugins] Loading plugins from: {pluginsDirectory}");
            DebugHelper.WriteLine($"[Plugins] Directory exists: {Directory.Exists(pluginsDirectory)}");

            if (!Directory.Exists(pluginsDirectory))
            {
                DebugHelper.WriteLine($"[Plugins] Creating Plugins directory...");
                try { Directory.CreateDirectory(pluginsDirectory); } catch { }
            }

            var discovery = new PluginDiscovery();

            // Discover all plugins
            var discovered = discovery.DiscoverPlugins(pluginsDirectory);
            DebugHelper.WriteLine($"[Plugins] Discovered {discovered.Count} plugin(s)");

            // Load each plugin
            int successCount = 0;
            int failureCount = 0;

            foreach (var metadata in discovered)
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
            DebugHelper.WriteLine($"[Plugins] Total providers in catalog: {_providers.Count}");
            DebugHelper.WriteLine($"[Plugins] ========================================");
        }
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
        lock (_lock)
        {
            if (_builtInInitialized) return;

            var providerTypes = typeof(ProviderCatalog).Assembly.GetTypes()
                .Where(t => typeof(IUploaderProvider).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in providerTypes)
            {
                try
                {
                    // Check if it has a parameterless constructor
                    if (type.GetConstructor(Type.EmptyTypes) != null)
                    {
                        Activator.CreateInstance(type);
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteLine($"Error initializing built-in provider {type.Name}: {ex.Message}");
                }
            }

            _builtInInitialized = true;
        }
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
        }
    }
}
