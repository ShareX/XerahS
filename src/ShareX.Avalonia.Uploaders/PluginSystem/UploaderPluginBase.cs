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
/// Base class for uploader plugins providing common functionality
/// </summary>
public abstract class UploaderPluginBase : IUploaderPlugin
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract Version Version { get; }
    public abstract UploaderCategory Category { get; }
    public abstract Type ConfigModelType { get; }

    /// <summary>
    /// Override to provide custom config view (Avalonia.Controls.UserControl), return null for property grid
    /// </summary>
    public virtual object? CreateConfigView()
    {
        return null;
    }

    public abstract Uploader CreateInstance(object config);

    /// <summary>
    /// Default validation checks if config is not null and is of correct type
    /// </summary>
    public virtual bool ValidateConfig(object config)
    {
        if (config == null) return false;
        return config.GetType() == ConfigModelType ||
               ConfigModelType.IsAssignableFrom(config.GetType());
    }

    public abstract object GetDefaultConfig();

    /// <summary>
    /// Event raised when the plugin's configuration has changed
    /// </summary>
    public event EventHandler? ConfigChanged;

    /// <summary>
    /// Raises the ConfigChanged event. Derived classes should call this when their config is modified.
    /// </summary>
    protected virtual void OnConfigChanged()
    {
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }
}
