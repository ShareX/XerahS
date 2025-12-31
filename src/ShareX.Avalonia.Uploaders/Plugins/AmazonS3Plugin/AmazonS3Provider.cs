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

using Newtonsoft.Json;
using ShareX.Avalonia.Uploaders.PluginSystem;

namespace ShareX.Avalonia.Uploaders.Plugins.AmazonS3Plugin;

/// <summary>
/// Amazon S3 file uploader provider (supports Image, Text, and File categories)
/// </summary>
public class AmazonS3Provider : UploaderProviderBase
{
    public override string ProviderId => "amazons3";
    public override string Name => "Amazon S3";
    public override string Description => "Upload files to Amazon Simple Storage Service (S3)";
    public override Version Version => new Version(1, 0, 0);
    public override UploaderCategory[] SupportedCategories => new[] { UploaderCategory.Image, UploaderCategory.Text, UploaderCategory.File };
    public override Type ConfigModelType => typeof(S3ConfigModel);

    public AmazonS3Provider()
    {
        // Register this provider with the catalog
        ProviderCatalog.RegisterProvider(this);
    }

    public override Uploader CreateInstance(string settingsJson)
    {
        var config = JsonConvert.DeserializeObject<S3ConfigModel>(settingsJson);
        if (config == null)
        {
            throw new InvalidOperationException("Failed to deserialize Amazon S3 settings");
        }

        return new AmazonS3Uploader(config);
    }

    public override Dictionary<UploaderCategory, string[]> GetSupportedFileTypes()
    {
        // S3 supports all file types for all categories
        var allTypes = new[] { 
            "png", "jpg", "jpeg", "gif", "bmp", "tiff", "webp", "svg",  // Common images
            "mp4", "avi", "mov", "mkv", "flv", "wmv", "webm",           // Videos  
            "txt", "log", "json", "xml", "md", "html", "css", "js",     // Text
            "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx",         // Documents
            "zip", "rar", "7z", "tar", "gz",                            // Archives
            "exe", "dll", "so", "dmg", "apk", "ipa"                     // Executables
        };

        return new Dictionary<UploaderCategory, string[]>
        {
            { UploaderCategory.Image, allTypes },
            { UploaderCategory.Text, allTypes },
            { UploaderCategory.File, allTypes }
        };
    }

    // UI view will be created later
    public override object? CreateConfigView()
    {
        // TODO: Return S3ConfigView instance when UI is implemented
        return null;
    }
}
