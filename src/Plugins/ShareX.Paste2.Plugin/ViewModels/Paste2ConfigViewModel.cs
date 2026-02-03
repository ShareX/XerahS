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

using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using XerahS.Uploaders.PluginSystem;

namespace ShareX.Paste2.Plugin.ViewModels;

/// <summary>
/// ViewModel for Paste2 configuration
/// </summary>
public partial class Paste2ConfigViewModel : ObservableObject, IUploaderConfigViewModel
{
    [ObservableProperty]
    private string _textFormat = "text";

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    public void LoadFromJson(string json)
    {
        try
        {
            var config = JsonConvert.DeserializeObject<Paste2ConfigModel>(json);
            if (config != null)
            {
                TextFormat = string.IsNullOrWhiteSpace(config.TextFormat) ? "text" : config.TextFormat;
                Description = config.Description ?? string.Empty;
            }
        }
        catch
        {
            StatusMessage = "Failed to load configuration";
        }
    }

    public string ToJson()
    {
        var config = new Paste2ConfigModel
        {
            TextFormat = string.IsNullOrWhiteSpace(TextFormat) ? "text" : TextFormat,
            Description = Description ?? string.Empty
        };

        return JsonConvert.SerializeObject(config, Formatting.Indented);
    }

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(TextFormat))
        {
            StatusMessage = "Text format is required";
            return false;
        }

        StatusMessage = null;
        return true;
    }
}
