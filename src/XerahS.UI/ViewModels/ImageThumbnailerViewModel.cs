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
using XerahS.Common;
using XerahS.Core;

namespace XerahS.UI.ViewModels;

public partial class ImageThumbnailerViewModel : ViewModelBase
{
    public ObservableCollection<string> InputFiles { get; } = new();

    [ObservableProperty]
    private string? _selectedFile;

    [ObservableProperty]
    private int _thumbnailWidth = 200;

    [ObservableProperty]
    private int _thumbnailHeight;

    [ObservableProperty]
    private string _outputFolder = "";

    [ObservableProperty]
    private int _quality = 85;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusText = "Add images to generate thumbnails";

    [ObservableProperty]
    private int _progressPercent;

    public event EventHandler? FilePickerRequested;
    public event EventHandler? FolderPickerRequested;

    public ImageThumbnailerViewModel()
    {
        string screenshotsFolder = TaskHelpers.GetScreenshotsFolder();
        OutputFolder = Path.Combine(screenshotsFolder, "Thumbnails");
    }

    [RelayCommand]
    private void AddFiles()
    {
        FilePickerRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void BrowseOutput()
    {
        FolderPickerRequested?.Invoke(this, EventArgs.Empty);
    }

    public void AddFileItem(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;
        if (InputFiles.Contains(filePath)) return;

        InputFiles.Add(filePath);
        UpdateStatus();
    }

    [RelayCommand]
    private void RemoveFile(string? filePath)
    {
        if (filePath == null) return;
        InputFiles.Remove(filePath);
        UpdateStatus();
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (InputFiles.Count == 0 || IsProcessing) return;

        IsProcessing = true;
        ProgressPercent = 0;
        StatusText = "Generating thumbnails...";

        try
        {
            var files = InputFiles.ToArray();
            int width = ThumbnailWidth;
            int height = ThumbnailHeight;
            int quality = Quality;
            string outputDir = OutputFolder;

            string? firstOutput = null;

            await Task.Run(() =>
            {
                FileHelpers.CreateDirectory(outputDir);

                for (int i = 0; i < files.Length; i++)
                {
                    using var bmp = ImageHelpers.LoadBitmap(files[i]);
                    if (bmp == null) continue;

                    using var resized = ImageHelpers.ResizeImage(bmp, width, height);
                    string baseName = Path.GetFileNameWithoutExtension(files[i]);
                    string fileName = $"{baseName}_thumb.jpg";
                    string outputPath = Path.Combine(outputDir, fileName);

                    // Handle existing files
                    if (File.Exists(outputPath))
                    {
                        outputPath = Path.Combine(outputDir, $"{baseName}_thumb_{DateTime.Now:HHmmss}.jpg");
                    }

                    ImageHelpers.SaveBitmap(resized, outputPath, quality);

                    firstOutput ??= outputPath;

                    int progress = (int)((double)(i + 1) / files.Length * 100);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => ProgressPercent = progress);
                }
            });

            StatusText = $"Generated {files.Length} thumbnails";
            ProgressPercent = 100;

            if (firstOutput != null)
            {
                FileHelpers.OpenFolderWithFile(firstOutput);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            DebugHelper.WriteException(ex, "ImageThumbnailer");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void Clear()
    {
        InputFiles.Clear();
        ProgressPercent = 0;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        StatusText = InputFiles.Count == 0
            ? "Add images to generate thumbnails"
            : $"{InputFiles.Count} images";
    }
}
