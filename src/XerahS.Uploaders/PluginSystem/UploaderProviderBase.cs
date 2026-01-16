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

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Base class for uploader providers with common functionality
/// </summary>
public abstract class UploaderProviderBase : IUploaderProvider
{
    public abstract string ProviderId { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract Version Version { get; }
    public abstract UploaderCategory[] SupportedCategories { get; }
    public abstract Type ConfigModelType { get; }

    /// <summary>
    /// Get file types supported by this provider for each category.
    /// Override to specify which file extensions each category supports.
    /// </summary>
    public abstract Dictionary<UploaderCategory, string[]> GetSupportedFileTypes();

    /// <summary>
    /// Create an uploader instance from serialized JSON settings
    /// </summary>
    public abstract Uploader CreateInstance(string settingsJson);

    /// <summary>
    /// Override to provide custom config view, return null for property grid
    /// </summary>
    public virtual object? CreateConfigView() => null;

    public virtual IUploaderConfigViewModel? CreateConfigViewModel() => null;


    /// <summary>
    /// Default validation: checks if JSON can be deserialized to ConfigModelType
    /// </summary>
    public virtual bool ValidateSettings(string settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
            return false;

        try
        {
            var config = JsonConvert.DeserializeObject(settingsJson, ConfigModelType);
            return config != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Default implementation: returns serialized default instance of ConfigModelType
    /// </summary>
    public virtual string GetDefaultSettings(UploaderCategory category)
    {
        var defaultConfig = Activator.CreateInstance(ConfigModelType);
        return JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);
    }

    /// <summary>
    /// Event raised when the provider's configuration has changed
    /// </summary>
    public event EventHandler? ConfigChanged;

    /// <summary>
    /// Raises the ConfigChanged event. Derived classes should call this when their config is modified.
    /// </summary>
    public virtual void OnConfigChanged()
    {
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }
}
