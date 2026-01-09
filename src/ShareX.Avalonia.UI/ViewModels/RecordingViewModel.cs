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

using System.Linq;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Core.Managers;
using XerahS.ScreenCapture.ScreenRecording;
using HotkeyInfo = XerahS.Platform.Abstractions.HotkeyInfo;

namespace XerahS.UI.ViewModels;

/// <summary>
/// ViewModel for screen recording controls
/// Manages recording state and provides commands for UI binding
/// Stage 5: Updated to use ScreenRecordingManager for shared state
/// </summary>
public partial class RecordingViewModel : ViewModelBase, IDisposable
{
    private readonly System.Timers.Timer _durationTimer;
    private WorkflowSettings _workflow;
    private TaskSettings _taskSettings;
    private bool _disposed;
    private bool _initialized;

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

    // Stage 6: Audio settings
    [ObservableProperty]
    private bool _captureSystemAudio = false;

    [ObservableProperty]
    private bool _captureMicrophone = false;

    [ObservableProperty]
    private RecordingIntent _recordingIntent = RecordingIntent.Default;

    /// <summary>
    /// Available recording intents
    /// </summary>
    public List<RecordingIntent> AvailableRecordingIntents { get; } = Enum.GetValues(typeof(RecordingIntent)).Cast<RecordingIntent>().ToList();

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

        InitializeWorkflow();

        // Subscribe to global recording manager events
        ScreenRecordingManager.Instance.StatusChanged += OnStatusChanged;
        ScreenRecordingManager.Instance.ErrorOccurred += OnErrorOccurred;

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

            if (!_initialized)
            {
                InitializeWorkflow();
            }

            // Update workflow TaskSettings from UI selections
            SyncSettingsToWorkflow();
            SettingManager.SaveWorkflowsConfigAsync();

            DebugHelper.WriteLine($"Starting recording (workflow: {_workflow?.Name ?? "unnamed"}): {Codec} @ {Fps}fps, {BitrateKbps}kbps, Cursor={ShowCursor}, Intent={RecordingIntent}");
            DebugHelper.WriteLine($"  Audio: SystemAudio={CaptureSystemAudio}, Microphone={CaptureMicrophone}");

            // Use unified pipeline through TaskHelpers.ExecuteWorkflow
            // This ensures recording goes through the same path as hotkey triggers
            await Core.Helpers.TaskHelpers.ExecuteWorkflow(_workflow);
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
            // Use global recording manager (Stage 5)
            await ScreenRecordingManager.Instance.StopRecordingAsync();
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

    private void InitializeWorkflow()
    {
        var workflow = SettingManager.WorkflowsConfig.Hotkeys.FirstOrDefault(w => w.Job == HotkeyType.ScreenRecorder);
        if (workflow == null)
        {
            workflow = new WorkflowSettings(HotkeyType.ScreenRecorder, new HotkeyInfo())
            {
                Name = "Screen Recorder (auto)"
            };

            SettingManager.WorkflowsConfig.Hotkeys.Add(workflow);
            SettingManager.SaveWorkflowsConfigAsync();
        }

        _workflow = workflow;
        _taskSettings = _workflow.TaskSettings ?? new TaskSettings();
        _workflow.TaskSettings = _taskSettings;

        var recordingSettings = _taskSettings.CaptureSettings.ScreenRecordingSettings;

        // Seed UI from workflow settings
        Fps = recordingSettings.FPS;
        BitrateKbps = recordingSettings.BitrateKbps;
        Codec = recordingSettings.Codec;
        ShowCursor = recordingSettings.ShowCursor;
        CaptureSystemAudio = recordingSettings.CaptureSystemAudio;
        CaptureMicrophone = recordingSettings.CaptureMicrophone;
        RecordingIntent = recordingSettings.RecordingIntent;
        OutputFilePath = null;
        _initialized = true;
    }

    private void SyncSettingsToWorkflow()
    {
        var recordingSettings = _taskSettings.CaptureSettings.ScreenRecordingSettings;

        recordingSettings.FPS = Fps;
        recordingSettings.BitrateKbps = BitrateKbps;
        recordingSettings.Codec = Codec;
        recordingSettings.ShowCursor = ShowCursor;
        recordingSettings.CaptureSystemAudio = CaptureSystemAudio;
        recordingSettings.CaptureMicrophone = CaptureMicrophone;
        recordingSettings.RecordingIntent = RecordingIntent;
        recordingSettings.ForceFFmpeg = CaptureSystemAudio || CaptureMicrophone;
    }

    private CaptureMode ResolveCaptureMode()
    {
        return _workflow.Job switch
        {
            HotkeyType.ScreenRecorderActiveWindow => CaptureMode.Window,
            HotkeyType.ScreenRecorderCustomRegion => CaptureMode.Region,
            _ => CaptureMode.Screen
        };
    }

    private string ResolveOutputPath()
    {
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string baseFolder = Path.Combine(documentsPath, "ShareX", "Recordings", DateTime.Now.ToString("yyyy-MM"));

        Directory.CreateDirectory(baseFolder);
        string fileName = $"Recording_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4";
        return Path.Combine(baseFolder, fileName);
    }

    partial void OnFpsChanged(int value)
    {
        if (!_initialized) return;
        _taskSettings.CaptureSettings.ScreenRecordingSettings.FPS = value;
    }

    partial void OnBitrateKbpsChanged(int value)
    {
        if (!_initialized) return;
        _taskSettings.CaptureSettings.ScreenRecordingSettings.BitrateKbps = value;
    }

    partial void OnCodecChanged(VideoCodec value)
    {
        if (!_initialized) return;
        _taskSettings.CaptureSettings.ScreenRecordingSettings.Codec = value;
    }

    partial void OnShowCursorChanged(bool value)
    {
        if (!_initialized) return;
        _taskSettings.CaptureSettings.ScreenRecordingSettings.ShowCursor = value;
    }

    partial void OnCaptureSystemAudioChanged(bool value)
    {
        if (!_initialized) return;
        _taskSettings.CaptureSettings.ScreenRecordingSettings.CaptureSystemAudio = value;
        _taskSettings.CaptureSettings.ScreenRecordingSettings.ForceFFmpeg = value || CaptureMicrophone;
    }

    partial void OnCaptureMicrophoneChanged(bool value)
    {
        if (!_initialized) return;
        _taskSettings.CaptureSettings.ScreenRecordingSettings.CaptureMicrophone = value;
        _taskSettings.CaptureSettings.ScreenRecordingSettings.ForceFFmpeg = value || CaptureSystemAudio;
    }

    partial void OnRecordingIntentChanged(RecordingIntent value)
    {
        if (!_initialized) return;
        _taskSettings.CaptureSettings.ScreenRecordingSettings.RecordingIntent = value;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _durationTimer.Stop();
        _durationTimer.Dispose();

        // Unsubscribe from global recording manager events (Stage 5)
        ScreenRecordingManager.Instance.StatusChanged -= OnStatusChanged;
        ScreenRecordingManager.Instance.ErrorOccurred -= OnErrorOccurred;

        GC.SuppressFinalize(this);
    }
}
