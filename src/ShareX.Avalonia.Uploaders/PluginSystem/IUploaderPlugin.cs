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
/// Interface for uploader plugins
/// </summary>
public interface IUploaderPlugin
{
    /// <summary>
    /// Unique identifier for the plugin
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name of the uploader
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Brief description of the uploader
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Plugin version
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// Category this uploader belongs to
    /// </summary>
    UploaderCategory Category { get; }

    /// <summary>
    /// Type of the configuration model
    /// </summary>
    Type ConfigModelType { get; }

    /// <summary>
    /// Creates a configuration view for this uploader (should be Avalonia.Controls.UserControl)
    /// Returns null if using default property grid
    /// </summary>
    object? CreateConfigView();

    /// <summary>
    /// Creates an instance of the uploader with the given configuration
    /// </summary>
    Uploader CreateInstance(object config);

    /// <summary>
    /// Validates the configuration object
    /// </summary>
    bool ValidateConfig(object config);

    /// <summary>
    /// Returns default configuration instance
    /// </summary>
    object GetDefaultConfig();

    /// <summary>
    /// Event raised when the plugin's configuration has changed.
    /// The host subscribes to this event to trigger saving UploadersConfig.
    /// </summary>
    event EventHandler? ConfigChanged;
}
