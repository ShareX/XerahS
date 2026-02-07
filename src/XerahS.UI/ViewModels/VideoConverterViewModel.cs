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

public partial class VideoConverterViewModel : ViewModelBase
{
    private FFmpegCLIManager? _ffmpeg;

    [ObservableProperty]
    private string? _inputFilePath;

    [ObservableProperty]
    private string _inputFileName = "";

    [ObservableProperty]
    private string _outputFolder = "";

    [ObservableProperty]
    private string _outputFileName = "";

    [ObservableProperty]
    private XerahS.Media.ConverterVideoCodecs _selectedCodec = XerahS.Media.ConverterVideoCodecs.x264;

    [ObservableProperty]
    private int _videoQuality = 23;

    [ObservableProperty]
    private bool _videoQualityUseBitrate;

    [ObservableProperty]
    private int _videoQualityBitrate = 3000;

    [ObservableProperty]
    private bool _useCustomArguments;

    [ObservableProperty]
    private string _customArguments = "";

    [ObservableProperty]
    private bool _isConverting;

    [ObservableProperty]
    private string _statusText = "Select a video file to convert";

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private string _previewArgs = "";

    [ObservableProperty]
    private bool _hasInput;

    public event EventHandler? FilePickerRequested;
    public event EventHandler? FolderPickerRequested;

    public VideoConverterViewModel()
    {
        OutputFolder = TaskHelpers.GetScreenshotsFolder();
    }

    partial void OnSelectedCodecChanged(XerahS.Media.ConverterVideoCodecs value) => UpdatePreviewArgs();
    partial void OnVideoQualityChanged(int value) => UpdatePreviewArgs();
    partial void OnVideoQualityUseBitrateChanged(bool value) => UpdatePreviewArgs();
    partial void OnVideoQualityBitrateChanged(int value) => UpdatePreviewArgs();
    partial void OnUseCustomArgumentsChanged(bool value) => UpdatePreviewArgs();
    partial void OnCustomArgumentsChanged(string value) => UpdatePreviewArgs();

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
        OutputFileName = Path.GetFileNameWithoutExtension(filePath);
        HasInput = true;
        StatusText = InputFileName;
        UpdatePreviewArgs();
    }

    private void UpdatePreviewArgs()
    {
        if (string.IsNullOrEmpty(InputFilePath)) return;

        var options = CreateOptions();
        PreviewArgs = options.Arguments;
    }

    private XerahS.Media.VideoConverterOptions CreateOptions()
    {
        return new XerahS.Media.VideoConverterOptions
        {
            InputFilePath = InputFilePath ?? "",
            OutputFolderPath = OutputFolder,
            OutputFileName = OutputFileName,
            VideoCodec = SelectedCodec,
            VideoQuality = VideoQuality,
            VideoQualityUseBitrate = VideoQualityUseBitrate,
            VideoQualityBitrate = VideoQualityBitrate,
            UseCustomArguments = UseCustomArguments,
            CustomArguments = CustomArguments
        };
    }

    [RelayCommand]
    private async Task ConvertAsync()
    {
        if (string.IsNullOrEmpty(InputFilePath) || IsConverting) return;

        string ffmpegPath = PathsManager.GetFFmpegPath();
        if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
        {
            StatusText = "FFmpeg not found. Please download FFmpeg first.";
            return;
        }

        IsConverting = true;
        ProgressPercent = 0;
        StatusText = "Converting...";

        try
        {
            var options = CreateOptions();
            string outputPath = options.OutputFilePath;

            bool result = await Task.Run(() =>
            {
                _ffmpeg = new FFmpegCLIManager(ffmpegPath)
                {
                    TrackEncodeProgress = true,
                    ShowError = false
                };

                _ffmpeg.EncodeProgressChanged += percentage =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        ProgressPercent = (int)Math.Clamp(percentage, 0, 99);
                        StatusText = $"Converting... {ProgressPercent}%";
                    });
                };

                return _ffmpeg.Run(options.Arguments);
            });

            if (result)
            {
                ProgressPercent = 100;
                StatusText = $"Completed: {Path.GetFileName(outputPath)}";

                if (File.Exists(outputPath))
                {
                    FileHelpers.OpenFolderWithFile(outputPath);
                }
            }
            else
            {
                StatusText = _ffmpeg?.StopRequested == true
                    ? "Conversion stopped"
                    : "Conversion failed";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            DebugHelper.WriteException(ex, "VideoConverter");
        }
        finally
        {
            _ffmpeg = null;
            IsConverting = false;
        }
    }

    [RelayCommand]
    private void Stop()
    {
        _ffmpeg?.Close();
    }

    [RelayCommand]
    private void Clear()
    {
        InputFilePath = null;
        InputFileName = "";
        OutputFileName = "";
        HasInput = false;
        ProgressPercent = 0;
        PreviewArgs = "";
        StatusText = "Select a video file to convert";
    }
}
