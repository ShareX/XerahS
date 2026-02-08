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
using SkiaSharp;
using XerahS.Common;
using XerahS.Core;

namespace XerahS.UI.ViewModels;

public partial class ImageSplitterViewModel : ViewModelBase
{
    [ObservableProperty]
    private string? _inputFilePath;

    [ObservableProperty]
    private string _inputFileName = "";

    [ObservableProperty]
    private int _columns = 2;

    [ObservableProperty]
    private int _rows = 2;

    [ObservableProperty]
    private string _outputFolder = "";

    [ObservableProperty]
    private int _imageWidth;

    [ObservableProperty]
    private int _imageHeight;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusText = "Select an image to split";

    [ObservableProperty]
    private bool _hasInput;

    public event EventHandler? FilePickerRequested;
    public event EventHandler? FolderPickerRequested;

    public ImageSplitterViewModel()
    {
        OutputFolder = TaskHelpers.GetScreenshotsFolder();
    }

    [RelayCommand]
    private void BrowseInput()
    {
        FilePickerRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void BrowseOutput()
    {
        FolderPickerRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SetInputFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        try
        {
            using var bmp = ImageHelpers.LoadBitmap(filePath);
            if (bmp == null) return;

            InputFilePath = filePath;
            InputFileName = Path.GetFileName(filePath);
            ImageWidth = bmp.Width;
            ImageHeight = bmp.Height;
            HasInput = true;
            StatusText = $"{InputFileName} ({ImageWidth} x {ImageHeight})";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading image: {ex.Message}";
            HasInput = false;
        }
    }

    [RelayCommand]
    private async Task SplitAsync()
    {
        if (string.IsNullOrEmpty(InputFilePath) || IsProcessing) return;
        if (Rows < 1 || Columns < 1) return;

        IsProcessing = true;
        StatusText = "Splitting image...";

        try
        {
            string inputPath = InputFilePath;
            int rows = Rows;
            int cols = Columns;
            string outputDir = OutputFolder;

            var outputPath = await Task.Run(() =>
            {
                FileHelpers.CreateDirectory(outputDir);

                using var sourceBmp = ImageHelpers.LoadBitmap(inputPath);
                if (sourceBmp == null) return null;

                string baseName = Path.GetFileNameWithoutExtension(inputPath);
                int cellWidth = sourceBmp.Width / cols;
                int cellHeight = sourceBmp.Height / rows;
                string? firstOutput = null;

                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        int x = c * cellWidth;
                        int y = r * cellHeight;

                        // Last column/row takes remaining pixels
                        int w = (c == cols - 1) ? sourceBmp.Width - x : cellWidth;
                        int h = (r == rows - 1) ? sourceBmp.Height - y : cellHeight;

                        using var piece = ImageHelpers.Crop(sourceBmp, x, y, w, h);
                        string fileName = $"{baseName}_{r + 1}_{c + 1}.png";
                        string filePath = Path.Combine(outputDir, fileName);

                        ImageHelpers.SaveBitmap(piece, filePath);

                        firstOutput ??= filePath;
                    }
                }

                return firstOutput;
            });

            if (outputPath != null)
            {
                int total = Rows * Columns;
                StatusText = $"Split into {total} pieces";
                FileHelpers.OpenFolderWithFile(outputPath);
            }
            else
            {
                StatusText = "Failed to split image";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            DebugHelper.WriteException(ex, "ImageSplitter");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void Clear()
    {
        InputFilePath = null;
        InputFileName = "";
        ImageWidth = 0;
        ImageHeight = 0;
        HasInput = false;
        StatusText = "Select an image to split";
    }
}
