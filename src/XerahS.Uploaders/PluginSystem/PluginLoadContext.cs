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

using System.Reflection;
using System.Runtime.Loader;

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Custom AssemblyLoadContext for plugin isolation
/// Allows plugins to be loaded and potentially unloaded
/// </summary>
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginDirectory;

    public PluginLoadContext(string pluginPath, string pluginDirectory)
        : base(isCollectible: true) // Enable unloading
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        _pluginDirectory = pluginDirectory;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Try to resolve from plugin directory first
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Check if it's a shared dependency (don't load, use host's version)
        if (IsSharedDependency(assemblyName))
        {
            return null; // Let default context handle it
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }

    private bool IsSharedDependency(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;

        // These should come from the host app, not plugin
        return name == "ShareX.Ava.Uploaders" ||
               name == "ShareX.Ava.Common" ||
               name == "Newtonsoft.Json" ||
               name == "CommunityToolkit.Mvvm" ||
               name?.StartsWith("System.") == true ||
               name?.StartsWith("Microsoft.") == true ||
               name?.StartsWith("Avalonia.") == true;
    }
}
