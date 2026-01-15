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
/// Represents a configured instance of an uploader provider
/// </summary>
public class UploaderInstance
{
    /// <summary>
    /// Unique identifier for this instance
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Provider identifier (e.g., "imgur", "amazons3")
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Category this instance is bound to
    /// </summary>
    public UploaderCategory Category { get; set; }

    /// <summary>
    /// User-defined display name (e.g., "Amazon S3 (Screenshots)")
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Serialized provider-specific configuration as JSON
    /// </summary>
    public string SettingsJson { get; set; } = "{}";

    /// <summary>
    /// Defines which file types this instance handles
    /// </summary>
    public FileTypeScope FileTypeRouting { get; set; } = new();

    /// <summary>
    /// When this instance was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// When this instance was last modified
    /// </summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Whether this provider plugin is currently available
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    public UploaderInstance()
    {
        CreatedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }
}
