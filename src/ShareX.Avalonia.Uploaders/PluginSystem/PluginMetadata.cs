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

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Runtime metadata for a loaded plugin
/// Wraps PluginManifest with additional runtime information
/// </summary>
public class PluginMetadata
{
    /// <summary>
    /// The plugin manifest (from plugin.json)
    /// </summary>
    public PluginManifest Manifest { get; }

    /// <summary>
    /// Full path to the plugin directory
    /// </summary>
    public string PluginDirectory { get; }

    /// <summary>
    /// Full path to the plugin assembly DLL
    /// </summary>
    public string AssemblyPath { get; }

    /// <summary>
    /// The loaded provider instance (null if not yet loaded)
    /// </summary>
    public IUploaderProvider? Provider { get; set; }

    /// <summary>
    /// Whether the plugin loaded successfully
    /// </summary>
    public bool IsLoaded => Provider != null;

    /// <summary>
    /// Load error message (if any)
    /// </summary>
    public string? LoadError { get; set; }

    /// <summary>
    /// When the plugin was loaded
    /// </summary>
    public DateTime LoadedAt { get; set; }

    public PluginMetadata(PluginManifest manifest, string pluginDirectory, string assemblyPath)
    {
        Manifest = manifest;
        PluginDirectory = pluginDirectory;
        AssemblyPath = assemblyPath;
        LoadedAt = DateTime.UtcNow;
    }

    public override string ToString()
    {
        return $"{Manifest.Name} v{Manifest.Version} ({Manifest.PluginId})";
    }
}
