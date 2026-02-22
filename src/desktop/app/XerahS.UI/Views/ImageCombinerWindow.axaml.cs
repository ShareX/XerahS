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

public partial class ImageCombinerWindow : Window
{
    private ImageCombinerViewModel? _viewModel;

    public ImageCombinerWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    public ImageCombinerViewModel? ViewModel => _viewModel;

    public void Initialize(ImageCombinerViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.FilePickerRequested += OnFilePickerRequested;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (_viewModel == null) return;

        foreach (var item in e.DataTransfer.Items)
        {
            if (item.TryGetRaw(DataFormat.File) is IStorageFile file)
            {
                var path = file.Path.LocalPath;
                if (!string.IsNullOrEmpty(path))
                {
                    _viewModel.AddFileItem(path);
                }
            }
        }
    }

    private async void OnFilePickerRequested(object? sender, EventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select images to combine",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Image files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp", "*.tiff" } },
                FilePickerFileTypes.All
            }
        });

        if (_viewModel != null && files.Count > 0)
        {
            foreach (var file in files)
            {
                var path = file.Path.LocalPath;
                if (!string.IsNullOrEmpty(path))
                {
                    _viewModel.AddFileItem(path);
                }
            }
        }
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
            _viewModel = null;
        }
    }
}
