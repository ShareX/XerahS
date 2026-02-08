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
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;
using System.Collections.ObjectModel;

namespace XerahS.UI.ViewModels;

/// <summary>
/// ViewModel for custom uploader configuration in the settings panel
/// </summary>
public partial class CustomUploaderConfigViewModel : ObservableObject, IUploaderConfigViewModel
{
    private CustomUploaderItem _item = CustomUploaderItem.Init();

    [ObservableProperty]
    private string _requestUrl = string.Empty;

    [ObservableProperty]
    private string _requestMethod = "POST";

    [ObservableProperty]
    private string _bodyType = "MultipartFormData";

    [ObservableProperty]
    private string _fileFormName = "file";

    [ObservableProperty]
    private string _urlSyntax = string.Empty;

    [ObservableProperty]
    private int _headerCount;

    [ObservableProperty]
    private int _parameterCount;

    [ObservableProperty]
    private int _argumentCount;

    public CustomUploaderConfigViewModel()
    {
    }

    public void LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            _item = CustomUploaderItem.Init();
        }
        else
        {
            try
            {
                var item = JsonConvert.DeserializeObject<CustomUploaderItem>(json);
                _item = item ?? CustomUploaderItem.Init();
            }
            catch
            {
                _item = CustomUploaderItem.Init();
            }
        }

        UpdatePropertiesFromItem();
    }

    public string ToJson()
    {
        return JsonConvert.SerializeObject(_item, Formatting.Indented, new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        });
    }

    public bool Validate()
    {
        return !string.IsNullOrWhiteSpace(_item.RequestURL);
    }

    [RelayCommand]
    private async Task EditAdvanced()
    {
        var editorViewModel = new CustomUploaderEditorViewModel();
        editorViewModel.LoadFromItem(_item);

        var dialog = new Views.CustomUploaderEditorDialog
        {
            DataContext = editorViewModel
        };

        var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (mainWindow != null)
        {
            var result = await dialog.ShowDialog<bool>(mainWindow);
            if (result)
            {
                _item = editorViewModel.ToItem();
                UpdatePropertiesFromItem();
                OnPropertyChanged(nameof(_item)); // Notify parent that data changed
            }
        }
    }

    private void UpdatePropertiesFromItem()
    {
        RequestUrl = _item.RequestURL ?? string.Empty;
        RequestMethod = _item.RequestMethod.ToString();
        BodyType = _item.Body.ToString();
        FileFormName = _item.FileFormName ?? "file";
        UrlSyntax = _item.URL ?? string.Empty;

        HeaderCount = _item.Headers?.Count ?? 0;
        ParameterCount = _item.Parameters?.Count ?? 0;
        ArgumentCount = _item.Arguments?.Count ?? 0;
    }

    public CustomUploaderItem GetItem() => _item;
}
