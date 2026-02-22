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
using XerahS.Common;
using XerahS.Core;
using XerahS.Media;

namespace XerahS.UI.ViewModels;

public partial class VideoThumbnailerViewModel : ViewModelBase
{
    [ObservableProperty]
    private string? _inputFilePath;

    [ObservableProperty]
    private string _inputFileName = "";

    [ObservableProperty]
    private int _thumbnailCount = 9;

    [ObservableProperty]
    private int _columnCount = 3;

    [ObservableProperty]
    private int _maxThumbnailWidth = 512;

    [ObservableProperty]
    private bool _combineScreenshots = true;

    [ObservableProperty]
    private bool _addVideoInfo = true;

    [ObservableProperty]
    private bool _addTimestamp = true;

    [ObservableProperty]
    private string _outputFolder = "";

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusText = "Select a video file";

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private bool _hasInput;

    public event EventHandler? FilePickerRequested;
    public event EventHandler? FolderPickerRequested;

    public VideoThumbnailerViewModel()
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

        InputFilePath = filePath;
        InputFileName = Path.GetFileName(filePath);
        HasInput = true;
        StatusText = InputFileName;
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (string.IsNullOrEmpty(InputFilePath) || IsProcessing) return;

        string ffmpegPath = PathsManager.GetFFmpegPath();
        if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
        {
            StatusText = "FFmpeg not found. Please download FFmpeg first.";
            return;
        }

        IsProcessing = true;
        ProgressPercent = 0;
        StatusText = "Generating thumbnails...";

        try
        {
            string inputPath = InputFilePath;
            string outputDir = OutputFolder;

            var options = new XerahS.Media.VideoThumbnailOptions
            {
                ThumbnailCount = ThumbnailCount,
                ColumnCount = ColumnCount,
                MaxThumbnailWidth = MaxThumbnailWidth,
                CombineScreenshots = CombineScreenshots,
                AddVideoInfo = AddVideoInfo,
                AddTimestamp = AddTimestamp,
                DefaultOutputDirectory = outputDir,
                OutputLocation = XerahS.Media.ThumbnailLocationType.DefaultFolder,
                OpenDirectory = false
            };

            var thumbnails = await Task.Run(() =>
            {
                FileHelpers.CreateDirectory(outputDir);

                var thumbnailer = new VideoThumbnailer(ffmpegPath, options);
                thumbnailer.ProgressChanged += (current, total) =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        ProgressPercent = (int)((double)current / total * 100);
                        StatusText = $"Generating thumbnail {current}/{total}...";
                    });
                };

                return thumbnailer.TakeThumbnails(inputPath);
            });

            if (thumbnails.Count > 0)
            {
                ProgressPercent = 100;
                StatusText = $"Generated {thumbnails.Count} thumbnail(s)";
                FileHelpers.OpenFolderWithFile(thumbnails[0].FilePath);
            }
            else
            {
                StatusText = "No thumbnails generated. Check video file.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            DebugHelper.WriteException(ex, "VideoThumbnailer");
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
        HasInput = false;
        ProgressPercent = 0;
        StatusText = "Select a video file";
    }
}
