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

using Newtonsoft.Json;
using XerahS.Common;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace ShareX.Auto.Plugin;

public sealed class AutoProvider : UploaderProviderBase
{
    public override string ProviderId => ProviderIds.Auto;
    public override string Name => "Auto";
    public override string Description => "Automatically uses the default uploader for the detected data type";
    public override Version Version => new Version(1, 0, 0);
    public override UploaderCategory[] SupportedCategories => new[] { UploaderCategory.File };
    public override Type ConfigModelType => typeof(AutoConfigModel);

    public override Dictionary<UploaderCategory, string[]> GetSupportedFileTypes()
    {
        return new Dictionary<UploaderCategory, string[]>
        {
            { UploaderCategory.Image, FileHelpers.ImageFileExtensions },
            { UploaderCategory.Text, FileHelpers.TextFileExtensions },
            { UploaderCategory.File, new[] { "*" } }
        };
    }

    public override Uploader CreateInstance(string settingsJson)
    {
        var config = JsonConvert.DeserializeObject<AutoConfigModel>(settingsJson) ?? new AutoConfigModel();
        return new AutoUploader(config.Category);
    }

    public override bool ValidateSettings(string settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return false;
        }

        try
        {
            return JsonConvert.DeserializeObject<AutoConfigModel>(settingsJson) != null;
        }
        catch
        {
            return false;
        }
    }

    public override string GetDefaultSettings(UploaderCategory category)
    {
        var config = new AutoConfigModel { Category = category };
        return JsonConvert.SerializeObject(config, Formatting.Indented);
    }
}
