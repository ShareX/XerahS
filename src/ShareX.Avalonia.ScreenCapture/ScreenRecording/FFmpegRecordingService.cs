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

using System.Diagnostics;
using XerahS.Media;
using XerahS.Common;
using XerahS.ScreenCapture.ScreenRecording;

namespace XerahS.ScreenCapture.ScreenRecording;

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
            if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
            {
                throw new FileNotFoundException("ffmpeg.exe not found. Please install FFmpeg or set FFmpegPath property.");
            }

            // Build ffmpeg arguments
            string args = BuildFFmpegArguments(options);

            // Create and start FFmpeg process
            _ffmpeg = new FFmpegCLIManager(ffmpegPath);
            _ffmpeg.ShowError = true;
            _ffmpeg.TrackEncodeProgress = true;

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

                    bool success = _ffmpeg.Run(args);

                    if (!success && !_ffmpeg.StopRequested)
                    {
                        HandleFatalError(new Exception($"FFmpeg process failed.\nOutput: {_ffmpeg.Output}"), true);
                    }
                }
                catch (Exception ex)
                {
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
        FFmpegCLIManager? ffmpeg;

        lock (_lock)
        {
            if (_status != RecordingStatus.Recording)
            {
                return Task.CompletedTask; // Already stopped or never started
            }

            UpdateStatus(RecordingStatus.Finalizing);
            _stopwatch.Stop();

            ffmpeg = _ffmpeg;
        }

        try
        {
            // Send 'q' to FFmpeg to stop gracefully
            ffmpeg?.WriteInput("q");

            // Wait for process to finish (with timeout)
            Task.Delay(5000).ContinueWith(_ =>
            {
                if (ffmpeg?.IsProcessRunning == true)
                {
                    ffmpeg.Close();
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

        return Task.CompletedTask;
    }

    private string FindFFmpegPath()
    {
        // 1. Check if explicitly set
        if (!string.IsNullOrEmpty(_ffmpegPath) && File.Exists(_ffmpegPath))
        {
            return _ffmpegPath;
        }

        // 2. Check if Options has path override
        if (Options?.OverrideCLIPath == true && !string.IsNullOrEmpty(Options.CLIPath) && File.Exists(Options.CLIPath))
        {
            return Options.CLIPath;
        }

        // 3. Check common locations
        string[] commonPaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "FFmpeg", "bin", "ffmpeg.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "FFmpeg", "bin", "ffmpeg.exe"),
        };

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        // 4. Check PATH environment variable
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv != null)
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                var ffmpegPath = Path.Combine(dir, "ffmpeg.exe");
                if (File.Exists(ffmpegPath))
                {
                    return ffmpegPath;
                }
            }
        }

        return string.Empty;
    }

    private string BuildFFmpegArguments(RecordingOptions options)
    {
        var settings = options.Settings ?? new ScreenRecordingSettings();
        var args = new List<string>();

        // Input source based on capture mode
        switch (options.Mode)
        {
            case CaptureMode.Screen:
                // Use gdigrab for screen capture on Windows
                args.Add("-f gdigrab");
                args.Add("-framerate " + settings.FPS);
                // Stage 2: Cursor capture support
                if (settings.ShowCursor)
                {
                    args.Add("-draw_mouse 1");
                }
                // Red border will be shown by RecordingBorderWindow
                args.Add("-i desktop");
                break;

            case CaptureMode.Window:
                // Window capture would need window title - defer to region for now
                args.Add("-f gdigrab");
                args.Add("-framerate " + settings.FPS);
                if (settings.ShowCursor)
                {
                    args.Add("-draw_mouse 1");
                }
                // Red border will be shown by RecordingBorderWindow
                args.Add("-i desktop");
                break;

            case CaptureMode.Region:
                // Region capture with gdigrab
                args.Add("-f gdigrab");
                args.Add("-framerate " + settings.FPS);
                if (settings.ShowCursor)
                {
                    args.Add("-draw_mouse 1");
                }
                if (options.Region.Width > 0 && options.Region.Height > 0)
                {
                    args.Add($"-offset_x {options.Region.X}");
                    args.Add($"-offset_y {options.Region.Y}");
                    args.Add($"-video_size {options.Region.Width}x{options.Region.Height}");
                }
                // Red border will be shown by RecordingBorderWindow
                args.Add("-i desktop");
                break;
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
