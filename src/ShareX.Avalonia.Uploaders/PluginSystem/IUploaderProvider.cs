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

namespace ShareX.Ava.Uploaders.PluginSystem;

/// <summary>
/// Interface for uploader providers (renamed from IUploaderPlugin to support multi-instance)
/// </summary>
public interface IUploaderProvider
{
    /// <summary>
    /// Unique provider identifier (e.g., "imgur", "amazons3")
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Display name of the provider
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Brief description of the provider
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Provider version
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// Categories this provider supports (can be added to multiple categories)
    /// </summary>
    UploaderCategory[] SupportedCategories { get; }

    /// <summary>
    /// Type of the configuration model
    /// </summary>
    Type ConfigModelType { get; }

    /// <summary>
    /// Creates a configuration view for this provider (should be Avalonia.Controls.UserControl)
    /// Plugins can return their own XAML UI for configuration
    /// Returns null if no custom UI provided (will use default property grid)
    /// </summary>
    object? CreateConfigView();

    /// <summary>
    /// Creates a configuration ViewModel for this provider.
    /// Returns null if no custom VM provided.
    /// </summary>
    IUploaderConfigViewModel? CreateConfigViewModel();

    /// <summary>
    /// Create an uploader instance from serialized JSON settings
    /// </summary>
    Uploader CreateInstance(string settingsJson);

    /// <summary>
    /// Get file types supported by this provider for each category.
    /// Returns dictionary of Category → Supported extensions (without leading dot, lowercase)
    /// </summary>
    Dictionary<UploaderCategory, string[]> GetSupportedFileTypes();

    /// <summary>
    /// Validates the serialized settings JSON
    /// </summary>
    bool ValidateSettings(string settingsJson);

    /// <summary>
    /// Returns default serialized settings JSON for the given category
    /// </summary>
    string GetDefaultSettings(UploaderCategory category);
}
