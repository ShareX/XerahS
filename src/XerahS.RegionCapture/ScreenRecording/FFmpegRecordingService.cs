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

using System.Diagnostics;
using XerahS.Media;
using XerahS.Common;

namespace XerahS.RegionCapture.ScreenRecording;

/// <summary>
/// FFmpeg-based screen recording service (fallback implementation)
/// Uses FFmpegCLIManager to orchestrate ffmpeg.exe process
/// Stage 4: Fallback for systems without Windows.Graphics.Capture support
/// </summary>
public class FFmpegRecordingService : IRecordingService
{
    private FFmpegCLIManager? _ffmpeg;
    private RecordingOptions? _currentOptions;
    private RecordingStatus _status = RecordingStatus.Idle;
    private readonly Stopwatch _stopwatch = new();
    private readonly object _lock = new();
    private bool _disposed;
    private string? _ffmpegPath;

    /// <summary>
    /// Gets or sets the path to ffmpeg.exe
    /// If null, will search in PATH and common locations
    /// </summary>
    public string? FFmpegPath
    {
        get => _ffmpegPath;
        set => _ffmpegPath = value;
    }

    /// <summary>
    /// FFmpeg options to use for encoding
    /// </summary>
    public FFmpegOptions? Options { get; set; }

    public event EventHandler<RecordingErrorEventArgs>? ErrorOccurred;
    public event EventHandler<RecordingStatusEventArgs>? StatusChanged;

    public Task StartRecordingAsync(RecordingOptions options)
    {
        Console.WriteLine("[FFmpegRecordingService] StartRecordingAsync called");
        if (options == null) throw new ArgumentNullException(nameof(options));

        lock (_lock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FFmpegRecordingService));
            if (_status != RecordingStatus.Idle)
            {
                throw new InvalidOperationException("Recording already in progress");
            }

            _currentOptions = options;
            UpdateStatus(RecordingStatus.Initializing);
        }

        try
        {
            // Locate ffmpeg.exe
            string ffmpegPath = FindFFmpegPath();
            Console.WriteLine($"[FFmpegRecordingService] FFmpeg Path resolved to: {ffmpegPath}");
            
            if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
            {
                string searched = $"Path not found. Checked: PATH, {Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe")}, {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "FFmpeg", "bin", "ffmpeg.exe")}";
                Console.WriteLine($"[FFmpegRecordingService] Critical Error: {searched}");
                DebugHelper.WriteLine($"[FFmpeg] {searched}");
                throw new FileNotFoundException($"ffmpeg.exe not found. {searched}");
            }

            // Build ffmpeg arguments
            string args = BuildFFmpegArguments(options);
            Console.WriteLine($"[FFmpegRecordingService] FFmpeg Arguments: {args}");

            // Create and start FFmpeg process
            _ffmpeg = new FFmpegCLIManager(ffmpegPath);
            _ffmpeg.ShowError = true;
            _ffmpeg.TrackEncodeProgress = true;

            Console.WriteLine("[FFmpegRecordingService] Starting FFmpeg process...");
            
            // Start FFmpeg in background
            Task.Run(() =>
            {
                try
                {
                    lock (_lock)
                    {
                        _stopwatch.Restart();
                        UpdateStatus(RecordingStatus.Recording);
                    }
                    
                    Console.WriteLine("[FFmpegRecordingService] FFmpeg process running...");

                    bool success = _ffmpeg.Run(args);
                    
                    Console.WriteLine($"[FFmpegRecordingService] FFmpeg process finished. Success: {success}");

                    if (!success && !_ffmpeg.StopRequested)
                    {
                        Console.WriteLine($"[FFmpegRecordingService] FFmpeg process failed. Output: {_ffmpeg.Output}");
                        HandleFatalError(new Exception($"FFmpeg process failed.\nOutput: {_ffmpeg.Output}"), true);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FFmpegRecordingService] Exception in background task: {ex}");
                    HandleFatalError(ex, true);
                }
            });

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            HandleFatalError(ex, true);
            throw;
        }
    }

    public Task StopRecordingAsync()
    {
        Console.WriteLine("[FFmpegRecordingService] StopRecordingAsync called");
        FFmpegCLIManager? ffmpeg;

        lock (_lock)
        {
            if (_status != RecordingStatus.Recording)
            {
                Console.WriteLine("[FFmpegRecordingService] StopRecordingAsync: Not recording (Status != Recording). Returning.");
                return Task.CompletedTask; // Already stopped or never started
            }

            UpdateStatus(RecordingStatus.Finalizing);
            _stopwatch.Stop();

            ffmpeg = _ffmpeg;
        }

        try
        {
            Console.WriteLine("[FFmpegRecordingService] Sending 'q' to FFmpeg process...");
            // Send 'q' to FFmpeg to stop gracefully
            ffmpeg?.WriteInput("q");
            
            // Wait for process to finish (with timeout)
            Task.Delay(5000).ContinueWith(_ =>
            {
                if (ffmpeg?.IsProcessRunning == true)
                {
                    Console.WriteLine("[FFmpegRecordingService] FFmpeg process still running after 5s timeout. Closing forcefully.");
                    ffmpeg.Close();
                }
                else 
                {
                     Console.WriteLine("[FFmpegRecordingService] FFmpeg process exited gracefully before timeout.");
                }
            });
        }
        catch (Exception ex)
        {
            HandleFatalError(ex, false);
        }
        finally
        {
            lock (_lock)
            {
                _ffmpeg = null;
                _currentOptions = null;
                UpdateStatus(RecordingStatus.Idle);
            }
        }

        Console.WriteLine("[FFmpegRecordingService] StopRecordingAsync completed.");
        return Task.CompletedTask;
    }

    private string FindFFmpegPath()
    {
        // 1. Check if explicitly set
        if (!string.IsNullOrEmpty(_ffmpegPath))
        {
            DebugHelper.WriteLine($"[FFmpeg] Checking explicitly set path: {_ffmpegPath}");
            if (File.Exists(_ffmpegPath))
            {
                DebugHelper.WriteLine($"[FFmpeg] Found FFmpeg at explicitly set path: {_ffmpegPath}");
                return _ffmpegPath;
            }
            DebugHelper.WriteLine($"[FFmpeg] FFmpeg not found at explicitly set path.");
        }

        // 2. Check if Options has path override
        if (Options?.OverrideCLIPath == true && !string.IsNullOrEmpty(Options.CLIPath))
        {
            DebugHelper.WriteLine($"[FFmpeg] Checking Options.CLIPath: {Options.CLIPath}");
            if (File.Exists(Options.CLIPath))
            {
                DebugHelper.WriteLine($"[FFmpeg] Found FFmpeg at Options.CLIPath: {Options.CLIPath}");
                return Options.CLIPath;
            }
            DebugHelper.WriteLine($"[FFmpeg] FFmpeg not found at Options.CLIPath.");
        }

        // 3. Use Centralized PathsManager
        return PathsManager.GetFFmpegPath();
    }

    private string BuildFFmpegArguments(RecordingOptions options)
    {
        var settings = options.Settings ?? new ScreenRecordingSettings();
        var args = new List<string>();

        // Input source based on capture mode
        // Input source based on capture mode
        if (OperatingSystem.IsWindows())
        {
            switch (options.Mode)
            {
                case CaptureMode.Screen:
                case CaptureMode.Window: // Fallback to region/screen for now
                case CaptureMode.Region:
                    // Use gdigrab for screen capture on Windows
                    args.Add("-f gdigrab");
                    args.Add("-framerate " + settings.FPS);
                    if (settings.ShowCursor)
                    {
                        args.Add("-draw_mouse 1");
                    }
                    else
                    {
                         args.Add("-draw_mouse 0");
                    }

                    if (options.Mode == CaptureMode.Region && options.Region.Width > 0 && options.Region.Height > 0)
                    {
                        args.Add($"-offset_x {options.Region.X}");
                        args.Add($"-offset_y {options.Region.Y}");
                        args.Add($"-video_size {options.Region.Width}x{options.Region.Height}");
                    }
                    
                    args.Add("-i desktop");
                    break;
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
             // macOS uses avfoundation
             args.Add("-f avfoundation");
             args.Add("-framerate " + settings.FPS);
             
             if (settings.ShowCursor)
             {
                 args.Add("-capture_cursor 1");
             }
             else 
             {
                 args.Add("-capture_cursor 0");
             }

             // Mouse clicks visualization optional
             // args.Add("-capture_mouse_clicks 1");

             // For now default to screen 1. 
             // Ideally we would enumerate screens or look at options.Region to determine screen index.
             // But for MVP/Fix, "1" or "1:" is usually main screen.
             // Using "1" as video input. 
             // Note: avfoundation input format is "video_device:audio_device" or just "video_device"
             args.Add("-i \"1\""); 
             
             // Region capture in avfoundation is not directly supported via offset/size args like gdigrab
             // It usually requires a complex filter chain (crop).
             if (options.Mode == CaptureMode.Region && options.Region.Width > 0 && options.Region.Height > 0)
             {
                 // Add crop filter: crop=w:h:x:y
                 args.Add($"-vf \"crop={options.Region.Width}:{options.Region.Height}:{options.Region.X}:{options.Region.Y}\"");
             }
        }
        else if (OperatingSystem.IsLinux())
        {
             // basic Linux fallback (x11grab)
             args.Add("-f x11grab");
             args.Add("-framerate " + settings.FPS);
             args.Add($"-video_size {options.Region.Width}x{options.Region.Height}");
             args.Add($"-i :0.0+{options.Region.X},{options.Region.Y}");
             if (settings.ShowCursor) args.Add("-draw_mouse 1");
        }

        // Video codec settings
        switch (settings.Codec)
        {
            case VideoCodec.H264:
                args.Add("-c:v libx264");
                args.Add("-preset ultrafast");
                args.Add($"-b:v {settings.BitrateKbps}k");
                break;

            case VideoCodec.HEVC:
                args.Add("-c:v libx265");
                args.Add("-preset ultrafast");
                args.Add($"-b:v {settings.BitrateKbps}k");
                break;

            case VideoCodec.VP9:
                args.Add("-c:v libvpx-vp9");
                args.Add($"-b:v {settings.BitrateKbps}k");
                break;

            case VideoCodec.AV1:
                args.Add("-c:v libaom-av1");
                args.Add($"-b:v {settings.BitrateKbps}k");
                break;
        }

        // Stage 6: Audio capture support
        if (settings.CaptureSystemAudio || settings.CaptureMicrophone)
        {
            if (OperatingSystem.IsWindows())
            {
                // Windows audio capture via dshow (DirectShow)
                if (settings.CaptureSystemAudio)
                {
                    // WASAPI loopback for system audio (Windows Vista+)
                    // Note: This captures the default audio output device
                    args.Add("-f dshow");
                    args.Add("-i audio=\"Stereo Mix\"");
                    args.Add("-c:a aac");
                    args.Add("-b:a 192k");

                    DebugHelper.WriteLine("FFmpeg: Capturing system audio via Stereo Mix (dshow)");
                    DebugHelper.WriteLine("  Note: Requires 'Stereo Mix' to be enabled in Windows Sound settings");
                }
                else if (settings.CaptureMicrophone)
                {
                    // Microphone capture
                    if (!string.IsNullOrEmpty(settings.MicrophoneDeviceId))
                    {
                        args.Add("-f dshow");
                        args.Add($"-i audio=\"{settings.MicrophoneDeviceId}\"");
                    }
                    else
                    {
                        // Use default microphone
                        args.Add("-f dshow");
                        args.Add("-i audio=\"@device_cm_{33D9A762-90C8-11D0-BD43-00A0C911CE86}\\wave_{00000000-0000-0000-0000-000000000000}\"");
                    }
                    args.Add("-c:a aac");
                    args.Add("-b:a 192k");

                    DebugHelper.WriteLine("FFmpeg: Capturing microphone audio via dshow");
                }

                // TODO: Mix both system audio and microphone requires filter_complex
                // For Stage 6 MVP, we support one audio source at a time
            }
            else if (OperatingSystem.IsLinux())
            {
                // Linux audio capture via PulseAudio or ALSA
                if (settings.CaptureSystemAudio)
                {
                    // PulseAudio loopback monitor
                    args.Add("-f pulse");
                    args.Add("-i default");
                    args.Add("-c:a aac");
                    args.Add("-b:a 192k");

                    DebugHelper.WriteLine("FFmpeg: Capturing system audio via PulseAudio (Linux)");
                }
                else if (settings.CaptureMicrophone)
                {
                    // PulseAudio microphone
                    args.Add("-f pulse");
                    args.Add(!string.IsNullOrEmpty(settings.MicrophoneDeviceId)
                        ? $"-i {settings.MicrophoneDeviceId}"
                        : "-i default");
                    args.Add("-c:a aac");
                    args.Add("-b:a 192k");

                    DebugHelper.WriteLine("FFmpeg: Capturing microphone via PulseAudio (Linux)");
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                // macOS audio capture via avfoundation
                if (settings.CaptureSystemAudio || settings.CaptureMicrophone)
                {
                    // avfoundation for audio on macOS
                    args.Add("-f avfoundation");
                    args.Add("-i \":0\""); // Default audio device
                    args.Add("-c:a aac");
                    args.Add("-b:a 192k");

                    DebugHelper.WriteLine("FFmpeg: Capturing audio via avfoundation (macOS)");
                }
            }
        }

        // Output options
        args.Add("-pix_fmt yuv420p");
        args.Add("-y"); // Overwrite output file

        // Output path
        string outputPath = options.OutputPath ?? GetDefaultOutputPath();
        args.Add($"\"{outputPath}\"");

        return string.Join(" ", args);
    }

    private string GetDefaultOutputPath()
    {
        // Default pattern: ShareX/Screenshots/yyyy-MM/Date_Time.mp4
        // Use PathsManager.ScreenshotsFolder (or potentially a new ScreencastsFolder logic if preferred, keeping it simple for now)
        // Wait, FFmpegRecordingService is for recording, so it should probably use ScreencastsFolder.
        // Let's check what I planned. "Update GetDefaultOutputPath to use PathsManager.ScreenshotsFolder (or ScreencastsFolder to match logic)."
        // ScreencastsFolder makes more sense.
        
        string screencastsFolder = PathsManager.ScreencastsFolder;
        Directory.CreateDirectory(screencastsFolder);

        string fileName = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4";
        return Path.Combine(screencastsFolder, fileName);
    }

    private void UpdateStatus(RecordingStatus newStatus)
    {
        lock (_lock)
        {
            if (_status == newStatus) return;

            _status = newStatus;
            var duration = _stopwatch.Elapsed;

            StatusChanged?.Invoke(this, new RecordingStatusEventArgs(newStatus, duration));
        }
    }

    private void HandleFatalError(Exception ex, bool isFatal)
    {
        lock (_lock)
        {
            if (_status != RecordingStatus.Error)
            {
                UpdateStatus(RecordingStatus.Error);
            }
        }

        ErrorOccurred?.Invoke(this, new RecordingErrorEventArgs(ex, isFatal));

        if (isFatal)
        {
            // Cleanup
            try
            {
                _ffmpeg?.Close();
            }
            catch
            {
                // Ignore cleanup errors
            }

            _ffmpeg = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            _disposed = true;

            try
            {
                if (_status == RecordingStatus.Recording)
                {
                    StopRecordingAsync().Wait();
                }
            }
            catch
            {
                // Best effort cleanup
            }

            _ffmpeg?.Close();
            _ffmpeg = null;
        }

        GC.SuppressFinalize(this);
    }
}
