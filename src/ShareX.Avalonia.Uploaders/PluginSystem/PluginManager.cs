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
/// Manages uploader plugin discovery, registration, and retrieval
/// </summary>
public class PluginManager
{
    private static readonly Lazy<PluginManager> _instance = new(() => new PluginManager());
    public static PluginManager Instance => _instance.Value;

    private readonly Dictionary<string, IUploaderPlugin> _plugins = new();
    private readonly object _lock = new();

    private PluginManager()
    {
        // Register built-in plugins
        RegisterBuiltInPlugins();
    }

    /// <summary>
    /// Register a plugin
    /// </summary>
    public void RegisterPlugin(IUploaderPlugin plugin)
    {
        if (plugin == null) throw new ArgumentNullException(nameof(plugin));
        
        lock (_lock)
        {
            if (_plugins.ContainsKey(plugin.Id))
            {
                throw new InvalidOperationException($"Plugin with ID '{plugin.Id}' is already registered.");
            }
            
            _plugins[plugin.Id] = plugin;
        }
    }

    /// <summary>
    /// Get plugin by ID
    /// </summary>
    public IUploaderPlugin? GetPlugin(string id)
    {
        lock (_lock)
        {
            return _plugins.TryGetValue(id, out var plugin) ? plugin : null;
        }
    }

    /// <summary>
    /// Get all plugins for a specific category
    /// </summary>
    public List<IUploaderPlugin> GetPluginsByCategory(UploaderCategory category)
    {
        lock (_lock)
        {
            return _plugins.Values
                .Where(p => p.Category == category)
                .OrderBy(p => p.Name)
                .ToList();
        }
    }

    /// <summary>
    /// Get all registered plugins
    /// </summary>
    public List<IUploaderPlugin> GetAllPlugins()
    {
        lock (_lock)
        {
            return _plugins.Values.OrderBy(p => p.Name).ToList();
        }
    }

    /// <summary>
    /// Discover and load plugins from a directory (future enhancement)
    /// </summary>
    public List<IUploaderPlugin> DiscoverPlugins(string directory)
    {
        // TODO: Implement DLL scanning and loading
        // For now, return empty list as we're using built-in plugins
        return new List<IUploaderPlugin>();
    }

    /// <summary>
    /// Register built-in plugins
    /// </summary>
    private void RegisterBuiltInPlugins()
    {
        // Plugins will be registered here as they are created
        // This method will be called from plugin constructors or explicitly
    }

    /// <summary>
    /// Clear all plugins (for testing)
    /// </summary>
    internal void ClearPlugins()
    {
        lock (_lock)
        {
            _plugins.Clear();
        }
    }
}
