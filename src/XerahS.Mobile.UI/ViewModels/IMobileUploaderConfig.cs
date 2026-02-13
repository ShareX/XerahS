#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

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

namespace XerahS.Mobile.UI.ViewModels;

/// <summary>
/// Interface for mobile-friendly uploader configuration view models.
/// Implement this to add support for new uploaders in the mobile app.
/// </summary>
public interface IMobileUploaderConfig
{
    /// <summary>
    /// Display name of the uploader (e.g., "Amazon S3", "Dropbox")
    /// </summary>
    string UploaderName { get; }

    /// <summary>
    /// Icon path or identifier for the uploader
    /// </summary>
    string IconPath { get; }

    /// <summary>
    /// Brief description of the uploader for the settings list
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Whether the configuration is valid and complete
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Load configuration from UploadersConfig
    /// </summary>
    void LoadConfig();

    /// <summary>
    /// Save configuration to UploadersConfig
    /// </summary>
    /// <returns>True if saved successfully</returns>
    bool SaveConfig();

    /// <summary>
    /// Test the configuration (optional, can be async)
    /// </summary>
    /// <returns>True if test passes</returns>
    Task<bool> TestConfigAsync();
}

/// <summary>
/// Metadata attribute to register mobile uploader configs
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class MobileUploaderConfigAttribute : Attribute
{
    public string UploaderId { get; }
    public string DisplayName { get; }
    public int Order { get; }

    public MobileUploaderConfigAttribute(string uploaderId, string displayName, int order = 0)
    {
        UploaderId = uploaderId;
        DisplayName = displayName;
        Order = order;
    }
}
