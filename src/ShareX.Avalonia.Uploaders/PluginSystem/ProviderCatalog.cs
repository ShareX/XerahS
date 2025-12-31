#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
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

namespace ShareX.Avalonia.Uploaders.PluginSystem;

/// <summary>
/// Static registry of available uploader providers (loaded from plugin DLLs)
/// </summary>
public static class ProviderCatalog
{
    private static readonly Dictionary<string, IUploaderProvider> _providers = new();
    private static readonly Dictionary<string, PluginMetadata> _pluginMetadata = new();
    private static readonly object _lock = new();
    private static bool _pluginsLoaded = false;

    /// <summary>
    /// Load plugins from the specified directory
    /// </summary>
    /// <param name="pluginsDirectory">Path to Plugins/ directory</param>
    public static void LoadPlugins(string pluginsDirectory)
    {
        lock (_lock)
        {
            if (_pluginsLoaded)
            {
                Console.WriteLine("Plugins already loaded, skipping");
                return;
            }

            Console.WriteLine($"Loading plugins from: {pluginsDirectory}");

            var discovery = new PluginDiscovery();
            var loader = new PluginLoader();

            // Discover all plugins
            var discovered = discovery.DiscoverPlugins(pluginsDirectory);
            Console.WriteLine($"Discovered {discovered.Count} plugins");

            // Load each plugin
            int successCount = 0;
            int failureCount = 0;

            foreach (var metadata in discovered)
            {
                try
                {
                    var provider = loader.LoadPlugin(metadata);
                    
                    if (provider != null && metadata.IsLoaded)
                    {
                        // Register the provider
                        _providers[provider.ProviderId] = provider;
                        _pluginMetadata[provider.ProviderId] = metadata;
                        successCount++;
                        Console.WriteLine($"✓ Loaded: {metadata.Manifest.Name}");
                    }
                    else
                    {
                        failureCount++;
                        Console.WriteLine($"✗ Failed: {metadata.Manifest.Name} - {metadata.LoadError}");
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    Console.WriteLine($"✗ Error loading {metadata.Manifest.Name}: {ex.Message}");
                }
            }

            _pluginsLoaded = true;
            Console.WriteLine($"Plugin loading complete: {successCount} succeeded, {failureCount} failed");
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
                Console.WriteLine($"Registered provider: {provider.Name} ({provider.ProviderId})");
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
        }
    }
}
