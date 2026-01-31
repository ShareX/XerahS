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

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System;
using System.Threading.Tasks;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class CustomUploaderEditorDialog : Window
{
    public CustomUploaderEditorDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is CustomUploaderEditorViewModel vm)
        {
            vm.CloseRequested = result => Close(result);
            vm.OpenFileRequester = OpenFileAsync;
            vm.SaveFileRequester = SaveFileAsync;
        }
    }

    private async Task<string?> OpenFileAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Custom Uploader",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Custom Uploader Files")
                {
                    Patterns = new[] { "*.sxcu", "*.json" }
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    private async Task<string?> SaveFileAsync(string? suggestedName, string? extension)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Custom Uploader",
            SuggestedFileName = suggestedName ?? "CustomUploader",
            DefaultExtension = extension ?? ".sxcu",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("ShareX Custom Uploader")
                {
                    Patterns = new[] { "*.sxcu" }
                },
                new FilePickerFileType("JSON File")
                {
                    Patterns = new[] { "*.json" }
                }
            }
        });

        return file?.Path.LocalPath;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
