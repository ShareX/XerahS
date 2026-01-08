#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.ScreenCapture.ScreenRecording;

namespace XerahS.UI.ViewModels;

/// <summary>
/// ViewModel for screen recording controls
/// Manages recording state and provides commands for UI binding
/// </summary>
public partial class RecordingViewModel : ViewModelBase, IDisposable
{
    private readonly ScreenRecorderService _recorderService;
    private readonly System.Timers.Timer _durationTimer;
    private bool _disposed;

    /// <summary>
    /// Singleton instance for easy access from UI
    /// </summary>
    public static RecordingViewModel? Current { get; private set; }

    [ObservableProperty]
    private RecordingStatus _status = RecordingStatus.Idle;

    [ObservableProperty]
    private TimeSpan _duration = TimeSpan.Zero;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private bool _canStart = true;

    [ObservableProperty]
    private bool _canStop;

    [ObservableProperty]
    private string? _lastError;

    [ObservableProperty]
    private string? _outputFilePath;

    // Stage 3: Recording settings
    [ObservableProperty]
    private int _fps = 30;

    [ObservableProperty]
    private int _bitrateKbps = 4000;

    [ObservableProperty]
    private VideoCodec _codec = VideoCodec.H264;

    [ObservableProperty]
    private bool _showCursor = true;

    /// <summary>
    /// Available codecs for selection
    /// </summary>
    public List<VideoCodec> AvailableCodecs { get; } = new()
    {
        VideoCodec.H264,
        VideoCodec.HEVC,
        VideoCodec.VP9,
        VideoCodec.AV1
    };

    /// <summary>
    /// Available FPS options
    /// </summary>
    public List<int> AvailableFPS { get; } = new() { 15, 24, 30, 60, 120 };

    /// <summary>
    /// Available bitrate options (in kbps)
    /// </summary>
    public List<int> AvailableBitrates { get; } = new() { 1000, 2000, 4000, 8000, 16000, 32000 };

    /// <summary>
    /// Encoder information for display
    /// Stage 3: Hardware encoder detection
    /// </summary>
    public string EncoderInfo
    {
        get
        {
            // Simple platform check - detailed detection happens at runtime
            if (OperatingSystem.IsWindows() && Environment.OSVersion.Version.Build >= 17134)
            {
                return "Modern recording available (Windows.Graphics.Capture + Media Foundation). Hardware encoding will be used if available.";
            }
            else if (OperatingSystem.IsWindows())
            {
                return "Using FFmpeg fallback for recording (requires Windows 10 1803+ for native recording).";
            }
            else
            {
                return "Platform-specific recording support not yet implemented. FFmpeg fallback will be used.";
            }
        }
    }

    public RecordingViewModel()
    {
        Current = this;
        _recorderService = new ScreenRecorderService();
        _recorderService.StatusChanged += OnStatusChanged;
        _recorderService.ErrorOccurred += OnErrorOccurred;

        // Timer to update duration display
        _durationTimer = new System.Timers.Timer(100); // Update every 100ms
        _durationTimer.Elapsed += OnDurationTimerElapsed;
    }

    private void OnStatusChanged(object? sender, RecordingStatusEventArgs e)
    {
        // Update properties on UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Status = e.Status;
            Duration = e.Duration;

            switch (e.Status)
            {
                case RecordingStatus.Idle:
                    StatusText = "Ready";
                    IsRecording = false;
                    CanStart = true;
                    CanStop = false;
                    _durationTimer.Stop();
                    break;

                case RecordingStatus.Initializing:
                    StatusText = "Initializing...";
                    IsRecording = false;
                    CanStart = false;
                    CanStop = false;
                    break;

                case RecordingStatus.Recording:
                    StatusText = "Recording";
                    IsRecording = true;
                    CanStart = false;
                    CanStop = true;
                    _durationTimer.Start();
                    break;

                case RecordingStatus.Finalizing:
                    StatusText = "Finalizing...";
                    IsRecording = false;
                    CanStart = false;
                    CanStop = false;
                    _durationTimer.Stop();
                    break;

                case RecordingStatus.Error:
                    StatusText = "Error";
                    IsRecording = false;
                    CanStart = true;
                    CanStop = false;
                    _durationTimer.Stop();
                    break;
            }
        });
    }

    private void OnErrorOccurred(object? sender, RecordingErrorEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            LastError = e.Error.Message;
            DebugHelper.WriteException(e.Error, "Recording error");

            if (e.IsFatal)
            {
                StatusText = $"Error: {e.Error.Message}";
            }
        });
    }

    private void OnDurationTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // Duration is updated by the status event, but we can force refresh here
        OnPropertyChanged(nameof(DurationFormatted));
    }

    /// <summary>
    /// Formatted duration string for display (MM:SS or HH:MM:SS)
    /// </summary>
    public string DurationFormatted
    {
        get
        {
            if (Duration.TotalHours >= 1)
            {
                return Duration.ToString(@"hh\:mm\:ss");
            }
            return Duration.ToString(@"mm\:ss");
        }
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartRecordingAsync()
    {
        try
        {
            LastError = null;

            var options = new RecordingOptions
            {
                Mode = CaptureMode.Screen,
                Settings = new ScreenRecordingSettings
                {
                    FPS = Fps,
                    BitrateKbps = BitrateKbps,
                    Codec = Codec,
                    ShowCursor = ShowCursor
                }
            };

            // Generate output path
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string recordingsPath = Path.Combine(documentsPath, "ShareX", "Recordings", DateTime.Now.ToString("yyyy-MM"));
            Directory.CreateDirectory(recordingsPath);
            options.OutputPath = Path.Combine(recordingsPath, $"Recording_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4");
            OutputFilePath = options.OutputPath;

            DebugHelper.WriteLine($"Starting recording: {Codec} @ {Fps}fps, {BitrateKbps}kbps, Cursor={ShowCursor}");
            DebugHelper.WriteLine($"Output path: {options.OutputPath}");

            await _recorderService.StartRecordingAsync(options);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to start recording");
            LastError = ex.Message;
            StatusText = "Failed to start";
            CanStart = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopRecordingAsync()
    {
        try
        {
            DebugHelper.WriteLine("Stopping recording...");
            await _recorderService.StopRecordingAsync();
            DebugHelper.WriteLine($"Recording saved to: {OutputFilePath}");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to stop recording");
            LastError = ex.Message;
        }
    }

    partial void OnCanStartChanged(bool value)
    {
        StartRecordingCommand.NotifyCanExecuteChanged();
    }

    partial void OnCanStopChanged(bool value)
    {
        StopRecordingCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _durationTimer.Stop();
        _durationTimer.Dispose();

        _recorderService.StatusChanged -= OnStatusChanged;
        _recorderService.ErrorOccurred -= OnErrorOccurred;
        _recorderService.Dispose();

        GC.SuppressFinalize(this);
    }
}
