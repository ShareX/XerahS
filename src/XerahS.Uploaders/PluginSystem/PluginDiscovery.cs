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
using XerahS.Common;

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Discovers plugins by scanning directories for plugin.json manifests
/// </summary>
public class PluginDiscovery
{
    private const string ManifestFileName = "plugin.json";
    private const string CurrentApiVersion = "1.0";

    /// <summary>
    /// Discover all plugins in the specified directory
    /// </summary>
    /// <param name="pluginsDirectory">Root plugins directory (e.g., "Plugins/")</param>
    /// <returns>List of discovered plugin metadata</returns>
    public List<PluginMetadata> DiscoverPlugins(string pluginsDirectory)
    {
        var discovered = new List<PluginMetadata>();

        if (!Directory.Exists(pluginsDirectory))
        {
            DebugHelper.WriteLine($"Plugins directory not found: {pluginsDirectory}");
            return discovered;
        }

        // Each subdirectory is a potential plugin
        var pluginDirs = Directory.GetDirectories(pluginsDirectory);

        foreach (var pluginDir in pluginDirs)
        {
            var manifestPath = Path.Combine(pluginDir, ManifestFileName);

            if (!File.Exists(manifestPath))
            {
                DebugHelper.WriteLine($"No {ManifestFileName} found in {Path.GetFileName(pluginDir)}, skipping");
                continue;
            }

            try
            {
                var manifest = LoadManifest(manifestPath);
                if (manifest == null)
                    continue;

                // Validate manifest
                if (!ValidateManifest(manifest, out var error))
                {
                    DebugHelper.WriteLine($"Invalid manifest in {Path.GetFileName(pluginDir)}: {error}");
                    continue;
                }

                // Find assembly
                var assemblyFileName = manifest.GetAssemblyFileName();
                var assemblyPath = Path.Combine(pluginDir, assemblyFileName);

                if (!File.Exists(assemblyPath))
                {
                    DebugHelper.WriteLine($"Plugin assembly not found: {assemblyFileName} in {Path.GetFileName(pluginDir)}");
                    continue;
                }

                var metadata = new PluginMetadata(manifest, pluginDir, assemblyPath);
                discovered.Add(metadata);

                DebugHelper.WriteLine($"Discovered plugin: {metadata}");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Error discovering plugin in {Path.GetFileName(pluginDir)}: {ex.Message}");
            }
        }

        return discovered;
    }

    /// <summary>
    /// Load and deserialize a plugin manifest from file
    /// </summary>
    private PluginManifest? LoadManifest(string manifestPath)
    {
        try
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonConvert.DeserializeObject<PluginManifest>(json);

            if (manifest == null)
            {
                DebugHelper.WriteLine($"Failed to deserialize manifest: {manifestPath}");
                return null;
            }

            return manifest;
        }
        catch (JsonException ex)
        {
            DebugHelper.WriteLine($"JSON error in {Path.GetFileName(manifestPath)}: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"Error loading manifest {Path.GetFileName(manifestPath)}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Validate a plugin manifest
    /// </summary>
    public bool ValidateManifest(PluginManifest manifest, out string? error)
    {
        // Basic validation
        if (!manifest.IsValid(out error))
            return false;

        // API version compatibility
        if (!manifest.IsCompatibleWith(CurrentApiVersion))
        {
            error = $"Incompatible API version: plugin requires {manifest.ApiVersion}, current is {CurrentApiVersion}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Get the current plugin API version
    /// </summary>
    public static string GetCurrentApiVersion() => CurrentApiVersion;
}
