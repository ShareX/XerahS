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
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class UploadContentWindow : SurfaceWindow
{
    private UploadContentViewModel? _viewModel;

    public UploadContentWindow()
    {
        InitializeComponent();
    }

    public UploadContentViewModel? ViewModel => _viewModel;

    public void Initialize(UploadContentViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.FilePickerRequested += OnFilePickerRequested;
        viewModel.FolderPickerRequested += OnFolderPickerRequested;
        viewModel.TextInputRequested += OnTextInputRequested;
        viewModel.URLInputRequested += OnURLInputRequested;
        viewModel.DownloadURLInputRequested += OnDownloadURLInputRequested;
        viewModel.GitHubTokenRequested = PromptForGitHubTokenAsync;

        var dropTarget = this.FindControl<Border>("DropTarget");
        if (dropTarget != null)
        {
            dropTarget.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            dropTarget.AddHandler(DragDrop.DropEvent, OnDrop);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = HasDroppedFiles(e.DataTransfer)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (_viewModel == null) return;

        foreach (var item in GetDroppedStorageItems(e.DataTransfer))
        {
            AddStorageItemToUploadList(item, _viewModel);
        }
    }

    internal static bool HasDroppedFiles(IDataTransfer dataTransfer) =>
        dataTransfer.TryGetFiles()?.Any() == true || dataTransfer.Formats.Contains(DataFormat.File);

    internal static IReadOnlyList<IStorageItem> GetDroppedStorageItems(IDataTransfer dataTransfer)
    {
        var droppedItems = dataTransfer.TryGetFiles()?.ToList() ?? new List<IStorageItem>();

        // Fallback for providers that expose files only through raw DataTransfer items.
        if (droppedItems.Count == 0)
        {
            foreach (var item in dataTransfer.Items)
            {
                if (item.TryGetRaw(DataFormat.File) is IStorageItem storageItem)
                {
                    droppedItems.Add(storageItem);
                }
            }
        }

        return droppedItems;
    }

    private static void AddStorageItemToUploadList(IStorageItem item, UploadContentViewModel viewModel)
    {
        var path = item.TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (item is IStorageFile)
        {
            viewModel.AddFileItem(path);
        }
        else if (item is IStorageFolder)
        {
            viewModel.AddFolderFiles(path);
        }
    }

    private async void OnFilePickerRequested(object? sender, EventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select files to upload",
            AllowMultiple = true
        });

        if (_viewModel != null && files.Count > 0)
        {
            foreach (var file in files)
            {
                var path = file.TryGetLocalPath();
                if (!string.IsNullOrEmpty(path))
                {
                    _viewModel.AddFileItem(path);
                }
            }
        }
    }

    private async void OnFolderPickerRequested(object? sender, EventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select folder to upload",
            AllowMultiple = false
        });

        if (_viewModel != null && folders.Count > 0)
        {
            var path = folders[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                _viewModel.AddFolderFiles(path);
            }
        }
    }

    private async void OnTextInputRequested(object? sender, EventArgs e)
    {
        var viewModel = new XerahS.UI.ViewModels.SimplePromptViewModel
        {
            Title = "Enter Text",
            Label = "Text to upload",
            ShowCancel = true,
            ShowInput = true,
            PrimaryButtonText = "OK"
        };

        var ok = await XerahS.UI.Services.ModalDialogHost.ShowAsync(
            viewModel,
            set => viewModel.CloseRequested = set,
            dismissResult: false,
            debugSource: "UploadContent.Enter Text");

        if (_viewModel != null && ok && !string.IsNullOrEmpty(viewModel.AcceptedInput))
        {
            _viewModel.AddTextItem(viewModel.AcceptedInput);
        }
    }

    private async void OnURLInputRequested(object? sender, EventArgs e)
    {
        var viewModel = new XerahS.UI.ViewModels.SimplePromptViewModel
        {
            Title = "Enter URL",
            Label = "https://...",
            ShowCancel = true,
            ShowInput = true,
            PrimaryButtonText = "OK"
        };

        var ok = await XerahS.UI.Services.ModalDialogHost.ShowAsync(
            viewModel,
            set => viewModel.CloseRequested = set,
            dismissResult: false,
            debugSource: "UploadContent.Enter URL");

        if (_viewModel != null && ok && !string.IsNullOrEmpty(viewModel.AcceptedInput))
        {
            _viewModel.AddURLItem(viewModel.AcceptedInput);
        }
    }

    private async void OnDownloadURLInputRequested(object? sender, EventArgs e)
    {
        var viewModel = new XerahS.UI.ViewModels.SimplePromptViewModel
        {
            Title = "Upload from URL",
            Message = "XerahS downloads the file and uploads it to your file destination. GitHub file links are fetched as the raw file.",
            Label = "https://...",
            ShowCancel = true,
            ShowInput = true,
            PrimaryButtonText = "Download"
        };

        var ok = await XerahS.UI.Services.ModalDialogHost.ShowAsync(
            viewModel,
            set => viewModel.CloseRequested = set,
            dismissResult: false,
            debugSource: "UploadContent.Upload from URL");

        if (_viewModel != null && ok && !string.IsNullOrWhiteSpace(viewModel.AcceptedInput))
        {
            await _viewModel.AddDownloadItemAsync(viewModel.AcceptedInput);
        }
    }

    private async Task<string?> PromptForGitHubTokenAsync(string reason)
    {
        var viewModel = new XerahS.UI.ViewModels.SimplePromptViewModel
        {
            Title = "GitHub token",
            Message = reason + " Enter a token with read access to the repository contents. It is saved in your keyring.",
            Label = "GitHub token",
            ShowCancel = true,
            ShowInput = true,
            IsPassword = true,
            PrimaryButtonText = "Download"
        };

        var ok = await XerahS.UI.Services.ModalDialogHost.ShowAsync(
            viewModel,
            set => viewModel.CloseRequested = set,
            dismissResult: false,
            debugSource: "UploadContent.GitHub token");

        return ok ? viewModel.AcceptedInput : null;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (_viewModel != null)
        {
            _viewModel.FilePickerRequested -= OnFilePickerRequested;
            _viewModel.FolderPickerRequested -= OnFolderPickerRequested;
            _viewModel.TextInputRequested -= OnTextInputRequested;
            _viewModel.URLInputRequested -= OnURLInputRequested;
            _viewModel.DownloadURLInputRequested -= OnDownloadURLInputRequested;
            _viewModel.GitHubTokenRequested = null;

            _viewModel.Dispose();
            _viewModel = null;
        }
    }
}
