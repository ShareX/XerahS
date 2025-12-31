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

namespace ShareX.Avalonia.Uploaders.Plugins.ImgurPlugin;

/// <summary>
/// Imgur image uploader provider (supports Image and Text categories)
/// </summary>
public class ImgurProvider : UploaderProviderBase
{
    public override string ProviderId => "imgur";
    public override string Name => "Imgur";
    public override string Description => "Upload images to Imgur - free image hosting service";
    public override Version Version => new Version(1, 0, 0);
    public override UploaderCategory[] SupportedCategories =>  new[] { UploaderCategory.Image, UploaderCategory.Text };
    public override Type ConfigModelType => typeof(ImgurConfigModel);

    public ImgurProvider()
    {
        // Register this provider with the catalog
        ProviderCatalog.RegisterProvider(this);
    }

    public override Uploader CreateInstance(string settingsJson)
    {
        var config = JsonConvert.DeserializeObject<ImgurConfigModel>(settingsJson);
        if (config == null)
        {
            throw new InvalidOperationException("Failed to deserialize Imgur settings");
        }

        return new ImgurUploader(config);
    }

    public override Dictionary<UploaderCategory, string[]> GetSupportedFileTypes()
    {
        return new Dictionary<UploaderCategory, string[]>
        {
            { 
                UploaderCategory.Image, 
                new[] { "png", "jpg", "jpeg", "gif", "apng", "bmp", "tiff", "webp", "mp4", "avi", "mov" } 
            },
            { 
                UploaderCategory.Text, 
                new[] { "txt", "log", "json", "xml", "md", "html", "css", "js" } 
            }
        };
    }

    // UI view will be created in Phase 3
    public override object? CreateConfigView()
    {
        // TODO: Return ImgurConfigView instance when UI is implemented
        return null;
    }
}
