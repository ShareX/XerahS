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

using System.IO;
using System.Collections.ObjectModel;
using System.Linq;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Bootstrap;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture.ScreenRecording;
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
    private WorkflowSettings _workflow = null!;
    private TaskSettings _taskSettings = null!;
    private readonly IScreenRecordingCoordinator _screenRecordingCoordinator;
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
    [NotifyCanExecuteChangedFor(nameof(MoveRecordingRegionCommand))]
    private bool _isPaused;

    [ObservableProperty]
    private bool _canStart = true;

    [ObservableProperty]
    private bool _canStop;

    [ObservableProperty]
    private bool _canPauseResume;

    [ObservableProperty]
    private bool _canAbort;

    [ObservableProperty]
    private string? _lastError;

    [ObservableProperty]
    private string? _outputFilePath;

    [ObservableProperty]
    private string _linuxRecordingDiagnosticsStatusText = string.Empty;

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
    private RecordingDevice? _selectedSystemAudioDevice;

    [ObservableProperty]
    private RecordingDevice? _selectedMicrophoneDevice;

    [ObservableProperty]
    private ScreenInfo? _selectedRecordingDisplay;

    [ObservableProperty]
    private float _systemAudioGain = 1;

    [ObservableProperty]
    private float _microphoneGain = 1;

    [ObservableProperty]
    private WindowRecordingMode _windowRecordingMode;

    [ObservableProperty]
    private bool _requireHardwareEncoder;

    [ObservableProperty]
    private bool _lowLatency = true;

    [ObservableProperty]
    private bool _hideCaptureBorder = true;

    [ObservableProperty]
    private bool _forceFfmpeg;

    [ObservableProperty]
    private string? _recordingDeviceError;


    public bool HasNativeRecordingSettings => PlatformServices.RecordingDevices != null;
    public ObservableCollection<RecordingDevice> SystemAudioDevices { get; } = new();
    public ObservableCollection<RecordingDevice> MicrophoneDevices { get; } = new();
    public ObservableCollection<ScreenInfo> RecordingDisplays { get; } = new();
    public IReadOnlyList<WindowRecordingMode> WindowRecordingModes { get; } = Enum.GetValues<WindowRecordingMode>();
    public bool CanMoveRecordingRegion => IsPaused && _screenRecordingCoordinator.CurrentCapabilities.SupportsRegionMove;

    [ObservableProperty]
    private RecordingIntent _recordingIntent = RecordingIntent.Default;

    [ObservableProperty]
    private string _codecAvailabilityMessage = string.Empty;

    /// <summary>
    /// Available recording intents
    /// </summary>
    public List<RecordingIntent> AvailableRecordingIntents { get; } = Enum.GetValues(typeof(RecordingIntent)).Cast<RecordingIntent>().ToList();

    /// <summary>
    /// Available codecs for selection
    /// </summary>
    public IReadOnlyList<VideoCodec> AvailableCodecs { get; } =
        RecordingCodecSupportPolicy.GetSelectableCodecs(IsFfmpegAvailableForAdvancedCodecs());

    /// <summary>
    /// Available FPS options
    /// </summary>
    public List<int> AvailableFPS { get; } = new() { 15, 24, 30, 60, 120 };

    /// <summary>
    /// Available bitrate options (in kbps)
    /// </summary>
    public List<int> AvailableBitrates { get; } = new() { 1000, 2000, 4000, 8000, 16000, 32000 };

    public bool IsLinuxPlatform => OperatingSystem.IsLinux();

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
                return IsFfmpegAvailableForAdvancedCodecs()
                    ? "Windows uses native capture with Media Foundation for H.264. HEVC, VP9, and AV1 automatically switch to the FFmpeg backend in this build."
                    : "Windows uses native capture with Media Foundation for H.264. Install FFmpeg to enable HEVC, VP9, and AV1 recording.";
            }
            else if (OperatingSystem.IsWindows())
            {
                return "Using FFmpeg fallback for recording (requires Windows 10 1803+ for native recording).";
            }
            else if (OperatingSystem.IsMacOS())
            {
                return IsFfmpegAvailableForAdvancedCodecs()
                    ? "macOS uses native recording for H.264. HEVC, VP9, and AV1 automatically switch to FFmpeg in this build."
                    : "macOS native recording currently covers H.264 only. Install FFmpeg to enable HEVC, VP9, and AV1 recording.";
            }
            else
            {
                return "Platform-specific recording support not yet implemented. FFmpeg fallback will be used.";
            }
        }
    }

    /// <summary>
    /// Platform-specific feature description
    /// </summary>
    public string FeatureDescription
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return "Record your screen to MP4 video using native Windows.Graphics.Capture";
            }
            else if (OperatingSystem.IsMacOS())
            {
                return "Record your screen to MP4 video using FFmpeg";
            }
            else if (OperatingSystem.IsLinux())
            {
                return "Record your screen to MP4 video using FFmpeg";
            }
            else
            {
                return "Record your screen to MP4 video";
            }
        }
    }

    /// <summary>
    /// Platform-specific usage notes
    /// </summary>
    public string UsageNotes
    {
        get
        {
            if (OperatingSystem.IsWindows() && Environment.OSVersion.Version.Build >= 17134)
            {
                return IsFfmpegAvailableForAdvancedCodecs()
                    ? "Note: H.264 records through Windows.Graphics.Capture + Media Foundation. HEVC, VP9, and AV1 are routed through FFmpeg automatically."
                    : "Note: H.264 records through Windows.Graphics.Capture + Media Foundation. Install FFmpeg to unlock HEVC, VP9, and AV1.";
            }
            else if (OperatingSystem.IsWindows())
            {
                return "Note: Recording uses FFmpeg for video encoding. Requires Windows 10 1803+ for native Windows.Graphics.Capture support.";
            }
            else if (OperatingSystem.IsMacOS())
            {
                return IsFfmpegAvailableForAdvancedCodecs()
                    ? "Note: H.264 records natively. HEVC, VP9, and AV1 use FFmpeg on macOS in this build."
                    : "Note: Install FFmpeg if you want HEVC, VP9, or AV1 on macOS.";
            }
            else
            {
                return "Note: Recording uses FFmpeg for video encoding. Ensure FFmpeg is installed and accessible in your system PATH.";
            }
        }
    }

    public RecordingViewModel(IScreenRecordingCoordinator screenRecordingCoordinator)
    {
        _screenRecordingCoordinator = screenRecordingCoordinator;
        Current = this;

        InitializeWorkflow();
        _ = InitializeNativeRecordingSettingsAsync();

        // Subscribe to global recording manager events
        // Note: Border window is now managed by TrayIconHelper for all recording types
        _screenRecordingCoordinator.StatusChanged += OnStatusChanged;
        _screenRecordingCoordinator.ErrorOccurred += OnErrorOccurred;

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
                    IsPaused = false;
                    CanStart = true;
                    CanStop = false;
                    CanPauseResume = false;
                    CanAbort = false;
                    _durationTimer.Stop();
                    break;

                case RecordingStatus.Initializing:
                    StatusText = "Initializing...";
                    IsRecording = false;
                    IsPaused = false;
                    CanStart = false;
                    CanStop = false;
                    CanPauseResume = false;
                    CanAbort = false;
                    break;

                case RecordingStatus.Recording:
                    StatusText = "Recording";
                    IsRecording = true;
                    IsPaused = false;
                    CanStart = false;
                    CanStop = true;
                    CanPauseResume = _screenRecordingCoordinator.CurrentCapabilities.SupportsPauseResume;
                    CanAbort = true;
                    _durationTimer.Start();
                    break;

                case RecordingStatus.Paused:
                    StatusText = "Paused";
                    IsRecording = false;
                    IsPaused = true;
                    CanStart = false;
                    CanStop = true;
                    CanPauseResume = _screenRecordingCoordinator.CurrentCapabilities.SupportsPauseResume;
                    CanAbort = true;
                    _durationTimer.Stop();
                    break;

                case RecordingStatus.Finalizing:
                    StatusText = "Finalizing...";
                    IsRecording = false;
                    IsPaused = false;
                    CanStart = false;
                    CanStop = false;
                    CanPauseResume = false;
                    CanAbort = false;
                    _durationTimer.Stop();
                    break;

                case RecordingStatus.Error:
                    StatusText = "Error";
                    IsRecording = false;
                    IsPaused = false;
                    CanStart = true;
                    CanStop = false;
                    CanPauseResume = false;
                    CanAbort = false;
                    _durationTimer.Stop();
                    break;
            }
            OnPropertyChanged(nameof(CanMoveRecordingRegion));
        });
    }

    [RelayCommand(CanExecute = nameof(CanMoveRecordingRegion))]
    private async Task MoveRecordingRegionAsync()
    {
        if (_screenRecordingCoordinator.CurrentOptions is not { Mode: CaptureMode.Region } options) return;
        try
        {
            var selected = await PlatformServices.ScreenCapture.SelectRegionAsync();
            if (selected.IsEmpty) return;
            var region = new System.Drawing.Rectangle(selected.Left, selected.Top, options.Region.Width, options.Region.Height);
            await _screenRecordingCoordinator.ResumeRecordingAtAsync(region);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            DebugHelper.WriteException(ex, "Moving the paused recording region");
        }
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
            _ = SettingsManager.SaveWorkflowsConfigAsync();

            DebugHelper.WriteLine($"Starting recording (workflow: {_workflow?.Name ?? "unnamed"}): {Codec} @ {Fps}fps, {BitrateKbps}kbps, Cursor={ShowCursor}, Intent={RecordingIntent}");
            DebugHelper.WriteLine($"  Audio: SystemAudio={CaptureSystemAudio}, Microphone={CaptureMicrophone}");

            // Use unified pipeline through TaskHelpers.ExecuteWorkflow
            // This ensures recording goes through the same path as hotkey triggers
            if (_workflow != null)
            {
                await Core.Helpers.TaskHelpers.ExecuteWorkflow(_workflow);
            }
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
            await _screenRecordingCoordinator.StopRecordingAsync();
            DebugHelper.WriteLine($"Recording saved to: {OutputFilePath}");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to stop recording");
            LastError = ex.Message;
        }
    }

    /// <summary>Discards the current take and records again with the same settings (ShareX #7255).</summary>
    [RelayCommand(CanExecute = nameof(CanStop))]
    private void RestartRecording()
    {
        DebugHelper.WriteLine("Restarting recording...");
        _screenRecordingCoordinator.RequestRestart();
    }

    [RelayCommand(CanExecute = nameof(CanPauseResume))]
    private async Task PauseResumeAsync()
    {
        if (!_screenRecordingCoordinator.CurrentCapabilities.SupportsPauseResume)
        {
            DebugHelper.WriteLine("RecordingViewModel: Pause/resume is unavailable for the active recording backend.");
            return;
        }

        try
        {
            await _screenRecordingCoordinator.TogglePauseResumeAsync();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to toggle pause/resume");
            LastError = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAbort))]
    private async Task AbortRecordingAsync()
    {
        try
        {
            await _screenRecordingCoordinator.AbortRecordingAsync();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to abort recording");
            LastError = ex.Message;
        }
    }

    [RelayCommand]
    private async Task RunLinuxRecordingDiagnosticsAsync()
    {
        if (!IsLinuxPlatform)
        {
            return;
        }

        LinuxRecordingDiagnosticsStatusText = "Running Linux recording diagnostics...";

        try
        {
            string reportPath = await Task.Run(() =>
                PlatformServices.Diagnostic.WriteRecordingDiagnostics(PathsManager.PersonalFolder));

            if (!string.IsNullOrWhiteSpace(reportPath))
            {
                LinuxRecordingDiagnosticsStatusText = $"Diagnostics report saved: {reportPath}";
            }
            else
            {
                LinuxRecordingDiagnosticsStatusText = "Diagnostics failed. Unable to write report.";
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Linux recording diagnostics failed.");
            LinuxRecordingDiagnosticsStatusText = "Diagnostics failed. Check logs for details.";
        }
    }

    partial void OnCanStartChanged(bool value)
    {
        StartRecordingCommand.NotifyCanExecuteChanged();
    }

    partial void OnCanStopChanged(bool value)
    {
        StopRecordingCommand.NotifyCanExecuteChanged();
        RestartRecordingCommand.NotifyCanExecuteChanged();
    }

    partial void OnCanPauseResumeChanged(bool value)
    {
        PauseResumeCommand.NotifyCanExecuteChanged();
    }

    partial void OnCanAbortChanged(bool value)
    {
        AbortRecordingCommand.NotifyCanExecuteChanged();
    }

    public bool IsRecordingOrPaused => IsRecording || IsPaused;

    partial void OnIsRecordingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRecordingOrPaused));
    }

    partial void OnIsPausedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRecordingOrPaused));
    }

    private void InitializeWorkflow()
    {
        var workflow = SettingsManager.WorkflowsConfig.Hotkeys.FirstOrDefault(w => w.Job == WorkflowType.ScreenRecorder);
        if (workflow == null)
        {
            workflow = new WorkflowSettings(WorkflowType.ScreenRecorder, new HotkeyInfo())
            {
                Name = "Screen Recorder (auto)"
            };

            SettingsManager.WorkflowsConfig.Hotkeys.Add(workflow);
            _ = SettingsManager.SaveWorkflowsConfigAsync();
        }

        _workflow = workflow;
        _taskSettings = _workflow.TaskSettings ?? new TaskSettings();
        _workflow.TaskSettings = _taskSettings;

        var recordingSettings = _taskSettings.CaptureSettings.ScreenRecordingSettings;
        if (!AvailableCodecs.Contains(recordingSettings.Codec))
        {
            recordingSettings.Codec = VideoCodec.H264;
            CodecAvailabilityMessage = "FFmpeg is not available, so advanced codecs are hidden and the recording codec was reset to H.264.";
        }
        else if ((OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()) && !IsFfmpegAvailableForAdvancedCodecs())
        {
            CodecAvailabilityMessage = "Install FFmpeg to enable HEVC, VP9, and AV1 on this platform.";
        }
        else
        {
            CodecAvailabilityMessage = string.Empty;
        }

        // Seed UI from workflow settings
        Fps = recordingSettings.FPS;
        BitrateKbps = recordingSettings.BitrateKbps;
        Codec = recordingSettings.Codec;
        ShowCursor = recordingSettings.ShowCursor;
        CaptureSystemAudio = recordingSettings.CaptureSystemAudio;
        CaptureMicrophone = recordingSettings.CaptureMicrophone;
        RecordingIntent = recordingSettings.RecordingIntent;
        SystemAudioGain = recordingSettings.SystemAudioGain;
        MicrophoneGain = recordingSettings.MicrophoneGain;
        WindowRecordingMode = recordingSettings.WindowRecordingMode;
        RequireHardwareEncoder = recordingSettings.RequireHardwareEncoder;
        LowLatency = recordingSettings.LowLatency;
        HideCaptureBorder = recordingSettings.HideCaptureBorder;
        ForceFfmpeg = recordingSettings.ForceFFmpeg;
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
        recordingSettings.ForceFFmpeg = ForceFfmpeg;
        if (HasNativeRecordingSettings)
        {
            recordingSettings.SystemAudioDeviceId = SelectedSystemAudioDevice?.Id;
            recordingSettings.MicrophoneDeviceId = SelectedMicrophoneDevice?.Id;
            recordingSettings.DisplayId = SelectedRecordingDisplay?.DeviceName;
            recordingSettings.SystemAudioGain = SystemAudioGain;
            recordingSettings.MicrophoneGain = MicrophoneGain;
            recordingSettings.WindowRecordingMode = WindowRecordingMode;
            recordingSettings.RequireHardwareEncoder = RequireHardwareEncoder;
            recordingSettings.LowLatency = LowLatency;
            recordingSettings.HideCaptureBorder = HideCaptureBorder;
        }
    }

    private void LoadNativeRecordingDevices()
    {
        if (PlatformServices.RecordingDevices is not { } devices) return;
        var settings = _taskSettings.CaptureSettings.ScreenRecordingSettings;
        SystemAudioDevices.Add(new RecordingDevice(null, "Default output"));
        MicrophoneDevices.Add(new RecordingDevice(null, "Default microphone"));
        try
        {
            foreach (var device in devices.GetSystemAudioDevices()) SystemAudioDevices.Add(device);
            foreach (var device in devices.GetMicrophones()) MicrophoneDevices.Add(device);
            foreach (var display in PlatformServices.Screen.GetAllScreens()) RecordingDisplays.Add(display);
        }
        catch (Exception ex)
        {
            RecordingDeviceError = $"Could not load recording devices: {ex.Message}";
            DebugHelper.WriteException(ex, "Loading native recording devices");
        }

        SelectedSystemAudioDevice = RestoreAudioDevice(SystemAudioDevices, settings.SystemAudioDeviceId);
        SelectedMicrophoneDevice = RestoreAudioDevice(MicrophoneDevices, settings.MicrophoneDeviceId);
        var selectedDisplay = RecordingDisplays.FirstOrDefault(display => display.DeviceName == settings.DisplayId);
        if (selectedDisplay == null && !string.IsNullOrWhiteSpace(settings.DisplayId))
        {
            selectedDisplay = new ScreenInfo { DeviceName = settings.DisplayId };
            RecordingDisplays.Add(selectedDisplay);
            RecordingDeviceError = "The selected display is unavailable. Choose an available display before recording.";
        }
        SelectedRecordingDisplay = selectedDisplay ?? RecordingDisplays.FirstOrDefault(display => display.IsPrimary);
    }

    private static RecordingDevice RestoreAudioDevice(IList<RecordingDevice> devices, string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return devices[0];
        var selected = devices.FirstOrDefault(device => device.Id == id || device.Name == id);
        if (selected != null) return selected;
        selected = new RecordingDevice(id, $"Unavailable device ({id})");
        devices.Add(selected);
        return selected;
    }

    private async Task InitializeNativeRecordingSettingsAsync()
    {
        try
        {
            if (_screenRecordingCoordinator.PlatformInitializationTask is { } initialization) await initialization;
            if (_disposed) return;
            LoadNativeRecordingDevices();
            OnPropertyChanged(nameof(HasNativeRecordingSettings));
        }
        catch (Exception ex)
        {
            RecordingDeviceError = $"Native recording settings are unavailable: {ex.Message}";
        }
    }

    private CaptureMode ResolveCaptureMode()
    {
        return _workflow.Job switch
        {
            WorkflowType.ScreenRecorderActiveWindow => CaptureMode.Window,
            WorkflowType.ScreenRecorderCustomRegion => CaptureMode.Region,
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
        OnPropertyChanged(nameof(EncoderInfo));
        OnPropertyChanged(nameof(UsageNotes));
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
    }

    partial void OnCaptureMicrophoneChanged(bool value)
    {
        if (!_initialized) return;
        _taskSettings.CaptureSettings.ScreenRecordingSettings.CaptureMicrophone = value;
    }

    partial void OnSelectedSystemAudioDeviceChanged(RecordingDevice? value)
    {
        if (_initialized) _taskSettings.CaptureSettings.ScreenRecordingSettings.SystemAudioDeviceId = value?.Id;
    }
    partial void OnSelectedMicrophoneDeviceChanged(RecordingDevice? value)
    {
        if (_initialized) _taskSettings.CaptureSettings.ScreenRecordingSettings.MicrophoneDeviceId = value?.Id;
    }
    partial void OnSelectedRecordingDisplayChanged(ScreenInfo? value)
    {
        if (_initialized) _taskSettings.CaptureSettings.ScreenRecordingSettings.DisplayId = value?.DeviceName;
    }
    partial void OnSystemAudioGainChanged(float value)
    {
        if (_initialized) _taskSettings.CaptureSettings.ScreenRecordingSettings.SystemAudioGain = value;
    }
    partial void OnMicrophoneGainChanged(float value)
    {
        if (_initialized) _taskSettings.CaptureSettings.ScreenRecordingSettings.MicrophoneGain = value;
    }
    partial void OnWindowRecordingModeChanged(WindowRecordingMode value)
    {
        if (_initialized) _taskSettings.CaptureSettings.ScreenRecordingSettings.WindowRecordingMode = value;
    }
    partial void OnRequireHardwareEncoderChanged(bool value)
    {
        if (_initialized) _taskSettings.CaptureSettings.ScreenRecordingSettings.RequireHardwareEncoder = value;
    }
    partial void OnLowLatencyChanged(bool value)
    {
        if (_initialized) _taskSettings.CaptureSettings.ScreenRecordingSettings.LowLatency = value;
    }
    partial void OnHideCaptureBorderChanged(bool value)
    {
        if (_initialized) _taskSettings.CaptureSettings.ScreenRecordingSettings.HideCaptureBorder = value;
    }
    partial void OnForceFfmpegChanged(bool value)
    {
        if (_initialized) _taskSettings.CaptureSettings.ScreenRecordingSettings.ForceFFmpeg = value;
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
        // Note: Border window is now managed by TrayIconHelper
        _screenRecordingCoordinator.StatusChanged -= OnStatusChanged;
        _screenRecordingCoordinator.ErrorOccurred -= OnErrorOccurred;

        GC.SuppressFinalize(this);
    }

    private static bool IsFfmpegAvailableForAdvancedCodecs()
    {
        if (OperatingSystem.IsLinux())
        {
            return true;
        }

        string ffmpegPath = PathsManager.GetFFmpegPath();
        return !string.IsNullOrWhiteSpace(ffmpegPath) && File.Exists(ffmpegPath);
    }
}
