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

public partial class UploadContentWindow : Window
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

        var dropTarget = this.FindControl<Border>("DropTarget");
        if (dropTarget != null)
        {
            dropTarget.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            dropTarget.AddHandler(DragDrop.DropEvent, OnDrop);
        }
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
            else if (item.TryGetRaw(DataFormat.File) is IStorageFolder folder)
            {
                var path = folder.Path.LocalPath;
                if (!string.IsNullOrEmpty(path))
                {
                    _viewModel.AddFolderFiles(path);
                }
            }
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
                var path = file.Path.LocalPath;
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
            var path = folders[0].Path.LocalPath;
            if (!string.IsNullOrEmpty(path))
            {
                _viewModel.AddFolderFiles(path);
            }
        }
    }

    private async void OnTextInputRequested(object? sender, EventArgs e)
    {
        var dialog = new Window
        {
            Title = "Enter Text",
            Width = 450,
            Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new DockPanel
            {
                Margin = new Avalonia.Thickness(12)
            }
        };

        var textBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Watermark = "Enter text to upload..."
        };

        var okButton = new Button { Content = "OK", MinWidth = 80, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        var cancelButton = new Button { Content = "Cancel", MinWidth = 80, Margin = new Avalonia.Thickness(8, 0, 0, 0) };

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        DockPanel.SetDock(buttonPanel, Dock.Bottom);
        var panel = (DockPanel)dialog.Content;
        panel.Children.Add(buttonPanel);
        panel.Children.Add(textBox);

        string? result = null;
        okButton.Click += (_, _) => { result = textBox.Text; dialog.Close(); };
        cancelButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);

        if (_viewModel != null && !string.IsNullOrEmpty(result))
        {
            _viewModel.AddTextItem(result);
        }
    }

    private async void OnURLInputRequested(object? sender, EventArgs e)
    {
        var dialog = new Window
        {
            Title = "Enter URL",
            Width = 450,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new DockPanel
            {
                Margin = new Avalonia.Thickness(12)
            }
        };

        var textBox = new TextBox
        {
            Watermark = "https://..."
        };

        var okButton = new Button { Content = "OK", MinWidth = 80 };
        var cancelButton = new Button { Content = "Cancel", MinWidth = 80 };

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        DockPanel.SetDock(buttonPanel, Dock.Bottom);
        var panel = (DockPanel)dialog.Content;
        panel.Children.Add(buttonPanel);
        panel.Children.Add(textBox);

        string? result = null;
        okButton.Click += (_, _) => { result = textBox.Text; dialog.Close(); };
        cancelButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);

        if (_viewModel != null && !string.IsNullOrEmpty(result))
        {
            _viewModel.AddURLItem(result);
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
            _viewModel.FolderPickerRequested -= OnFolderPickerRequested;
            _viewModel.TextInputRequested -= OnTextInputRequested;
            _viewModel.URLInputRequested -= OnURLInputRequested;

            _viewModel.Dispose();
            _viewModel = null;
        }
    }
}
