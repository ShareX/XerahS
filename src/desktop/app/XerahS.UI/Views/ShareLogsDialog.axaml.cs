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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

/// <summary>Debug tab "Share logs" modal, shown through IViewDialogService.ShowShareLogsAsync.</summary>
public partial class ShareLogsDialog : UserControl
{
    public ShareLogsDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not ShareLogsViewModel viewModel)
        {
            return;
        }

        viewModel.CopyToClipboard = async text =>
        {
            if (XerahS.Platform.Abstractions.PlatformServices.IsInitialized)
            {
                await XerahS.Platform.Abstractions.PlatformServices.Clipboard.SetTextAsync(text);
            }
        };
        viewModel.PickSavePath = async suggestedName =>
        {
            if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            {
                return null;
            }

            IStorageFile? file = await storage.SavePickerAsync(suggestedName);
            return file?.TryGetLocalPath();
        };
    }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is ShareLogsViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }
}

internal static class ShareLogsStorageExtensions
{
    public static Task<IStorageFile?> SavePickerAsync(this IStorageProvider storage, string suggestedName) =>
        storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save a copy of the report",
            SuggestedFileName = suggestedName,
            DefaultExtension = "json",
            FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }],
        });
}
