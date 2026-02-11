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

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using XerahS.Common;
using XerahS.Core;
using XerahS.Media;

namespace XerahS.UI.ViewModels;

public partial class ImageCombinerInputItem : ObservableObject
{
    [ObservableProperty]
    private string _filePath = "";

    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private string _dimensions = "";
}

public partial class ImageCombinerViewModel : ViewModelBase
{
    public ObservableCollection<ImageCombinerInputItem> Items { get; } = new();

    [ObservableProperty]
    private ImageCombinerInputItem? _selectedItem;

    [ObservableProperty]
    private ImageCombinerOrientation _orientation = ImageCombinerOrientation.Vertical;

    [ObservableProperty]
    private XerahS.Media.ImageCombinerAlignment _alignment = XerahS.Media.ImageCombinerAlignment.Center;

    [ObservableProperty]
    private int _spacing;

    [ObservableProperty]
    private bool _autoFillBackground;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusText = "Add images to combine";

    public event EventHandler? FilePickerRequested;

    [RelayCommand]
    private void AddFiles()
    {
        FilePickerRequested?.Invoke(this, EventArgs.Empty);
    }

    public void AddFileItem(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        string dimensions = "";
        try
        {
            using var bmp = ImageHelpers.LoadBitmap(filePath);
            if (bmp != null)
            {
                dimensions = $"{bmp.Width} x {bmp.Height}";
            }
        }
        catch
        {
            // Not a valid image
            return;
        }

        Items.Add(new ImageCombinerInputItem
        {
            FilePath = filePath,
            DisplayName = Path.GetFileName(filePath),
            Dimensions = dimensions
        });

        UpdateStatus();
    }

    [RelayCommand]
    private void RemoveItem(ImageCombinerInputItem? item)
    {
        if (item == null) return;
        Items.Remove(item);
        UpdateStatus();
    }

    [RelayCommand]
    private void MoveUp(ImageCombinerInputItem? item)
    {
        if (item == null) return;
        int index = Items.IndexOf(item);
        if (index > 0)
        {
            Items.Move(index, index - 1);
        }
    }

    [RelayCommand]
    private void MoveDown(ImageCombinerInputItem? item)
    {
        if (item == null) return;
        int index = Items.IndexOf(item);
        if (index >= 0 && index < Items.Count - 1)
        {
            Items.Move(index, index + 1);
        }
    }

    [RelayCommand]
    private async Task CombineAsync()
    {
        if (Items.Count < 2 || IsProcessing) return;

        IsProcessing = true;
        StatusText = "Combining images...";

        try
        {
            var filePaths = Items.Select(i => i.FilePath).ToArray();

            var result = await Task.Run(() =>
            {
                var combiner = new ImageCombiner();
                var bitmaps = new List<SKBitmap>();

                try
                {
                    foreach (var path in filePaths)
                    {
                        var bmp = ImageHelpers.LoadBitmap(path);
                        if (bmp != null)
                        {
                            bitmaps.Add(bmp);
                        }
                    }

                    if (bitmaps.Count < 2) return null;

                    SKColor? bgColor = AutoFillBackground ? SKColors.White : null;
                    return combiner.Combine(bitmaps, Orientation, Spacing, Alignment, bgColor);
                }
                finally
                {
                    foreach (var bmp in bitmaps)
                    {
                        bmp.Dispose();
                    }
                }
            });

            if (result != null)
            {
                using (result)
                {
                    string outputFolder = TaskHelpers.GetScreenshotsFolder();
                    FileHelpers.CreateDirectory(outputFolder);
                    string fileName = $"Combined_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    string outputPath = Path.Combine(outputFolder, fileName);

                    ImageHelpers.SaveBitmap(result, outputPath);

                    StatusText = $"Saved: {fileName}";
                    FileHelpers.OpenFolderWithFile(outputPath);
                }
            }
            else
            {
                StatusText = "Failed to combine images";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            DebugHelper.WriteException(ex, "ImageCombiner");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void Clear()
    {
        Items.Clear();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        StatusText = Items.Count == 0
            ? "Add images to combine"
            : $"{Items.Count} images";
    }
}
