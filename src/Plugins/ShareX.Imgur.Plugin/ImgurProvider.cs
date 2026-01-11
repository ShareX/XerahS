#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
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
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace ShareX.Imgur.Plugin;

/// <summary>
/// Imgur image uploader provider (supports Image category only)
/// </summary>
public class ImgurProvider : UploaderProviderBase
{
    public override string ProviderId => "imgur";
    public override string Name => "Imgur";
    public override string Description => "Upload images to Imgur - free image hosting service";
    public override Version Version => new Version(1, 0, 0);
    public override UploaderCategory[] SupportedCategories => new[] { UploaderCategory.Image };
    public override Type ConfigModelType => typeof(ImgurConfigModel);

    public ImgurProvider()
    {
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
            }
        };
    }

    public override object? CreateConfigView()
    {
        return new Views.ImgurConfigView();
    }

    public override IUploaderConfigViewModel? CreateConfigViewModel()
    {
        return new ViewModels.ImgurConfigViewModel();
    }
}
