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
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Ava.ViewModels;

namespace Ava.Views;

public partial class MobileUploadView : UserControl
{
    public MobileUploadView()
    {
        InitializeComponent();
    }

    private async void OnChooseFilesClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MobileUploadViewModel vm)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        var storageProvider = topLevel?.StorageProvider;
        if (storageProvider == null || !storageProvider.CanOpen)
        {
            vm.StatusText = "File picker is not available on this device.";
            return;
        }

        IReadOnlyList<IStorageFile> files;

        try
        {
            files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose photos or files",
                AllowMultiple = true,
                FileTypeFilter = [FilePickerFileTypes.ImageAll, FilePickerFileTypes.All]
            });
        }
        catch (Exception ex)
        {
            vm.StatusText = $"Could not open picker: {ex.Message}";
            return;
        }

        if (files.Count == 0)
        {
            vm.StatusText = "No files selected.";
            return;
        }

        var localPaths = new List<string>(files.Count);

        foreach (var file in files)
        {
            if (file.Path is { IsFile: true } uri)
            {
                localPaths.Add(uri.LocalPath);
                continue;
            }

            try
            {
                await using var source = await file.OpenReadAsync();
                var extension = Path.GetExtension(file.Name);
                var tempPath = Path.Combine(Path.GetTempPath(), $"xerahs_mobile_{Guid.NewGuid():N}{extension}");
                await using var target = File.Create(tempPath);
                await source.CopyToAsync(target);
                localPaths.Add(tempPath);
            }
            catch (Exception ex)
            {
                vm.StatusText = $"Failed to read {file.Name}: {ex.Message}";
            }
        }

        if (localPaths.Count > 0)
        {
            vm.ProcessFiles(localPaths.ToArray());
        }
    }
}
