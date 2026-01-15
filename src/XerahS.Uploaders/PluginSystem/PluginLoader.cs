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

using XerahS.Common;
using System.Reflection;

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Loads plugin assemblies and instantiates providers
/// </summary>
public class PluginLoader
{
    private readonly Dictionary<string, PluginLoadContext> _loadedContexts = new();

    /// <summary>
    /// Load a plugin from its metadata
    /// </summary>
    public IUploaderProvider? LoadPlugin(PluginMetadata metadata)
    {
        try
        {
            DebugHelper.WriteLine($"Loading plugin: {metadata.Manifest.PluginId} from {metadata.AssemblyPath}");

            // Create isolated load context
            var loadContext = new PluginLoadContext(metadata.AssemblyPath, metadata.PluginDirectory);

            // Load the plugin assembly
            var assembly = loadContext.LoadFromAssemblyPath(metadata.AssemblyPath);

            // Find and instantiate the provider type
            var providerType = assembly.GetType(metadata.Manifest.EntryPoint);
            if (providerType == null)
            {
                metadata.LoadError = $"Entry point type not found: {metadata.Manifest.EntryPoint}";
                DebugHelper.WriteLine($"ERROR: {metadata.LoadError}");
                return null;
            }

            // Verify it implements IUploaderProvider
            if (!typeof(IUploaderProvider).IsAssignableFrom(providerType))
            {
                metadata.LoadError = $"Type {providerType.FullName} does not implement IUploaderProvider";
                DebugHelper.WriteLine($"ERROR: {metadata.LoadError}");
                return null;
            }

            // Instantiate the provider
            var provider = Activator.CreateInstance(providerType) as IUploaderProvider;
            if (provider == null)
            {
                metadata.LoadError = "Failed to instantiate provider";
                DebugHelper.WriteLine($"ERROR: {metadata.LoadError}");
                return null;
            }

            // Verify plugin ID matches
            if (provider.ProviderId != metadata.Manifest.PluginId)
            {
                DebugHelper.WriteLine($"WARNING: Plugin ID mismatch - manifest: {metadata.Manifest.PluginId}, provider: {provider.ProviderId}");
                // Allow it but warn
            }

            // Store load context for potential unloading
            _loadedContexts[metadata.Manifest.PluginId] = loadContext;

            metadata.Provider = provider;
            DebugHelper.WriteLine($"Successfully loaded plugin: {metadata}");

            return provider;
        }
        catch (FileNotFoundException ex)
        {
            metadata.LoadError = $"Assembly not found: {ex.FileName}";
            DebugHelper.WriteLine($"ERROR loading plugin {metadata.Manifest.PluginId}: {metadata.LoadError}");
        }
        catch (TypeLoadException ex)
        {
            metadata.LoadError = $"Type load error: {ex.Message}";
            DebugHelper.WriteLine($"ERROR loading plugin {metadata.Manifest.PluginId}: {metadata.LoadError}");
        }
        catch (ReflectionTypeLoadException ex)
        {
            metadata.LoadError = $"Reflection error: {ex.Message}";
            foreach (var loaderEx in ex.LoaderExceptions)
            {
                DebugHelper.WriteLine($"  Loader exception: {loaderEx?.Message}");
            }
        }
        catch (Exception ex)
        {
            metadata.LoadError = $"Unexpected error: {ex.Message}";
            DebugHelper.WriteLine($"ERROR loading plugin {metadata.Manifest.PluginId}: {metadata.LoadError}");
            DebugHelper.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        return null;
    }

    /// <summary>
    /// Unload a plugin (experimental - requires further testing)
    /// </summary>
    public bool UnloadPlugin(string pluginId)
    {
        if (!_loadedContexts.TryGetValue(pluginId, out var context))
        {
            return false;
        }

        try
        {
            context.Unload();
            _loadedContexts.Remove(pluginId);

            // Force GC to collect unloaded assemblies
            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            DebugHelper.WriteLine($"Unloaded plugin: {pluginId}");
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"Error unloading plugin {pluginId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get list of loaded plugin contexts
    /// </summary>
    public IReadOnlyDictionary<string, PluginLoadContext> GetLoadedContexts() => _loadedContexts;
}
